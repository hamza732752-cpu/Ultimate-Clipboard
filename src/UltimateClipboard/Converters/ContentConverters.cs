using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UltimateClipboard.Helpers;
using UltimateClipboard.Models;

namespace UltimateClipboard.Converters;

/// <summary>
/// Converts a ClipboardContentType to a corresponding icon/symbol string.
/// </summary>
public class ContentTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardContentType type)
        {
            return type switch
            {
                ClipboardContentType.Text => "\uE8C1",       // Document icon
                ClipboardContentType.RichText => "\uE8AB",    // Font icon
                ClipboardContentType.Html => "\uE774",        // Globe icon
                ClipboardContentType.Image => "\uEB9F",       // Image icon
                ClipboardContentType.Files => "\uE8B7",       // Folder icon
                _ => "\uE7C3"                                  // Unknown
            };
        }
        return "\uE7C3";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts byte[] thumbnail data to a BitmapImage for display.
/// </summary>
public class BytesToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is byte[] data && data.Length > 0)
        {
            return ImageHelper.BytesToBitmapImage(data);
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a boolean to Visibility (true = Visible, false = Collapsed).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value switch
        {
            bool b => b,
            int i => i > 0,
            long l => l > 0,
            byte[] bytes => bytes.Length > 0,
            string s => !string.IsNullOrWhiteSpace(s),
            null => false,
            _ => true
        };
        bool invert = parameter is string sParam && sParam.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        return (boolValue ^ invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts DateTime to a human-readable relative time string.
/// </summary>
public class RelativeTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
        {
            var span = DateTime.UtcNow - dt;
            if (span.TotalSeconds < 60) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return dt.ToLocalTime().ToString("MMM d, yyyy");
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts content size in bytes to a human-readable string.
/// </summary>
public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts pinned/favorite state to a star/pin symbol.
/// </summary>
public class BoolToSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;
        string type = parameter as string ?? "pin";

        return type.ToLowerInvariant() switch
        {
            "pin" => boolValue ? "\uE840" : "\uE718",       // Pinned vs Unpin
            "fav" => boolValue ? "\uE735" : "\uE734",       // Filled star vs Empty star
            _ => ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a hex color string (e.g. #FF5500 or #1E90FF) to a SolidColorBrush.
/// </summary>
public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex.Trim());
                return new SolidColorBrush(color);
            }
            catch { }
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts 0-based AlternationIndex (0..8) to quick shortcut number string ("1".."9").
/// </summary>
public class AlternationIndexToShortcutConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && index >= 0 && index < 9)
        {
            return (index + 1).ToString();
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns Visible for top 9 items (index 0..8), Collapsed otherwise.
/// </summary>
public class AlternationIndexToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && index >= 0 && index < 9)
        {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
