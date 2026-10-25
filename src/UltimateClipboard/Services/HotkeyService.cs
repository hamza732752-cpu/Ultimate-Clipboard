using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using UltimateClipboard.Helpers;

namespace UltimateClipboard.Services;

/// <summary>
/// Manages global keyboard shortcuts using RegisterHotKey.
/// Supports both Win+Shift+V and Ctrl+Shift+V simultaneously for maximum reliability.
/// </summary>
public class HotkeyService : IDisposable
{
    private const int HOTKEY_ID_WIN_SHIFT_V = 9001;
    private const int HOTKEY_ID_CTRL_SHIFT_V = 9002;
    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private bool _registeredWinShiftV;
    private bool _registeredCtrlShiftV;
    private bool _disposed;

    /// <summary>
    /// Fired when any registered global hotkey is pressed.
    /// </summary>
    public event EventHandler? HotkeyPressed;

    /// <summary>
    /// The currently registered hotkeys description.
    /// </summary>
    public string CurrentHotkey
    {
        get
        {
            if (_registeredWinShiftV && _registeredCtrlShiftV) return "Win+Shift+V, Ctrl+Shift+V";
            if (_registeredWinShiftV) return "Win+Shift+V";
            if (_registeredCtrlShiftV) return "Ctrl+Shift+V";
            return "None (conflict)";
        }
    }

    /// <summary>
    /// Registers global hotkeys using the window's HWND.
    /// </summary>
    public bool Register(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);

        // Register Win+Shift+V
        _registeredWinShiftV = Win32Interop.RegisterHotKey(_windowHandle, HOTKEY_ID_WIN_SHIFT_V,
            Win32Interop.MOD_WIN | Win32Interop.MOD_SHIFT | Win32Interop.MOD_NOREPEAT,
            Win32Interop.VK_V);

        // Also register Ctrl+Shift+V so either shortcut works
        _registeredCtrlShiftV = Win32Interop.RegisterHotKey(_windowHandle, HOTKEY_ID_CTRL_SHIFT_V,
            Win32Interop.MOD_CONTROL | Win32Interop.MOD_SHIFT | Win32Interop.MOD_NOREPEAT,
            Win32Interop.VK_V);

        return _registeredWinShiftV || _registeredCtrlShiftV;
    }

    /// <summary>
    /// Unregisters all global hotkeys.
    /// </summary>
    public void Unregister()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            if (_registeredWinShiftV)
            {
                Win32Interop.UnregisterHotKey(_windowHandle, HOTKEY_ID_WIN_SHIFT_V);
                _registeredWinShiftV = false;
            }
            if (_registeredCtrlShiftV)
            {
                Win32Interop.UnregisterHotKey(_windowHandle, HOTKEY_ID_CTRL_SHIFT_V);
                _registeredCtrlShiftV = false;
            }
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Interop.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_ID_WIN_SHIFT_V || id == HOTKEY_ID_CTRL_SHIFT_V)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unregister();
        _hwndSource?.RemoveHook(WndProc);
        GC.SuppressFinalize(this);
    }
}
