using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UltimateClipboard.Models;

public enum ClipboardContentType
{
    Text = 0,
    RichText = 1,
    Html = 2,
    Image = 3,
    Files = 4,
    Unknown = 5
}

public class ClipboardEntry
{
    public long Id { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public ClipboardContentType ContentType { get; set; }
    public string? PlainText { get; set; }
    public string? RichTextData { get; set; }
    public string? HtmlData { get; set; }
    public byte[]? ImageData { get; set; }
    public byte[]? ThumbnailData { get; set; }
    public string? FilePaths { get; set; } // JSON array of file paths
    public string? SourceApplication { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public bool IsPinned { get; set; }
    public bool IsFavorite { get; set; }
    public string? Tags { get; set; } // comma-separated tags
    public long ContentSizeBytes { get; set; }

    /// <summary>
    /// Gets a display-friendly preview of the content (first ~200 chars for text, file count for files, dimensions for images).
    /// </summary>
    public string Preview
    {
        get
        {
            return ContentType switch
            {
                ClipboardContentType.Text or ClipboardContentType.RichText or ClipboardContentType.Html =>
                    string.IsNullOrEmpty(PlainText)
                        ? "[Empty text]"
                        : PlainText.Length > 200 ? PlainText[..200] + "\u2026" : PlainText,
                ClipboardContentType.Image => !string.IsNullOrWhiteSpace(PlainText) ? PlainText : "Image",
                ClipboardContentType.Files => FormatFilesPreview(),
                _ => "[Unknown content]"
            };
        }
    }

    /// <summary>
    /// Returns a short content-type label for UI display.
    /// </summary>
    public string TypeLabel => ContentType switch
    {
        ClipboardContentType.Text => "Text",
        ClipboardContentType.RichText => "Rich Text",
        ClipboardContentType.Html => "HTML",
        ClipboardContentType.Image => "Image",
        ClipboardContentType.Files => "Files",
        _ => "Other"
    };

    [JsonIgnore]
    public string[] ParsedFilePaths
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FilePaths)) return Array.Empty<string>();
            try
            {
                return JsonSerializer.Deserialize<string[]>(FilePaths) ?? Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }

    [JsonIgnore]
    public bool IsFileEntry => ContentType == ClipboardContentType.Files;

    [JsonIgnore]
    public List<FileItemPreview> FileItems
    {
        get
        {
            if (ContentType != ClipboardContentType.Files) return new List<FileItemPreview>();
            var result = new List<FileItemPreview>();
            var paths = ParsedFilePaths;
            foreach (var path in paths.Take(4))
            {
                result.Add(new FileItemPreview(path));
            }
            return result;
        }
    }

    [JsonIgnore]
    public int ExtraFileCount => ParsedFilePaths.Length > 4 ? ParsedFilePaths.Length - 4 : 0;

    private string FormatFilesPreview()
    {
        var paths = ParsedFilePaths;
        if (paths.Length == 0) return "[No files]";

        if (paths.Length == 1)
        {
            string path = paths[0];
            string fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName)) fileName = path;

            if (!string.IsNullOrWhiteSpace(PlainText) && PlainText != path && !PlainText.Contains("\n" + path))
            {
                return $"{fileName} — {PlainText}";
            }
            return fileName;
        }

        var names = paths.Take(3).Select(p => Path.GetFileName(p)).Where(n => !string.IsNullOrEmpty(n));
        string joined = string.Join(", ", names);
        if (paths.Length > 3)
            return $"{paths.Length} Files: {joined} (+{paths.Length - 3} more)";
        return $"{paths.Length} Files: {joined}";
    }
}
