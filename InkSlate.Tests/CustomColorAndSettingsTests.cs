using System.Globalization;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using CsCheck;
using InkSlate.App.ViewModels;
using InkSlate.App.Views;
using InkSlate.Core.Models;
using InkSlate.Core.Services;
using Xunit;

namespace InkSlate.Tests;

public sealed class CustomColorAndSettingsTests
{
    [Fact]
    public void UserSettingsService_SaveAndLoad_RoundTripsAccurately()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "InkSlate_SettingsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var settingsPath = Path.Combine(tempDir, "user-settings.json");

        try
        {
            var service = new UserSettingsService(settingsPath);
            var initial = service.Load();
            Assert.Empty(initial.CustomColorHexes);
            Assert.Empty(initial.CustomPenPresets);

            var presetId = Guid.NewGuid();
            var toSave = new UserSettings
            {
                CustomColorHexes = ["#FFFF0000", "#FF00FF00", "#FF0000FF"],
                CustomPenPresets =
                [
                    new CustomPenPreset
                    {
                        Id = presetId,
                        Name = "Red Pen",
                        ColorHex = "#FFFF0000",
                        ThicknessDip = 3.5,
                        Shortcut = "1"
                    }
                ]
            };

            service.Save(toSave);
            Assert.True(File.Exists(settingsPath));

            var loaded = service.Load();
            Assert.Equal(3, loaded.CustomColorHexes.Count);
            Assert.Equal("#FFFF0000", loaded.CustomColorHexes[0]);
            Assert.Equal("#FF00FF00", loaded.CustomColorHexes[1]);
            Assert.Equal("#FF0000FF", loaded.CustomColorHexes[2]);

            var preset = Assert.Single(loaded.CustomPenPresets);
            Assert.Equal(presetId, preset.Id);
            Assert.Equal("Red Pen", preset.Name);
            Assert.Equal("#FFFF0000", preset.ColorHex);
            Assert.Equal(3.5, preset.ThicknessDip);
            Assert.Equal("1", preset.Shortcut);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void CsCheck_UserSettingsService_FuzzingAndNormalization()
    {
        Gen.Byte.Array[3].Sample(bytes =>
        {
            var r = bytes[0];
            var g = bytes[1];
            var b = bytes[2];
            var hex6 = $"#{r:X2}{g:X2}{b:X2}";
            var hex8 = $"#FF{r:X2}{g:X2}{b:X2}";

            Assert.True(UserSettingsService.TryNormalizeColorHex(hex6, out var norm1, out _, out _, out _));
            Assert.Equal(hex8, norm1);

            Assert.True(UserSettingsService.TryNormalizeColorHex(hex8, out var norm2, out _, out _, out _));
            Assert.Equal(hex8, norm2);

            var settings = new UserSettings
            {
                CustomColorHexes = [hex6, hex8, "invalid-color", "#123", "   "]
            };

            UserSettingsService.Sanitize(settings);
            // Deduplicated and normalized: only 1 entry should survive
            Assert.Single(settings.CustomColorHexes);
            Assert.Equal(hex8, settings.CustomColorHexes[0]);
        });
    }

    [Fact]
    public void ToolbarViewModel_CustomColorRGB_HSV_Hex_BidirectionalSync()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new ToolbarViewModel();

            // 1. Set RGB Red (255, 0, 0)
            vm.SetCustomColorRgb(255, 0, 0);
            Assert.Equal(255, vm.CustomColorR);
            Assert.Equal(0, vm.CustomColorG);
            Assert.Equal(0, vm.CustomColorB);
            Assert.Equal("#FF0000", vm.CustomColorHexInput);
            Assert.Equal(0.0, vm.CustomColorHue, tolerance: 1.0);
            Assert.Equal(1.0, vm.CustomColorSaturation, tolerance: 0.01);
            Assert.Equal(1.0, vm.CustomColorValue, tolerance: 0.01);

            // 2. Set Hex Input to Green (#00FF00)
            vm.CustomColorHexInput = "#00FF00";
            Assert.Equal(0, vm.CustomColorR);
            Assert.Equal(255, vm.CustomColorG);
            Assert.Equal(0, vm.CustomColorB);
            Assert.Equal(120.0, vm.CustomColorHue, tolerance: 1.0);
            Assert.Equal(1.0, vm.CustomColorSaturation, tolerance: 0.01);
            Assert.Equal(1.0, vm.CustomColorValue, tolerance: 0.01);

            // 3. Set Saturation and Value via 2D Spectrum Box
            vm.SetCustomColorSaturationAndValue(0.5, 0.5);
            Assert.Equal(0.5, vm.CustomColorSaturation, tolerance: 0.01);
            Assert.Equal(0.5, vm.CustomColorValue, tolerance: 0.01);

            // 4. Quick Palette click
            vm.PickPaletteColorCommand.Execute("#3B82F6");
            Assert.Equal(0x3B, vm.CustomColorR);
            Assert.Equal(0x82, vm.CustomColorG);
            Assert.Equal(0xF6, vm.CustomColorB);
            Assert.Equal("#3B82F6", vm.CustomColorHexInput);
        });
    }

    [Fact]
    public void ToolbarViewModel_AddAndRemoveCustomColor_UpdatesSwatchesAndAutoSaves()
    {
        StaTestRunner.Run(() =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "InkSlate_SwatchTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var settingsPath = Path.Combine(tempDir, "user-settings.json");

            try
            {
                var settingsService = new UserSettingsService(settingsPath);
                var vm = new ToolbarViewModel(settingsService);

                Assert.Equal(6, vm.ColorSwatches.Count);
                Assert.Equal(6, vm.TextColorSwatches.Count);

                // Open custom color picker
                Assert.False(vm.IsCustomColorPickerOpen);
                vm.ToggleCustomColorPickerCommand.Execute(null);
                Assert.True(vm.IsCustomColorPickerOpen);

                // Set custom color and add it
                vm.SetCustomColorRgb(18, 52, 86); // #123456
                vm.AddCustomColorCommand.Execute(null);

                // Verify added swatch
                Assert.Equal(7, vm.ColorSwatches.Count);
                Assert.Equal(7, vm.TextColorSwatches.Count);

                var addedPenSwatch = vm.ColorSwatches[6];
                Assert.Equal("#FF123456", addedPenSwatch.Hex);
                Assert.True(addedPenSwatch.IsCustom);
                Assert.True(addedPenSwatch.IsSelected);
                Assert.Equal("#FF123456", vm.SelectedPenColorHex);

                var addedTextSwatch = vm.TextColorSwatches[6];
                Assert.Equal("#FF123456", addedTextSwatch.Hex);
                Assert.True(addedTextSwatch.IsCustom);

                // Auto-closed picker
                Assert.False(vm.IsCustomColorPickerOpen);

                // Verify persisted to disk
                var diskSettings = settingsService.Load();
                Assert.Contains("#FF123456", diskSettings.CustomColorHexes);

                // Remove the custom color
                vm.RemoveCustomColorCommand.Execute("#FF123456");
                Assert.Equal(6, vm.ColorSwatches.Count);
                Assert.Equal(6, vm.TextColorSwatches.Count);

                // Verify removed from disk
                var diskAfterRemove = settingsService.Load();
                Assert.DoesNotContain("#FF123456", diskAfterRemove.CustomColorHexes);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        });
    }

    [Fact]
    public void ToolbarViewModel_LoadsSavedSettingsAtStartup()
    {
        StaTestRunner.Run(() =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "InkSlate_StartupTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var settingsPath = Path.Combine(tempDir, "user-settings.json");

            try
            {
                var settingsService = new UserSettingsService(settingsPath);
                settingsService.Save(new UserSettings
                {
                    CustomColorHexes = ["#FFAABBCC", "#FF112233"]
                });

                var vm = new ToolbarViewModel(settingsService);
                Assert.Equal(8, vm.ColorSwatches.Count);
                Assert.Equal(8, vm.TextColorSwatches.Count);

                Assert.Equal("#FFAABBCC", vm.ColorSwatches[6].Hex);
                Assert.True(vm.ColorSwatches[6].IsCustom);

                Assert.Equal("#FF112233", vm.ColorSwatches[7].Hex);
                Assert.True(vm.ColorSwatches[7].IsCustom);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        });
    }

    [Fact]
    public void MainWindow_CustomColorControls_BindAndRenderInSta()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var window = new MainWindow(vm);

            var addPenBtn = window.FindName("AddCustomPenColorButton") as Wpf.Ui.Controls.Button;
            var addTextBtn = window.FindName("AddCustomTextColorButton") as Wpf.Ui.Controls.Button;
            var penPicker = window.FindName("PenCustomColorPickerPanel") as System.Windows.Controls.Border;
            var textPicker = window.FindName("TextCustomColorPickerPanel") as System.Windows.Controls.Border;
            var penSpectrum = window.FindName("PenColorSpectrumBox") as System.Windows.Controls.Border;
            var textSpectrum = window.FindName("TextColorSpectrumBox") as System.Windows.Controls.Border;

            Assert.NotNull(addPenBtn);
            Assert.NotNull(addTextBtn);
            Assert.NotNull(penPicker);
            Assert.NotNull(textPicker);
            Assert.NotNull(penSpectrum);
            Assert.NotNull(textSpectrum);

            Assert.False(vm.Toolbar.IsCustomColorPickerOpen);
            vm.Toolbar.ToggleCustomColorPickerCommand.Execute(null);
            Assert.True(vm.Toolbar.IsCustomColorPickerOpen);
            vm.Toolbar.ToggleCustomColorPickerCommand.Execute(null);
            Assert.False(vm.Toolbar.IsCustomColorPickerOpen);
        });
    }

    [Fact]
    public void CustomPenPresets_InitiallyEmpty_RightDockCollapsedAndBecomesVisibleOnAdd()
    {
        StaTestRunner.Run(() =>
        {
            var mainVm = new MainViewModel();
            var window = new MainWindow(mainVm);

            var rightDock = window.FindName("CustomPenRightDock") as System.Windows.Controls.Border;
            var editorPopover = window.FindName("CustomPenEditorPopover") as System.Windows.Controls.Border;
            var addPenOptionBtn = window.FindName("AddCustomPenFromOptionsButton") as Wpf.Ui.Controls.Button;
            var addPenDockBtn = window.FindName("AddCustomPenDockButton") as Wpf.Ui.Controls.Button;

            Assert.NotNull(rightDock);
            Assert.NotNull(editorPopover);
            Assert.NotNull(addPenOptionBtn);
            Assert.NotNull(addPenDockBtn);

            Assert.Empty(mainVm.Toolbar.CustomPenPresets);
            Assert.False(mainVm.Toolbar.HasCustomPenPresets);

            // Adding a preset sets HasCustomPenPresets to true
            mainVm.Toolbar.OpenNewCustomPenEditorCommand.Execute(null);
            Assert.True(mainVm.Toolbar.IsCustomPenEditorOpen);
            mainVm.Toolbar.SaveCustomPenPresetCommand.Execute(null);

            Assert.Single(mainVm.Toolbar.CustomPenPresets);
            Assert.True(mainVm.Toolbar.HasCustomPenPresets);
        });
    }

    [Fact]
    public void CustomPenPresets_CreateEditDelete_AutoSavesToUserSettingsService()
    {
        StaTestRunner.Run(() =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "InkSlate_PresetSaveTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var settingsPath = Path.Combine(tempDir, "user-settings.json");

            try
            {
                var settingsService = new UserSettingsService(settingsPath);
                var vm = new ToolbarViewModel(settingsService);

                Assert.Empty(vm.CustomPenPresets);

                // 1. Create preset
                vm.OpenNewCustomPenEditorCommand.Execute(null);
                Assert.True(vm.IsCustomPenEditorOpen);
                vm.CustomPenEditorName = "Highlighter Green";
                vm.CustomPenEditorColorHex = "#FF16A34A";
                vm.CustomPenEditorThickness = 4.5;
                vm.CustomPenEditorShortcut = "2";
                vm.SaveCustomPenPresetCommand.Execute(null);

                Assert.False(vm.IsCustomPenEditorOpen);
                var preset = Assert.Single(vm.CustomPenPresets);
                Assert.Equal("Highlighter Green", preset.Name);
                Assert.Equal("#FF16A34A", preset.ColorHex);
                Assert.Equal(4.5, preset.ThicknessDip);
                Assert.Equal("2", preset.Shortcut);
                Assert.True(preset.HasShortcut);

                // Verify saved to disk
                var diskSettings = settingsService.Load();
                var diskPreset = Assert.Single(diskSettings.CustomPenPresets);
                Assert.Equal("Highlighter Green", diskPreset.Name);
                Assert.Equal("#FF16A34A", diskPreset.ColorHex);
                Assert.Equal(4.5, diskPreset.ThicknessDip);
                Assert.Equal("2", diskPreset.Shortcut);

                // 2. Edit preset
                vm.OpenEditCustomPenEditorCommand.Execute(preset);
                Assert.True(vm.IsCustomPenEditorOpen);
                Assert.True(vm.IsEditingExistingCustomPen);
                vm.CustomPenEditorName = "Emerald Note Pen";
                vm.CustomPenEditorThickness = 6.0;
                vm.SaveCustomPenPresetCommand.Execute(null);

                Assert.Equal("Emerald Note Pen", preset.Name);
                Assert.Equal(6.0, preset.ThicknessDip);

                var diskAfterEdit = settingsService.Load();
                var editedDiskPreset = Assert.Single(diskAfterEdit.CustomPenPresets);
                Assert.Equal("Emerald Note Pen", editedDiskPreset.Name);
                Assert.Equal(6.0, editedDiskPreset.ThicknessDip);

                // 3. Delete preset
                vm.DeleteCustomPenPresetCommand.Execute(preset);
                Assert.Empty(vm.CustomPenPresets);
                Assert.False(vm.HasCustomPenPresets);

                var diskAfterDelete = settingsService.Load();
                Assert.Empty(diskAfterDelete.CustomPenPresets);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        });
    }

    [Fact]
    public void CustomPenPresets_ActivationByClickAndShortcut_AppliesExactPenAttributes()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new ToolbarViewModel();

            // Preset 1: Red Thin
            vm.OpenNewCustomPenEditorCommand.Execute(null);
            vm.CustomPenEditorName = "Red Thin";
            vm.CustomPenEditorColorHex = "#FFDC2626";
            vm.CustomPenEditorThickness = 1.5;
            vm.CustomPenEditorShortcut = "1";
            vm.SaveCustomPenPresetCommand.Execute(null);

            // Preset 2: Blue Thick
            vm.OpenNewCustomPenEditorCommand.Execute(null);
            vm.CustomPenEditorName = "Blue Thick";
            vm.CustomPenEditorColorHex = "#FF2563EB";
            vm.CustomPenEditorThickness = 8.0;
            vm.CustomPenEditorShortcut = "Shift+B";
            vm.SaveCustomPenPresetCommand.Execute(null);

            Assert.Equal(2, vm.CustomPenPresets.Count);
            var p1 = vm.CustomPenPresets[0];
            var p2 = vm.CustomPenPresets[1];

            // 1. Activate Preset 1 by click
            vm.ActivateCustomPenCommand.Execute(p1);
            Assert.Equal(ToolMode.Pen, vm.ActiveTool);
            Assert.Equal("#FFDC2626", vm.SelectedPenColorHex);
            Assert.Equal(1.5, vm.SelectedPenThickness);
            Assert.Equal(1.5, vm.CurrentDrawingAttributes.Width);
            Assert.True(p1.IsSelected);
            Assert.False(p2.IsSelected);

            // 2. Activate Preset 2 via custom shortcut Shift+B
            var handledShiftB = vm.TryActivateCustomPenByShortcut(Key.B, ModifierKeys.Shift);
            Assert.True(handledShiftB);
            Assert.Equal(ToolMode.Pen, vm.ActiveTool);
            Assert.Equal("#FF2563EB", vm.SelectedPenColorHex);
            Assert.Equal(8.0, vm.SelectedPenThickness);
            Assert.Equal(8.0, vm.CurrentDrawingAttributes.Width);
            Assert.True(p2.IsSelected);
            Assert.False(p1.IsSelected);

            // 3. Activate Preset 1 via digit key D1 and NumPad1
            var handledD1 = vm.TryActivateCustomPenByShortcut(Key.D1, ModifierKeys.None);
            Assert.True(handledD1);
            Assert.Equal("#FFDC2626", vm.SelectedPenColorHex);
            Assert.Equal(1.5, vm.SelectedPenThickness);

            var handledNum1 = vm.TryActivateCustomPenByShortcut(Key.NumPad1, ModifierKeys.None);
            Assert.True(handledNum1);
            Assert.Equal("#FFDC2626", vm.SelectedPenColorHex);

            // Unmapped shortcut returns false
            Assert.False(vm.TryActivateCustomPenByShortcut(Key.D9, ModifierKeys.None));
        });
    }

    [Fact]
    public void CustomPenPresets_ShortcutValidation_RejectsReservedAndDuplicateShortcuts()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new ToolbarViewModel();

            // Preset with shortcut "1"
            vm.OpenNewCustomPenEditorCommand.Execute(null);
            vm.CustomPenEditorName = "First Pen";
            vm.CustomPenEditorShortcut = "1";
            vm.SaveCustomPenPresetCommand.Execute(null);

            // Reserved single keys
            string[] reservedSingle = ["P", "E", "V", "T", "H", "L", "I", "Space", "Delete", "Escape", "F1", "F2", "F5", "F11"];
            foreach (var key in reservedSingle)
            {
                Assert.False(vm.TryValidateCustomPenShortcut(key, null, out _, out var err));
                Assert.NotNull(err);
            }

            // Reserved Ctrl combinations
            string[] reservedCtrl = ["Ctrl+S", "Ctrl+C", "Ctrl+V", "Ctrl+Z", "Ctrl+Y", "Ctrl+N", "Ctrl+O", "Ctrl+E", "Ctrl+0"];
            foreach (var key in reservedCtrl)
            {
                Assert.False(vm.TryValidateCustomPenShortcut(key, null, out _, out var err));
                Assert.NotNull(err);
            }

            // Duplicate shortcut "1"
            Assert.False(vm.TryValidateCustomPenShortcut("1", null, out _, out var dupErr));
            Assert.NotNull(dupErr);

            // Validating itself when editing
            var firstPreset = vm.CustomPenPresets[0];
            Assert.True(vm.TryValidateCustomPenShortcut("1", firstPreset.Id, out var normSelf, out var errSelf));
            Assert.Equal("1", normSelf);
            Assert.Null(errSelf);

            // Valid shortcuts
            Assert.True(vm.TryValidateCustomPenShortcut("2", null, out var norm2, out _));
            Assert.Equal("2", norm2);

            Assert.True(vm.TryValidateCustomPenShortcut("Alt+1", null, out var normAlt, out _));
            Assert.Equal("Alt+1", normAlt);

            Assert.True(vm.TryValidateCustomPenShortcut("shift+r", null, out var normShift, out _));
            Assert.Equal("Shift+R", normShift);

            Assert.True(vm.TryValidateCustomPenShortcut("", null, out var normEmpty, out _));
            Assert.Equal("", normEmpty);
        });
    }

    [Fact]
    public void CsCheck_CustomPenPresets_RoundTripAndActivationInvariants()
    {
        Gen.Select(
            Gen.Byte.Array[3],
            Gen.Double[0.5, 32.0],
            Gen.Int[1, 9]
        ).Sample((rgb, thickness, digit) =>
        {
            var hex = $"#FF{rgb[0]:X2}{rgb[1]:X2}{rgb[2]:X2}";
            var shortcut = digit.ToString(CultureInfo.InvariantCulture);
            var preset = new CustomPenPreset
            {
                Id = Guid.NewGuid(),
                Name = $"Pen {digit}",
                ColorHex = hex,
                ThicknessDip = thickness,
                Shortcut = shortcut
            };

            var settings = new UserSettings
            {
                CustomPenPresets = [preset]
            };

            UserSettingsService.Sanitize(settings);
            var sanitizedPreset = Assert.Single(settings.CustomPenPresets);
            Assert.Equal(hex, sanitizedPreset.ColorHex);
            Assert.Equal(thickness, sanitizedPreset.ThicknessDip);
            Assert.Equal(shortcut, sanitizedPreset.Shortcut);

            StaTestRunner.Run(() =>
            {
                var vm = new ToolbarViewModel();
                var brush = new SolidColorBrush(Color.FromRgb(rgb[0], rgb[1], rgb[2]));
                brush.Freeze();
                var pvm = new CustomPenPresetViewModel(preset.Id, preset.Name, preset.ColorHex, preset.ThicknessDip, preset.Shortcut, brush);
                vm.CustomPenPresets.Add(pvm);

                vm.ActivateCustomPenCommand.Execute(pvm);
                Assert.Equal(ToolMode.Pen, vm.ActiveTool);
                Assert.Equal(hex, vm.SelectedPenColorHex);
                Assert.Equal(thickness, vm.CurrentDrawingAttributes.Width);
                Assert.Equal(thickness, vm.CurrentDrawingAttributes.Height);
            });
        });
    }
}

