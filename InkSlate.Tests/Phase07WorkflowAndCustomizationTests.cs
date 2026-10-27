using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using CsCheck;
using InkSlate.App.ViewModels;
using InkSlate.App.Views;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.Services;
using Xunit;

namespace InkSlate.Tests;

/// <summary>
/// Consolidated end-to-end integration and property-based test suite for Phase 7:
/// 1. Document Title & Help Dialog (Step 7.1)
/// 2. Canvas Object & Stroke Copy/Paste (Step 7.2)
/// 3. Custom Color & Pen Settings Persistence (Steps 7.3 & 7.4)
/// 4. Bottom-Left PDF Export Progress & Completion (Step 7.5)
/// </summary>
public sealed class Phase07WorkflowAndCustomizationTests : IDisposable
{
    private readonly string _testDirectory;

    public Phase07WorkflowAndCustomizationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"InkSlate_Phase07Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failure in temporary test directory
        }
    }

    // =========================================================================
    // Pillar 1: Document Title & Help Dialog (Step 7.1)
    // =========================================================================

    [Fact]
    public void TitleDisplayPill_SingleClickDoesNotEdit_DoubleClickEntersEditMode_AndRepositionedOutsideTitleBar()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var window = new MainWindow(vm);

            var titleHeader = window.FindName("DocumentTitleHeaderBar") as Border;
            Assert.NotNull(titleHeader);

            // DocumentTitleHeaderBar must reside inside the main workspace grid, not the OS TitleBar
            var parent = VisualTreeHelper.GetParent(titleHeader) as FrameworkElement;
            Assert.NotNull(parent);
            Assert.IsType<Grid>(parent);

            var titlePill = window.FindName("TitleDisplayPill") as Border;
            Assert.NotNull(titlePill);
            Assert.Contains("Double-click to edit document title", titlePill.ToolTip?.ToString() ?? string.Empty);

            Assert.False(vm.IsEditingTitle);

            // Single click must NOT enter edit mode
            window.HandleTitlePillClick(1);
            Assert.False(vm.IsEditingTitle);

            // Double click MUST enter edit mode
            window.HandleTitlePillClick(2);
            Assert.True(vm.IsEditingTitle);
            Assert.Equal(vm.DocumentTitle, vm.TitleDraft);
        });
    }

    [Fact]
    public void CsCheck_DocumentTitle_CommitAndCancel_PreservesInvariantsAndSanitizesFileName()
    {
        Gen.Select(
            Gen.Char.Array[1, 20],
            Gen.Bool
        ).Sample((rawChars, shouldCommit) =>
        {
            var rawTitle = new string(rawChars);
            var vm = new MainViewModel();
            var initialTitle = vm.DocumentTitle;

            vm.BeginEditTitleCommand.Execute(null);
            Assert.True(vm.IsEditingTitle);

            vm.TitleDraft = rawTitle;

            if (shouldCommit)
            {
                vm.CommitTitleEditCommand.Execute(null);
                Assert.False(vm.IsEditingTitle);

                var expectedTitle = string.IsNullOrWhiteSpace(rawTitle) ? initialTitle : rawTitle.Trim();
                Assert.Equal(expectedTitle, vm.DocumentTitle);
            }
            else
            {
                vm.CancelTitleEditCommand.Execute(null);
                Assert.False(vm.IsEditingTitle);
                Assert.Equal(initialTitle, vm.DocumentTitle);
            }

            // Safe file name must never contain illegal path characters
            var safeName = vm.GetSafeDocumentTitleForFileName();
            Assert.False(string.IsNullOrWhiteSpace(safeName));
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var ch in invalidChars)
            {
                Assert.DoesNotContain(ch, safeName);
            }
        });
    }

    [Fact]
    public void HelpDialog_ToggleHelpAndF1Shortcut_EnforcesFullPopoverMutualExclusivity()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var window = new MainWindow(vm);

            Assert.False(vm.IsHelpOpen);

            // F1 shortcut toggles help dialog open
            var handledF1 = window.HandleNavigationKey(Key.F1, ModifierKeys.None);
            Assert.True(handledF1);
            Assert.True(vm.IsHelpOpen);

            // Opening another popover (e.g. Page Setup) closes Help dialog
            vm.TogglePageSetupCommand.Execute(null);
            Assert.True(vm.IsPageSetupOpen);
            Assert.False(vm.IsHelpOpen);

            // Reopening Help dialog closes Page Setup
            vm.ToggleHelpCommand.Execute(null);
            Assert.True(vm.IsHelpOpen);
            Assert.False(vm.IsPageSetupOpen);

            // Opening Pen Options closes Help dialog
            vm.Toolbar.IsPenOptionsOpen = true;
            Assert.False(vm.IsHelpOpen);
            Assert.True(vm.Toolbar.IsPenOptionsOpen);

            // Reopening Help dialog closes Pen Options
            vm.ToggleHelpCommand.Execute(null);
            Assert.True(vm.IsHelpOpen);
            Assert.False(vm.Toolbar.IsPenOptionsOpen);

            // Escape closes Help dialog
            var handledEsc = window.HandleNavigationKey(Key.Escape, ModifierKeys.None);
            Assert.True(handledEsc);
            Assert.False(vm.IsHelpOpen);

            // Verify visual tree controls
            var overlay = window.FindName("HelpDialogOverlay") as Grid;
            Assert.NotNull(overlay);
            var closeBtn = window.FindName("CloseHelpDialogButton") as Wpf.Ui.Controls.Button;
            Assert.NotNull(closeBtn);
        });
    }

    // =========================================================================
    // Pillar 2: Canvas Object & Stroke Copy/Paste (Step 7.2)
    // =========================================================================

    [Fact]
    public void CsCheck_CopyAndCascadingPaste_OffsetsElementsAndStrokesAndSupportsSingleStepUndoRedo()
    {
        StaTestRunner.Run(() =>
        {
            Gen.Select(
                Gen.Double[10.0, 300.0],
                Gen.Double[10.0, 300.0],
                Gen.Int[1, 3]
            ).Sample((startX, startY, pasteCount) =>
            {
                var vm = new MainViewModel();
                var page = vm.ActivePage!;
                page.Hydrate();

                // Add text element
                var textModel = new TextCanvasElement
                {
                    X = startX,
                    Y = startY,
                    Width = 150,
                    Height = 60,
                    Text = "Fuzzed Copy Text"
                };
                page.AddElement(textModel);
                page.Elements[0].IsSelected = true;

                // Add stroke
                var originalPoints = new StylusPointCollection
                {
                    new StylusPoint(startX, startY),
                    new StylusPoint(startX + 20, startY + 20)
                };
                var originalStroke = new Stroke(originalPoints);
                page.Strokes!.Add(originalStroke);
                page.SelectedStrokes = [originalStroke];

                // Copy both simultaneously
                vm.CopySelectedCommand.Execute(null);

                // Perform cascading pastes
                for (var i = 1; i <= pasteCount; i++)
                {
                    vm.PasteClipboardCommand.Execute(null);
                    var expectedOffset = i * 20.0;

                    // Text element offset and unique ID
                    var pastedText = page.Elements[^1];
                    Assert.NotEqual(textModel.Id, pastedText.Model.Id);
                    Assert.Equal(startX + expectedOffset, pastedText.X, tolerance: 0.001);
                    Assert.Equal(startY + expectedOffset, pastedText.Y, tolerance: 0.001);

                    // Stroke offset
                    var pastedStroke = page.Strokes[^1];
                    Assert.NotSame(originalStroke, pastedStroke);
                    Assert.Equal(startX + expectedOffset, pastedStroke.StylusPoints[0].X, tolerance: 0.05);
                    Assert.Equal(startY + expectedOffset, pastedStroke.StylusPoints[0].Y, tolerance: 0.05);
                }

                // Verify each paste is rolled back atomically in a single Undo() step
                for (var i = pasteCount; i >= 1; i--)
                {
                    Assert.Equal(1 + i, page.Elements.Count);
                    Assert.Equal(1 + i, page.Strokes.Count);
                    vm.UndoCommand.Execute(null);
                }

                Assert.Single(page.Elements);
                Assert.Single(page.Strokes);

                // Redo restores them atomically
                for (var i = 1; i <= pasteCount; i++)
                {
                    vm.RedoCommand.Execute(null);
                    Assert.Equal(1 + i, page.Elements.Count);
                    Assert.Equal(1 + i, page.Strokes.Count);
                }
            });
        });
    }

    // =========================================================================
    // Pillar 3: Custom Color & Pen Settings Persistence (Steps 7.3 & 7.4)
    // =========================================================================

    [Fact]
    public void CsCheck_UserSettingsService_DiskRoundTripAndCorruptedJsonResilience()
    {
        Gen.Select(
            Gen.Byte.Array[3],
            Gen.Double[0.5, 24.0],
            Gen.Int[1, 9]
        ).Sample((rgb, thickness, digit) =>
        {
            var tempSettingsPath = Path.Combine(_testDirectory, $"settings_{Guid.NewGuid():N}.json");
            var service = new UserSettingsService(tempSettingsPath);

            var hex = $"#FF{rgb[0]:X2}{rgb[1]:X2}{rgb[2]:X2}";
            var shortcut = digit.ToString(CultureInfo.InvariantCulture);
            var presetId = Guid.NewGuid();

            var originalSettings = new UserSettings
            {
                CustomColorHexes = [hex],
                CustomPenPresets =
                [
                    new CustomPenPreset
                    {
                        Id = presetId,
                        Name = $"Preset {digit}",
                        ColorHex = hex,
                        ThicknessDip = thickness,
                        Shortcut = shortcut
                    }
                ]
            };

            // 1. Save and reload round-trip
            service.Save(originalSettings);
            Assert.True(File.Exists(tempSettingsPath));

            var loaded = service.Load();
            var loadedHex = Assert.Single(loaded.CustomColorHexes);
            Assert.Equal(hex, loadedHex);

            var loadedPreset = Assert.Single(loaded.CustomPenPresets);
            Assert.Equal(presetId, loadedPreset.Id);
            Assert.Equal(hex, loadedPreset.ColorHex);
            Assert.Equal(thickness, loadedPreset.ThicknessDip);
            Assert.Equal(shortcut, loadedPreset.Shortcut);

            // 2. Resilience against corrupted JSON on disk
            File.WriteAllText(tempSettingsPath, "{ corrupted invalid json <<<");
            var recoveredSettings = service.Load();
            Assert.NotNull(recoveredSettings);
            Assert.Empty(recoveredSettings.CustomColorHexes);
            Assert.Empty(recoveredSettings.CustomPenPresets);
        });
    }

    [Fact]
    public void CustomPenRightDock_VisibilityAndShortcutActivation_AutoSavesOnEditAndDelete()
    {
        StaTestRunner.Run(() =>
        {
            var tempSettingsPath = Path.Combine(_testDirectory, $"dock_settings_{Guid.NewGuid():N}.json");
            var settingsService = new UserSettingsService(tempSettingsPath);
            var toolbarVm = new ToolbarViewModel(settingsService);
            var vm = new MainViewModel(new NoteDocument(), new InkSlate.Core.UndoRedo.UndoRedoManager(), toolbarVm);

            var window = new MainWindow(vm);

            var rightDock = window.FindName("CustomPenRightDock") as Border;
            Assert.NotNull(rightDock);

            // 1. Initially empty dock is collapsed
            Assert.Empty(vm.Toolbar.CustomPenPresets);
            Assert.False(vm.Toolbar.HasCustomPenPresets);

            // 2. Add preset via editor
            vm.Toolbar.OpenNewCustomPenEditorCommand.Execute(null);
            Assert.True(vm.Toolbar.IsCustomPenEditorOpen);

            vm.Toolbar.CustomPenEditorName = "Cobalt Pen";
            vm.Toolbar.CustomPenEditorColorHex = "#FF1E40AF";
            vm.Toolbar.CustomPenEditorThickness = 3.2;
            vm.Toolbar.CustomPenEditorShortcut = "1";
            vm.Toolbar.SaveCustomPenPresetCommand.Execute(null);

            Assert.False(vm.Toolbar.IsCustomPenEditorOpen);
            Assert.True(vm.Toolbar.HasCustomPenPresets);
            var preset = Assert.Single(vm.Toolbar.CustomPenPresets);
            Assert.Equal("Cobalt Pen", preset.Name);

            // Auto-saved to disk
            var diskSettings = settingsService.Load();
            var diskPreset = Assert.Single(diskSettings.CustomPenPresets);
            Assert.Equal("Cobalt Pen", diskPreset.Name);
            Assert.Equal("#FF1E40AF", diskPreset.ColorHex);

            // 3. Activate preset via shortcut (Key.D1)
            var handledKey = window.HandleNavigationKey(Key.D1, ModifierKeys.None);
            Assert.True(handledKey);
            Assert.Equal(ToolMode.Pen, vm.Toolbar.ActiveTool);
            Assert.Equal("#FF1E40AF", vm.Toolbar.SelectedPenColorHex);
            Assert.Equal(3.2, vm.Toolbar.SelectedPenThickness);

            // 4. Edit preset
            vm.Toolbar.OpenEditCustomPenEditorCommand.Execute(preset);
            Assert.True(vm.Toolbar.IsCustomPenEditorOpen);
            vm.Toolbar.CustomPenEditorThickness = 5.0;
            vm.Toolbar.SaveCustomPenPresetCommand.Execute(null);

            Assert.Equal(5.0, preset.ThicknessDip);
            var diskAfterEdit = settingsService.Load();
            Assert.Equal(5.0, diskAfterEdit.CustomPenPresets[0].ThicknessDip);

            // 5. Delete preset
            vm.Toolbar.DeleteCustomPenPresetCommand.Execute(preset);
            Assert.Empty(vm.Toolbar.CustomPenPresets);
            Assert.False(vm.Toolbar.HasCustomPenPresets);

            var diskAfterDelete = settingsService.Load();
            Assert.Empty(diskAfterDelete.CustomPenPresets);
        });
    }

    // =========================================================================
    // Pillar 4: Bottom-Left PDF Export Progress & Completion (Step 7.5)
    // =========================================================================

    [Fact]
    public void CsCheck_PdfExportProgress_MonotonicityAndBounds()
    {
        Gen.Select(
            Gen.Int[0, 50],
            Gen.Int[0, 50]
        ).Sample((completed, total) =>
        {
            var progress = new PdfExportProgress(completed, total);
            var pct = progress.Percentage;

            // Clamped within 0.0 and 100.0
            Assert.InRange(pct, 0.0, 100.0);

            // Monotonic property: if completed <= completed + 1, percentage cannot decrease
            var nextProgress = new PdfExportProgress(completed + 1, total);
            Assert.True(nextProgress.Percentage >= progress.Percentage);
        });
    }

    [Fact]
    public async Task ExecutePdfExportAsync_ObservesInProgressState_TransitionsToFinished_AndAutoHides()
    {
        var vm = new MainViewModel
        {
            ExportStatusDismissDelay = TimeSpan.FromMilliseconds(60)
        };
        vm.AddPageCommand.Execute(null); // Document now has 2 pages

        var targetPath = Path.Combine(_testDirectory, $"export_test_{Guid.NewGuid():N}.pdf");
        vm.FilePdfPicker = () => targetPath;
        vm.IsPdfExportOpen = true;

        var observedInProgress = false;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsExportingPdf) && vm.IsExportingPdf)
            {
                observedInProgress = true;
            }
        };

        // Execute export and await completion
        var exportTask = vm.ExecutePdfExportAsync();
        await exportTask;

        // Verify that during execution in-progress state was reached:
        Assert.True(observedInProgress);

        // Upon completion:
        Assert.False(vm.IsPdfExportOpen);
        Assert.False(vm.IsExportingPdf);
        Assert.True(vm.IsExportStatusVisible);
        Assert.True(vm.IsExportCompleted);
        Assert.Equal(100.0, vm.ExportProgressPercentage);
        Assert.Equal("Export finished", vm.ExportStatusMessage);
        Assert.True(File.Exists(targetPath));

        // After the configured dismiss delay, pill auto-hides
        await Task.Delay(180);
        Assert.False(vm.IsExportStatusVisible);
    }
}
