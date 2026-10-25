using System;
using System.IO;

namespace UltimateClipboard.Models;

/// <summary>
/// Model representing a single file within a clipboard file-drop entry for UI preview.
/// </summary>
public class FileItemPreview
{
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "\uE8A5";
    public bool Exists { get; set; }

    public FileItemPreview() { }

    public FileItemPreview(string fullPath)
    {
        FullPath = fullPath;
        try
        {
            FileName = Path.GetFileName(fullPath);
            if (string.IsNullOrEmpty(FileName)) FileName = fullPath;
            DirectoryPath = Path.GetDirectoryName(fullPath) ?? string.Empty;
            Extension = Path.GetExtension(fullPath).ToLowerInvariant();

            bool isDir = Directory.Exists(fullPath);
            bool isFile = File.Exists(fullPath);
            Exists = isDir || isFile;

            if (isFile)
            {
                long bytes = new FileInfo(fullPath).Length;
                SizeText = FormatSize(bytes);
            }
            else if (isDir)
            {
                SizeText = "Folder";
            }
            else
            {
                SizeText = "File not found";
            }

            IconGlyph = GetIconForExtension(Extension, isDir);
        }
        catch
        {
            FileName = fullPath;
            SizeText = string.Empty;
            IconGlyph = "\uE8A5";
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    private static string GetIconForExtension(string ext, bool isDir)
    {
        if (isDir) return "\uE8B7"; // Folder
        return ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".svg" or ".ico" => "\uEB9F", // Image
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".webm" => "\uE714",                     // Video
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" => "\uE8D6",                     // Audio
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "\uE8B7",                                   // Archive
            ".pdf" => "\uEA90",                                                                         // PDF
            ".doc" or ".docx" => "\uE8BD",                                                              // Word
            ".xls" or ".xlsx" or ".csv" => "\uE8C0",                                                    // Spreadsheet
            ".ppt" or ".pptx" => "\uE8BA",                                                              // Presentation
            ".cs" or ".js" or ".ts" or ".py" or ".cpp" or ".h" or ".json" or ".xml" or ".html" or ".css" => "\uE943", // Code
            ".txt" or ".md" or ".log" => "\uE8C1",                                                      // Text
            ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1" => "\uE756",                                 // Executable/Script
            _ => "\uE8A5"                                                                               // Generic file
        };
    }
}
