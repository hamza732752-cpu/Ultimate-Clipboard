using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using UltimateClipboard.Helpers;
using UltimateClipboard.Models;
using IDataObject = System.Windows.IDataObject;
using DataFormats = System.Windows.DataFormats;
using Clipboard = System.Windows.Clipboard;

namespace UltimateClipboard.Services;

/// <summary>
/// Extracts clipboard contents and creates ClipboardEntry objects.
/// Must be called from the UI (STA) thread.
/// </summary>
public class ClipboardCapture
{
    private readonly int _maxImageSizeKB;

    public ClipboardCapture(int maxImageSizeKB = 5120)
    {
        _maxImageSizeKB = maxImageSizeKB;
    }

    /// <summary>
    /// Captures the current clipboard contents and returns a ClipboardEntry, or null if the clipboard is empty or unsupported.
    /// Retries on COMException to handle temporary clipboard locks by other processes.
    /// </summary>
    public ClipboardEntry? CaptureCurrentClipboard()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var data = Clipboard.GetDataObject();
                if (data is null)
                {
                    if (attempt < 4) { System.Threading.Thread.Sleep(30); continue; }
                    return null;
                }

                var entry = new ClipboardEntry
                {
                    CapturedAt = DateTime.UtcNow,
                    SourceApplication = GetForegroundProcessName()
                };

                // Priority: Files > Image > HTML > RichText > Text
                if (data.GetDataPresent(DataFormats.FileDrop) || Clipboard.ContainsFileDropList())
                {
                    var fileEntry = CaptureFiles(data, entry);
                    if (fileEntry is not null) return fileEntry;
                }

                if (data.GetDataPresent(DataFormats.Bitmap) ||
                    data.GetDataPresent(DataFormats.Dib) ||
                    data.GetDataPresent("DeviceIndependentBitmap") ||
                    Clipboard.ContainsImage())
                {
                    return CaptureImage(data, entry);
                }

                if (data.GetDataPresent(DataFormats.Html))
                {
                    return CaptureHtml(data, entry);
                }

                if (data.GetDataPresent(DataFormats.Rtf))
                {
                    return CaptureRichText(data, entry);
                }

                if (data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Text))
                {
                    return CaptureText(data, entry);
                }

                return null;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                if (attempt < 4)
                {
                    System.Threading.Thread.Sleep(35);
                    continue;
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
        return null;
    }

    private ClipboardEntry CaptureText(IDataObject data, ClipboardEntry entry)
    {
        string? text = data.GetData(DataFormats.UnicodeText) as string
                     ?? data.GetData(DataFormats.Text) as string;

        if (string.IsNullOrEmpty(text)) return entry;

        entry.ContentType = ClipboardContentType.Text;
        entry.PlainText = text;
        entry.ContentHash = ContentHasher.ComputeHash(text);
        entry.ContentSizeBytes = Encoding.UTF8.GetByteCount(text);
        return entry;
    }

    private ClipboardEntry CaptureRichText(IDataObject data, ClipboardEntry entry)
    {
        entry.ContentType = ClipboardContentType.RichText;
        entry.RichTextData = data.GetData(DataFormats.Rtf) as string;

        // Also capture plain text version for search and preview
        entry.PlainText = data.GetData(DataFormats.UnicodeText) as string
                        ?? data.GetData(DataFormats.Text) as string;

        entry.ContentHash = ContentHasher.ComputeHash(entry.PlainText ?? entry.RichTextData);
        entry.ContentSizeBytes = Encoding.UTF8.GetByteCount(entry.PlainText ?? entry.RichTextData ?? "");
        return entry;
    }

    private ClipboardEntry CaptureHtml(IDataObject data, ClipboardEntry entry)
    {
        entry.ContentType = ClipboardContentType.Html;
        entry.HtmlData = data.GetData(DataFormats.Html) as string;

        // Also capture plain text version
        entry.PlainText = data.GetData(DataFormats.UnicodeText) as string
                        ?? data.GetData(DataFormats.Text) as string;

        entry.ContentHash = ContentHasher.ComputeHash(entry.PlainText ?? entry.HtmlData);
        entry.ContentSizeBytes = Encoding.UTF8.GetByteCount(entry.PlainText ?? entry.HtmlData ?? "");
        return entry;
    }

    private ClipboardEntry? CaptureImage(IDataObject data, ClipboardEntry entry)
    {
        BitmapSource? bitmap = null;
        try
        {
            if (Clipboard.ContainsImage())
            {
                bitmap = Clipboard.GetImage();
            }
        }
        catch { }

        if (bitmap is null)
        {
            try
            {
                var raw = data.GetData(DataFormats.Bitmap);
                if (raw is BitmapSource bs)
                {
                    bitmap = bs;
                }
                else if (raw is System.Drawing.Bitmap gdi)
                {
                    using var ms = new MemoryStream();
                    gdi.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] bytes = ms.ToArray();
                    return CreateImageEntry(bytes, gdi.Width, gdi.Height, data, entry);
                }
            }
            catch { }
        }

        if (bitmap is null) return null;

        byte[] imageBytes = ImageHelper.EncodeToPng(bitmap);
        return CreateImageEntry(imageBytes, bitmap.PixelWidth, bitmap.PixelHeight, data, entry);
    }

    private ClipboardEntry? CreateImageEntry(byte[] imageBytes, int width, int height, IDataObject data, ClipboardEntry entry)
    {
        // Enforce size limit
        if (imageBytes.Length > _maxImageSizeKB * 1024)
            return null;

        entry.ContentType = ClipboardContentType.Image;
        entry.ImageData = imageBytes;
        entry.ContentHash = ContentHasher.ComputeHash(imageBytes);
        entry.ContentSizeBytes = imageBytes.Length;

        // Generate a descriptive, searchable name for the image
        string text = data.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(text))
        {
            entry.PlainText = text;
        }
        else
        {
            string app = !string.IsNullOrWhiteSpace(entry.SourceApplication) ? entry.SourceApplication : "Clipboard";
            entry.PlainText = $"{app} Image ({width} \u00d7 {height})";
        }

        return entry;
    }

    private ClipboardEntry? CaptureFiles(IDataObject data, ClipboardEntry entry)
    {
        string[]? files = null;
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                var dropList = Clipboard.GetFileDropList();
                if (dropList is not null && dropList.Count > 0)
                {
                    files = new string[dropList.Count];
                    dropList.CopyTo(files, 0);
                }
            }
        }
        catch { }

        if (files is null || files.Length == 0)
        {
            try
            {
                var raw = data.GetData(DataFormats.FileDrop);
                if (raw is string[] sArray)
                {
                    files = sArray;
                }
                else if (raw is StringCollection sc)
                {
                    files = new string[sc.Count];
                    sc.CopyTo(files, 0);
                }
                else if (raw is System.Collections.IEnumerable enumerable)
                {
                    var list = new System.Collections.Generic.List<string>();
                    foreach (var item in enumerable)
                    {
                        if (item is string s) list.Add(s);
                    }
                    if (list.Count > 0) files = list.ToArray();
                }
            }
            catch { }
        }

        if (files is null || files.Length == 0) return null;

        entry.ContentType = ClipboardContentType.Files;
        entry.FilePaths = JsonSerializer.Serialize(files);

        // Calculate total file size on disk for existing files
        long totalBytes = 0;
        foreach (var file in files)
        {
            try
            {
                if (File.Exists(file))
                    totalBytes += new FileInfo(file).Length;
            }
            catch { }
        }

        entry.ContentSizeBytes = totalBytes > 0 ? totalBytes : Encoding.UTF8.GetByteCount(entry.FilePaths);
        entry.PlainText = string.Join("\n", files.Select(f => Path.GetFileName(f)));
        entry.ContentHash = ContentHasher.ComputeHash(string.Join("\n", files));
        return entry;
    }

    /// <summary>
    /// Gets the process name of the foreground window (best-effort source app detection).
    /// </summary>
    private static string? GetForegroundProcessName()
    {
        try
        {
            IntPtr hwnd = Win32Interop.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            Win32Interop.GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0) return null;

            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }
}
