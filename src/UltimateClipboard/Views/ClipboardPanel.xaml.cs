using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using UltimateClipboard.Helpers;
using UltimateClipboard.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace UltimateClipboard.Views;

public partial class ClipboardPanel : Window
{
    private readonly MainViewModel _viewModel;
    public event EventHandler? SettingsRequested;

    public ClipboardPanel(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // Attach smooth scrolling handler
        EntriesList.AddHandler(
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(EntriesList_PreviewMouseWheel),
            true);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FocusSearchBox();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        // Hide panel when user clicks away
        Hide();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            FocusSearchBox();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (_viewModel.SelectedEntry is not null)
            {
                _viewModel.SelectAndCopyCommand.Execute(_viewModel.SelectedEntry);
                Hide();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Down && SearchBox.IsFocused)
        {
            if (EntriesList.Items.Count > 0)
            {
                if (EntriesList.SelectedIndex < 0)
                    EntriesList.SelectedIndex = 0;
                var item = (ListBoxItem)EntriesList.ItemContainerGenerator.ContainerFromIndex(EntriesList.SelectedIndex);
                item?.Focus();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Delete && !SearchBox.IsFocused)
        {
            if (_viewModel.SelectedEntry is not null)
            {
                _viewModel.DeleteEntryCommand.Execute(_viewModel.SelectedEntry);
                e.Handled = true;
            }
            return;
        }
    }

    public void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    public void ShowAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            Win32Interop.SetForegroundWindow(helper.Handle);
        }
        FocusSearchBox();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void EntriesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedEntry is not null)
        {
            _viewModel.SelectAndCopyCommand.Execute(_viewModel.SelectedEntry);
            Hide();
        }
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear all unpinned clipboard history?\nPinned items will be preserved.",
            "Clear History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.ClearHistoryCommand.Execute(null);
        }
    }

    private double _targetVerticalOffset = -1;

    /// <summary>
    /// Smooth animated scrolling on mouse wheel.
    /// </summary>
    private void EntriesList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = GetScrollViewer(EntriesList);
        if (scrollViewer is null) return;

        double baseOffset = _targetVerticalOffset >= 0 ? _targetVerticalOffset : scrollViewer.VerticalOffset;
        double targetOffset = baseOffset - (e.Delta * 0.85);
        targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.ScrollableHeight));
        _targetVerticalOffset = targetOffset;

        var animation = new DoubleAnimation
        {
            To = targetOffset,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };

        animation.Completed += (s, ev) =>
        {
            _targetVerticalOffset = -1;
        };

        scrollViewer.BeginAnimation(ScrollViewerHelper.VerticalOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
        e.Handled = true;
    }

    private static ScrollViewer? GetScrollViewer(DependencyObject dep)
    {
        if (dep is ScrollViewer sv) return sv;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
        {
            var child = VisualTreeHelper.GetChild(dep, i);
            var result = GetScrollViewer(child);
            if (result is not null) return result;
        }
        return null;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
