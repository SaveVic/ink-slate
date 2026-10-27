# InkSlate User & Lecturer Guide

A comprehensive guide for teachers, lecturers, presenters, and note-takers using **InkSlate** to derive formulas, explain diagrams, and deliver digital presentations.

---

## 1. Workspace Layout

```
+-----------------------------------------------------------------------------------+
|  [Logo] InkSlate - Calculus Lecture 1 • [Saved Badge]            [-] [square] [X] |  <- TitleBar
+-----------------------------------------------------------------------------------+
|  [Open] [Save] [Export] | [V] [P] [H] [E] [L] [T] [I] | [Undo] [Redo] | [Page Setup]|  <- Toolbar
+-----------------------------------------------------------------------------------+
|                                                                                   |
|                               +-------------------+                               |
|                     Page 1    |                   |  [Delete Page]                |
|                               |  f(x) = x^2 + 1   |                               |
|                               |                   |                               |
|                               +-------------------+                               |
|                                                                                   |
|                               +-------------------+                               |
|                     Page 2    |                   |  [Delete Page]                |
|                               |  [Vector Graphic] |                               |
|                               |                   |                               |
|                               +-------------------+                               |
|                                                                                   |
|  [ < ]  1 / 2  [ > ]  [ 100% ]                                                    |  <- Corner HUD
+-----------------------------------------------------------------------------------+
```

### Key UI Elements
- **TitleBar**:
  - Displays document title centered in the header.
  - Live status indicator: `Saved`, `Unsaved`, or `Not saved`.
  - Click the title or press `F2` to edit the document name inline.
- **Adaptive Command Bar**:
  - Can be docked to the **Top**, placed as a **Left Sidebar**, or float as a **Movable Pill**.
  - Cycle positions with the placement button or shortcut.
- **Corner HUD**:
  - Bottom-left floating glass pill with page navigation (`<`, `>`), page counter (`1 / 5`), and zoom reset button (`100%`).

---

## 2. Inking and Annotation Tools

### 🖊️ Fountain Pen (`P`)
- Smooth, natural handwriting with pressure sensitivity and Bézier curve smoothing.
- Open options to choose pen thickness (`1.5`, `2.5`, `4.0`, `6.5` DIPs) and vibrant dark-mode friendly color swatches.

### 🖍️ Highlighter (`H`)
- Wide, semi-transparent strokes designed to emphasize text and formulas without obscuring underlying handwriting or images.

### 🧹 Smart Eraser (`E`)
Press `E` repeatedly to cycle between three eraser modes:
1. **Stroke Eraser**: Removes entire strokes on contact (ideal for fast corrections).
2. **Point (Segment) Eraser**: Erases exact segments under the stylus tip like a real pencil eraser.
3. **Object Eraser**: Deletes full text cards or image elements on click.

### ⭕ Geometric Lasso (`L`)
Draw a freehand loop around items to select them. Press `L` repeatedly to cycle the filter mode:
1. **All Objects**: Selects both handwriting strokes and movable elements.
2. **Strokes Only**: Selects only ink lines (handy when selecting handwriting written over an image).
3. **Elements Only**: Selects only text and image boxes.

---

## 3. Movable Canvas Elements

### 📝 Text Cards (`T`)
1. Select the Text tool (`T`) and click anywhere on the canvas.
2. An editable card appears with instant focus.
3. Format with the text popover (Font Size, Bold, Italic, Color).
4. Click outside to finish editing; empty cards automatically discard themselves.
5. Move cards by dragging their header bar; resize using the corner handles.

### 🖼️ Images & Diagrams (`I`)
- **Insert File**: Tap `I` to pick an image file (`.png`, `.jpg`, `.jpeg`, `.bmp`).
- **Clipboard Paste**: Press `Ctrl + V` anywhere on the canvas to paste a copied screenshot or graphic.
- **Resize**: Grab any corner handle to resize while maintaining natural aspect ratio.

---

## 4. Lecture & Presentation View Modes

Press `F5` to switch between two distinct lecture modes:

| Mode | Purpose | Ideal For |
| :--- | :--- | :--- |
| **Consecutive Mode** | Continuous vertical paper scroll with natural separation between pages. | Mathematical derivations, engineering proofs, lecture notes. |
| **Separated Mode** | Centers a single slide page on screen, hiding other pages. | Slide presentations, prepared lecture walkthroughs. |

- Press `F11` in either mode to maximize full screen without taskbars or distractions.
- Hold `Space` and drag the mouse/pen to pan the viewport smoothly at any time.

---

## 5. Page Management & Geometry

- **Add Page**: Click the `+` button in the toolbar or press `Ctrl + Enter`.
- **Delete Page**: Click the delete trash button on the top-right of any page header.
  - The single remaining page cannot be deleted.
  - Deleting a page with notes prompts for confirmation to prevent accidental loss.
- **Page Setup**:
  - Open the **Page Setup** panel from the command bar.
  - Switch between presets: **A4**, **US Letter**, **Legal**, and **16:9 Slide**.
  - Enter custom dimensions in either **Millimeters (mm)** or **Pixels (px)**.

---

## 6. Vector PDF Export (`Ctrl + E`)

1. Press `Ctrl + E` to open the PDF export flyout.
2. Choose your page export mode:
   - **All Pages**: Exports the entire notebook into a multi-page PDF.
   - **Current Page**: Exports only the active slide.
   - **Custom Range**: Enter comma-separated ranges such as `1, 3, 5-8`.
3. Choose your target file destination and export.
4. Output is generated as a true vector PDF using SkiaSharp—preserving crisp vector strokes at any zoom level.

---

## 7. Crash Recovery & Auto-Save

InkSlate continuously protects your lecture notes:
- Every 60 seconds of idle time following a modification, a snapshot is saved to `%LOCALAPPDATA%\InkSlate\Recovery\{DocumentId}.notex.recovery`.
- Recovery files are locked with current-user-only Windows ACLs.
- Cleanly saving your file (`Ctrl + S`) or exiting the app automatically removes the recovery snapshot.

