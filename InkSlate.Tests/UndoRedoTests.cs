using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using CsCheck;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.UndoRedo;
using Xunit;

namespace InkSlate.Tests;

public sealed class UndoRedoTests
{
    private static Stroke CreateSampleStroke(double x, double y)
    {
        var points = new StylusPointCollection
        {
            new StylusPoint(x, y),
            new StylusPoint(x + 10.0, y + 10.0)
        };
        return new Stroke(points);
    }

    [Fact]
    public void StrokesChangedOperationUndoesAndRedoesCleanly()
    {
        var pageId = Guid.NewGuid();
        var targetStrokes = new StrokeCollection();
        var stroke1 = CreateSampleStroke(0, 0);
        var stroke2 = CreateSampleStroke(50, 50);

        // Initially add stroke1
        targetStrokes.Add(stroke1);

        // Simulate operation: adding stroke2, removing stroke1
        var added = new StrokeCollection { stroke2 };
        var removed = new StrokeCollection { stroke1 };

        // Mutate target to reflect the action
        targetStrokes.Add(stroke2);
        targetStrokes.Remove(stroke1);

        var op = new StrokesChangedOperation(pageId, targetStrokes, added, removed);

        // Undo should restore stroke1 and remove stroke2
        op.Undo();
        Assert.Contains(stroke1, targetStrokes);
        Assert.DoesNotContain(stroke2, targetStrokes);

        // Redo should remove stroke1 and add stroke2 back
        op.Redo();
        Assert.DoesNotContain(stroke1, targetStrokes);
        Assert.Contains(stroke2, targetStrokes);
    }

    [Fact]
    public void ElementAddedOperationUndoesAndRedoesCleanly()
    {
        var pageId = Guid.NewGuid();
        var elements = new List<CanvasElement>();
        var text = new TextCanvasElement { Text = "Hello" };

        var op = new ElementAddedOperation(pageId, elements, text);

        elements.Add(text);
        Assert.Single(elements);

        op.Undo();
        Assert.Empty(elements);

        op.Redo();
        Assert.Single(elements);
        Assert.Same(text, elements[0]);
    }

    [Fact]
    public void ElementRemovedOperationRestoresOriginalIndexOnUndo()
    {
        var pageId = Guid.NewGuid();
        var el0 = new TextCanvasElement { Text = "First" };
        var el1 = new TextCanvasElement { Text = "Middle" };
        var el2 = new TextCanvasElement { Text = "Last" };

        var elements = new List<CanvasElement> { el0, el1, el2 };

        // Removing the middle element
        var op = new ElementRemovedOperation(pageId, elements, el1);
        elements.RemoveAt(1);
        Assert.Equal(2, elements.Count);

        // Undo should restore el1 at index 1
        op.Undo();
        Assert.Equal(3, elements.Count);
        Assert.Same(el1, elements[1]);

        // Redo should remove el1 again
        op.Redo();
        Assert.Equal(2, elements.Count);
        Assert.DoesNotContain(el1, elements);
    }

    [Fact]
    public void ElementTransformedOperationUndoesAndRedoesBounds()
    {
        var pageId = Guid.NewGuid();
        var element = new TextCanvasElement
        {
            X = 10.0,
            Y = 20.0,
            Width = 100.0,
            Height = 50.0
        };

        var oldBounds = new Rect(10.0, 20.0, 100.0, 50.0);
        var newBounds = new Rect(50.0, 80.0, 300.0, 150.0);

        var op = new ElementTransformedOperation(pageId, element, oldBounds, newBounds);

        // Simulate transformation
        element.X = newBounds.X;
        element.Y = newBounds.Y;
        element.Width = newBounds.Width;
        element.Height = newBounds.Height;

        op.Undo();
        Assert.Equal(10.0, element.X);
        Assert.Equal(20.0, element.Y);
        Assert.Equal(100.0, element.Width);
        Assert.Equal(50.0, element.Height);

        op.Redo();
        Assert.Equal(50.0, element.X);
        Assert.Equal(80.0, element.Y);
        Assert.Equal(300.0, element.Width);
        Assert.Equal(150.0, element.Height);
    }

    [Fact]
    public void UndoRedoManagerEvictsOldestWhenMaxDepthExceeded()
    {
        var manager = new UndoRedoManager(maxHistoryDepth: 3);
        var pageId = Guid.NewGuid();
        var list = new List<CanvasElement>();

        var op1 = new ElementAddedOperation(pageId, list, new TextCanvasElement { Text = "1" });
        var op2 = new ElementAddedOperation(pageId, list, new TextCanvasElement { Text = "2" });
        var op3 = new ElementAddedOperation(pageId, list, new TextCanvasElement { Text = "3" });
        var op4 = new ElementAddedOperation(pageId, list, new TextCanvasElement { Text = "4" });

        manager.ExecuteAndRecord(op1);
        manager.ExecuteAndRecord(op2);
        manager.ExecuteAndRecord(op3);
        manager.ExecuteAndRecord(op4);

        // Capacity capped at 3, so op1 was evicted!
        Assert.Equal(3, manager.UndoCount);
        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);

        // Can undo op4, op3, op2
        Assert.True(manager.Undo());
        Assert.True(manager.Undo());
        Assert.True(manager.Undo());

        // op1 is gone, cannot undo further
        Assert.False(manager.Undo());
        Assert.False(manager.CanUndo);
        Assert.Equal(3, manager.RedoCount);
    }

    [Fact]
    public void RecordingNewActionClearsRedoStack()
    {
        var manager = new UndoRedoManager();
        var pageId = Guid.NewGuid();
        var list = new List<CanvasElement>();

        var op1 = new ElementAddedOperation(pageId, list, new TextCanvasElement { Text = "1" });
        var op2 = new ElementAddedOperation(pageId, list, new TextCanvasElement { Text = "2" });

        manager.ExecuteAndRecord(op1);
        manager.Undo();
        Assert.True(manager.CanRedo);

        // New action recorded while redo stack has items
        manager.ExecuteAndRecord(op2);
        Assert.False(manager.CanRedo);
        Assert.Equal(0, manager.RedoCount);
    }

    [Fact]
    public void StateChangedFiresOnRecordUndoRedoClear()
    {
        var manager = new UndoRedoManager();
        var pageId = Guid.NewGuid();
        var list = new List<CanvasElement>();
        var op = new ElementAddedOperation(pageId, list, new TextCanvasElement { Text = "1" });

        var eventCount = 0;
        manager.StateChanged += (_, _) => eventCount++;

        manager.ExecuteAndRecord(op);
        Assert.Equal(1, eventCount);

        manager.Undo();
        Assert.Equal(2, eventCount);

        manager.Redo();
        Assert.Equal(3, eventCount);

        manager.Clear();
        Assert.Equal(4, eventCount);
    }

    [Fact]
    public void PropertyBasedUndoRedoCycleRestoresEmptyState()
    {
        Gen.Int[1, 50].Sample(count =>
        {
            var manager = new UndoRedoManager(maxHistoryDepth: 100);
            var pageId = Guid.NewGuid();
            var list = new List<CanvasElement>();

            for (var i = 0; i < count; i++)
            {
                var el = new TextCanvasElement { Text = $"Item {i}" };
                var op = new ElementAddedOperation(pageId, list, el);
                manager.ExecuteAndRecord(op);
            }

            Assert.Equal(count, list.Count);

            // Undo all operations
            while (manager.CanUndo)
            {
                manager.Undo();
            }

            Assert.Empty(list);

            // Redo all operations
            while (manager.CanRedo)
            {
                manager.Redo();
            }

            Assert.Equal(count, list.Count);
        });
    }
}

