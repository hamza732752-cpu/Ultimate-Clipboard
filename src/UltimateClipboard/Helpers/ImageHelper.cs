using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UltimateClipboard.Helpers;

/// <summary>
/// Helper for encoding images to PNG byte arrays and generating thumbnails.
/// </summary>
public static class ImageHelper
{
    private const int ThumbnailMaxWidth = 200;
    private const int ThumbnailMaxHeight = 150;

    /// <summary>
    /// Encodes a BitmapSource to a PNG byte array.
    /// </summary>
    public static byte[] EncodeToPng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Creates a thumbnail from PNG image data.
    /// </summary>
    public static Task<byte[]?> CreateThumbnailAsync(byte[] imageData)
    {
        return Task.Run(() =>
        {
            try
            {
                using var stream = new MemoryStream(imageData);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];

                double scaleX = (double)ThumbnailMaxWidth / frame.PixelWidth;
                double scaleY = (double)ThumbnailMaxHeight / frame.PixelHeight;
                double scale = Math.Min(scaleX, scaleY);

                if (scale >= 1.0)
                    return imageData; // already small enough

                var scaled = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
                return EncodeToPng(scaled);
            }
            catch
            {
                return null;
            }
        });
    }

    /// <summary>
    /// Creates a BitmapImage from a byte array for display in WPF.
    /// </summary>
    public static BitmapImage? BytesToBitmapImage(byte[]? data)
    {
        if (data is null || data.Length == 0)
            return null;

        try
        {
            var image = new BitmapImage();
            using var stream = new MemoryStream(data);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
