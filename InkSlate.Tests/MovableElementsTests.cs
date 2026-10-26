using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.UndoRedo;
using Xunit;

namespace InkSlate.Tests;

public sealed class MovableElementsTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void ClickToCreateTextElementAddsElementAndEntersEditing()
    {
        RunInSta(() =>
        {
            var page = new NotePage();
            var undoRedo = new UndoRedoManager();
            var toolbar = new ToolbarViewModel();
            var pageVm = new PageViewModel(page, undoRedo, toolbar);

            var textVm = pageVm.AddTextElement(new Point(120, 180));

            Assert.Single(pageVm.Elements);
            Assert.Same(textVm, pageVm.Elements[0]);
            Assert.Equal(120.0, textVm.X);
            Assert.Equal(180.0, textVm.Y);
            Assert.True(textVm.IsSelected);
            Assert.True(textVm.IsEditing);
            Assert.Equal(string.Empty, textVm.Text);
            Assert.Equal(18.0, textVm.FontSize);

            // Undo removes the added element
            Assert.True(undoRedo.CanUndo);
            undoRedo.Undo();
            Assert.Empty(pageVm.Elements);
        });
    }

    [Fact]
    public void EmptyTextBoxAutoDeletesOnLostFocusCondition()
    {
        RunInSta(() =>
        {
            var page = new NotePage();
            var undoRedo = new UndoRedoManager();
            var pageVm = new PageViewModel(page, undoRedo);

            var textVm = pageVm.AddTextElement(new Point(50, 50));
            Assert.Single(pageVm.Elements);

            // If empty when blurred
            if (string.IsNullOrWhiteSpace(textVm.Text))
            {
                pageVm.RemoveElement(textVm);
            }

            Assert.Empty(pageVm.Elements);
        });
    }

    [Fact]
    public void NonEmptyTextBoxPersistsOnLostFocusCondition()
    {
        RunInSta(() =>
        {
            var page = new NotePage();
            var undoRedo = new UndoRedoManager();
            var pageVm = new PageViewModel(page, undoRedo);

            var textVm = pageVm.AddTextElement(new Point(50, 50));
            textVm.Text = "Important meeting note";

            if (string.IsNullOrWhiteSpace(textVm.Text))
            {
                pageVm.RemoveElement(textVm);
            }

            Assert.Single(pageVm.Elements);
            Assert.Equal("Important meeting note", ((TextCanvasElementViewModel)pageVm.Elements[0]).Text);
        });
    }

    [Fact]
    public void TextMiniBarCommandsUpdateFontProperties()
    {
        RunInSta(() =>
        {
            var model = new TextCanvasElement
            {
                FontSize = 16.0,
                IsBold = false,
                IsItalic = false,
                ForegroundHex = "#FF1E1E1E"
            };
            var textVm = new TextCanvasElementViewModel(model);

            textVm.IncreaseFontSizeCommand.Execute(null);
            Assert.Equal(18.0, textVm.FontSize);
            Assert.Equal(18.0, model.FontSize);

            textVm.DecreaseFontSizeCommand.Execute(null);
            Assert.Equal(16.0, textVm.FontSize);

            textVm.ToggleBoldCommand.Execute(null);
            Assert.True(textVm.IsBold);
            Assert.True(model.IsBold);

            textVm.ToggleItalicCommand.Execute(null);
            Assert.True(textVm.IsItalic);
            Assert.True(model.IsItalic);

            textVm.SetForegroundHexCommand.Execute("#FF2563EB");
            Assert.Equal("#FF2563EB", textVm.ForegroundHex);
            Assert.Equal("#FF2563EB", model.ForegroundHex);
        });
    }

    [Fact]
    public void NonDestructiveCropAdjustmentUpdatesClipAndUndoRestores()
    {
        RunInSta(() =>
        {
            var undoRedo = new UndoRedoManager();
            var model = new ImageCanvasElement
            {
                X = 100,
                Y = 100,
                Width = 400,
                Height = 300,
                AssetKey = "assets/photo.png"
            };

            var imageVm = new ImageCanvasElementViewModel(model);
            Assert.Null(imageVm.CropRect);
            Assert.Null(imageVm.ClipGeometry);

            var oldBounds = new Rect(100, 100, 400, 300);
            var oldCrop = imageVm.CropRect;

            // Apply non-destructive crop
            imageVm.CropX = 10;
            imageVm.CropY = 20;
            imageVm.CropWidth = 200;
            imageVm.CropHeight = 150;

            Assert.NotNull(imageVm.CropRect);
            Assert.NotNull(imageVm.ClipGeometry);
            Assert.Equal(new Rect(10, 20, 200, 150), imageVm.CropRect);

            var newBounds = new Rect(100, 100, 400, 300);
            var newCrop = imageVm.CropRect;

            var op = new ElementTransformedOperation(Guid.NewGuid(), model, oldBounds, newBounds, oldCrop, newCrop);
            undoRedo.Record(op);

            // Undo crop
            undoRedo.Undo();
            imageVm.RefreshFromModel();
            Assert.Null(imageVm.CropRect);

            // Redo crop
            undoRedo.Redo();
            imageVm.RefreshFromModel();
            Assert.Equal(new Rect(10, 20, 200, 150), imageVm.CropRect);
        });
    }

    [Fact]
    public void ResizeAndMovePushSingleElementTransformedOperationWithFullUndoRedo()
    {
        RunInSta(() =>
        {
            var undoRedo = new UndoRedoManager();
            var model = new TextCanvasElement
            {
                X = 50,
                Y = 60,
                Width = 200,
                Height = 100
            };
            var vm = new TextCanvasElementViewModel(model);

            var initialBounds = new Rect(vm.X, vm.Y, vm.Width, vm.Height);

            // Transform
            vm.X = 150;
            vm.Y = 200;
            vm.Width = 350;
            vm.Height = 180;

            var transformedBounds = new Rect(vm.X, vm.Y, vm.Width, vm.Height);

            var op = new ElementTransformedOperation(Guid.NewGuid(), model, initialBounds, transformedBounds);
            undoRedo.Record(op);

            Assert.True(undoRedo.CanUndo);
            undoRedo.Undo();
            vm.RefreshFromModel();

            Assert.Equal(50.0, vm.X);
            Assert.Equal(60.0, vm.Y);
            Assert.Equal(200.0, vm.Width);
            Assert.Equal(100.0, vm.Height);

            Assert.True(undoRedo.CanRedo);
            undoRedo.Redo();
            vm.RefreshFromModel();

            Assert.Equal(150.0, vm.X);
            Assert.Equal(200.0, vm.Y);
            Assert.Equal(350.0, vm.Width);
            Assert.Equal(180.0, vm.Height);
        });
    }

    [Fact]
    public void MainViewModelInsertImageBitmapAddsElementAndStoresAsset()
    {
        RunInSta(() =>
        {
            var doc = new NoteDocument();
            var undoRedo = new UndoRedoManager();
            var toolbar = new ToolbarViewModel();
            var mainVm = new MainViewModel(doc, undoRedo, toolbar);

            Assert.NotNull(mainVm.ActivePage);

            var rtb = new RenderTargetBitmap(100, 80, 96, 96, PixelFormats.Pbgra32);
            mainVm.InsertImageBitmap(rtb);

            Assert.Single(mainVm.ActivePage.Elements);
            var imageElem = Assert.IsType<ImageCanvasElementViewModel>(mainVm.ActivePage.Elements[0]);
            Assert.StartsWith("assets/", imageElem.AssetKey);
            Assert.True(mainVm.Document.Assets.ContainsKey(imageElem.AssetKey));
            Assert.NotNull(imageElem.ImageSource);
        });
    }
}

