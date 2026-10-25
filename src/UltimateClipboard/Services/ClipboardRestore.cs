using System;
using System.Collections.Specialized;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using UltimateClipboard.Helpers;
using UltimateClipboard.Models;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using Clipboard = System.Windows.Clipboard;

namespace UltimateClipboard.Services;

/// <summary>
/// Restores clipboard entries back onto the Windows clipboard.
/// Must be called from the UI (STA) thread.
/// </summary>
public class ClipboardRestore
{
    /// <summary>
    /// Places the given clipboard entry back onto the Windows clipboard.
    /// Returns true if successful.
    /// </summary>
    public bool RestoreToClipboard(ClipboardEntry entry)
    {
        try
        {
            var dataObject = new DataObject();

            switch (entry.ContentType)
            {
                case ClipboardContentType.Text:
                    if (entry.PlainText is not null)
                        dataObject.SetText(entry.PlainText);
                    break;

                case ClipboardContentType.RichText:
                    if (entry.RichTextData is not null)
                        dataObject.SetData(DataFormats.Rtf, entry.RichTextData);
                    if (entry.PlainText is not null)
                        dataObject.SetText(entry.PlainText);
                    break;

                case ClipboardContentType.Html:
                    if (entry.HtmlData is not null)
                        dataObject.SetData(DataFormats.Html, entry.HtmlData);
                    if (entry.PlainText is not null)
                        dataObject.SetText(entry.PlainText);
                    break;

                case ClipboardContentType.Image:
                    if (entry.ImageData is not null)
                    {
                        var bitmapImage = ImageHelper.BytesToBitmapImage(entry.ImageData);
                        if (bitmapImage is not null)
                            dataObject.SetImage(bitmapImage);
                    }
                    break;

                case ClipboardContentType.Files:
                    if (entry.FilePaths is not null)
                    {
                        var paths = JsonSerializer.Deserialize<string[]>(entry.FilePaths);
                        if (paths is not null)
                        {
                            var collection = new StringCollection();
                            collection.AddRange(paths);
                            dataObject.SetFileDropList(collection);
                        }
                    }
                    break;

                default:
                    return false;
            }

            // Retry up to 5 times in case another application temporarily locked the clipboard
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(dataObject, true);
                    return true;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    System.Threading.Thread.Sleep(40);
                }
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
