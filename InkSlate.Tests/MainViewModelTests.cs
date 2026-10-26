using CsCheck;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.UndoRedo;
using Xunit;

namespace InkSlate.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void InitialStateHasSingleA4PageAndDefaultZoom()
    {
        var vm = new MainViewModel();

        Assert.Single(vm.Pages);
        Assert.NotNull(vm.ActivePage);
        Assert.Equal(1, vm.ActivePage.PageNumber);
        Assert.Equal(PageDimensions.A4.Width, vm.ActivePage.Width);
        Assert.Equal(PageDimensions.A4.Height, vm.ActivePage.Height);
        Assert.Equal("1 / 1", vm.PageIndicatorDisplay);
        Assert.Equal("100%", vm.ZoomPercentageDisplay);
        Assert.Equal(ViewMode.Consecutive, vm.CurrentViewMode);
        Assert.False(vm.DeletePageCommand.CanExecute(null));
        Assert.False(vm.PreviousPageCommand.CanExecute(null));
        Assert.False(vm.NextPageCommand.CanExecute(null));
    }

    [Fact]
    public void AddPageIncreasesPageCountAndSelectsNewPage()
    {
        var vm = new MainViewModel();

        vm.AddPageCommand.Execute(null);

        Assert.Equal(2, vm.Pages.Count);
        Assert.Equal(2, vm.ActivePage?.PageNumber);
        Assert.Equal("2 / 2", vm.PageIndicatorDisplay);
        Assert.True(vm.DeletePageCommand.CanExecute(null));
        Assert.True(vm.PreviousPageCommand.CanExecute(null));
        Assert.False(vm.NextPageCommand.CanExecute(null));
    }

    [Fact]
    public void NavigationCommandsSwitchActivePage()
    {
        var vm = new MainViewModel();
        vm.AddPageCommand.Execute(null); // Now on page 2

        Assert.True(vm.PreviousPageCommand.CanExecute(null));
        vm.PreviousPageCommand.Execute(null);

        Assert.Equal(1, vm.ActivePage?.PageNumber);
        Assert.Equal("1 / 2", vm.PageIndicatorDisplay);
        Assert.False(vm.PreviousPageCommand.CanExecute(null));
        Assert.True(vm.NextPageCommand.CanExecute(null));

        vm.NextPageCommand.Execute(null);
        Assert.Equal(2, vm.ActivePage?.PageNumber);
        Assert.Equal("2 / 2", vm.PageIndicatorDisplay);
    }

    [Fact]
    public void DeletePageMaintainsMinimumOnePageAndRenumbers()
    {
        var vm = new MainViewModel();
        vm.AddPageCommand.Execute(null);
        vm.AddPageCommand.Execute(null); // 3 pages, on page 3

        Assert.Equal(3, vm.Pages.Count);

        vm.DeletePageCommand.Execute(null); // Deleted page 3, now on page 2
        Assert.Equal(2, vm.Pages.Count);
        Assert.Equal(2, vm.ActivePage?.PageNumber);
        Assert.Equal("2 / 2", vm.PageIndicatorDisplay);

        vm.DeletePageCommand.Execute(null); // Deleted page 2, now on page 1
        Assert.Single(vm.Pages);
        Assert.Equal(1, vm.ActivePage?.PageNumber);
        Assert.Equal("1 / 1", vm.PageIndicatorDisplay);

        // Cannot delete the last remaining page
        Assert.False(vm.DeletePageCommand.CanExecute(null));
    }

    [Fact]
    public void ToggleViewModeSwitchesConsecutiveAndSeparated()
    {
        var vm = new MainViewModel();
        Assert.Equal(ViewMode.Consecutive, vm.CurrentViewMode);

        vm.ToggleViewModeCommand.Execute(null);
        Assert.Equal(ViewMode.Separated, vm.CurrentViewMode);

        vm.ToggleViewModeCommand.Execute(null);
        Assert.Equal(ViewMode.Consecutive, vm.CurrentViewMode);
    }

    [Fact]
    public void ZoomClampingAndResetWorkAccurately()
    {
        var vm = new MainViewModel();

        vm.SetZoom(2.5);
        Assert.Equal(2.5, vm.ZoomLevel);
        Assert.Equal("250%", vm.ZoomPercentageDisplay);

        vm.SetZoom(10.0);
        Assert.Equal(MainViewModel.MaxZoomLevel, vm.ZoomLevel);

        vm.SetZoom(0.01);
        Assert.Equal(MainViewModel.MinZoomLevel, vm.ZoomLevel);

        vm.SetZoom(double.NaN);
        Assert.Equal(MainViewModel.DefaultZoomLevel, vm.ZoomLevel);

        vm.SetZoom(1.85);
        vm.ResetZoomCommand.Execute(null);
        Assert.Equal(1.0, vm.ZoomLevel);
        Assert.Equal("100%", vm.ZoomPercentageDisplay);
    }

    [Fact]
    public void PageSetupPresetSelectionUpdatesInputsAndApplies()
    {
        var vm = new MainViewModel();

        vm.TogglePageSetupCommand.Execute(null);
        Assert.True(vm.IsPageSetupOpen);

        vm.SelectPresetCommand.Execute(PageDimensionPreset.Slide16x9);
        Assert.Equal("1280", vm.CustomWidthInput);
        Assert.Equal("720", vm.CustomHeightInput);

        vm.ApplyPageDimensionsCommand.Execute(null);
        Assert.False(vm.IsPageSetupOpen);
        Assert.Equal(1280.0, vm.ActivePage?.Width);
        Assert.Equal(720.0, vm.ActivePage?.Height);
    }

    [Fact]
    public void PageSetupRejectsInvalidDimensions()
    {
        var vm = new MainViewModel();
        var initialWidth = vm.ActivePage?.Width;

        vm.TogglePageSetupCommand.Execute(null);
        vm.CustomWidthInput = "invalid_number";
        vm.ApplyPageDimensionsCommand.Execute(null);

        Assert.True(vm.HasDimensionInputError);
        Assert.True(vm.IsPageSetupOpen);
        Assert.Equal(initialWidth, vm.ActivePage?.Width);

        vm.CustomWidthInput = "-500";
        vm.ApplyPageDimensionsCommand.Execute(null);
        Assert.True(vm.HasDimensionInputError);
        Assert.Equal(initialWidth, vm.ActivePage?.Width);
    }

    [Fact]
    public void UnitToggleConvertsBetweenPxAndMm()
    {
        var vm = new MainViewModel();

        Assert.False(vm.IsMillimeterUnit);
        Assert.Equal("px", vm.DimensionUnitLabel);

        vm.ToggleDimensionUnitCommand.Execute(null);
        Assert.True(vm.IsMillimeterUnit);
        Assert.Equal("mm", vm.DimensionUnitLabel);
        Assert.Equal("210", vm.CustomWidthInput);
        Assert.Equal("297", vm.CustomHeightInput);

        vm.ToggleDimensionUnitCommand.Execute(null);
        Assert.False(vm.IsMillimeterUnit);
        Assert.Equal("px", vm.DimensionUnitLabel);
        Assert.Equal("793.7", vm.CustomWidthInput);
        Assert.Equal("1122.5", vm.CustomHeightInput);
    }

    [Fact]
    public void UndoRedoCommandsTrackUnderlyingManager()
    {
        var vm = new MainViewModel();
        Assert.False(vm.UndoCommand.CanExecute(null));
        Assert.False(vm.RedoCommand.CanExecute(null));

        var pageId = vm.ActivePage!.Id;
        var element = new TextCanvasElement { Text = "Test" };
        var op = new ElementAddedOperation(pageId, vm.ActivePage.Model.Elements, element);

        vm.UndoRedo.ExecuteAndRecord(op);
        Assert.True(vm.UndoCommand.CanExecute(null));
        Assert.False(vm.RedoCommand.CanExecute(null));

        vm.UndoCommand.Execute(null);
        Assert.False(vm.UndoCommand.CanExecute(null));
        Assert.True(vm.RedoCommand.CanExecute(null));

        vm.RedoCommand.Execute(null);
        Assert.True(vm.UndoCommand.CanExecute(null));
        Assert.False(vm.RedoCommand.CanExecute(null));
    }

    [Fact]
    public void ZoomPropertyFuzzingAlwaysMaintainsValidBounds()
    {
        Gen.Double.Sample(requestedZoom =>
        {
            var vm = new MainViewModel();
            vm.SetZoom(requestedZoom);

            return double.IsFinite(vm.ZoomLevel)
                && vm.ZoomLevel >= MainViewModel.MinZoomLevel
                && vm.ZoomLevel <= MainViewModel.MaxZoomLevel;
        });
    }

    [Fact]
    public void DimensionInputFuzzingNeverCrashes()
    {
        Gen.Select(Gen.String, Gen.String).Sample((w, h) =>
        {
            var vm = new MainViewModel
            {
                CustomWidthInput = w,
                CustomHeightInput = h
            };

            vm.ApplyPageDimensionsCommand.Execute(null);

            return vm.ActivePage is not null
                && double.IsFinite(vm.ActivePage.Width)
                && double.IsFinite(vm.ActivePage.Height);
        });
    }
}
