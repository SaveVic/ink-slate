namespace InkSlate.Core.UndoRedo;

public interface IUndoableOperation
{
    Guid PageId { get; }
    void Undo();
    void Redo();
}

