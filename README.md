<div align="center">

# InkSlate

**High-Performance, Keyboard-Driven Digital Ink Slate & Lecture Presentation Studio**

[![Build & Test](https://github.com/SaveVic/ink-slate/actions/workflows/ci.yml/badge.svg)](https://github.com/SaveVic/ink-slate/actions/workflows/ci.yml)
[![Target Framework](https://img.shields.io/badge/.NET-10.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Tests Passing](https://img.shields.io/badge/Tests-271%20Passed-brightgreen)](https://github.com/SaveVic/ink-slate)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11%20(x64%20%7C%20ARM64)-0078D6?logo=windows)](https://github.com/SaveVic/ink-slate)

*Built from the ground up for educators, mathematicians, engineers, and presenters who require real-time stylus responsiveness, fluid keyboard ergonomics, and pristine vector exports.*

</div>

---

## 🌟 Overview

**InkSlate** is a standalone, lightweight digital inking and presentation studio for Windows desktop. Engineered on **.NET 10**, **WPF**, and **SkiaSharp**, it combines the immediate physical feel of a digital chalkboard with the structural power of a paginated notebook.

Designed specifically for interactive teaching and mathematical lectures, InkSlate eliminates UI friction: every tool, mode, eraser variant, and canvas transition is bound to muscle-memory single-key shortcuts so the speaker never has to break eye contact or fumble with floating dialogs.

---

## ✨ Key Features

### ⚡ Ultra-Low Latency Inking Engine
- **Hardware-Accelerated Inking**: Direct WPF `InkCanvas` stylus pipeline with pressure sensitivity and natural cubic Bézier stroke smoothing.
- **Smart Tools**: Fountain pen, highlighter with semi-transparent blending, and an intelligent three-stage eraser:
  $$\text{Stroke Eraser} \longrightarrow \text{Point (Segment) Eraser} \longrightarrow \text{Object Eraser}$$
- **Cycle Quickly**: Tap `E` to cycle eraser behaviors, or `L` to cycle smart lasso filtering without opening option panels.

### 🎯 Keyboard-First Teaching Workflow
- Keep presentations moving at lecture pace with single-key shortcuts:
  - `V` for Pointer & Canvas Pan mode
  - `P` for Pen, `H` for Highlighter, `E` for Eraser cycling, `L` for Lasso cycling
  - `T` for Instant Text placement, `I` for Image insertion
  - Hold `Space` for temporary hand pan across the canvas
  - `F2` to rename the document title inline in the TitleBar
  - `F5` to toggle between continuous vertical scrolling and single-slide presentation mode
  - `F11` to toggle true fullscreen presentation view

### 📄 Dual View Modes & Adaptive Canvas
- **Consecutive Mode**: Virtualized, continuous vertical paper roll for long-form lecture derivation and lecture note taking.
- **Separated Mode**: Distraction-free, single-page presentation mode ideal for slide-based lecturing.
- **Standard Dimensions**: Instant presets for **A4**, **US Letter**, **Legal**, and **16:9 Slide**, plus custom millimeter or pixel dimensioning.

### 🖼️ Rich Movable Canvas Elements
- **Draggable Text Cards**: Click-to-type floating text blocks with inline styling (bold, italic, color swatches, font size).
- **Embedded Visuals**: Paste images directly from the clipboard (`Ctrl+V`) or drag-and-drop image files (`.png`, `.jpg`, `.bmp`).
- **Proportional Resizing**: Interactive corner grips maintain element aspect ratio while keeping selections active.

### 🔍 Geometric Smart Lasso
- Custom polygonal lasso selection tool powered by ray-casting point-in-polygon algorithms.
- Filter selections on the fly: **All Objects**, **Strokes Only**, or **Canvas Elements Only**.

### 📦 Robust `.notex` Document Package
- Standard, open, ZIP-based archive containing:
  - `manifest.json`: Document tree, page geometry, element positions, and styling.
  - `pages/{guid}.isf`: Standard Microsoft Ink Serialized Format (ISF) stroke binary data.
  - `assets/{guid}.*`: Deduplicated, embedded media assets.
- **Security Hardened**: Enforces strict path allowlists (Zip Slip prevention) and entry decompression quotas (Zip Bomb prevention).

### 🖨️ Precision Vector PDF Export
- Exports documents directly to vector PDF using **SkiaSharp**—preserving native vector strokes, crisp typography, and high-fidelity embedded media.
- Export all pages, current page, or flexible custom ranges (`"1, 3, 5-8"`).

### 🛡️ Crash Recovery & Auto-Save Guard
- Background idle timer writes recovery snapshots to `%LOCALAPPDATA%\InkSlate\Recovery` after 60 seconds of idle time following edits.
- Protected with restrictive current-user-only Windows Access Control Lists (ACLs) to prevent unauthorized local access.
- Automatically cleans up recovery artifacts upon clean save, new document, or safe application exit.

---

## ⌨️ Keyboard Shortcuts Reference

| Shortcut | Action | Scope / Context |
| :--- | :--- | :--- |
| `V` | **Pointer Tool** (Select, Move, Pan) | Global canvas |
| `P` | **Pen Tool** | Global canvas |
| `H` | **Highlighter Tool** | Global canvas |
| `E` | **Eraser Tool** (Press repeatedly to cycle Stroke → Point → Object) | Global canvas |
| `L` | **Lasso Tool** (Press repeatedly to cycle All → Strokes → Elements) | Global canvas |
| `T` | **Text Tool** (Click canvas to create text card) | Global canvas |
| `I` | **Image Tool** (Open file picker to insert image) | Global canvas |
| `Space` *(Hold)* | **Temporary Pan Mode** | Viewport navigation |
| `Delete` / `Backspace` | Delete selected elements or lasso-selected strokes | Not editing text |
| `F2` | Rename Document Title in TitleBar | TitleBar |
| `F5` | Toggle View Mode (**Consecutive** / **Separated**) | Viewport |
| `F11` | Toggle **Fullscreen / Normal** window state | Window |
| `PageUp` / `Left` | Navigate to **Previous Page** | Viewport |
| `PageDown` / `Right` | Navigate to **Next Page** | Viewport |
| `Ctrl + N` | **New Document** (Prompts if unsaved) | Global |
| `Ctrl + O` | **Open** `.notex` document | Global |
| `Ctrl + S` | **Save** `.notex` document | Global |
| `Ctrl + E` | **Export PDF** dialog | Global |
| `Ctrl + Z` | **Undo** last action | History |
| `Ctrl + Y` | **Redo** last action | History |
| `Ctrl + Enter` | **Add New Page** after active page | Document |
| `Ctrl + 0` | **Reset Zoom** to 100% | Viewport |

---

## 🏗️ Architecture & Solution Layout

```
ink-slate/
├── InkSlate.Core/               # Headless domain models, services, & serialization
│   ├── Models/                  # NoteDocument, NotePage, CanvasElement, Text, Image
│   ├── Serialization/           # System.Text.Json context, DocumentValidator
│   ├── Services/                # FilePackageService, PdfExportService, CrashRecoveryService
│   └── UndoRedo/                # UndoRedoManager, atomic reversible operations
├── InkSlate.App/                # Modern WPF presentation layer (.NET 10 + WPF-UI)
│   ├── Assets/                  # Application icons and frozen resources
│   ├── Controls/                # PageCanvasControl, ZoomScrollViewer, ElementContainer
│   ├── Converters/              # HexToFrozenBrushConverter, XAML helpers
│   ├── ViewModels/              # MainViewModel, PageViewModel, ToolbarViewModel
│   └── Views/                   # MainWindow.xaml (Mica Fluent shell)
├── InkSlate.Tests/              # Automated testing suite (xUnit + CsCheck)
│   ├── Property-based tests     # PageRangeParser, DocumentValidator fuzzing
│   ├── Security test suites     # Zip Slip, Zip Bomb, and corrupted ISF recovery
│   └── Lifecycle tests          # Undo/redo, viewports, shortcuts, rendering audit
└── docs/                        # In-depth architectural and developer documentation
    ├── architecture.md          # Data structures, serialization format, rendering pipeline
    ├── user-guide.md            # Lecturer workflows, shortcuts, and canvas features
    └── development.md           # Build environment, test runners, and publishing
```

---

## 🚀 Getting Started

### Prerequisites
- **Operating System**: Windows 10 (version 1903+) or Windows 11 (x64 or ARM64)
- **SDK**: [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.100 or newer)

### Building and Running

```powershell
# 1. Clone the repository
git clone https://github.com/SaveVic/ink-slate.git
cd ink-slate

# 2. Restore NuGet dependencies
dotnet restore

# 3. Build the solution (warnings treated as errors)
dotnet build --configuration Release /p:TreatWarningsAsErrors=true

# 4. Run InkSlate
dotnet run --project InkSlate.App/InkSlate.App.csproj --configuration Release
```

### Running Automated Tests

InkSlate includes a test suite combining **xUnit** unit tests and **CsCheck** property-based fuzzing:

```powershell
dotnet test --configuration Release /p:TreatWarningsAsErrors=true
```

---

## 📦 Building Single-File Release Executables

InkSlate can be published as a high-speed, compact single-file executable targeting Windows x64 or ARM64:

### Framework-Dependent (~8 MB)
*Requires the machine to have the .NET 10 Desktop Runtime installed:*

```powershell
dotnet publish InkSlate.App/InkSlate.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  -p:DebugType=embedded `
  -o ./dist/InkSlate-win-x64
```

### Self-Contained Zero-Prerequisite Portable Binary
*Runs out-of-the-box on any Windows 10/11 x64 machine without installing any runtime:*

```powershell
dotnet publish InkSlate.App/InkSlate.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=embedded `
  -o ./dist/InkSlate-win-x64-standalone
```

---

## 📚 Documentation

For comprehensive guides and technical deep-dives:
- 🏛️ [**Architecture & Internals Guide**](docs/architecture.md): Data model, package specification, undo/redo mechanics, and SkiaSharp PDF pipeline.
- 🎓 [**User & Lecturer Guide**](docs/user-guide.md): Presentation ergonomics, layout tools, shortcuts, and PDF export workflows.
- 🛠️ [**Developer & Contributing Guide**](docs/development.md): Environment setup, conventions, property testing patterns, and release procedures.

---

## 📄 License

InkSlate is licensed under the [MIT License](LICENSE).
Copyright © 2026 InkSlate Contributors.

