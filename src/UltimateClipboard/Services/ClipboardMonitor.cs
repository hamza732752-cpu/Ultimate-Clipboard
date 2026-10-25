using System;
using System.Windows;
using System.Windows.Interop;
using UltimateClipboard.Helpers;

namespace UltimateClipboard.Services;

/// <summary>
/// Monitors the Windows clipboard for changes using AddClipboardFormatListener.
/// Must be created on the UI thread and attached to a WPF window.
/// </summary>
public class ClipboardMonitor : IDisposable
{
    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private bool _isListening;
    private bool _disposed;

    /// <summary>
    /// Fired when the clipboard content changes.
    /// </summary>
    public event EventHandler? ClipboardChanged;

    /// <summary>
    /// Whether monitoring is currently active.
    /// </summary>
    public bool IsMonitoring => _isListening;

    /// <summary>
    /// Starts listening for clipboard changes by hooking into the specified window.
    /// </summary>
    public void Start(Window window)
    {
        if (_isListening) return;

        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);

        if (Win32Interop.AddClipboardFormatListener(_windowHandle))
        {
            _isListening = true;
        }
    }

    /// <summary>
    /// Stops listening for clipboard changes.
    /// </summary>
    public void Stop()
    {
        if (!_isListening) return;

        Win32Interop.RemoveClipboardFormatListener(_windowHandle);
        _hwndSource?.RemoveHook(WndProc);
        _isListening = false;
    }

    /// <summary>
    /// Pauses monitoring without unregistering (for temporary pause/resume).
    /// </summary>
    public bool IsPaused { get; set; }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Interop.WM_CLIPBOARDUPDATE && !IsPaused)
        {
            ClipboardChanged?.Invoke(this, EventArgs.Empty);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }
}
