using System.Windows.Ink;
using System.Windows.Input;
using CsCheck;
using InkSlate.App.Controls;
using InkSlate.Core.UndoRedo;
using Xunit;

namespace InkSlate.Tests;

public sealed class PageCanvasControlTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void StylusLatencyAndClippingConfigurationAreDisabled()
    {
        RunInSta(() =>
        {
            var canvas = new PageCanvasControl();

            Assert.True(canvas.ClipToBounds);
            Assert.False(Stylus.GetIsPressAndHoldEnabled(canvas));
            Assert.False(Stylus.GetIsFlicksEnabled(canvas));
            Assert.False(Stylus.GetIsTapFeedbackEnabled(canvas));
            Assert.False(Stylus.GetIsTouchFeedbackEnabled(canvas));

            Assert.True(canvas.InkSurface.ClipToBounds);
            Assert.False(Stylus.GetIsPressAndHoldEnabled(canvas.InkSurface));
            Assert.False(Stylus.GetIsFlicksEnabled(canvas.InkSurface));
            Assert.False(Stylus.GetIsTapFeedbackEnabled(canvas.InkSurface));
            Assert.False(Stylus.GetIsTouchFeedbackEnabled(canvas.InkSurface));
        });
    }

    [Fact]
    public void DefaultDrawingAttributesAreLowLatencyAndBezierFitted()
    {
        RunInSta(() =>
        {
            var canvas = new PageCanvasControl();
            var attributes = canvas.InkSurface.DefaultDrawingAttributes;

            Assert.True(attributes.FitToCurve);
            Assert.False(attributes.IgnorePressure);
            Assert.Equal(StylusTip.Ellipse, attributes.StylusTip);
            Assert.Equal(2.5, attributes.Width);
            Assert.Equal(2.5, attributes.Height);
        });
    }

    [Fact]
    public void TwoWayStrokeCollectionSynchronizationUpdatesSurfaceWithoutFalseUndo()
    {
        RunInSta(() =>
        {
            var undoRedo = new UndoRedoManager();
            var canvas = new PageCanvasControl
            {
                UndoRedoManager = undoRedo,
                PageId = Guid.NewGuid()
            };

            var initialStroke = new Stroke(new StylusPointCollection
            {
                new StylusPoint(10, 10),
                new StylusPoint(20, 20)
            });
            var strokes = new StrokeCollection { initialStroke };

            canvas.PageStrokes = strokes;

            Assert.Same(strokes, canvas.InkSurface.Strokes);
            Assert.Equal(0, undoRedo.UndoCount);
            Assert.False(canvas.IsPageDirty);

            // Replacing with another collection detaches cleanly without false undo
            var newStrokes = new StrokeCollection();
            canvas.PageStrokes = newStrokes;

            Assert.Same(newStrokes, canvas.InkSurface.Strokes);
            Assert.Equal(0, undoRedo.UndoCount);
        });
    }

    [Fact]
    public void DrawingStrokeRecordsStrokesChangedOperationAndTogglesDirty()
    {
        RunInSta(() =>
        {
            var undoRedo = new UndoRedoManager();
            var pageId = Guid.NewGuid();
            var canvas = new PageCanvasControl
            {
                UndoRedoManager = undoRedo,
                PageId = pageId,
                PageStrokes = []
            };

            Assert.False(canvas.IsPageDirty);
            Assert.False(undoRedo.CanUndo);

            var stroke = new Stroke(new StylusPointCollection
            {
                new StylusPoint(5, 5),
                new StylusPoint(15, 25)
            });

            // Simulate user drawing
            canvas.InkSurface.Strokes.Add(stroke);

            Assert.True(canvas.IsPageDirty);
            Assert.True(undoRedo.CanUndo);
            Assert.Equal(1, undoRedo.UndoCount);

            // Undo removes the stroke and leaves redo ready
            var undone = undoRedo.Undo();
            Assert.True(undone);
            Assert.Empty(canvas.InkSurface.Strokes);
            Assert.True(undoRedo.CanRedo);
            Assert.False(undoRedo.CanUndo);

            // Redo restores the stroke
            var redone = undoRedo.Redo();
            Assert.True(redone);
            Assert.Single(canvas.InkSurface.Strokes);
            Assert.True(undoRedo.CanUndo);
            Assert.False(undoRedo.CanRedo);
        });
    }

    [Fact]
    public void CsCheckPropertyFuzzing_InkingUndoRedoCycleMaintainsConsistency()
    {
        Gen.Int[1, 15].Sample(strokeCount =>
        {
            RunInSta(() =>
            {
                var undoRedo = new UndoRedoManager();
                var canvas = new PageCanvasControl
                {
                    UndoRedoManager = undoRedo,
                    PageId = Guid.NewGuid(),
                    PageStrokes = []
                };

                for (var i = 0; i < strokeCount; i++)
                {
                    var stroke = new Stroke(new StylusPointCollection
                    {
                        new StylusPoint(i * 10, i * 20),
                        new StylusPoint(i * 10 + 5, i * 20 + 5)
                    });
                    canvas.InkSurface.Strokes.Add(stroke);
                }

                Assert.Equal(strokeCount, canvas.InkSurface.Strokes.Count);
                Assert.Equal(strokeCount, undoRedo.UndoCount);

                // Undo all
                for (var i = 0; i < strokeCount; i++)
                {
                    Assert.True(undoRedo.Undo());
                }
                Assert.Empty(canvas.InkSurface.Strokes);

                // Redo all
                for (var i = 0; i < strokeCount; i++)
                {
                    Assert.True(undoRedo.Redo());
                }
                Assert.Equal(strokeCount, canvas.InkSurface.Strokes.Count);
            });
        });
    }
}
