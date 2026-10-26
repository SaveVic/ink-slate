using System.Diagnostics.CodeAnalysis;

namespace InkSlate.Core.Models;

public enum ToolMode
{
    Pen = 0,
    Highlighter = 1,
    Eraser = 2,
    Lasso = 3,
    Pan = 4,
    Text = 5,
    Image = 6
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
