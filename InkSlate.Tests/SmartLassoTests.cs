using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using CsCheck;
using InkSlate.App.Controls;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.UndoRedo;
using Xunit;

namespace InkSlate.Tests;

public sealed class SmartLassoTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void LassoToolSetsInkCanvasEditingModeToSelect()
    {
        RunInSta(() =>
        {
            var control = new PageCanvasControl();
            Assert.Equal(InkCanvasEditingMode.None, control.InkSurface.EditingMode);

            control.ActiveToolMode = ToolMode.Lasso;
            Assert.Equal(InkCanvasEditingMode.Select, control.InkSurface.EditingMode);

            control.ActiveToolMode = ToolMode.Pen;
            Assert.Equal(InkCanvasEditingMode.Ink, control.InkSurface.EditingMode);
        });
    }

    [Fact]
    public void LassoFilterStrokesOnlyExcludesElements()
    {
        RunInSta(() =>
        {
            var page = new NotePage();
            var element = new TextCanvasElement { X = 50, Y = 50, Width = 100, Height = 50, Text = "Hello" };
            page.Elements.Add(element);

            var pageVm = new PageViewModel(page);
            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                PageElements = pageVm.Elements,
                ActiveToolMode = ToolMode.Lasso,
                ActiveLassoFilterMode = LassoFilterMode.StrokesOnly
            };

            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(10, 10), new StylusPoint(20, 20) });
            control.InkSurface.Strokes.Add(stroke);

            var container = control.GetContainer(pageVm.Elements[0]);
            Assert.NotNull(container);

            // Trigger selection with both strokes and elements
            control.InkSurface.Select(new StrokeCollection { stroke }, [container]);

            // Under StrokesOnly filter, container must be stripped out and stroke retained
            var selectedElements = control.InkSurface.GetSelectedElements();
            var selectedStrokes = control.InkSurface.GetSelectedStrokes();

            Assert.Empty(selectedElements);
            Assert.Single(selectedStrokes);
            Assert.Same(stroke, selectedStrokes[0]);
        });
    }

    [Fact]
    public void LassoFilterElementsOnlyExcludesStrokes()
    {
        RunInSta(() =>
        {
            var page = new NotePage();
            var element = new TextCanvasElement { X = 50, Y = 50, Width = 100, Height = 50, Text = "Hello" };
            page.Elements.Add(element);

            var pageVm = new PageViewModel(page);
            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                PageElements = pageVm.Elements,
                ActiveToolMode = ToolMode.Lasso,
                ActiveLassoFilterMode = LassoFilterMode.ElementsOnly
            };

            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(10, 10), new StylusPoint(20, 20) });
            control.InkSurface.Strokes.Add(stroke);

            var container = control.GetContainer(pageVm.Elements[0]);
            Assert.NotNull(container);

            // Trigger selection with both strokes and elements
            control.InkSurface.Select(new StrokeCollection { stroke }, [container]);

            // Under ElementsOnly filter, stroke must be stripped out and container retained
            var selectedElements = control.InkSurface.GetSelectedElements();
            var selectedStrokes = control.InkSurface.GetSelectedStrokes();

            Assert.Single(selectedElements);
            Assert.Same(container, selectedElements[0]);
            Assert.Empty(selectedStrokes);
        });
    }

    [Fact]
    public void LassoFilterAllObjectsRetainsBothStrokesAndElements()
    {
        RunInSta(() =>
        {
            var page = new NotePage();
            var element = new TextCanvasElement { X = 50, Y = 50, Width = 100, Height = 50, Text = "Hello" };
            page.Elements.Add(element);

            var pageVm = new PageViewModel(page);
            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                PageElements = pageVm.Elements,
                ActiveToolMode = ToolMode.Lasso,
                ActiveLassoFilterMode = LassoFilterMode.AllObjects
            };

            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(10, 10), new StylusPoint(20, 20) });
            control.InkSurface.Strokes.Add(stroke);

            var container = control.GetContainer(pageVm.Elements[0]);
            Assert.NotNull(container);

            control.InkSurface.Select(new StrokeCollection { stroke }, [container]);

            var selectedElements = control.InkSurface.GetSelectedElements();
            var selectedStrokes = control.InkSurface.GetSelectedStrokes();

            Assert.Single(selectedElements);
            Assert.Same(container, selectedElements[0]);
            Assert.Single(selectedStrokes);
            Assert.Same(stroke, selectedStrokes[0]);
        });
    }

    [Fact]
    public void SelectionMovedUpdatesElementCoordinatesAndPushesCompoundUndo()
    {
        RunInSta(() =>
        {
            var undoRedo = new UndoRedoManager();
            var page = new NotePage();
            var element = new TextCanvasElement { X = 40, Y = 60, Width = 100, Height = 50, Text = "Move test" };
            page.Elements.Add(element);

            var pageVm = new PageViewModel(page, undoRedo);
            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                PageElements = pageVm.Elements,
                UndoRedoManager = undoRedo,
                ActiveToolMode = ToolMode.Lasso,
                ActiveLassoFilterMode = LassoFilterMode.AllObjects
            };

            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(10, 10), new StylusPoint(20, 20) });
            control.InkSurface.Strokes.Add(stroke);

            var container = control.GetContainer(pageVm.Elements[0]);
            Assert.NotNull(container);

            // Select both
            control.InkSurface.Select(new StrokeCollection { stroke }, [container]);

            // Simulate move transform: shift container by 50, 40 and stroke points
            InkCanvas.SetLeft(container, 90.0);
            InkCanvas.SetTop(container, 100.0);
            stroke.StylusPoints = new StylusPointCollection { new StylusPoint(60, 50), new StylusPoint(70, 60) };

            // Trigger SelectionMoved event
            var selectionMovedMethod = typeof(InkCanvas).GetMethod("OnSelectionMoved", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            selectionMovedMethod?.Invoke(control.InkSurface, [EventArgs.Empty]);

            // Verify element viewmodel coordinates updated
            Assert.Equal(90.0, pageVm.Elements[0].X);
            Assert.Equal(100.0, pageVm.Elements[0].Y);
            Assert.Equal(90.0, element.X);
            Assert.Equal(100.0, element.Y);

            // Verify compound undo recorded
            Assert.True(undoRedo.CanUndo);
            undoRedo.Undo();

            // Reverted
            Assert.Equal(40.0, pageVm.Elements[0].X);
            Assert.Equal(60.0, pageVm.Elements[0].Y);
            Assert.Equal(10.0, stroke.StylusPoints[0].X);
            Assert.Equal(10.0, stroke.StylusPoints[0].Y);

            // Redo
            Assert.True(undoRedo.CanRedo);
            undoRedo.Redo();

            Assert.Equal(90.0, pageVm.Elements[0].X);
            Assert.Equal(100.0, pageVm.Elements[0].Y);
            Assert.Equal(60.0, stroke.StylusPoints[0].X);
            Assert.Equal(50.0, stroke.StylusPoints[0].Y);
        });
    }

    [Fact]
    public void SelectionResizedUpdatesElementDimensionsAndPushesCompoundUndo()
    {
        RunInSta(() =>
        {
            var undoRedo = new UndoRedoManager();
            var page = new NotePage();
            var element = new ImageCanvasElement { X = 100, Y = 100, Width = 200, Height = 150, AssetKey = "assets/pic.png" };
            page.Elements.Add(element);

            var pageVm = new PageViewModel(page, undoRedo);
            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                PageElements = pageVm.Elements,
                UndoRedoManager = undoRedo,
                ActiveToolMode = ToolMode.Lasso,
                ActiveLassoFilterMode = LassoFilterMode.ElementsOnly
            };

            var container = control.GetContainer(pageVm.Elements[0]);
            Assert.NotNull(container);

            control.InkSurface.Select([], [container]);

            // Simulate resize transform
            InkCanvas.SetLeft(container, 120.0);
            InkCanvas.SetTop(container, 130.0);
            container.Width = 300.0;
            container.Height = 225.0;

            var selectionResizedMethod = typeof(InkCanvas).GetMethod("OnSelectionResized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            selectionResizedMethod?.Invoke(control.InkSurface, [EventArgs.Empty]);

            Assert.Equal(120.0, pageVm.Elements[0].X);
            Assert.Equal(130.0, pageVm.Elements[0].Y);
            Assert.Equal(300.0, pageVm.Elements[0].Width);
            Assert.Equal(225.0, pageVm.Elements[0].Height);

            Assert.True(undoRedo.CanUndo);
            undoRedo.Undo();

            Assert.Equal(100.0, pageVm.Elements[0].X);
            Assert.Equal(100.0, pageVm.Elements[0].Y);
            Assert.Equal(200.0, pageVm.Elements[0].Width);
            Assert.Equal(150.0, pageVm.Elements[0].Height);
        });
    }

    [Fact]
    public void ToolbarLassoOptionsTogglesAndFilterModeSelection()
    {
        var toolbar = new ToolbarViewModel();
        Assert.False(toolbar.IsLassoToolActive);
        Assert.False(toolbar.IsLassoOptionsOpen);
        Assert.Equal(LassoFilterMode.AllObjects, toolbar.SelectedLassoFilterMode);
        Assert.True(toolbar.IsLassoAllObjects);
        Assert.False(toolbar.IsLassoStrokesOnly);
        Assert.False(toolbar.IsLassoElementsOnly);

        // Select Lasso tool
        toolbar.SelectToolCommand.Execute(ToolMode.Lasso);
        Assert.True(toolbar.IsLassoToolActive);
        Assert.False(toolbar.IsLassoOptionsOpen);

        // Click again to toggle popover
        toolbar.SelectToolCommand.Execute(ToolMode.Lasso);
        Assert.True(toolbar.IsLassoOptionsOpen);

        // Select Strokes Only filter
        toolbar.SelectLassoFilterCommand.Execute(LassoFilterMode.StrokesOnly);
        Assert.Equal(LassoFilterMode.StrokesOnly, toolbar.SelectedLassoFilterMode);
        Assert.True(toolbar.IsLassoStrokesOnly);
        Assert.False(toolbar.IsLassoOptionsOpen);

        // Select Elements Only filter
        toolbar.SelectLassoFilterCommand.Execute(LassoFilterMode.ElementsOnly);
        Assert.Equal(LassoFilterMode.ElementsOnly, toolbar.SelectedLassoFilterMode);
        Assert.True(toolbar.IsLassoElementsOnly);
    }

    [Fact]
    public void CsCheckPropertyFuzzing_LassoFilterNeverProducesNullOrCorruptedSelection()
    {
        var genFilterMode = Gen.OneOf(
            Gen.Const(LassoFilterMode.StrokesOnly),
            Gen.Const(LassoFilterMode.ElementsOnly),
            Gen.Const(LassoFilterMode.AllObjects));

        var genStrokeCount = Gen.Int[0, 5];
        var genElementCount = Gen.Int[0, 5];

        Gen.Select(genFilterMode, genStrokeCount, genElementCount).Sample((filterMode, strokeCount, elementCount) =>
        {
            RunInSta(() =>
            {
                var page = new NotePage();
                for (var i = 0; i < elementCount; i++)
                {
                    page.Elements.Add(new TextCanvasElement { X = i * 20, Y = i * 20, Width = 100, Height = 50 });
                }

                var pageVm = new PageViewModel(page);
                var control = new PageCanvasControl
                {
                    PageId = pageVm.Id,
                    PageElements = pageVm.Elements,
                    ActiveToolMode = ToolMode.Lasso,
                    ActiveLassoFilterMode = filterMode
                };

                var strokes = new StrokeCollection();
                for (var i = 0; i < strokeCount; i++)
                {
                    var stroke = new Stroke(new StylusPointCollection { new StylusPoint(i * 10, i * 10), new StylusPoint(i * 10 + 5, i * 10 + 5) });
                    strokes.Add(stroke);
                    control.InkSurface.Strokes.Add(stroke);
                }

                var elementsToSelect = new List<UIElement>();
                for (var i = 0; i < elementCount; i++)
                {
                    var container = control.GetContainer(pageVm.Elements[i]);
                    if (container is not null)
                    {
                        elementsToSelect.Add(container);
                    }
                }

                control.InkSurface.Select(strokes, elementsToSelect);

                var selectedStrokes = control.InkSurface.GetSelectedStrokes();
                var selectedElements = control.InkSurface.GetSelectedElements();

                Assert.NotNull(selectedStrokes);
                Assert.NotNull(selectedElements);

                switch (filterMode)
                {
                    case LassoFilterMode.StrokesOnly:
                        Assert.Empty(selectedElements);
                        Assert.Equal(strokeCount, selectedStrokes.Count);
                        break;

                    case LassoFilterMode.ElementsOnly:
                        Assert.Empty(selectedStrokes);
                        Assert.Equal(elementsToSelect.Count, selectedElements.Count);
                        break;

                    case LassoFilterMode.AllObjects:
                        Assert.Equal(strokeCount, selectedStrokes.Count);
                        Assert.Equal(elementsToSelect.Count, selectedElements.Count);
                        break;
                }
            });
        });
    }
}

