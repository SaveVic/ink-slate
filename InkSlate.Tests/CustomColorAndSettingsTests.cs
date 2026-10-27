using System.IO;
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
}

