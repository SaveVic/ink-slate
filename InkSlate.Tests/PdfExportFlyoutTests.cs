using System.IO;
using System.Windows.Controls;
using InkSlate.App.ViewModels;
using InkSlate.App.Views;
using InkSlate.Core.Models;
using InkSlate.Core.Services;
using Xunit;

namespace InkSlate.Tests;

public sealed class PdfExportFlyoutTests : IDisposable
{
    private readonly string _testDirectory;

    public PdfExportFlyoutTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"InkSlate_FlyoutTests_{Guid.NewGuid():N}");
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
            // Ignore cleanup failure in test directory
        }
    }

    [Fact]
    public void TogglePdfExport_ClosesOtherPopoversAndTogglesOpen()
    {
        var vm = new MainViewModel();

        // 1. Open Page Setup first
        vm.TogglePageSetupCommand.Execute(null);
        Assert.True(vm.IsPageSetupOpen);

        // 2. Open PDF export -> Page Setup must close
        vm.TogglePdfExportCommand.Execute(null);
        Assert.True(vm.IsPdfExportOpen);
        Assert.False(vm.IsPageSetupOpen);

        // 3. Open Pen options on toolbar -> PDF export must close
        vm.Toolbar.IsPenOptionsOpen = true;
        Assert.False(vm.IsPdfExportOpen);

        // 4. Reopen PDF export -> Pen options must close
        vm.TogglePdfExportCommand.Execute(null);
        Assert.True(vm.IsPdfExportOpen);
        Assert.False(vm.Toolbar.IsPenOptionsOpen);

        // 5. Toggle PDF export again -> closes
        vm.TogglePdfExportCommand.Execute(null);
        Assert.False(vm.IsPdfExportOpen);
    }

    [Fact]
    public void SelectPdfExportMode_SwitchesActiveModeAndFlags()
    {
        var vm = new MainViewModel();

        Assert.Equal(PdfExportPageMode.AllPages, vm.PdfExportMode);
        Assert.True(vm.IsPdfExportAll);
        Assert.False(vm.IsPdfExportCurrent);
        Assert.False(vm.IsPdfExportRange);

        // Switch to Current
        vm.SelectPdfExportModeCommand.Execute(PdfExportPageMode.CurrentPage);
        Assert.Equal(PdfExportPageMode.CurrentPage, vm.PdfExportMode);
        Assert.False(vm.IsPdfExportAll);
        Assert.True(vm.IsPdfExportCurrent);
        Assert.False(vm.IsPdfExportRange);

        // Switch to CustomRange
        vm.SelectPdfExportModeCommand.Execute(PdfExportPageMode.CustomRange);
        Assert.Equal(PdfExportPageMode.CustomRange, vm.PdfExportMode);
        Assert.False(vm.IsPdfExportAll);
        Assert.False(vm.IsPdfExportCurrent);
        Assert.True(vm.IsPdfExportRange);
    }

    [Fact]
    public async Task ExecuteExport_WithInvalidRange_FlagsErrorWithoutOpeningDialog()
    {
        var vm = new MainViewModel();
        vm.SelectPdfExportModeCommand.Execute(PdfExportPageMode.CustomRange);
        vm.PdfCustomRangeInput = "8-5"; // Inverted invalid range

        var pickerInvoked = false;
        vm.FilePdfPicker = () =>
        {
            pickerInvoked = true;
            return Path.Combine(_testDirectory, "never_created.pdf");
        };

        await vm.ExecutePdfExportAsync();

        Assert.True(vm.HasPdfRangeError);
        Assert.False(pickerInvoked);
    }

    [Fact]
    public async Task ExecuteExport_WithValidRange_ExportsTargetPagesAndCloses()
    {
        var vm = new MainViewModel();
        vm.AddPageCommand.Execute(null); // Now has 2 pages

        vm.SelectPdfExportModeCommand.Execute(PdfExportPageMode.CustomRange);
        vm.PdfCustomRangeInput = "1";
        vm.IsPdfExportOpen = true;

        var targetPath = Path.Combine(_testDirectory, "custom_export.pdf");
        vm.FilePdfPicker = () => targetPath;

        await vm.ExecutePdfExportAsync();

        Assert.False(vm.HasPdfRangeError);
        Assert.False(vm.IsPdfExportOpen);
        Assert.True(File.Exists(targetPath));

        var bytes = await File.ReadAllBytesAsync(targetPath);
        Assert.Equal((byte)'%', bytes[0]);
    }

    [Fact]
    public async Task ExecuteExport_AllPagesMode_ExportsSuccessfully()
    {
        var vm = new MainViewModel();
        vm.AddPageCommand.Execute(null);

        vm.SelectPdfExportModeCommand.Execute(PdfExportPageMode.AllPages);
        vm.IsPdfExportOpen = true;

        var targetPath = Path.Combine(_testDirectory, "all_pages_export.pdf");
        vm.FilePdfPicker = () => targetPath;

        await vm.ExecutePdfExportAsync();

        Assert.False(vm.IsPdfExportOpen);
        Assert.True(File.Exists(targetPath));
    }

    [Fact]
    public async Task ExecuteExport_WhenPickerCancelled_DoesNotThrow()
    {
        var vm = new MainViewModel();
        vm.IsPdfExportOpen = true;
        vm.FilePdfPicker = () => null; // User clicked Cancel in dialog

        await vm.ExecutePdfExportAsync();

        // Should not crash and state remains clean
        Assert.False(vm.IsExportingPdf);
    }

    [Fact]
    public void CustomRangeInputChanged_ClearsRangeError()
    {
        var vm = new MainViewModel();
        vm.HasPdfRangeError = true;

        vm.PdfCustomRangeInput = "1, 2";

        Assert.False(vm.HasPdfRangeError);
    }

    [Fact]
    public void PdfExportProgress_CalculatesCorrectPercentages()
    {
        // 0 of 4 -> 0%
        var progress0 = new PdfExportProgress(0, 4);
        Assert.Equal(0.0, progress0.Percentage);

        // 1 of 4 -> 25%
        var progress1 = new PdfExportProgress(1, 4);
        Assert.Equal(25.0, progress1.Percentage);

        // 2 of 4 -> 50%
        var progress2 = new PdfExportProgress(2, 4);
        Assert.Equal(50.0, progress2.Percentage);

        // 4 of 4 -> 100%
        var progress4 = new PdfExportProgress(4, 4);
        Assert.Equal(100.0, progress4.Percentage);

        // 0 of 0 -> 100%
        var progressEmpty = new PdfExportProgress(0, 0);
        Assert.Equal(100.0, progressEmpty.Percentage);

        // Clamping checks
        var progressOverflow = new PdfExportProgress(10, 4);
        Assert.Equal(100.0, progressOverflow.Percentage);

        var progressUnderflow = new PdfExportProgress(-2, 4);
        Assert.Equal(0.0, progressUnderflow.Percentage);
    }

    [Fact]
    public async Task PdfExportService_ReportsMonotonicProgressDuringExport()
    {
        var doc = new NoteDocument();
        doc.Pages.Add(new NotePage { PageNumber = 1, Width = 800, Height = 600 });
        doc.Pages.Add(new NotePage { PageNumber = 2, Width = 800, Height = 600 });
        doc.Pages.Add(new NotePage { PageNumber = 3, Width = 800, Height = 600 });

        var progressReports = new List<PdfExportProgress>();
        var progress = new Progress<PdfExportProgress>(p => progressReports.Add(p));

        using var ms = new MemoryStream();
        await PdfExportService.ExportAsync(doc, [0, 1, 2], ms, progress: progress);

        // Allow any asynchronous Progress<T> dispatch to complete
        await Task.Delay(50);

        Assert.NotEmpty(progressReports);
        Assert.Equal(0.0, progressReports[0].Percentage);
        Assert.Equal(100.0, progressReports[^1].Percentage);

        // Must be monotonically increasing
        for (var i = 1; i < progressReports.Count; i++)
        {
            Assert.True(progressReports[i].Percentage >= progressReports[i - 1].Percentage);
        }
    }

    [Fact]
    public async Task ExecuteExport_UpdatesProgressAndTransitionsToFinished()
    {
        var vm = new MainViewModel();
        vm.AddPageCommand.Execute(null); // 2 pages total

        var targetPath = Path.Combine(_testDirectory, "status_test.pdf");
        vm.FilePdfPicker = () => targetPath;
        vm.IsPdfExportOpen = true;

        await vm.ExecutePdfExportAsync();

        Assert.False(vm.IsPdfExportOpen);
        Assert.False(vm.IsExportingPdf);
        Assert.True(vm.IsExportStatusVisible);
        Assert.True(vm.IsExportCompleted);
        Assert.Equal(100.0, vm.ExportProgressPercentage);
        Assert.Equal("Export finished", vm.ExportStatusMessage);
        Assert.True(File.Exists(targetPath));
    }

    [Fact]
    public async Task ExecuteExport_AutoDismissesAfterConfiguredDelay()
    {
        var vm = new MainViewModel
        {
            ExportStatusDismissDelay = TimeSpan.FromMilliseconds(50)
        };

        var targetPath = Path.Combine(_testDirectory, "dismiss_test.pdf");
        vm.FilePdfPicker = () => targetPath;

        await vm.ExecutePdfExportAsync();

        Assert.True(vm.IsExportStatusVisible);
        Assert.True(vm.IsExportCompleted);

        // Wait for dismiss delay to expire
        await Task.Delay(180);

        Assert.False(vm.IsExportStatusVisible);
    }

    [Fact]
    public async Task DismissExportStatusCommand_ImmediatelyHidesStatusPill()
    {
        var vm = new MainViewModel
        {
            ExportStatusDismissDelay = TimeSpan.FromSeconds(60) // long delay so it doesn't auto-dismiss immediately
        };

        var targetPath = Path.Combine(_testDirectory, "manual_dismiss.pdf");
        vm.FilePdfPicker = () => targetPath;

        await vm.ExecutePdfExportAsync();

        Assert.True(vm.IsExportStatusVisible);

        vm.DismissExportStatusCommand.Execute(null);

        Assert.False(vm.IsExportStatusVisible);
    }

    [Fact]
    public void MainWindow_ContainsExportStatusPillAndBindings()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var window = new MainWindow(vm);

            var pill = window.FindName("ExportStatusPill") as Border;
            Assert.NotNull(pill);

            var inProgressPanel = window.FindName("ExportInProgressPanel") as StackPanel;
            Assert.NotNull(inProgressPanel);

            var completedPanel = window.FindName("ExportCompletedPanel") as StackPanel;
            Assert.NotNull(completedPanel);

            var progressBar = window.FindName("ExportProgressBar") as ProgressBar;
            Assert.NotNull(progressBar);
            Assert.Equal(0, progressBar.Minimum);
            Assert.Equal(100, progressBar.Maximum);

            var progressText = window.FindName("ExportProgressText") as TextBlock;
            Assert.NotNull(progressText);

            var finishedText = window.FindName("ExportFinishedText") as TextBlock;
            Assert.NotNull(finishedText);

            var dismissBtn = window.FindName("DismissExportStatusButton") as Wpf.Ui.Controls.Button;
            Assert.NotNull(dismissBtn);
        });
    }
}
