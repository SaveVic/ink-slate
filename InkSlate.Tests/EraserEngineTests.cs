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

public sealed class EraserEngineTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void StrokeEraserMode_SetsEraseByStrokeAndRecordsUndoRedoOnStrokeRemoval()
    {
        RunInSta(() =>
        {
            var undoRedo = new UndoRedoManager();
            var page = new NotePage();
            var pageVm = new PageViewModel(page, undoRedo);
            pageVm.Hydrate();

            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                PageStrokes = pageVm.Strokes,
                UndoRedoManager = undoRedo,
                ActiveToolMode = ToolMode.Eraser,
                ActiveEraserMode = EraserMode.Stroke
            };

            Assert.Equal(InkCanvasEditingMode.EraseByStroke, control.InkSurface.EditingMode);
            Assert.Equal(Visibility.Collapsed, control.EraserCursor.Visibility);

            var stroke = new Stroke(new StylusPointCollection
            {
                new StylusPoint(10, 10),
                new StylusPoint(20, 20)
            });
            control.InkSurface.Strokes.Add(stroke);
            Assert.Contains(stroke, control.InkSurface.Strokes);

            // Simulate stroke eraser removing the stroke
            control.InkSurface.Strokes.Remove(stroke);
            Assert.DoesNotContain(stroke, control.InkSurface.Strokes);
            Assert.True(undoRedo.CanUndo);

            // Undo restores stroke
            undoRedo.Undo();
            Assert.Contains(stroke, control.InkSurface.Strokes);

            // Redo removes stroke
            undoRedo.Redo();
            Assert.DoesNotContain(stroke, control.InkSurface.Strokes);
        });
    }

    [Fact]
    public void PointEraserMode_SetsEraseByPointAndRefreshesEllipseStylusShape()
    {
        RunInSta(() =>
        {
            var control = new PageCanvasControl
            {
                ActiveToolMode = ToolMode.Eraser,
                ActiveEraserMode = EraserMode.Point,
                EraserSize = 32.0
            };

            Assert.Equal(InkCanvasEditingMode.EraseByPoint, control.InkSurface.EditingMode);
            Assert.IsType<EllipseStylusShape>(control.InkSurface.EraserShape);
            Assert.Equal(32.0, control.InkSurface.EraserShape.Width);
            Assert.Equal(32.0, control.InkSurface.EraserShape.Height);
            Assert.Equal(32.0, control.EraserCursor.Width);
            Assert.Equal(32.0, control.EraserCursor.Height);

            // Update cursor position
            control.UpdateEraserCursor(new Point(100.0, 150.0));
            Assert.Equal(Visibility.Visible, control.EraserCursor.Visibility);
            Assert.Equal(100.0 - 16.0, Canvas.GetLeft(control.EraserCursor));
            Assert.Equal(150.0 - 16.0, Canvas.GetTop(control.EraserCursor));

            // Dynamically change size to 8.0 DIPs
            control.EraserSize = 8.0;
            Assert.Equal(8.0, control.InkSurface.EraserShape.Width);
            Assert.Equal(8.0, control.EraserCursor.Width);

            control.UpdateEraserCursor(new Point(50.0, 50.0));
            Assert.Equal(50.0 - 4.0, Canvas.GetLeft(control.EraserCursor));
            Assert.Equal(50.0 - 4.0, Canvas.GetTop(control.EraserCursor));

            // Switch to Pen tool -> hides cursor
            control.ActiveToolMode = ToolMode.Pen;
            Assert.Equal(InkCanvasEditingMode.Ink, control.InkSurface.EditingMode);
            Assert.Equal(Visibility.Collapsed, control.EraserCursor.Visibility);
        });
    }

    [Fact]
    public void ObjectEraserMode_SetsEditingModeNoneAndLeavesStrokesUntouched()
    {
        RunInSta(() =>
        {
            var control = new PageCanvasControl
            {
                ActiveToolMode = ToolMode.Eraser,
                ActiveEraserMode = EraserMode.Object
            };

            Assert.Equal(InkCanvasEditingMode.None, control.InkSurface.EditingMode);
            Assert.Equal(Visibility.Collapsed, control.EraserCursor.Visibility);

            var stroke = new Stroke(new StylusPointCollection
            {
                new StylusPoint(10, 10),
                new StylusPoint(20, 20)
            });
            control.InkSurface.Strokes.Add(stroke);

            // Moving pointer in Object Eraser mode does not erase strokes
            control.EraseElementsAtPoint(new Point(15, 15));
            Assert.Contains(stroke, control.InkSurface.Strokes);
        });
    }

    [Fact]
    public void ObjectEraserMode_HitTestsAndRemovesIntersectingElementsWithUndoRedo()
    {
        RunInSta(() =>
        {
            var undoRedo = new UndoRedoManager();
            var textElement = new TextCanvasElement
            {
                X = 50.0,
                Y = 50.0,
                Width = 100.0,
                Height = 50.0,
                Text = "Teaching note"
            };
            var imageElement = new ImageCanvasElement
            {
                X = 300.0,
                Y = 300.0,
                Width = 200.0,
                Height = 150.0,
                AssetKey = "assets/diagram.png"
            };

            var page = new NotePage();
            page.Elements.Add(textElement);
            page.Elements.Add(imageElement);

            var pageVm = new PageViewModel(page, undoRedo);
            Assert.Equal(2, pageVm.Elements.Count);

            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                UndoRedoManager = undoRedo,
                PageElements = pageVm.Elements,
                RemoveElementCommand = pageVm.RemoveElementCommand,
                ActiveToolMode = ToolMode.Eraser,
                ActiveEraserMode = EraserMode.Object
            };

            // Hit test outside both elements
            var removedMiss = control.EraseElementsAtPoint(new Point(10.0, 10.0));
            Assert.Equal(0, removedMiss);
            Assert.Equal(2, pageVm.Elements.Count);

            // Hit test inside text element (50..150, 50..100)
            var removedHit = control.EraseElementsAtPoint(new Point(80.0, 75.0));
            Assert.Equal(1, removedHit);
            Assert.Single(pageVm.Elements);
            Assert.Equal(imageElement.Id, pageVm.Elements[0].Id);
            Assert.True(pageVm.IsDirty);
            Assert.True(undoRedo.CanUndo);

            // Undo restores text element at original index 0
            undoRedo.Undo();
            Assert.Equal(2, pageVm.Elements.Count);
            Assert.Equal(textElement.Id, pageVm.Elements[0].Id);
            Assert.Equal(imageElement.Id, pageVm.Elements[1].Id);

            // Redo removes text element again
            undoRedo.Redo();
            Assert.Single(pageVm.Elements);
            Assert.Equal(imageElement.Id, pageVm.Elements[0].Id);
        });
    }

    [Fact]
    public void ToolbarViewModel_EraserOptionsPopoverAndCommandsWorkCleanly()
    {
        var toolbar = new ToolbarViewModel();

        // Presets are verified
        Assert.Equal(3, toolbar.EraserSizeOptions.Count);
        Assert.Equal(8.0, toolbar.EraserSizeOptions[0].SizeDip);
        Assert.Equal(16.0, toolbar.EraserSizeOptions[1].SizeDip);
        Assert.Equal(32.0, toolbar.EraserSizeOptions[2].SizeDip);

        // Initial default mode and size
        Assert.Equal(EraserMode.Stroke, toolbar.SelectedEraserMode);
        Assert.True(toolbar.IsStrokeEraserMode);
        Assert.False(toolbar.IsPointEraserMode);
        Assert.False(toolbar.IsObjectEraserMode);
        Assert.Equal(ToolbarViewModel.DefaultEraserSize, toolbar.SelectedEraserSize);
        Assert.False(toolbar.IsEraserOptionsOpen);

        // Selecting Eraser tool first time switches to tool without opening popover
        toolbar.SelectToolCommand.Execute(ToolMode.Eraser);
        Assert.True(toolbar.IsEraserToolActive);
        Assert.False(toolbar.IsEraserOptionsOpen);

        // Clicking Eraser tool again toggles popover open
        toolbar.SelectToolCommand.Execute(ToolMode.Eraser);
        Assert.True(toolbar.IsEraserOptionsOpen);

        // Selecting mode updates mode and auto-closes popover
        toolbar.SelectEraserModeCommand.Execute(EraserMode.Point);
        Assert.Equal(EraserMode.Point, toolbar.SelectedEraserMode);
        Assert.True(toolbar.IsPointEraserMode);
        Assert.False(toolbar.IsEraserOptionsOpen);

        // Selecting size switches to Point mode, updates size, and auto-closes popover
        toolbar.SelectEraserModeCommand.Execute(EraserMode.Object);
        Assert.True(toolbar.IsObjectEraserMode);

        toolbar.SelectEraserSizeCommand.Execute(32.0);
        Assert.Equal(32.0, toolbar.SelectedEraserSize);
        Assert.True(toolbar.IsPointEraserMode);
        Assert.False(toolbar.IsEraserOptionsOpen);

        // Switching to Pen tool closes eraser options
        toolbar.SelectToolCommand.Execute(ToolMode.Pen);
        Assert.True(toolbar.IsPenToolActive);
        Assert.False(toolbar.IsEraserOptionsOpen);
    }

    [Fact]
    public void CsCheckPropertyFuzzing_RandomElementPlacementsAndObjectEraserMaintainExactUndoRedoParity()
    {
        // Generator for bounding box coordinates [0..800]
        var coordGen = Gen.Double[0.0, 800.0];
        var sizeGen = Gen.Double[10.0, 200.0];

        Gen.Select(coordGen, coordGen, sizeGen, sizeGen, coordGen, coordGen)
            .Sample((x, y, w, h, clickX, clickY) =>
            {
                RunInSta(() =>
                {
                    var undoRedo = new UndoRedoManager();
                    var element = new TextCanvasElement
                    {
                        X = x,
                        Y = y,
                        Width = w,
                        Height = h,
                        Text = "Fuzzed element"
                    };

                    var page = new NotePage();
                    page.Elements.Add(element);

                    var pageVm = new PageViewModel(page, undoRedo);
                    var control = new PageCanvasControl
                    {
                        PageId = pageVm.Id,
                        UndoRedoManager = undoRedo,
                        PageElements = pageVm.Elements,
                        RemoveElementCommand = pageVm.RemoveElementCommand,
                        ActiveToolMode = ToolMode.Eraser,
                        ActiveEraserMode = EraserMode.Object
                    };

                    var clickPoint = new Point(clickX, clickY);
                    var expectedHit = clickX >= x && clickX <= x + w && clickY >= y && clickY <= y + h;

                    var removed = control.EraseElementsAtPoint(clickPoint);

                    if (expectedHit)
                    {
                        Assert.Equal(1, removed);
                        Assert.Empty(pageVm.Elements);
                        Assert.True(undoRedo.CanUndo);

                        undoRedo.Undo();
                        Assert.Single(pageVm.Elements);
                        Assert.Equal(element.Id, pageVm.Elements[0].Id);

                        undoRedo.Redo();
                        Assert.Empty(pageVm.Elements);
                    }
                    else
                    {
                        Assert.Equal(0, removed);
                        Assert.Single(pageVm.Elements);
                        Assert.False(undoRedo.CanUndo);
                    }
                });
            });
    }
}

