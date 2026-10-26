using System.Text.Json;
using CsCheck;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.Serialization;
using Xunit;

namespace InkSlate.Tests;

public sealed class DocumentSerializationAndValidationTests
{
    [Fact]
    public void DocumentSerializesAndDeserializesRoundTripWithPolymorphicElements()
    {
        var doc = new NoteDocument
        {
            Title = "Math Lecture 1",
            DefaultPageWidth = 1280.0,
            DefaultPageHeight = 720.0
        };

        var page = new NotePage
        {
            PageNumber = 1,
            Width = 1280.0,
            Height = 720.0,
            BackgroundHex = "#FFF0F0F0",
            IsfEntryPath = "pages/11111111-2222-3333-4444-555555555555.isf"
        };

        page.Elements.Add(new TextCanvasElement
        {
            Text = "Pythagorean Theorem",
            FontSize = 28.0,
            IsBold = true,
            ForegroundHex = "#FF0055AA"
        });

        page.Elements.Add(new ImageCanvasElement
        {
            AssetKey = "assets/22222222-3333-4444-5555-666666666666.png",
            Width = 600.0,
            Height = 400.0
        });

        doc.Pages.Add(page);

        var json = JsonSerializer.Serialize(doc, InkSlateJsonContext.Default.NoteDocument);
        Assert.Contains("\"kind\":\"text\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"image\"", json, StringComparison.Ordinal);
        Assert.Contains("\"title\":\"Math Lecture 1\"", json, StringComparison.Ordinal);

        var deserialized = JsonSerializer.Deserialize(json, InkSlateJsonContext.Default.NoteDocument);
        Assert.NotNull(deserialized);
        Assert.Equal("Math Lecture 1", deserialized.Title);
        Assert.Single(deserialized.Pages);

        var loadedPage = deserialized.Pages[0];
        Assert.Equal(2, loadedPage.Elements.Count);
        Assert.IsType<TextCanvasElement>(loadedPage.Elements[0]);
        Assert.IsType<ImageCanvasElement>(loadedPage.Elements[1]);

        var text = (TextCanvasElement)loadedPage.Elements[0];
        Assert.Equal("Pythagorean Theorem", text.Text);
        Assert.Equal(28.0, text.FontSize);

        var image = (ImageCanvasElement)loadedPage.Elements[1];
        Assert.Equal("assets/22222222-3333-4444-5555-666666666666.png", image.AssetKey);
    }

    [Fact]
    public void DeserializerSkipsUnknownMembersWithoutFailing()
    {
        const string jsonWithUnknownFields = """
        {
            "formatVersion": 1,
            "title": "Document with extra metadata",
            "unknownFutureField": "ignored_value",
            "pages": []
        }
        """;

        var doc = JsonSerializer.Deserialize(jsonWithUnknownFields, InkSlateJsonContext.Default.NoteDocument);
        Assert.NotNull(doc);
        Assert.Equal("Document with extra metadata", doc.Title);
    }

    [Theory]
    [InlineData("#FFFFFF", true)]
    [InlineData("#FFFFFFFF", true)]
    [InlineData("#123456", true)]
    [InlineData("#AABBCCDD", true)]
    [InlineData("#12345", false)]
    [InlineData("#1234567", false)]
    [InlineData("FFFFFF", false)]
    [InlineData("red", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void HexColorValidationMatchesExpected(string? hex, bool expectedValid)
    {
        Assert.Equal(expectedValid, DocumentValidator.IsValidHexColor(hex));
    }

    [Theory]
    [InlineData("pages/12345678-1234-1234-1234-123456789abc.isf", true)]
    [InlineData("pages/12345678-1234-1234-1234-123456789ABC.isf", true)]
    [InlineData("../pages/12345678-1234-1234-1234-123456789abc.isf", false)]
    [InlineData("pages/../12345678-1234-1234-1234-123456789abc.isf", false)]
    [InlineData("pages/badname.isf", false)]
    [InlineData("pages/12345678-1234-1234-1234-123456789abc.exe", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsfEntryPathValidationMatchesExpected(string? path, bool expectedValid)
    {
        Assert.Equal(expectedValid, DocumentValidator.IsValidIsfEntryPath(path));
    }

    [Theory]
    [InlineData("assets/12345678-1234-1234-1234-123456789abc.png", true)]
    [InlineData("assets/12345678-1234-1234-1234-123456789abc.jpg", true)]
    [InlineData("assets/12345678-1234-1234-1234-123456789abc.jpeg", true)]
    [InlineData("../../assets/12345678-1234-1234-1234-123456789abc.png", false)]
    [InlineData("assets/12345678-1234-1234-1234-123456789abc.svg", false)]
    [InlineData("assets/12345678-1234-1234-1234-123456789abc.dll", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void AssetKeyValidationMatchesExpected(string? key, bool expectedValid)
    {
        Assert.Equal(expectedValid, DocumentValidator.IsValidAssetKey(key));
    }

    [Fact]
    public void SanitizeEnforcesSafetyInvariantsOnCorruptedDocument()
    {
        var doc = new NoteDocument
        {
            FormatVersion = -5,
            Title = "   ",
            DefaultPageWidth = double.NaN,
            DefaultPageHeight = double.PositiveInfinity
        };

        var page = new NotePage
        {
            PageNumber = -10,
            Width = -100.0,
            Height = 9999999.0,
            BackgroundHex = "invalid_color",
            IsfEntryPath = "../../evil.isf"
        };

        page.Elements.Add(new TextCanvasElement
        {
            X = double.NaN,
            Y = double.NegativeInfinity,
            Width = 0.5,
            Height = 100000.0,
            FontSize = -20.0,
            ForegroundHex = "bad_color",
            FontFamily = ""
        });

        page.Elements.Add(new ImageCanvasElement
        {
            AssetKey = "../../evil.png" // Should be rejected and stripped!
        });

        doc.Pages.Add(page);

        DocumentValidator.Sanitize(doc);

        Assert.Equal(NoteDocument.CurrentFormatVersion, doc.FormatVersion);
        Assert.Equal(NoteDocument.DefaultTitle, doc.Title);
        Assert.Equal(PageDimensions.A4.Width, doc.DefaultPageWidth);
        Assert.Equal(PageDimensions.A4.Height, doc.DefaultPageHeight);

        Assert.Single(doc.Pages);
        var sanitizedPage = doc.Pages[0];
        Assert.Equal(1, sanitizedPage.PageNumber);
        Assert.Equal(PageDimensions.MinDimensionDip, sanitizedPage.Width);
        Assert.Equal(PageDimensions.MaxDimensionDip, sanitizedPage.Height);
        Assert.Equal(NotePage.DefaultBackgroundHex, sanitizedPage.BackgroundHex);
        Assert.Null(sanitizedPage.IsfEntryPath);

        // The image with path traversal should have been removed:
        Assert.Single(sanitizedPage.Elements);
        Assert.IsType<TextCanvasElement>(sanitizedPage.Elements[0]);

        var sanitizedText = (TextCanvasElement)sanitizedPage.Elements[0];
        Assert.Equal(0.0, sanitizedText.X);
        Assert.Equal(0.0, sanitizedText.Y);
        Assert.Equal(DocumentValidator.MinElementDimensionDip, sanitizedText.Width);
        Assert.Equal(DocumentValidator.MaxElementDimensionDip, sanitizedText.Height);
        Assert.Equal(DocumentValidator.MinFontSize, sanitizedText.FontSize);
        Assert.Equal(TextCanvasElement.DefaultForegroundHex, sanitizedText.ForegroundHex);
        Assert.Equal(TextCanvasElement.DefaultFontFamily, sanitizedText.FontFamily);
    }

    [Fact]
    public void CsCheck_Sanitize_EnforcesStrictSafetyInvariantsOnAdversarialDoublesAndStrings()
    {
        var pathologicalDoubles = Gen.OneOf(
            Gen.Const(double.NaN),
            Gen.Const(double.NegativeInfinity),
            Gen.Const(double.PositiveInfinity),
            Gen.Const(double.MaxValue),
            Gen.Const(double.MinValue),
            Gen.Const(-99999.0),
            Gen.Const(-1.0),
            Gen.Const(0.0),
            Gen.Double[-5000, 5000]);

        var pathologicalStrings = Gen.OneOf(
            Gen.Const(""),
            Gen.Const("   "),
            Gen.Const("../../Windows/System32/cmd.exe"),
            Gen.Const("..\\..\\evil.isf"),
            Gen.Const("assets/../../../cmd.exe"),
            Gen.Const("#GGGGGG"),
            Gen.Const("not-a-hex"),
            Gen.Const("#FFFFFFFF"),
            Gen.Const("pages/12345678-1234-1234-1234-123456789abc.isf"),
            Gen.Const("assets/12345678-1234-1234-1234-123456789abc.png"),
            Gen.String);

        Gen.Select(pathologicalDoubles, pathologicalDoubles, pathologicalStrings, pathologicalStrings)
            .Sample((d1, d2, s1, s2) =>
            {
                var doc = new NoteDocument
                {
                    Id = Guid.Empty,
                    FormatVersion = -99,
                    Title = s1,
                    DefaultPageWidth = d1,
                    DefaultPageHeight = d2
                };

                var page = new NotePage
                {
                    Id = Guid.Empty,
                    PageNumber = -5,
                    Width = d2,
                    Height = d1,
                    BackgroundHex = s1,
                    IsfEntryPath = s2
                };

                page.Elements.Add(new TextCanvasElement
                {
                    X = d1,
                    Y = d2,
                    Width = d1,
                    Height = d2,
                    FontSize = d1,
                    ForegroundHex = s1,
                    FontFamily = s2
                });

                page.Elements.Add(new ImageCanvasElement
                {
                    AssetKey = s2,
                    X = d2,
                    Y = d1,
                    Width = d2,
                    Height = d1,
                    CropX = d1,
                    CropY = d2,
                    CropWidth = d1,
                    CropHeight = d2
                });

                doc.Pages.Add(page);

                DocumentValidator.Sanitize(doc);

                // Document postcondition invariants
                Assert.NotEqual(Guid.Empty, doc.Id);
                Assert.Equal(NoteDocument.CurrentFormatVersion, doc.FormatVersion);
                Assert.False(string.IsNullOrWhiteSpace(doc.Title));
                Assert.InRange(doc.DefaultPageWidth, PageDimensions.MinDimensionDip, PageDimensions.MaxDimensionDip);
                Assert.InRange(doc.DefaultPageHeight, PageDimensions.MinDimensionDip, PageDimensions.MaxDimensionDip);

                // Page postcondition invariants
                Assert.Single(doc.Pages);
                var sanitizedPage = doc.Pages[0];
                Assert.NotEqual(Guid.Empty, sanitizedPage.Id);
                Assert.Equal(1, sanitizedPage.PageNumber);
                Assert.InRange(sanitizedPage.Width, PageDimensions.MinDimensionDip, PageDimensions.MaxDimensionDip);
                Assert.InRange(sanitizedPage.Height, PageDimensions.MinDimensionDip, PageDimensions.MaxDimensionDip);
                Assert.True(DocumentValidator.IsValidHexColor(sanitizedPage.BackgroundHex));
                if (sanitizedPage.IsfEntryPath is not null)
                {
                    Assert.True(DocumentValidator.IsValidIsfEntryPath(sanitizedPage.IsfEntryPath));
                }

                // Element postcondition invariants
                foreach (var element in sanitizedPage.Elements)
                {
                    Assert.InRange(element.X, DocumentValidator.MinElementCoordinateDip, DocumentValidator.MaxElementCoordinateDip);
                    Assert.InRange(element.Y, DocumentValidator.MinElementCoordinateDip, DocumentValidator.MaxElementCoordinateDip);
                    Assert.InRange(element.Width, DocumentValidator.MinElementDimensionDip, DocumentValidator.MaxElementDimensionDip);
                    Assert.InRange(element.Height, DocumentValidator.MinElementDimensionDip, DocumentValidator.MaxElementDimensionDip);

                    if (element is TextCanvasElement text)
                    {
                        Assert.InRange(text.FontSize, DocumentValidator.MinFontSize, DocumentValidator.MaxFontSize);
                        Assert.True(DocumentValidator.IsValidHexColor(text.ForegroundHex));
                        Assert.False(string.IsNullOrWhiteSpace(text.FontFamily));
                    }
                    else if (element is ImageCanvasElement img)
                    {
                        Assert.True(DocumentValidator.IsValidAssetKey(img.AssetKey));
                        Assert.True(img.CropX >= 0.0);
                        Assert.True(img.CropY >= 0.0);
                        Assert.True(img.CropWidth >= 0.0);
                        Assert.True(img.CropHeight >= 0.0);
                    }
                }
            });
    }

    [Fact]
    public void DocumentSanitizerNeverFailsUnderPropertyFuzzing()
    {
        Gen.Select(Gen.Double, Gen.Double, Gen.Double, Gen.Double).Sample((w, h, elW, elH) =>
        {
            var doc = new NoteDocument
            {
                DefaultPageWidth = w,
                DefaultPageHeight = h
            };

            var page = new NotePage
            {
                Width = w,
                Height = h
            };

            page.Elements.Add(new TextCanvasElement
            {
                Width = elW,
                Height = elH,
                FontSize = elW
            });

            DocumentValidator.Sanitize(doc);

            return double.IsFinite(doc.DefaultPageWidth)
                && double.IsFinite(doc.DefaultPageHeight)
                && double.IsFinite(page.Width)
                && double.IsFinite(page.Height)
                && page.Elements.TrueForAll(e => double.IsFinite(e.Width) && double.IsFinite(e.Height));
        });
    }
}

