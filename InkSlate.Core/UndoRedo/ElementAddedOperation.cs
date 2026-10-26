using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.Core.UndoRedo;

public sealed class ElementAddedOperation : IUndoableOperation
{
    private readonly IList<CanvasElement> _targetElements;
    private readonly CanvasElement _element;

    public ElementAddedOperation(Guid pageId, IList<CanvasElement> targetElements, CanvasElement element)
    {
        ArgumentNullException.ThrowIfNull(targetElements);
        ArgumentNullException.ThrowIfNull(element);

        PageId = pageId;
        _targetElements = targetElements;
        _element = element;
    }

    public Guid PageId { get; }

    public void Undo() => _targetElements.Remove(_element);

    public void Redo()
    {
        if (!_targetElements.Contains(_element))
        {
            _targetElements.Add(_element);
        }
    }
}

