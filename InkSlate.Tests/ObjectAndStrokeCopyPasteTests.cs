using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using InkSlate.App.Controls;
using InkSlate.App.ViewModels;
using InkSlate.App.Views;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using Xunit;

namespace InkSlate.Tests;

public sealed class ObjectAndStrokeCopyPasteTests
{
    [Fact]
    public void CopyAndPasteTextElement_ClonesWithOffsetAndCascades()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var activePage = vm.ActivePage!;
            activePage.Hydrate();

            var textModel = new TextCanvasElement
            {
                X = 100,
                Y = 100,
                Width = 200,
                Height = 80,
                Text = "Original Note Text",
                FontSize = 24.0,
                ForegroundHex = "#FF336699",
                IsBold = true,
                IsItalic = false
            };
            activePage.AddElement(textModel);

            var originalVm = activePage.Elements[0];
            originalVm.IsSelected = true;

            // Copy
            vm.CopySelectedCommand.Execute(null);

            // 1st Paste (+20 DIP)
            vm.PasteClipboardCommand.Execute(null);

            Assert.Equal(2, activePage.Elements.Count);
            var pasted1 = activePage.Elements[1];
            Assert.NotEqual(originalVm.Model.Id, pasted1.Model.Id);
            Assert.Equal(120.0, pasted1.X, tolerance: 0.001);
            Assert.Equal(120.0, pasted1.Y, tolerance: 0.001);
            Assert.Equal("Original Note Text", ((TextCanvasElement)pasted1.Model).Text);
            Assert.Equal(24.0, ((TextCanvasElement)pasted1.Model).FontSize);
            Assert.Equal("#FF336699", ((TextCanvasElement)pasted1.Model).ForegroundHex);
            Assert.True(((TextCanvasElement)pasted1.Model).IsBold);
            Assert.True(pasted1.IsSelected);
            Assert.False(originalVm.IsSelected);

            // 2nd Paste (+40 DIP)
            vm.PasteClipboardCommand.Execute(null);

            Assert.Equal(3, activePage.Elements.Count);
            var pasted2 = activePage.Elements[2];
            Assert.NotEqual(originalVm.Model.Id, pasted2.Model.Id);
            Assert.NotEqual(pasted1.Model.Id, pasted2.Model.Id);
            Assert.Equal(140.0, pasted2.X, tolerance: 0.001);
            Assert.Equal(140.0, pasted2.Y, tolerance: 0.001);
            Assert.True(pasted2.IsSelected);
            Assert.False(pasted1.IsSelected);
        });
    }

    [Fact]
    public void CopyAndPasteImageElement_PreservesAssetKeyAndAssetData()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var activePage = vm.ActivePage!;
            activePage.Hydrate();

            var assetKey = "assets/image-123.png";
            var fakeBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            vm.Document.Assets[assetKey] = fakeBytes;

            var imageModel = new ImageCanvasElement
            {
                AssetKey = assetKey,
                X = 50,
                Y = 80,
                Width = 300,
                Height = 200,
                CropX = 10,
                CropY = 10,
                CropWidth = 100,
                CropHeight = 100
            };
            activePage.AddElement(imageModel);

            var originalImgVm = activePage.Elements[0];
            originalImgVm.IsSelected = true;

            vm.CopySelectedCommand.Execute(null);
            vm.PasteClipboardCommand.Execute(null);

            Assert.Equal(2, activePage.Elements.Count);
            var pastedImg = (ImageCanvasElement)activePage.Elements[1].Model;
            Assert.Equal(70.0, pastedImg.X, tolerance: 0.001);
            Assert.Equal(100.0, pastedImg.Y, tolerance: 0.001);
            Assert.Equal(assetKey, pastedImg.AssetKey);
            Assert.Equal(10.0, pastedImg.CropX, tolerance: 0.001);
            Assert.True(vm.Document.Assets.ContainsKey(assetKey));
        });
    }

    [Fact]
    public void CopyAndPasteStrokes_ClonesStylusPointsWithOffset()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var activePage = vm.ActivePage!;
            activePage.Hydrate();

            var originalPoints = new StylusPointCollection
            {
                new StylusPoint(50, 50),
                new StylusPoint(75, 75),
                new StylusPoint(100, 100)
            };
            var stroke = new Stroke(originalPoints);
            activePage.Strokes!.Add(stroke);
            activePage.SelectedStrokes = new StrokeCollection([stroke]);

            vm.CopySelectedCommand.Execute(null);
            vm.PasteClipboardCommand.Execute(null);

            Assert.Equal(2, activePage.Strokes.Count);
            var pastedStroke = activePage.Strokes[1];
            Assert.NotSame(stroke, pastedStroke);
            Assert.Equal(originalPoints.Count, pastedStroke.StylusPoints.Count);
            Assert.Equal(70.0, pastedStroke.StylusPoints[0].X, tolerance: 0.05);
            Assert.Equal(70.0, pastedStroke.StylusPoints[0].Y, tolerance: 0.05);
            Assert.Equal(120.0, pastedStroke.StylusPoints[2].X, tolerance: 0.05);
            Assert.Equal(120.0, pastedStroke.StylusPoints[2].Y, tolerance: 0.05);

            Assert.NotNull(activePage.SelectedStrokes);
            Assert.Contains(pastedStroke, activePage.SelectedStrokes);
        });
    }

    [Fact]
    public void UndoRedo_AtomicPasteRollbackForElementsAndStrokes()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var activePage = vm.ActivePage!;
            activePage.Hydrate();

            // Add element
            var textModel = new TextCanvasElement { X = 40, Y = 40, Text = "Dual copy item" };
            activePage.AddElement(textModel);
            var textVm = activePage.Elements[0];
            textVm.IsSelected = true;

            // Add stroke
            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(20, 20), new StylusPoint(30, 30) });
            activePage.Strokes!.Add(stroke);
            activePage.SelectedStrokes = new StrokeCollection([stroke]);

            Assert.Single(activePage.Elements);
            Assert.Single(activePage.Strokes);

            // Copy and Paste both simultaneously
            vm.CopySelectedCommand.Execute(null);
            vm.PasteClipboardCommand.Execute(null);

            Assert.Equal(2, activePage.Elements.Count);
            Assert.Equal(2, activePage.Strokes.Count);

            // Undo once - should roll back both element and stroke together!
            vm.UndoCommand.Execute(null);

            Assert.Single(activePage.Elements);
            Assert.Single(activePage.Strokes);

            // Redo once - should restore both element and stroke together!
            vm.RedoCommand.Execute(null);

            Assert.Equal(2, activePage.Elements.Count);
            Assert.Equal(2, activePage.Strokes.Count);
        });
    }

    [Fact]
    public void PasteAcrossPages_PastesOntoActivePage()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var page1 = vm.ActivePage!;
            page1.Hydrate();

            var textModel = new TextCanvasElement { X = 80, Y = 80, Text = "Page 1 Content" };
            page1.AddElement(textModel);
            page1.Elements[0].IsSelected = true;

            vm.CopySelectedCommand.Execute(null);

            // Add and switch to Page 2
            vm.AddPageCommand.Execute(null);
            var page2 = vm.ActivePage!;
            Assert.NotSame(page1, page2);
            page2.Hydrate();
            Assert.Empty(page2.Elements);

            // Paste on Page 2
            vm.PasteClipboardCommand.Execute(null);

            Assert.Single(page2.Elements);
            Assert.Equal("Page 1 Content", ((TextCanvasElement)page2.Elements[0].Model).Text);
            Assert.Equal(100.0, page2.Elements[0].X, tolerance: 0.001);
            Assert.Equal(100.0, page2.Elements[0].Y, tolerance: 0.001);

            // Page 1 should still have only 1 element
            Assert.Single(page1.Elements);
        });
    }

    [Fact]
    public void NavigationKeys_CtrlC_And_CtrlV_AreHandled()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var window = new MainWindow(vm);

            var handledCtrlC = window.HandleNavigationKey(Key.C, ModifierKeys.Control);
            var handledCtrlV = window.HandleNavigationKey(Key.V, ModifierKeys.Control);

            Assert.True(handledCtrlC);
            Assert.True(handledCtrlV);
        });
    }

    [Fact]
    public void ClearSelection_DeselectsElementsAndStrokes()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var page = vm.ActivePage!;
            page.Hydrate();

            var text = new TextCanvasElement { X = 20, Y = 20, Text = "Test" };
            page.AddElement(text);
            page.Elements[0].IsSelected = true;
            page.SelectedStrokes = [new Stroke(new StylusPointCollection { new StylusPoint(10, 10), new StylusPoint(20, 20) })];

            Assert.True(page.Elements[0].IsSelected);
            Assert.NotNull(page.SelectedStrokes);

            page.ClearSelection();

            Assert.False(page.Elements[0].IsSelected);
            Assert.Null(page.SelectedStrokes);
        });
    }

    [Fact]
    public void CopyAndPasteTextElement_AtCursorPosition_PlacesElementAtTargetPosition()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var activePage = vm.ActivePage!;
            activePage.Hydrate();

            var originalModel = new TextCanvasElement
            {
                X = 100.0,
                Y = 100.0,
                Width = 200.0,
                Height = 60.0,
                Text = "Cursor Paste Test"
            };
            activePage.AddElement(originalModel);
            activePage.Elements[0].IsSelected = true;

            vm.CopySelectedCommand.Execute(null);

            // Paste at cursor position (350, 420)
            vm.PasteClipboardCommand.Execute(new Point(350, 420));

            Assert.Equal(2, activePage.Elements.Count);
            var pasted = activePage.Elements[1];
            Assert.NotEqual(originalModel.Id, pasted.Model.Id);
            Assert.Equal(350.0, pasted.X, tolerance: 0.001);
            Assert.Equal(420.0, pasted.Y, tolerance: 0.001);
            Assert.True(pasted.IsSelected);

            // Undo removes pasted element
            vm.UndoCommand.Execute(null);
            Assert.Single(activePage.Elements);

            // Redo restores pasted element at the cursor position
            vm.RedoCommand.Execute(null);
            Assert.Equal(2, activePage.Elements.Count);
            Assert.Equal(350.0, activePage.Elements[1].X, tolerance: 0.001);
            Assert.Equal(420.0, activePage.Elements[1].Y, tolerance: 0.001);
        });
    }

    [Fact]
    public void CopyAndPasteStrokes_AtCursorPosition_PlacesStrokesAtTargetPosition()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var activePage = vm.ActivePage!;
            activePage.Hydrate();

            var originalPoints = new StylusPointCollection
            {
                new StylusPoint(50, 60),
                new StylusPoint(100, 110),
                new StylusPoint(150, 160)
            };
            var stroke = new Stroke(originalPoints);
            activePage.Strokes!.Add(stroke);
            activePage.SelectedStrokes = [stroke];

            vm.CopySelectedCommand.Execute(null);

            // Paste at cursor position (300, 450)
            vm.PasteClipboardCommand.Execute(new Point(300, 450));

            Assert.Equal(2, activePage.Strokes.Count);
            var pastedStroke = activePage.Strokes[1];
            Assert.NotSame(stroke, pastedStroke);

            var bounds = pastedStroke.GetBounds();
            Assert.Equal(300.0, bounds.Left, tolerance: 0.05);
            Assert.Equal(450.0, bounds.Top, tolerance: 0.05);
            Assert.NotNull(activePage.SelectedStrokes);
            Assert.Contains(pastedStroke, activePage.SelectedStrokes);
        });
    }

    [Fact]
    public void CopyAndPasteMultipleObjects_AtCursorPosition_PreservesRelativeOffsetAndPlacesAtTarget()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var activePage = vm.ActivePage!;
            activePage.Hydrate();

            var text = new TextCanvasElement { X = 100, Y = 100, Width = 80, Height = 40, Text = "Multiple" };
            activePage.AddElement(text);
            activePage.Elements[0].IsSelected = true;

            var stroke = new Stroke(new StylusPointCollection
            {
                new StylusPoint(150, 200),
                new StylusPoint(200, 250)
            });
            activePage.Strokes!.Add(stroke);
            activePage.SelectedStrokes = [stroke];

            // Overall bounds top-left is (100, 100)
            vm.CopySelectedCommand.Execute(null);

            // Paste at cursor position (500, 600) -> delta (+400, +500)
            vm.PasteClipboardCommand.Execute(new Point(500, 600));

            Assert.Equal(2, activePage.Elements.Count);
            Assert.Equal(2, activePage.Strokes.Count);

            var pastedText = activePage.Elements[1];
            var pastedStroke = activePage.Strokes[1];

            Assert.Equal(500.0, pastedText.X, tolerance: 0.001);
            Assert.Equal(600.0, pastedText.Y, tolerance: 0.001);

            Assert.Equal(550.0, pastedStroke.StylusPoints[0].X, tolerance: 0.05);
            Assert.Equal(700.0, pastedStroke.StylusPoints[0].Y, tolerance: 0.05);
        });
    }

    [Fact]
    public void CopyAndPaste_CursorPositionProvider_UsedWhenNoParameterPassed()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var activePage = vm.ActivePage!;
            activePage.Hydrate();

            var text = new TextCanvasElement { X = 80, Y = 80, Text = "Provider Test" };
            activePage.AddElement(text);
            activePage.Elements[0].IsSelected = true;

            vm.CopySelectedCommand.Execute(null);

            // Register provider that simulates active cursor position on canvas
            vm.CursorPositionProvider = () => new Point(250, 350);

            vm.PasteClipboardCommand.Execute(null);

            Assert.Equal(2, activePage.Elements.Count);
            var pasted = activePage.Elements[1];
            Assert.Equal(250.0, pasted.X, tolerance: 0.001);
            Assert.Equal(350.0, pasted.Y, tolerance: 0.001);
        });
    }

    [Fact]
    public void Paste_DeselectsPreviousElement_AndSelectsPastedElement()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var activePage = vm.ActivePage!;
            activePage.Hydrate();

            var text = new TextCanvasElement { X = 50, Y = 50, Text = "Element 1" };
            activePage.AddElement(text);
            activePage.Elements[0].IsSelected = true;

            vm.CopySelectedCommand.Execute(null);

            // Prior to paste, element 0 is selected
            Assert.True(activePage.Elements[0].IsSelected);

            vm.PasteClipboardCommand.Execute(new Point(200, 200));

            // After paste: previous element MUST be deselected, pasted element MUST be selected
            Assert.Equal(2, activePage.Elements.Count);
            Assert.False(activePage.Elements[0].IsSelected);
            Assert.True(activePage.Elements[1].IsSelected);
        });
    }

    [Fact]
    public void Paste_DeselectsPreviousStroke_AndSelectsPastedStroke()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var activePage = vm.ActivePage!;
            activePage.Hydrate();

            var stroke1 = new Stroke(new StylusPointCollection
            {
                new StylusPoint(30, 30),
                new StylusPoint(60, 60)
            });
            activePage.Strokes!.Add(stroke1);
            activePage.SelectedStrokes = [stroke1];

            vm.CopySelectedCommand.Execute(null);

            // Prior to paste, stroke1 is selected
            Assert.Contains(stroke1, activePage.SelectedStrokes);

            vm.PasteClipboardCommand.Execute(new Point(300, 300));

            // After paste: stroke1 MUST be deselected, stroke2 MUST be selected
            Assert.Equal(2, activePage.Strokes.Count);
            var stroke2 = activePage.Strokes[1];
            Assert.NotNull(activePage.SelectedStrokes);
            Assert.DoesNotContain(stroke1, activePage.SelectedStrokes);
            Assert.Contains(stroke2, activePage.SelectedStrokes);
        });
    }

    [Fact]
    public void PageCanvasControl_PasteSync_UpdatesControlSelectionAndBorder()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new MainViewModel();
            var activePage = vm.ActivePage!;
            activePage.Hydrate();

            var control = new PageCanvasControl
            {
                DataContext = activePage,
                PageStrokes = activePage.Strokes,
                ActiveToolMode = ToolMode.Pointer
            };

            var stroke1 = new Stroke(new StylusPointCollection
            {
                new StylusPoint(100, 100),
                new StylusPoint(150, 150)
            });
            activePage.Strokes!.Add(stroke1);

            // Select stroke 1
            control.PointerLeftDown(new Point(100, 100));
            control.PointerLeftUp(new Point(100, 100));

            Assert.Single(control.InkSurface.GetSelectedStrokes());
            Assert.Same(stroke1, control.InkSurface.GetSelectedStrokes()[0]);
            Assert.Equal(Visibility.Visible, control.StrokeSelectionBorder.Visibility);

            // Copy stroke 1 and paste at (400, 400)
            vm.CopySelectedCommand.Execute(null);
            vm.PasteClipboardCommand.Execute(new Point(400, 400));
            control.SyncSelectionFromViewModel(activePage.SelectedStrokes);

            // After paste: stroke 1 is deselected, stroke 2 is selected on control
            var selectedStrokes = control.InkSurface.GetSelectedStrokes();
            Assert.Single(selectedStrokes);
            Assert.NotSame(stroke1, selectedStrokes[0]);
            Assert.Equal(Visibility.Visible, control.StrokeSelectionBorder.Visibility);

            var bounds = selectedStrokes[0].GetBounds();
            Assert.Equal(400.0, bounds.Left, tolerance: 0.05);
            Assert.Equal(400.0, bounds.Top, tolerance: 0.05);
        });
    }
}

