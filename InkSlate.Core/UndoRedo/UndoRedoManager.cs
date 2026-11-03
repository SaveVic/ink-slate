namespace InkSlate.Core.UndoRedo;

public sealed class UndoRedoManager
{
    public const int DefaultMaxHistoryDepth = 100;

    private readonly LinkedList<IUndoableOperation> _undoList = new();
    private readonly Stack<IUndoableOperation> _redoStack = new();

    public UndoRedoManager(int maxHistoryDepth = DefaultMaxHistoryDepth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxHistoryDepth, 0);
        MaxHistoryDepth = maxHistoryDepth;
    }

    public int MaxHistoryDepth { get; }
    public bool IsReplaying { get; private set; }
    public int UndoCount => _undoList.Count;
    public int RedoCount => _redoStack.Count;
    public bool CanUndo => _undoList.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public IUndoableOperation? PeekUndo() => _undoList.Last?.Value;
    public IUndoableOperation? PeekRedo() => _redoStack.Count > 0 ? _redoStack.Peek() : null;
    public bool HasOperationsForPage(Guid pageId) =>
        _undoList.Any(op => op.PageId == pageId) || _redoStack.Any(op => op.PageId == pageId);

    public event EventHandler? StateChanged;

    /// <summary>
    /// Records an operation that has already taken place on the canvas (e.g., InkCanvas.StrokesChanged).
    /// Ignored if <see cref="IsReplaying"/> is true.
    /// </summary>
    public void Record(IUndoableOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (IsReplaying)
        {
            return;
        }

        if (_undoList.Count >= MaxHistoryDepth)
        {
            _undoList.RemoveFirst();
        }

        _undoList.AddLast(operation);
        _redoStack.Clear();
        OnStateChanged();
    }

    /// <summary>
    /// Executes <see cref="IUndoableOperation.Redo"/> and records the operation onto the undo stack.
    /// </summary>
    public void ExecuteAndRecord(IUndoableOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var prevReplaying = IsReplaying;
        IsReplaying = true;
        try
        {
            operation.Redo();
        }
        finally
        {
            IsReplaying = prevReplaying;
        }

        Record(operation);
    }

    public bool Undo()
    {
        if (_undoList.Last is null)
        {
            return false;
        }

        var operation = _undoList.Last.Value;
        _undoList.RemoveLast();

        IsReplaying = true;
        try
        {
            operation.Undo();
        }
        finally
        {
            IsReplaying = false;
        }

        _redoStack.Push(operation);
        OnStateChanged();
        return true;
    }

    public bool Redo()
    {
        if (!_redoStack.TryPop(out var operation))
        {
            return false;
        }

        IsReplaying = true;
        try
        {
            operation.Redo();
        }
        finally
        {
            IsReplaying = false;
        }

        if (_undoList.Count >= MaxHistoryDepth)
        {
            _undoList.RemoveFirst();
        }

        _undoList.AddLast(operation);
        OnStateChanged();
        return true;
    }

    public void Clear()
    {
        if (_undoList.Count == 0 && _redoStack.Count == 0)
        {
            return;
        }

        _undoList.Clear();
        _redoStack.Clear();
        OnStateChanged();
    }

    public bool RemoveLastUndoIf(Func<IUndoableOperation, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (_undoList.Last is { } lastNode && predicate(lastNode.Value))
        {
            _undoList.RemoveLast();
            OnStateChanged();
            return true;
        }

        return false;
    }

    public void RemoveOperationsForPage(Guid pageId)
    {
        var changed = false;

        var node = _undoList.First;
        while (node is not null)
        {
            var next = node.Next;
            if (node.Value.PageId == pageId)
            {
                _undoList.Remove(node);
                changed = true;
            }
            node = next;
        }

        if (_redoStack.Count > 0)
        {
            var remaining = _redoStack.Where(op => op.PageId != pageId).Reverse().ToList();
            if (remaining.Count != _redoStack.Count)
            {
                _redoStack.Clear();
                foreach (var op in remaining)
                {
                    _redoStack.Push(op);
                }
                changed = true;
            }
        }

        if (changed)
        {
            OnStateChanged();
        }
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}

