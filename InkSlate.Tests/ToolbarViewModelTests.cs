using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using CsCheck;
using InkSlate.App.Controls;
using InkSlate.App.ViewModels;
using InkSlate.App.Views;
using InkSlate.Core.Models;
using Xunit;

namespace InkSlate.Tests;

public sealed class ToolbarViewModelTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void DefaultStateMatchesPenMode()
    {
        var vm = new ToolbarViewModel();

        Assert.Equal(ToolMode.Pen, vm.ActiveTool);
        Assert.True(vm.IsPenToolActive);
        Assert.False(vm.IsHighlighterToolActive);
        Assert.False(vm.IsEraserToolActive);
        Assert.False(vm.IsLassoToolActive);
        Assert.False(vm.IsTextToolActive);
        Assert.False(vm.IsImageToolActive);
        Assert.False(vm.IsPenOptionsOpen);

        var attrs = vm.CurrentDrawingAttributes;
        Assert.True(attrs.FitToCurve);
        Assert.False(attrs.IgnorePressure);
        Assert.False(attrs.IsHighlighter);
        Assert.Equal(StylusTip.Ellipse, attrs.StylusTip);
        Assert.Equal(2.5, attrs.Width);
        Assert.Equal(2.5, attrs.Height);
        Assert.Equal(Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E), attrs.Color);
    }

    [Fact]
    public void SwitchingToHighlighterUpdatesAttributesInPlace()
    {
        var vm = new ToolbarViewModel();
        var initialAttributes = vm.CurrentDrawingAttributes;

        vm.SelectToolCommand.Execute(ToolMode.Highlighter);

        Assert.Same(initialAttributes, vm.CurrentDrawingAttributes);
        Assert.Equal(ToolMode.Highlighter, vm.ActiveTool);
        Assert.True(vm.IsHighlighterToolActive);
        Assert.False(vm.IsPenToolActive);

        var attrs = vm.CurrentDrawingAttributes;
        Assert.False(attrs.FitToCurve);
        Assert.True(attrs.IgnorePressure);
        Assert.True(attrs.IsHighlighter);
        Assert.Equal(StylusTip.Rectangle, attrs.StylusTip);
        Assert.Equal(12.0, attrs.Width);
        Assert.Equal(24.0, attrs.Height);
        Assert.Equal(0x66, attrs.Color.A);
    }

    [Fact]
    public void CuratedColorSwatchesArePreFrozen()
    {
        var vm = new ToolbarViewModel();

        Assert.Equal(6, vm.ColorSwatches.Count);
        foreach (var swatch in vm.ColorSwatches)
        {
            Assert.True(swatch.FrozenBrush.IsFrozen);
            Assert.StartsWith("#", swatch.Hex);
        }
    }

    [Fact]
    public void MicroFlyoutToggleAndAutoClose()
    {
        var vm = new ToolbarViewModel();
        Assert.False(vm.IsPenOptionsOpen);

        // Clicking active tool toggles popover open
        vm.SelectToolCommand.Execute(ToolMode.Pen);
        Assert.True(vm.IsPenOptionsOpen);

        // Clicking again toggles popover closed
        vm.SelectToolCommand.Execute(ToolMode.Pen);
        Assert.False(vm.IsPenOptionsOpen);

        // Re-open and select color swatch -> auto-closes and updates color
        vm.SelectToolCommand.Execute(ToolMode.Pen);
        Assert.True(vm.IsPenOptionsOpen);

        vm.SelectColorSwatchCommand.Execute("#FFDC2626"); // Crimson Red
        Assert.False(vm.IsPenOptionsOpen);
        Assert.Equal(Color.FromArgb(0xFF, 0xDC, 0x26, 0x26), vm.CurrentDrawingAttributes.Color);

        // Re-open and select thickness -> auto-closes and updates size
        vm.SelectToolCommand.Execute(ToolMode.Pen);
        Assert.True(vm.IsPenOptionsOpen);

        vm.SelectPenThicknessCommand.Execute(4.0);
        Assert.False(vm.IsPenOptionsOpen);
        Assert.Equal(4.0, vm.CurrentDrawingAttributes.Width);
        Assert.Equal(4.0, vm.CurrentDrawingAttributes.Height);
    }

    [Fact]
    public void PageCanvasControlSharesDrawingAttributesAndRespondsToToolSwitch()
    {
        RunInSta(() =>
        {
            var toolbar = new ToolbarViewModel();
            var canvas = new PageCanvasControl
            {
                InkDrawingAttributes = toolbar.CurrentDrawingAttributes,
                ActiveToolMode = toolbar.ActiveTool
            };

            Assert.Same(toolbar.CurrentDrawingAttributes, canvas.InkSurface.DefaultDrawingAttributes);
            Assert.Equal(InkCanvasEditingMode.Ink, canvas.InkSurface.EditingMode);

            // Change thickness in toolbar -> canvas reflects it without reallocation
            toolbar.SelectPenThicknessCommand.Execute(6.5);
            Assert.Equal(6.5, canvas.InkSurface.DefaultDrawingAttributes.Width);

            // Switch to Eraser
            toolbar.SelectToolCommand.Execute(ToolMode.Eraser);
            canvas.ActiveToolMode = toolbar.ActiveTool;
            Assert.Equal(InkCanvasEditingMode.EraseByStroke, canvas.InkSurface.EditingMode);

            // Switch to Lasso
            toolbar.SelectToolCommand.Execute(ToolMode.Lasso);
            canvas.ActiveToolMode = toolbar.ActiveTool;
            Assert.Equal(InkCanvasEditingMode.Select, canvas.InkSurface.EditingMode);

            // Switch to Pan
            toolbar.SelectToolCommand.Execute(ToolMode.Pan);
            canvas.ActiveToolMode = toolbar.ActiveTool;
            Assert.Equal(InkCanvasEditingMode.None, canvas.InkSurface.EditingMode);
        });
    }

    [Fact]
    public void MainWindowToolButtonsAndFlyoutElementsExistAndRespondToKeyShortcuts()
    {
        RunInSta(() =>
        {
            var window = new MainWindow();

            var popover = window.FindName("PenOptionsPopover") as System.Windows.Controls.Border;
            Assert.NotNull(popover);

            var swatches = window.FindName("ColorSwatchesItemsControl") as System.Windows.Controls.ItemsControl;
            Assert.NotNull(swatches);

            var dots = window.FindName("ThicknessDotsItemsControl") as System.Windows.Controls.ItemsControl;
            Assert.NotNull(dots);

            // Shortcuts P, H, E, L, T, I
            Assert.True(window.HandleNavigationKey(Key.H, ModifierKeys.None));
            Assert.Equal(ToolMode.Highlighter, window.ViewModel.Toolbar.ActiveTool);

            Assert.True(window.HandleNavigationKey(Key.E, ModifierKeys.None));
            Assert.Equal(ToolMode.Eraser, window.ViewModel.Toolbar.ActiveTool);

            Assert.True(window.HandleNavigationKey(Key.L, ModifierKeys.None));
            Assert.Equal(ToolMode.Lasso, window.ViewModel.Toolbar.ActiveTool);

            Assert.True(window.HandleNavigationKey(Key.T, ModifierKeys.None));
            Assert.Equal(ToolMode.Text, window.ViewModel.Toolbar.ActiveTool);

            Assert.True(window.HandleNavigationKey(Key.I, ModifierKeys.None));
            Assert.Equal(ToolMode.Image, window.ViewModel.Toolbar.ActiveTool);

            Assert.True(window.HandleNavigationKey(Key.P, ModifierKeys.None));
            Assert.Equal(ToolMode.Pen, window.ViewModel.Toolbar.ActiveTool);
        });
    }

    [Fact]
    public void CsCheckPropertyFuzzing_ThicknessAndColorAttributesAlwaysRemainValid()
    {
        Gen.Select(
            Gen.Double[-5.0, 50.0],
            Gen.OneOf(
                Gen.Const("#FF1E1E1E"),
                Gen.Const("#FF2563EB"),
                Gen.Const("#FFDC2626"),
                Gen.Const("invalid"),
                Gen.Const("")),
            Gen.OneOf(
                Gen.Const(ToolMode.Pen),
                Gen.Const(ToolMode.Highlighter),
                Gen.Const(ToolMode.Eraser)))
        .Sample((thickness, colorHex, tool) =>
        {
            var vm = new ToolbarViewModel();

            vm.SelectToolCommand.Execute(tool);
            vm.SelectPenThicknessCommand.Execute(thickness);
            vm.SelectColorSwatchCommand.Execute(colorHex);

            Assert.True(vm.CurrentDrawingAttributes.Width >= 0.5);
            Assert.True(vm.CurrentDrawingAttributes.Width <= 32.0);
            Assert.True(vm.CurrentDrawingAttributes.Height >= 0.5);
            Assert.True(vm.CurrentDrawingAttributes.Height <= 32.0);
            Assert.True(double.IsFinite(vm.CurrentDrawingAttributes.Width));
            Assert.True(double.IsFinite(vm.CurrentDrawingAttributes.Height));
        });
    }
}
