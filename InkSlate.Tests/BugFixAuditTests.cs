using System.Text.Json;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using InkSlate.App.Controls;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.Serialization;
using InkSlate.Core.UndoRedo;
using Xunit;

namespace InkSlate.Tests;

public sealed class BugFixAuditTests
{
    [Fact]
    public void DocumentValidator_RecoversNullPagesAndElements_AndDeduplicatesPageIds()
    {
        var sharedPageId = Guid.NewGuid();
        var doc = new NoteDocument
        {
            Pages = null!
        };

        DocumentValidator.Sanitize(doc);
        Assert.NotNull(doc.Pages);
        Assert.Empty(doc.Pages);

        var page1 = new NotePage { Id = sharedPageId, Elements = null! };
        var page2 = new NotePage { Id = sharedPageId, Elements = [] };
        doc.Pages = [page1, page2];

        DocumentValidator.Sanitize(doc);
        Assert.NotNull(page1.Elements);
        Assert.NotEqual(page1.Id, page2.Id);
    }

    [Fact]
    public void DocumentValidator_TruncateSafe_DoesNotSplitUtf16SurrogatePair()
    {
        // Place a 2-char UTF-16 surrogate pair ("𝄞" = \uD834\uDD1E) right across the maxLength boundary (index 4..5)
        var input = "ABCD\uD834\uDD1EFG";
        var truncated = DocumentValidator.TruncateSafe(input, 5);

        Assert.Equal(4, truncated.Length);
        Assert.Equal("ABCD", truncated);

        // Must serialize cleanly without EncoderFallbackException
        var json = JsonSerializer.Serialize(truncated);
        Assert.Equal("\"ABCD\"", json);
    }

    [Fact]
    public void PageDimensions_Clamp_InvalidFallback_UsesA4Defaults()
    {
        var invalidFallback = new PageDimensions(double.PositiveInfinity, 50000.0);
        var clamped = PageDimensions.Clamp(double.NaN, double.PositiveInfinity, invalidFallback);

        Assert.Equal(PageDimensions.A4.Width, clamped.Width);
        Assert.Equal(PageDimensions.A4.Height, clamped.Height);
    }

    [Fact]
    public void UndoRedoManager_RemoveOperationsForPage_PurgesOnlyTargetPage()
    {
        var mgr = new UndoRedoManager();
        var page1Id = Guid.NewGuid();
        var page2Id = Guid.NewGuid();
        var list1 = new List<CanvasElement>();
        var list2 = new List<CanvasElement>();

        mgr.ExecuteAndRecord(new ElementAddedOperation(page1Id, list1, new TextCanvasElement()));
        mgr.ExecuteAndRecord(new ElementAddedOperation(page2Id, list2, new TextCanvasElement()));

        Assert.True(mgr.HasOperationsForPage(page1Id));
        Assert.True(mgr.HasOperationsForPage(page2Id));

        mgr.RemoveOperationsForPage(page1Id);

        Assert.False(mgr.HasOperationsForPage(page1Id));
        Assert.True(mgr.HasOperationsForPage(page2Id));
        Assert.Equal(1, mgr.UndoCount);
    }

    [Fact]
    public void PageViewModel_ElementPropertyMutation_MarksPageDirty()
    {
        var pageModel = new NotePage { PageNumber = 1 };
        var textModel = new TextCanvasElement { Text = "Initial", FontSize = 18.0 };
        pageModel.Elements.Add(textModel);

        var pageVm = new PageViewModel(pageModel);
        pageVm.Hydrate();
        pageVm.IsDirty = false;

        var textVm = Assert.IsType<TextCanvasElementViewModel>(Assert.Single(pageVm.Elements));
        textVm.Text = "Updated text";

        Assert.True(pageVm.IsDirty);
    }

    [Fact]
    public void PageViewModel_UncommittedEmptyTextElement_RemovesAddOperationFromUndoStack()
    {
        var undoRedo = new UndoRedoManager();
        var pageVm = new PageViewModel(new NotePage { PageNumber = 1 }, undoRedo);
        pageVm.Hydrate();

        var textVm = pageVm.AddTextElement(new Point(100, 100));
        Assert.True(undoRedo.CanUndo);
        Assert.Single(pageVm.Elements);

        // User clicks away without typing any text
        textVm.IsEditing = false;

        Assert.Empty(pageVm.Elements);
        Assert.Empty(pageVm.Model.Elements);
        Assert.False(undoRedo.CanUndo);
    }

    [Fact]
    public void MainViewModel_TextToolbarPropertyChange_UpdatesOnlyChangedProperty()
    {
        var mainVm = new MainViewModel();
        var pageVm = mainVm.ActivePage!;

        var el1 = new TextCanvasElement
        {
            Text = "Title",
            FontSize = 32.0,
            ForegroundHex = "#FFAA0000",
            IsBold = false,
            IsItalic = true
        };
        pageVm.AddElement(el1);
        var vm1 = Assert.IsType<TextCanvasElementViewModel>(Assert.Single(pageVm.Elements));
        vm1.IsSelected = true;

        // Toggling Bold on Toolbar should not clobber FontSize (32.0), ForegroundHex (#FFAA0000), or IsItalic (true)
        mainVm.Toolbar.IsTextBold = true;

        Assert.True(vm1.IsBold);
        Assert.Equal(32.0, vm1.FontSize);
        Assert.Equal("#FFAA0000", vm1.ForegroundHex);
        Assert.True(vm1.IsItalic);
    }

    [Fact]
    public void MainViewModel_UndoOnDehydratedPage_HydratesAndAppliesUndoCleanly()
    {
        var mainVm = new MainViewModel();
        var page1 = mainVm.ActivePage!;

        var textModel = new TextCanvasElement { X = 20, Y = 20, Width = 100, Height = 40, Text = "Hello" };
        page1.AddElement(textModel);
        Assert.Single(page1.Model.Elements);

        // Add several pages and dehydrate page 1
        mainVm.AddPageCommand.Execute(null);
        mainVm.AddPageCommand.Execute(null);
        mainVm.AddPageCommand.Execute(null);
        page1.Dehydrate();
        Assert.False(page1.IsHydrated);

        // Undo the element addition on dehydrated page 1
        mainVm.UndoCommand.Execute(null);

        Assert.Empty(page1.Model.Elements);
    }

    [Fact]
    public void PageCanvasControl_HitTestTopmostElement_RespectsZIndexOverCollectionOrder()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var pageVm = new PageViewModel(new NotePage { PageNumber = 1 });
                pageVm.Hydrate();

                // Add a text element first (ZIndex = 0)
                var textModel = new TextCanvasElement
                {
                    X = 50,
                    Y = 50,
                    Width = 200,
                    Height = 100,
                    ZIndex = 0,
                    Text = "Foreground Text"
                };
                pageVm.AddElement(textModel);

                // Add an image element after the text element, but with lower ZIndex (-1), matching AddImageElement behavior
                var imgModel = new ImageCanvasElement
                {
                    AssetKey = $"assets/{Guid.NewGuid():D}.png",
                    X = 10,
                    Y = 10,
                    Width = 400,
                    Height = 400,
                    ZIndex = -1
                };
                pageVm.AddElement(imgModel);

                var control = new PageCanvasControl
                {
                    DataContext = pageVm,
                    PageElements = pageVm.Elements
                };

                var hit = control.HitTestTopmostElement(new Point(100, 100));
                Assert.NotNull(hit);
                Assert.Same(textModel, hit!.Model);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx is not null)
        {
            throw threadEx;
        }
    }

    [Fact]
    public void PageCanvasControl_PointerLeftMove_ClampsStrokeDeltaUniformlyWithoutCrushingGeometry()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var pageVm = new PageViewModel(new NotePage { PageNumber = 1 });
                pageVm.Hydrate();

                var stroke = new Stroke(new StylusPointCollection
                {
                    new StylusPoint(20, 30),
                    new StylusPoint(120, 150)
                });
                pageVm.Strokes!.Add(stroke);

                var control = new PageCanvasControl
                {
                    DataContext = pageVm,
                    PageStrokes = pageVm.Strokes,
                    PageElements = pageVm.Elements,
                    ActiveToolMode = ToolMode.Pointer
                };

                control.SelectSingleObject(null, stroke);
                control.PointerLeftDown(new Point(20, 30));

                // Drag far past the top-left boundary (-100, -100)
                control.PointerLeftMove(new Point(-100, -100));

                // Stroke's leftmost point clamps at X=0, Y=0, and second point maintains relative offset (100, 120)
                Assert.Equal(0.0, stroke.StylusPoints[0].X, 3);
                Assert.Equal(0.0, stroke.StylusPoints[0].Y, 3);
                Assert.Equal(100.0, stroke.StylusPoints[1].X, 3);
                Assert.Equal(120.0, stroke.StylusPoints[1].Y, 3);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx is not null)
        {
            throw threadEx;
        }
    }
}
