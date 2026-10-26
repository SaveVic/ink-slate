using System.IO;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;
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
}
