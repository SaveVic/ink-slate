# InkSlate Developer & Contributing Guide

This document outlines the environment setup, architectural guidelines, testing patterns, and release procedures for developers contributing to **InkSlate**.

---

## 1. Prerequisites & Environment Setup

- **Operating System**: Windows 10 (Version 1903+) or Windows 11 (x64 or ARM64)
- **.NET SDK**: [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0-windows` workload)
- **IDE**:
  - Visual Studio 2026 (or Visual Studio 2022 v17.12+ with .NET 10 preview features)
  - Visual Studio Code with the **C# Dev Kit** extension
  - JetBrains Rider 2024.3+

### Cloning & Building
```powershell
# Clone repository
git clone https://github.com/waffiq/ink-slate.git
cd ink-slate

# Restore NuGet dependencies
dotnet restore

# Build with warnings treated as errors
dotnet build --configuration Release /p:TreatWarningsAsErrors=true

# Launch application
dotnet run --project InkSlate.App/InkSlate.App.csproj --configuration Release
```

---

## 2. Solution Structure

```
InkSlate.sln
├── InkSlate.Core/               # net10.0-windows class library (SkiaSharp)
│   ├── Models/                  # NoteDocument, NotePage, CanvasElement hierarchy
│   ├── Serialization/           # JsonContext, DocumentValidator
│   ├── Services/                # FilePackageService, PdfExportService, CrashRecoveryService
│   └── UndoRedo/                # UndoRedoManager, atomic operations
├── InkSlate.App/                # net10.0-windows WPF application (WPF-UI, MVVM Toolkit)
│   ├── Assets/                  # App icon (inkslate.ico), frozen graphic resources
│   ├── Controls/                # PageCanvasControl, ZoomScrollViewer, ElementContainer
│   ├── Converters/              # Value converters (HexToFrozenBrushConverter)
│   ├── ViewModels/              # MainViewModel, PageViewModel, ToolbarViewModel
│   └── Views/                   # MainWindow.xaml Fluent shell
└── InkSlate.Tests/              # net10.0-windows xUnit & CsCheck test suite
```

---

## 3. Coding Guidelines & Invariants

### 1. Zero Compiler Warnings Policy
All projects enforce `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. All compiler diagnostics and Code Analysis rules (e.g., CA1845, CA2201, CS8600) must be resolved without suppressing warnings globally.

### 2. Nullable Reference Types & Flow Analysis
Nullable annotations are enabled across all projects (`<Nullable>enable</Nullable>`). Avoid null-forgiving operators (`!`) unless operating on validated properties that the compiler's flow analysis cannot infer.

### 3. WPF Resource Freezing
Any `SolidColorBrush`, `Pen`, or `Effect` created dynamically or defined in XAML must be frozen:
- In C#: `brush.Freeze();` or static readonly frozen instances.
- In XAML: `po:Freeze="True"` using `xmlns:po="http://schemas.microsoft.com/winfx/2006/xaml/presentation/options"`.

### 4. Virtualization Lifecycle Invariants
Never attach event handlers in `UserControl` constructors without detaching them in `Unloaded`. Virtualized containers reuse controls; detached visual controls will leak if events remain hooked to view models.

---

## 4. Testing with `xUnit` and `CsCheck`

InkSlate combines standard stateful unit tests with generative property-based testing:

### Running the Test Suite
```powershell
dotnet test --configuration Release /p:TreatWarningsAsErrors=true
```

### Property-Based Testing Guidelines
- Use `CsCheck` generators (`Gen`) to fuzz inputs and explore edge cases:
  ```csharp
  Gen.Int[1, 100].Sample(totalPages =>
  {
      // Verify invariant across 100 random samples
  });
  ```
- Always test pathological boundaries (`double.NaN`, `double.NegativeInfinity`, `-99999.0`, `0.0`, empty strings, malicious path traversal strings).

---

## 5. Release & Publishing Workflows

### Manual Local Publish

#### Framework-Dependent Single-File (~8 MB)
```powershell
dotnet publish InkSlate.App/InkSlate.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  -p:DebugType=embedded `
  -o ./dist/InkSlate-win-x64
```

#### Self-Contained Zero-Dependency (~60 MB)
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

### GitHub Actions Release Pipeline
1. Create and push a version tag:
   ```powershell
   git tag v1.0.0
   git push origin v1.0.0
   ```
2. The `.github/workflows/release.yml` workflow will:
   - Run the full test suite.
   - Publish `win-x64` (framework-dependent and standalone) and `win-arm64` packages.
   - Generate SHA-256 checksums.
   - Create a GitHub Release with attached `.zip` files and release notes.

