using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using Xunit;

namespace InkSlate.Tests;

public sealed class DocumentModelTests
{
    [Fact]
    public void NoteDocumentHasExpectedDefaults()
    {
        var doc = new NoteDocument();

        Assert.Equal(NoteDocument.CurrentFormatVersion, doc.FormatVersion);
        Assert.Equal(NoteDocument.DefaultTitle, doc.Title);
        Assert.Equal(PageDimensions.A4.Width, doc.DefaultPageWidth);
        Assert.Equal(PageDimensions.A4.Height, doc.DefaultPageHeight);
        Assert.Empty(doc.Pages);
        Assert.True(doc.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.True(doc.UpdatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void NotePageHasExpectedDefaults()
    {
        var page = new NotePage();

        Assert.NotEqual(Guid.Empty, page.Id);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(PageDimensions.A4.Width, page.Width);
        Assert.Equal(PageDimensions.A4.Height, page.Height);
        Assert.Equal(NotePage.DefaultBackgroundHex, page.BackgroundHex);
        Assert.Null(page.IsfEntryPath);
        Assert.Empty(page.Elements);
    }

    [Fact]
    public void NotePageSupportsPolymorphicElements()
    {
        var page = new NotePage();

        var textElement = new TextCanvasElement
        {
            Text = "Sample note header",
            X = 50.0,
            Y = 100.0,
            FontSize = 24.0,
            IsBold = true
        };

        var imageElement = new ImageCanvasElement
        {
            AssetKey = "assets/sample.png",
            X = 50.0,
            Y = 200.0,
            Width = 400.0,
            Height = 300.0
        };

        page.Elements.Add(textElement);
        page.Elements.Add(imageElement);

        Assert.Equal(2, page.Elements.Count);
        Assert.IsType<TextCanvasElement>(page.Elements[0]);
        Assert.IsType<ImageCanvasElement>(page.Elements[1]);

        var retrievedText = (TextCanvasElement)page.Elements[0];
        Assert.Equal("Sample note header", retrievedText.Text);
        Assert.True(retrievedText.IsBold);

        var retrievedImage = (ImageCanvasElement)page.Elements[1];
        Assert.Equal("assets/sample.png", retrievedImage.AssetKey);
        Assert.Equal(400.0, retrievedImage.Width);
    }
}

