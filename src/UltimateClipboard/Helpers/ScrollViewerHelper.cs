using System.Windows;
using System.Windows.Controls;

namespace UltimateClipboard.Helpers;

public static class ScrollViewerHelper
{
    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.RegisterAttached(
            "VerticalOffset",
            typeof(double),
            typeof(ScrollViewerHelper),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVerticalOffsetChanged));

    public static double GetVerticalOffset(DependencyObject target) =>
        (double)target.GetValue(VerticalOffsetProperty);

    public static void SetVerticalOffset(DependencyObject target, double value) =>
        target.SetValue(VerticalOffsetProperty, value);

    private static void OnVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
        }
    }
}
