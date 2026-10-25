using System.IO;
using System.Text.Json;
using UltimateClipboard.Models;
using Xunit;

namespace UltimateClipboard.Tests;

public class FilePreviewTests
{
    [Fact]
    public void FileItemPreview_RecognizesExtensionsAndGlyphs()
    {
        var imageItem = new FileItemPreview(@"C:\Users\User\Pictures\photo.png");
        Assert.Equal("photo.png", imageItem.FileName);
        Assert.Equal(".png", imageItem.Extension);
        Assert.Equal("\uEB9F", imageItem.IconGlyph); // Image glyph

        var pdfItem = new FileItemPreview(@"C:\Users\User\Documents\contract.pdf");
        Assert.Equal("contract.pdf", pdfItem.FileName);
        Assert.Equal(".pdf", pdfItem.Extension);
        Assert.Equal("\uEA90", pdfItem.IconGlyph); // PDF glyph

        var codeItem = new FileItemPreview(@"C:\Users\User\Source\App.cs");
        Assert.Equal("App.cs", codeItem.FileName);
        Assert.Equal("\uE943", codeItem.IconGlyph); // Code glyph

        var archiveItem = new FileItemPreview(@"C:\Users\User\Downloads\archive.zip");
        Assert.Equal("archive.zip", archiveItem.FileName);
        Assert.Equal("\uE8B7", archiveItem.IconGlyph); // Archive glyph
    }

    [Fact]
    public void ClipboardEntry_WithSingleFile_FormatsCleanPreview()
    {
        var files = new[] { @"C:\Users\User\Documents\Report.docx" };
        var entry = new ClipboardEntry
        {
            ContentType = ClipboardContentType.Files,
            FilePaths = JsonSerializer.Serialize(files)
        };

        Assert.True(entry.IsFileEntry);
        Assert.Single(entry.FileItems);
        Assert.Equal("Report.docx", entry.FileItems[0].FileName);
        Assert.Equal("Report.docx", entry.Preview);
        Assert.Equal(0, entry.ExtraFileCount);
    }

    [Fact]
    public void ClipboardEntry_WithMultipleFiles_FormatsSummaryAndExtraCount()
    {
        var files = new[]
        {
            @"C:\folder\file1.txt",
            @"C:\folder\file2.jpg",
            @"C:\folder\file3.pdf",
            @"C:\folder\file4.zip",
            @"C:\folder\file5.mp4"
        };
        var entry = new ClipboardEntry
        {
            ContentType = ClipboardContentType.Files,
            FilePaths = JsonSerializer.Serialize(files)
        };

        Assert.True(entry.IsFileEntry);
        Assert.Equal(4, entry.FileItems.Count); // Capped at 4 items
        Assert.Equal(1, entry.ExtraFileCount);   // 5 - 4 = 1
        Assert.Contains("5 Files:", entry.Preview);
        Assert.Contains("file1.txt", entry.Preview);
    }
}
