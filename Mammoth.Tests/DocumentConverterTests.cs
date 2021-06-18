﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Sdk;

namespace Mammoth.Tests
{
  public class DocumentConverterTests
  {
    [Fact]
    public void DocxContainingOneParagraphIsConvertedToSingleParagraphElement()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("single-paragraph.docx"),
          "<p>Walking on imported air</p>");
    }

    [Fact]
    public void CanReadFilesWithUtf8Bom()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("utf8-bom.docx"),
          "<p>This XML has a byte order mark.</p>");
    }

    [Fact]
    public void EmptyParagraphsAreIgnoredByDefault()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("empty.docx"),
          "");
    }

    [Fact]
    public void EmptyParagraphsArePreservedIfIgnoreEmptyParagraphsIsFalse()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("empty.docx", converter => converter.PreserveEmptyParagraphs()),
          "<p></p>");
    }

    [Fact]
    public void SimpleListIsConvertedToListElements()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("simple-list.docx"),
          "<ul><li>Apple</li><li>Banana</li></ul>");
    }

    [Fact]
    public void WordTablesAreConvertedToHtmlTables()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("tables.docx"),
          "<p>Above</p>" +
          "<table>" +
          "<tr><td><p>Top left</p></td><td><p>Top right</p></td></tr>" +
          "<tr><td><p>Bottom left</p></td><td><p>Bottom right</p></td></tr>" +
          "</table>" +
          "<p>Below</p>");
    }

    [Fact]
    public void InlineImagesReferencedByPathRelativeToPartAreIncludedInOutput()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("tiny-picture.docx"),
          "<p><img src=\"data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAIAAAACUFjqAAAAAXNSR0IArs4c6QAAAAlwSFlzAAAOvgAADr4B6kKxwAAAABNJREFUKFNj/M+ADzDhlWUYqdIAQSwBE8U+X40AAAAASUVORK5CYII=\" /></p>");
    }

    [Fact]
    public void InlineImagesReferencedByPathRelativeToBaseAreIncludedInOutput()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("tiny-picture-target-base-relative.docx"),
          "<p><img src=\"data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAIAAAACUFjqAAAAAXNSR0IArs4c6QAAAAlwSFlzAAAOvgAADr4B6kKxwAAAABNJREFUKFNj/M+ADzDhlWUYqdIAQSwBE8U+X40AAAAASUVORK5CYII=\" /></p>");
    }

    [Fact]
    public void ImagesStoredOutsideOfDocumentAreIncludedInOutput()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("external-picture.docx"),
          "<p><img src=\"data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAIAAAACUFjqAAAAAXNSR0IArs4c6QAAAAlwSFlzAAAOvgAADr4B6kKxwAAAABNJREFUKFNj/M+ADzDhlWUYqdIAQSwBE8U+X40AAAAASUVORK5CYII=\" /></p>");
    }

    [Fact]
    public void WarnIfDocumentHasImagesStoredOutsideOfDocumentWhenPathOfDocumentIsUnknown()
    {
      using (var file = File.OpenRead(TestFilePath("external-picture.docx")))
      {
        var result = new DocumentConverter().ConvertToHtml(file);
        Assert.Equal("", result.Value);
        Assert.Equal(new[] { "could not open external image 'tiny-picture.png': path of document is unknown, but is required for relative URI" }, result.Warnings);
      }
    }

    [Fact]
    public void WarnIfImagesStoredOutsideOfDocumentAreNotFound()
    {
      var tempDirectory = Path.Combine(Path.GetTempPath(), "mammoth-" + Guid.NewGuid());
      Directory.CreateDirectory(tempDirectory);
      try
      {
        var documentPath = Path.Combine(tempDirectory, "external-picture.docx");
        File.Copy(TestFilePath("external-picture.docx"), documentPath);
        var result = new DocumentConverter().ConvertToHtml(documentPath);
        Assert.Equal("", result.Value);
        Assert.StartsWith("could not open external image 'tiny-picture.png':", result.Warnings.Single());
      }
      finally
      {
        Directory.Delete(tempDirectory, recursive: true);
      }
    }

    [Fact]
    public void ImageConversionCanBeCustomised()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("tiny-picture.docx", mammoth => mammoth.ImageConverter(ConvertImage)),
          "<p><img src=\"iV,image/png\" /></p>"
      );
    }

    private IDictionary<string, string> ConvertImage(IImage image)
    {
      using (var stream = image.GetStream())
      {
        var base64 = StreamToBase64(stream);
        var src = base64.Substring(0, 2) + "," + image.ContentType;
        return new Dictionary<string, string> { { "src", src } };
      }
    }

    private static string StreamToBase64(System.IO.Stream stream)
    {
      var memoryStream = new System.IO.MemoryStream();
      stream.CopyTo(memoryStream);
      return System.Convert.ToBase64String(memoryStream.ToArray());
    }

    [Fact]
    public void ContentTypesAreRead()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("tiny-picture-custom-content-type.docx"),
          "<p><img src=\"data:image/gif;base64,iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAIAAAACUFjqAAAAAXNSR0IArs4c6QAAAAlwSFlzAAAOvgAADr4B6kKxwAAAABNJREFUKFNj/M+ADzDhlWUYqdIAQSwBE8U+X40AAAAASUVORK5CYII=\" /></p>");
    }

    [Fact]
    public void FootnotesAreAppendedToText()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("footnotes.docx", mammoth => mammoth.IdPrefix("doc-42-")),
              "<p>Ouch" +
              "<sup><a href=\"#doc-42-footnote-1\" id=\"doc-42-footnote-ref-1\">[1]</a></sup>." +
              "<sup><a href=\"#doc-42-footnote-2\" id=\"doc-42-footnote-ref-2\">[2]</a></sup></p>" +
              "<ol><li id=\"doc-42-footnote-1\"><p> A tachyon walks into a bar. <a href=\"#doc-42-footnote-ref-1\">↑</a></p></li>" +
              "<li id=\"doc-42-footnote-2\"><p> Fin. <a href=\"#doc-42-footnote-ref-2\">↑</a></p></li></ol>");
    }

    [Fact]
    public void EndNotesAreAppendedToText()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("endnotes.docx", mammoth => mammoth.IdPrefix("doc-42-")),
              "<p>Ouch" +
              "<sup><a href=\"#doc-42-endnote-2\" id=\"doc-42-endnote-ref-2\">[1]</a></sup>." +
              "<sup><a href=\"#doc-42-endnote-3\" id=\"doc-42-endnote-ref-3\">[2]</a></sup></p>" +
              "<ol><li id=\"doc-42-endnote-2\"><p> A tachyon walks into a bar. <a href=\"#doc-42-endnote-ref-2\">↑</a></p></li>" +
              "<li id=\"doc-42-endnote-3\"><p> Fin. <a href=\"#doc-42-endnote-ref-3\">↑</a></p></li></ol>");
    }

    [Fact]
    public void RelationshipsAreReadForEachFileContainingBodyXml()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("footnote-hyperlink.docx", mammoth => mammoth.IdPrefix("doc-42-")),

              "<p><sup><a href=\"#doc-42-footnote-1\" id=\"doc-42-footnote-ref-1\">[1]</a></sup></p>" +
              "<ol><li id=\"doc-42-footnote-1\"><p> <a href=\"http://www.example.com\">Example</a> <a href=\"#doc-42-footnote-ref-1\">↑</a></p></li></ol>");
    }

    [Fact]
    public void TextBoxesAreRead()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("text-box.docx"),
          "<p>Datum plane</p>");
    }

    [Fact]
    public void CanUseCustomStyleMap()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("underline.docx", mammoth => mammoth.AddStyleMap("u => em")),
          "<p><strong>The <em>Sunset</em> Tree</strong></p>");
    }

    [Fact]
    public void MostRecentlyAddedStyleMapTakesPrecedence()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("underline.docx", mammoth => mammoth.AddStyleMap("u => em").AddStyleMap("u => strong")),
          "<p><strong>The <strong>Sunset</strong> Tree</strong></p>");
    }

    [Fact]
    public void RulesFromPreviouslyAddedStyleMapsStillTakeEffectIfNotOverridden()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("underline.docx", mammoth => mammoth.AddStyleMap("u => em").AddStyleMap("strike => del")),
          "<p><strong>The <em>Sunset</em> Tree</strong></p>");
    }

    [Fact]
    public void ErrorIsRaisedIfStyleMapCannotBeParsed()
    {
      var exception = Assert.ThrowsAny<Exception>(() =>
    new DocumentConverter().AddStyleMap("p =>\np[style-name=] =>"));
      Assert.Equal(
    "error reading style map at line 2, character 14: expected token of type _STRING but was of type _SYMBOL\n\n" +
                "p[style-name=] =>\n" +
                "             ^",
    exception.Message);
    }

    [Fact]
    public void CanDisableDefaultStyleMap()
    {
      var result = ConvertToHtml("simple-list.docx", mammoth => mammoth.DisableDefaultStyleMap());
      Assert.Equal(new[] { "Unrecognised paragraph style: List Paragraph (Style ID: ListParagraph)" }, result.Warnings);
      Assert.Equal("<p>Apple</p><p>Banana</p>", result.Value);
    }

    [Fact]
    public void EmbeddedStyleMapIsUsedIfPresent()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("embedded-style-map.docx"),
          "<h1>Walking on imported air</h1>"
      );
    }

    [Fact]
    public void ExplicitStyleMapTakesPrecedenceOverEmbeddedStyleMap()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("embedded-style-map.docx", mammoth => mammoth.AddStyleMap("p => p")),
          "<p>Walking on imported air</p>"
      );
    }

    [Fact]
    public void ExplicitStyleMapIsCombinedWithEmbeddedStyleMap()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("embedded-style-map.docx", mammoth => mammoth.AddStyleMap("r => strong")),
          "<h1><strong>Walking on imported air</strong></h1>"
      );
    }

    [Fact]
    public void embeddedStyleMapsCanBeDisabled()
    {
      AssertSuccessfulConversion(
          ConvertToHtml("embedded-style-map.docx", mammoth => mammoth.DisableEmbeddedStyleMap()),
          "<p>Walking on imported air</p>"
      );
    }

    [Fact]
    public void CanExtractRawTextFromFile()
    {
      AssertSuccessfulConversion(
          new DocumentConverter().ExtractRawText(TestFilePath("simple-list.docx")),
          "Apple\n\nBanana\n\n");
    }

    [Fact]
    public void CanExtractRawTextFromStream()
    {
      using (var file = File.OpenRead(TestFilePath("simple-list.docx")))
      {
        AssertSuccessfulConversion(
            new DocumentConverter().ExtractRawText(file),
            "Apple\n\nBanana\n\n");
      }
    }

    private void AssertSuccessfulConversion(IResult<string> result, string expectedValue)
    {
      if (result.Warnings.Count > 0)
      {
        throw new XunitException("Unexpected warnings: " + string.Join(", ", result.Warnings));
      }
      Assert.Equal(expectedValue, result.Value);
    }

    private IResult<string> ConvertToHtml(string name) => ConvertToHtml(name, converter => converter);

    private IResult<string> ConvertToHtml(string name, Func<DocumentConverter, DocumentConverter> configure) => configure(new DocumentConverter()).ConvertToHtml(TestFilePath(name));

    private string TestFilePath(string name) => Path.Combine("../../../TestData", name);
  }
}