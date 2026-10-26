using System.Diagnostics.CodeAnalysis;

namespace InkSlate.Core.Models;

public enum ToolMode
{
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Domain term representing the canvas selection and pan pointer tool.")]
    Pointer = 0,
    Pen = 1,
    Highlighter = 2,
    Eraser = 3,
    Lasso = 4,
    Pan = 5,
    Text = 6,
    Image = 7
}

public enum ToolbarPlacement
{
    TopDocked = 0,
    LeftDocked = 1,
    TopFloating = 2
}

public enum EraserMode
{
    Stroke = 0,
    Point = 1,
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Domain term matching multi-mode inking eraser (Stroke, Point, Object).")]
    Object = 2
}

public enum LassoFilterMode
{
    StrokesOnly = 0,
    ElementsOnly = 1,
    AllObjects = 2
}

public enum ViewMode
{
    Consecutive = 0,
    Separated = 1
}

public enum PdfExportPageMode
{
    AllPages = 0,
    CurrentPage = 1,
    CustomRange = 2
}
