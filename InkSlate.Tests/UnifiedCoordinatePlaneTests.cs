using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using CsCheck;
using InkSlate.App.Controls;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.Serialization;
using InkSlate.Core.UndoRedo;
using Xunit;

namespace InkSlate.Tests;

public sealed class UnifiedCoordinatePlaneTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void InitialElementsAreMaterializedAsInkCanvasChildrenWithExactGeometry()
    {
        RunInSta(() =>
        {
            var text = new TextCanvasElement
            {
                X = 40.0,
                Y = 60.0,
                Width = 250.0,
                Height = 120.0,
                ZIndex = 2,
                Text = "Coordinate plane test"
            };
            var image = new ImageCanvasElement
            {
                X = 200.0,
                Y = 300.0,
                Width = 400.0,
                Height = 300.0,
                ZIndex = 1,
                AssetKey = "assets/image.png"
            };

            var page = new NotePage();
            page.Elements.Add(text);
            page.Elements.Add(image);

            var pageVm = new PageViewModel(page);
            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                PageElements = pageVm.Elements
            };

            Assert.Equal(2, control.ElementContainers.Count);

            var textContainer = control.GetContainer(pageVm.Elements[0]);
            var imageContainer = control.GetContainer(pageVm.Elements[1]);

            Assert.NotNull(textContainer);
            Assert.NotNull(imageContainer);
            Assert.True(control.InkSurface.Children.Contains(textContainer));
            Assert.True(control.InkSurface.Children.Contains(imageContainer));

            Assert.Equal(40.0, InkCanvas.GetLeft(textContainer));
            Assert.Equal(60.0, InkCanvas.GetTop(textContainer));
            Assert.Equal(250.0, textContainer.Width);
            Assert.Equal(120.0, textContainer.Height);
            Assert.Equal(2, Panel.GetZIndex(textContainer));

            Assert.Equal(200.0, InkCanvas.GetLeft(imageContainer));
            Assert.Equal(300.0, InkCanvas.GetTop(imageContainer));
            Assert.Equal(400.0, imageContainer.Width);
            Assert.Equal(300.0, imageContainer.Height);
            Assert.Equal(1, Panel.GetZIndex(imageContainer));
        });
    }

    [Fact]
    public void AddingAndRemovingElementsUpdatesChildrenImmediately()
    {
        RunInSta(() =>
        {
            var page = new NotePage();
            var pageVm = new PageViewModel(page);
            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                PageElements = pageVm.Elements
            };

            Assert.Empty(control.ElementContainers);
            Assert.Empty(control.InkSurface.Children);

            var text = new TextCanvasElement { X = 10, Y = 20, Width = 100, Height = 50 };
            pageVm.AddElement(text);

            Assert.Single(control.ElementContainers);
            Assert.Single(control.InkSurface.Children);

            var vm = pageVm.Elements[0];
            var container = control.GetContainer(vm);
            Assert.NotNull(container);
            Assert.True(control.InkSurface.Children.Contains(container));

            pageVm.RemoveElement(vm);
            Assert.Empty(control.ElementContainers);
            Assert.Empty(control.InkSurface.Children);
        });
    }

    [Fact]
    public void UndoRedoOfObjectEraserRestoresAndRemovesContainer()
    {
        RunInSta(() =>
        {
            var undoRedo = new UndoRedoManager();
            var page = new NotePage();
            var text = new TextCanvasElement { X = 50, Y = 50, Width = 100, Height = 50, Text = "Erase me" };
            page.Elements.Add(text);

            var pageVm = new PageViewModel(page, undoRedo);
            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                UndoRedoManager = undoRedo,
                PageElements = pageVm.Elements,
                RemoveElementCommand = pageVm.RemoveElementCommand,
                ActiveToolMode = ToolMode.Eraser,
                ActiveEraserMode = EraserMode.Object
            };

            Assert.Single(control.ElementContainers);
            var vm = pageVm.Elements[0];

            // Erase element via hit testing
            var removed = control.EraseElementsAtPoint(new System.Windows.Point(75, 75));
            Assert.Equal(1, removed);
            Assert.Empty(control.ElementContainers);
            Assert.Empty(control.InkSurface.Children);

            // Undo restores container
            undoRedo.Undo();
            Assert.Single(control.ElementContainers);
            Assert.Single(control.InkSurface.Children);
            var restoredContainer = control.GetContainer(pageVm.Elements[0]);
            Assert.NotNull(restoredContainer);
            Assert.Equal(50.0, InkCanvas.GetLeft(restoredContainer));

            // Redo removes container
            undoRedo.Redo();
            Assert.Empty(control.ElementContainers);
            Assert.Empty(control.InkSurface.Children);
        });
    }

    [Fact]
    public void ViewModelGeometryChangesArePushedToContainer()
    {
        RunInSta(() =>
        {
            var text = new TextCanvasElement { X = 10, Y = 20, Width = 100, Height = 50, ZIndex = 0 };
            var page = new NotePage();
            page.Elements.Add(text);

            var pageVm = new PageViewModel(page);
            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                PageElements = pageVm.Elements
            };

            var vm = pageVm.Elements[0];
            var container = control.GetContainer(vm);
            Assert.NotNull(container);

            vm.X = 150.0;
            vm.Y = 220.0;
            vm.Width = 320.0;
            vm.Height = 180.0;
            vm.ZIndex = 8;

            Assert.Equal(150.0, InkCanvas.GetLeft(container));
            Assert.Equal(220.0, InkCanvas.GetTop(container));
            Assert.Equal(320.0, container.Width);
            Assert.Equal(180.0, container.Height);
            Assert.Equal(8, Panel.GetZIndex(container));
        });
    }

    [Fact]
    public void NonFiniteAndNegativeGeometryIsClampedSafely()
    {
        RunInSta(() =>
        {
            var text = new TextCanvasElement { X = 0, Y = 0, Width = 100, Height = 50 };
            var page = new NotePage();
            page.Elements.Add(text);

            var pageVm = new PageViewModel(page);
            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                PageElements = pageVm.Elements
            };

            var vm = pageVm.Elements[0];
            var container = control.GetContainer(vm);
            Assert.NotNull(container);

            // Set pathological values
            vm.Width = double.NaN;
            vm.Height = -100.0;
            vm.X = double.PositiveInfinity;
            vm.Y = -999999.0;

            // Clamped without crashing WPF layout
            Assert.Equal(200.0, container.Width); // Fallback
            Assert.Equal(DocumentValidator.MinElementDimensionDip, container.Height);
            Assert.Equal(0.0, InkCanvas.GetLeft(container)); // Fallback
            Assert.Equal(DocumentValidator.MinElementCoordinateDip, InkCanvas.GetTop(container));
        });
    }

    [Fact]
    public void SwappingPageElementsDetachesOldCollectionWithoutLeaks()
    {
        RunInSta(() =>
        {
            var page1 = new NotePage();
            page1.Elements.Add(new TextCanvasElement { Text = "Page 1" });
            var pageVm1 = new PageViewModel(page1);

            var page2 = new NotePage();
            page2.Elements.Add(new ImageCanvasElement { AssetKey = "assets/p2.png" });
            var pageVm2 = new PageViewModel(page2);

            var control = new PageCanvasControl
            {
                PageId = pageVm1.Id,
                PageElements = pageVm1.Elements
            };

            Assert.Single(control.ElementContainers);
            Assert.Equal(pageVm1.Elements[0].Id, control.ElementContainers.First().Element.Id);

            // Swap to page 2 elements
            control.PageElements = pageVm2.Elements;
            Assert.Single(control.ElementContainers);
            Assert.Equal(pageVm2.Elements[0].Id, control.ElementContainers.First().Element.Id);

            // Modifying old collection has zero effect on control
            pageVm1.AddElement(new TextCanvasElement { Text = "Ignored" });
            Assert.Single(control.ElementContainers);
        });
    }

    [Fact]
    public void ResetRebuildsAllContainers()
    {
        RunInSta(() =>
        {
            var elements = new ObservableCollection<CanvasElementViewModel>
            {
                new(new TextCanvasElement { Text = "1" }),
                new(new TextCanvasElement { Text = "2" })
            };

            var control = new PageCanvasControl { PageElements = elements };
            Assert.Equal(2, control.ElementContainers.Count);

            elements.Clear();
            Assert.Empty(control.ElementContainers);
            Assert.Empty(control.InkSurface.Children);
        });
    }

    [Fact]
    public void StrokesAndElementsCoexistOnSameCanvas()
    {
        RunInSta(() =>
        {
            var page = new NotePage();
            page.Elements.Add(new TextCanvasElement());
            var pageVm = new PageViewModel(page);
            pageVm.Hydrate();

            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                PageStrokes = pageVm.Strokes,
                PageElements = pageVm.Elements
            };

            Assert.Single(control.ElementContainers);
            Assert.Single(control.InkSurface.Children);

            // Adding stroke does not affect Children
            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(0, 0), new StylusPoint(10, 10) });
            control.InkSurface.Strokes.Add(stroke);
            Assert.Single(control.InkSurface.Children);
            Assert.Single(control.InkSurface.Strokes);

            // Adding element does not affect Strokes
            pageVm.AddElement(new ImageCanvasElement { AssetKey = "assets/diagram.png" });
            Assert.Equal(2, control.InkSurface.Children.Count);
            Assert.Single(control.InkSurface.Strokes);
        });
    }

    [Fact]
    public void ElementsAreHitTestInvisibleInInkModesAndInteractiveInTextImageModes()
    {
        RunInSta(() =>
        {
            var page = new NotePage();
            page.Elements.Add(new TextCanvasElement());
            var pageVm = new PageViewModel(page);

            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                PageElements = pageVm.Elements,
                ActiveToolMode = ToolMode.Pen
            };

            var container = control.ElementContainers.First();
            Assert.False(container.IsHitTestVisible);

            control.ActiveToolMode = ToolMode.Highlighter;
            Assert.False(container.IsHitTestVisible);

            control.ActiveToolMode = ToolMode.Eraser;
            Assert.False(container.IsHitTestVisible);

            control.ActiveToolMode = ToolMode.Text;
            Assert.True(container.IsHitTestVisible);

            control.ActiveToolMode = ToolMode.Image;
            Assert.True(container.IsHitTestVisible);

            control.ActiveToolMode = ToolMode.Pen;
            Assert.False(container.IsHitTestVisible);
        });
    }

    [Fact]
    public void ImagesDefaultBelowTextAndZOrderCommandsAreUndoable()
    {
        RunInSta(() =>
        {
            var undoRedo = new UndoRedoManager();
            var page = new NotePage();
            var pageVm = new PageViewModel(page, undoRedo);

            var text = new TextCanvasElement { Text = "Title" };
            pageVm.AddElement(text);
            var textVm = pageVm.Elements[0];

            // Second element is an image -> default stacking puts it under text
            var image = new ImageCanvasElement { AssetKey = "assets/slide.png" };
            pageVm.AddElement(image);
            var imageVm = pageVm.Elements[1];

            Assert.True(imageVm.ZIndex < textVm.ZIndex);

            var control = new PageCanvasControl
            {
                PageId = pageVm.Id,
                UndoRedoManager = undoRedo,
                PageElements = pageVm.Elements
            };

            var textContainer = control.GetContainer(textVm);
            var imageContainer = control.GetContainer(imageVm);
            Assert.NotNull(textContainer);
            Assert.NotNull(imageContainer);

            Assert.True(Panel.GetZIndex(imageContainer) < Panel.GetZIndex(textContainer));

            // Bring image to front
            pageVm.BringElementToFrontCommand.Execute(imageVm);
            Assert.True(imageVm.ZIndex > textVm.ZIndex);
            Assert.True(Panel.GetZIndex(imageContainer) > Panel.GetZIndex(textContainer));
            Assert.True(undoRedo.CanUndo);

            // Undo restores original Z
            undoRedo.Undo();
            Assert.True(imageVm.ZIndex < textVm.ZIndex);
            Assert.True(Panel.GetZIndex(imageContainer) < Panel.GetZIndex(textContainer));

            // Redo brings image back to front
            undoRedo.Redo();
            Assert.True(imageVm.ZIndex > textVm.ZIndex);
            Assert.True(Panel.GetZIndex(imageContainer) > Panel.GetZIndex(textContainer));
        });
    }

    [Fact]
    public void CsCheckPropertyFuzzing_RandomElementOperationsKeepChildrenInOneToOneSync()
    {
        var coordGen = Gen.Double[-100.0, 1000.0];
        var sizeGen = Gen.Double[10.0, 500.0];

        Gen.Select(coordGen, coordGen, sizeGen, sizeGen).Sample((x, y, w, h) =>
        {
            RunInSta(() =>
            {
                var undoRedo = new UndoRedoManager();
                var page = new NotePage();
                var pageVm = new PageViewModel(page, undoRedo);
                var control = new PageCanvasControl
                {
                    PageId = pageVm.Id,
                    UndoRedoManager = undoRedo,
                    PageElements = pageVm.Elements
                };

                var el1 = new TextCanvasElement { X = x, Y = y, Width = w, Height = h };
                pageVm.AddElement(el1);

                Assert.Single(control.ElementContainers);
                Assert.Single(control.InkSurface.Children);

                var vm1 = pageVm.Elements[0];
                var container = control.GetContainer(vm1);
                Assert.NotNull(container);

                var expectedX = Math.Clamp(x, DocumentValidator.MinElementCoordinateDip, DocumentValidator.MaxElementCoordinateDip);
                var expectedY = Math.Clamp(y, DocumentValidator.MinElementCoordinateDip, DocumentValidator.MaxElementCoordinateDip);
                var expectedW = Math.Clamp(w, DocumentValidator.MinElementDimensionDip, DocumentValidator.MaxElementDimensionDip);
                var expectedH = Math.Clamp(h, DocumentValidator.MinElementDimensionDip, DocumentValidator.MaxElementDimensionDip);

                Assert.Equal(expectedX, InkCanvas.GetLeft(container));
                Assert.Equal(expectedY, InkCanvas.GetTop(container));
                Assert.Equal(expectedW, container.Width);
                Assert.Equal(expectedH, container.Height);

                pageVm.RemoveElement(vm1);
                Assert.Empty(control.ElementContainers);
                Assert.Empty(control.InkSurface.Children);

                undoRedo.Undo();
                Assert.Single(control.ElementContainers);
                Assert.Single(control.InkSurface.Children);

                undoRedo.Redo();
                Assert.Empty(control.ElementContainers);
                Assert.Empty(control.InkSurface.Children);
            });
        });
    }
}

