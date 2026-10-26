using System.IO;
using System.Windows.Ink;
using System.Windows.Input;
using InkSlate.App.Controls;
using InkSlate.App.ViewModels;
using InkSlate.Core.Ink;
using InkSlate.Core.Models;
using InkSlate.Core.UndoRedo;
using Xunit;

namespace InkSlate.Tests;

public sealed class PathologicalStrokeGuardTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void OversizedInkedStrokeIsSplitAtomicallyWithSingleUndoEntry()
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
                ActiveToolMode = ToolMode.Pen
            };

            // Build an oversized stroke exceeding MaxPointsPerStroke (10,000)
            const int totalPoints = 15_000;
            var points = new StylusPointCollection();
            for (var i = 0; i < totalPoints; i++)
            {
                points.Add(new StylusPoint(i * 0.1, i * 0.2));
            }
            var oversizedStroke = new Stroke(points);

            // Simulating WPF InkCanvas workflow:
            // 1. StylusUp commits the stroke to Strokes collection
            control.InkSurface.Strokes.Add(oversizedStroke);

            // Verified deferred: no premature undo operation recorded yet
            Assert.Equal(0, undoRedo.UndoCount);

            // 2. StrokeCollected event fires on InkCanvas
            var chunkCount = control.SplitCollectedStroke(oversizedStroke);

            // Verify chunks were created
            Assert.Equal(2, chunkCount);
            Assert.Equal(2, control.InkSurface.Strokes.Count);
            Assert.DoesNotContain(oversizedStroke, control.InkSurface.Strokes);
            Assert.All(control.InkSurface.Strokes, s => Assert.True(s.StylusPoints.Count <= StrokeSplitter.MaxPointsPerStroke));

            // Verify atomic single undo record
            Assert.Equal(1, undoRedo.UndoCount);
            Assert.True(undoRedo.CanUndo);

            // Undo removes all chunks atomically
            undoRedo.Undo();
            Assert.Empty(control.InkSurface.Strokes);

            // Redo restores all chunks atomically
            undoRedo.Redo();
            Assert.Equal(2, control.InkSurface.Strokes.Count);
        });
    }

    [Fact]
    public void NormalStrokeIsNotDeferredAndRecordsImmediately()
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
                ActiveToolMode = ToolMode.Pen
            };

            var points = new StylusPointCollection();
            for (var i = 0; i < 500; i++)
            {
                points.Add(new StylusPoint(i, i));
            }
            var normalStroke = new Stroke(points);

            // InkCanvas adds normal stroke -> recorded immediately
            control.InkSurface.Strokes.Add(normalStroke);
            Assert.Equal(1, undoRedo.UndoCount);

            // SplitCollectedStroke on normal stroke returns 0 and leaves it untouched
            var chunks = control.SplitCollectedStroke(normalStroke);
            Assert.Equal(0, chunks);
            Assert.Single(control.InkSurface.Strokes);
            Assert.Same(normalStroke, control.InkSurface.Strokes[0]);
            Assert.Equal(1, undoRedo.UndoCount);

            // Undo removes the stroke
            undoRedo.Undo();
            Assert.Empty(control.InkSurface.Strokes);
        });
    }

    [Fact]
    public void PointEraseReplaceIsSingleUndoEntryAndRestoresOriginal()
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
                ActiveEraserMode = EraserMode.Point
            };

            var points = new StylusPointCollection();
            for (var i = 0; i < 100; i++)
            {
                points.Add(new StylusPoint(i, i));
            }
            var originalStroke = new Stroke(points);
            control.InkSurface.Strokes.Add(originalStroke);
            undoRedo.Clear();

            // When EraseByPoint cuts a stroke in two, InkCanvas performs Strokes.Replace(original, [frag1, frag2])
            var frag1 = new Stroke(new StylusPointCollection { new StylusPoint(0, 0), new StylusPoint(30, 30) });
            var frag2 = new Stroke(new StylusPointCollection { new StylusPoint(70, 70), new StylusPoint(99, 99) });
            var fragments = new StrokeCollection { frag1, frag2 };

            control.InkSurface.Strokes.Replace(originalStroke, fragments);

            Assert.Equal(1, undoRedo.UndoCount);
            Assert.Equal(2, control.InkSurface.Strokes.Count);
            Assert.DoesNotContain(originalStroke, control.InkSurface.Strokes);

            // Undo restores original continuous stroke
            undoRedo.Undo();
            Assert.Single(control.InkSurface.Strokes);
            Assert.Same(originalStroke, control.InkSurface.Strokes[0]);

            // Redo restores fragments
            undoRedo.Redo();
            Assert.Equal(2, control.InkSurface.Strokes.Count);
            Assert.Contains(frag1, control.InkSurface.Strokes);
            Assert.Contains(frag2, control.InkSurface.Strokes);
        });
    }

    [Fact]
    public void HydrateSplitsOversizedStrokesFromIsf()
    {
        RunInSta(() =>
        {
            // Build an ISF payload with a 15,000-point oversized stroke
            var points = new StylusPointCollection();
            for (var i = 0; i < 15_000; i++)
            {
                points.Add(new StylusPoint(i * 0.1, i * 0.1));
            }
            var stroke = new Stroke(points);
            var source = new StrokeCollection { stroke };

            byte[] isfBytes;
            using (var ms = new MemoryStream())
            {
                source.Save(ms, compress: true);
                isfBytes = ms.ToArray();
            }

            var page = new NotePage();
            var pageVm = new PageViewModel(page);
            pageVm.LoadIsfBytes(isfBytes);
            pageVm.Hydrate();

            Assert.NotNull(pageVm.Strokes);
            Assert.Equal(2, pageVm.Strokes.Count);
            Assert.All(pageVm.Strokes, s => Assert.True(s.StylusPoints.Count <= StrokeSplitter.MaxPointsPerStroke));
        });
    }
}

