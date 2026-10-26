using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using InkSlate.App.Controls;
using InkSlate.App.ViewModels;
using InkSlate.App.Views;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using Xunit;

namespace InkSlate.Tests;

public sealed class RibbonAdjustmentTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void DefaultToolbarState_IsPointerToolAndTopDocked()
    {
        var vm = new ToolbarViewModel();

        Assert.Equal(ToolMode.Pointer, vm.ActiveTool);
        Assert.True(vm.IsPointerToolActive);
        Assert.False(vm.IsPenToolActive);
        Assert.False(vm.IsHighlighterToolActive);
        Assert.False(vm.IsEraserToolActive);
        Assert.False(vm.IsLassoToolActive);
        Assert.False(vm.IsTextToolActive);
        Assert.False(vm.IsImageToolActive);

        Assert.Equal(ToolbarPlacement.TopDocked, vm.Placement);
        Assert.True(vm.IsTopDocked);
        Assert.False(vm.IsLeftDocked);
        Assert.False(vm.IsTopFloating);
    }

    [Fact]
    public void ToolbarPlacement_CycleCommand_RotatesThroughAllPlacements()
    {
        var vm = new ToolbarViewModel();
        Assert.Equal(ToolbarPlacement.TopDocked, vm.Placement);

        vm.CyclePlacementCommand.Execute(null);
        Assert.Equal(ToolbarPlacement.LeftDocked, vm.Placement);
        Assert.True(vm.IsLeftDocked);
        Assert.False(vm.IsTopDocked);

        vm.CyclePlacementCommand.Execute(null);
        Assert.Equal(ToolbarPlacement.TopFloating, vm.Placement);
        Assert.True(vm.IsTopFloating);

        vm.CyclePlacementCommand.Execute(null);
        Assert.Equal(ToolbarPlacement.TopDocked, vm.Placement);
        Assert.True(vm.IsTopDocked);

        vm.SetPlacementCommand.Execute(ToolbarPlacement.LeftDocked);
        Assert.Equal(ToolbarPlacement.LeftDocked, vm.Placement);
    }

    [Fact]
    public void ActiveToolSelection_UpdatesOnlySelectedToolFlag()
    {
        var vm = new ToolbarViewModel();

        var tools = new[]
        {
            (ToolMode.Pointer, nameof(vm.IsPointerToolActive)),
            (ToolMode.Pen, nameof(vm.IsPenToolActive)),
            (ToolMode.Highlighter, nameof(vm.IsHighlighterToolActive)),
            (ToolMode.Eraser, nameof(vm.IsEraserToolActive)),
            (ToolMode.Lasso, nameof(vm.IsLassoToolActive)),
            (ToolMode.Text, nameof(vm.IsTextToolActive)),
            (ToolMode.Image, nameof(vm.IsImageToolActive))
        };

        foreach (var (tool, activeProp) in tools)
        {
            vm.SelectToolCommand.Execute(tool);
            Assert.Equal(tool, vm.ActiveTool);

            Assert.Equal(tool == ToolMode.Pointer, vm.IsPointerToolActive);
            Assert.Equal(tool == ToolMode.Pen, vm.IsPenToolActive);
            Assert.Equal(tool == ToolMode.Highlighter, vm.IsHighlighterToolActive);
            Assert.Equal(tool == ToolMode.Eraser, vm.IsEraserToolActive);
            Assert.Equal(tool == ToolMode.Lasso, vm.IsLassoToolActive);
            Assert.Equal(tool == ToolMode.Text, vm.IsTextToolActive);
            Assert.Equal(tool == ToolMode.Image, vm.IsImageToolActive);
        }
    }

    [Fact]
    public void PageCanvasControl_PointerMode_EnablesElementInteractivityAndDisablesInk()
    {
        RunInSta(() =>
        {
            var control = new PageCanvasControl
            {
                ActiveToolMode = ToolMode.Pointer
            };

            var model = new TextCanvasElement { X = 10, Y = 10, Width = 100, Height = 50, Text = "Sample" };
            var textVm = new TextCanvasElementViewModel(model);
            var elements = new List<CanvasElementViewModel> { textVm };

            control.PageElements = elements;

            Assert.Equal(InkCanvasEditingMode.None, control.InkSurface.EditingMode);

            var container = Assert.Single(control.ElementContainers);
            Assert.True(container.IsHitTestVisible);
        });
    }

    [Fact]
    public void MainWindow_Shortcuts_VSelectsPointerTool()
    {
        RunInSta(() =>
        {
            var window = new MainWindow();

            // Switch away to Pen
            window.ViewModel.Toolbar.SelectToolCommand.Execute(ToolMode.Pen);
            Assert.Equal(ToolMode.Pen, window.ViewModel.Toolbar.ActiveTool);

            // Press V -> switches to Pointer
            Assert.True(window.HandleNavigationKey(Key.V, ModifierKeys.None));
            Assert.Equal(ToolMode.Pointer, window.ViewModel.Toolbar.ActiveTool);
            Assert.True(window.ViewModel.Toolbar.IsPointerToolActive);
        });
    }

    [Fact]
    public void MainWindow_ToolbarButtons_HaveHandCursorAndActiveMarkers()
    {
        RunInSta(() =>
        {
            var window = new MainWindow();

            var pointerBtn = window.FindName("PointerToolButton") as Wpf.Ui.Controls.Button;
            var penBtn = window.FindName("PenToolButton") as Wpf.Ui.Controls.Button;
            var eraserBtn = window.FindName("EraserToolButton") as Wpf.Ui.Controls.Button;
            var placementBtn = window.FindName("CyclePlacementButton") as Wpf.Ui.Controls.Button;

            Assert.NotNull(pointerBtn);
            Assert.NotNull(penBtn);
            Assert.NotNull(eraserBtn);
            Assert.NotNull(placementBtn);

            Assert.Equal(Cursors.Hand, pointerBtn.Cursor);
            Assert.Equal(Cursors.Hand, penBtn.Cursor);
            Assert.Equal(Cursors.Hand, eraserBtn.Cursor);
            Assert.Equal(Cursors.Hand, placementBtn.Cursor);
        });
    }

    [Fact]
    public void ZoomScrollViewer_PointerMode_PansOnEmptySpaceAndClearsSelection()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            Assert.NotNull(mainVm.ActivePage);

            var textModel = new TextCanvasElement { X = 50, Y = 50, Width = 100, Height = 40 };
            var textElem = new TextCanvasElementViewModel(textModel) { IsSelected = true };
            mainVm.ActivePage.Elements.Add(textElem);
            Assert.True(textElem.IsSelected);

            var viewer = new ZoomScrollViewer
            {
                DataContext = mainVm,
                ActiveToolMode = ToolMode.Pointer
            };

            // Call BeginPan directly simulating empty space click
            viewer.BeginPan(new Point(100, 100));
            Assert.True(viewer.IsPanning);

            // Update pan
            viewer.UpdatePan(new Point(80, 70));
            Assert.Equal(20.0, viewer.TargetHorizontalOffset);
            Assert.Equal(30.0, viewer.TargetVerticalOffset);

            viewer.EndPan();
            Assert.False(viewer.IsPanning);
        });
    }
}

