---
name: inkslate-canvas
description: Rules for Windows WPF InkCanvas, StrokeCollection, Lasso filtering, and SkiaSharp vector PDF export. Use whenever modifying canvas controls, erasers, or export services.
---

# InkSlate Canvas & Vector Rules

## 1. InkCanvas & MVVM Integration
- `InkCanvas.Strokes` is not a standard DependencyProperty that binds cleanly out of the box in all scenarios; ensure two-way synchronization inside `PageCanvasControl.xaml.cs` or via a custom attached property.
- Never place `InkCanvas` inside a control that intercepts stylus events without setting `Stylus.IsPressAndHoldEnabled="False"` and handling `PreviewMouseWheel` for zoom when `Ctrl` is pressed.

## 2. Eraser & Lasso Modes
- For Stroke Eraser: Use `InkCanvasEditingMode.EraseByStroke`.
- For Point Eraser: Use `InkCanvasEditingMode.EraseByPoint` and sync `EraserShape = new EllipseStylusShape(width, height)`.
- For Selective Lasso: Always hook `InkCanvas.SelectionChanging`. Use `e.GetSelectedStrokes()` and `e.GetSelectedElements()` and filter them via `e.SetSelectedStrokes()` / `e.SetSelectedElements()` based on the active `LassoFilterMode`.

## 3. Coordinate Systems & PDF Export (SkiaSharp)
- WPF `InkCanvas` uses 96 DPI device-independent pixels (DIPs). Standard PDF points are 72 DPI.
- When rendering `StrokeCollection` to `SKDocument.CreatePdf(stream, 72f)`, either scale the `SKCanvas` by `72f / 96f` (0.75) or set the target PDF DPI parameter to `96f` so page boundaries, images, and stroke thickness match 1:1.
- Extract Bézier control points using `stroke.GetBezierStylusPoints()` for smooth vector paths in `SKPath` rather than raw polyline points.