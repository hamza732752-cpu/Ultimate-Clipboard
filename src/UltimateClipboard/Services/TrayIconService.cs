using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace UltimateClipboard.Services;

/// <summary>
/// Manages the system tray (notification area) icon and context menu.
/// </summary>
public class TrayIconService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private bool _disposed;

    public event EventHandler? ShowPanelRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? CheckUpdatesRequested;
    public event EventHandler? PauseResumeRequested;
    public event EventHandler? ClearHistoryRequested;
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Whether monitoring is currently paused (affects the menu text).
    /// </summary>
    public bool IsPaused { get; set; }

    public void Initialize()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "Ultimate Clipboard",
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) => ShowPanelRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.ContextMenuStrip = CreateContextMenu();
    }

    public void UpdateTooltip(string text)
    {
        if (_notifyIcon is not null)
        {
            // NotifyIcon.Text has a 128 char limit
            _notifyIcon.Text = text.Length > 127 ? text[..127] : text;
        }
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("Show Clipboard Panel");
        showItem.Click += (s, e) => ShowPanelRequested?.Invoke(this, EventArgs.Empty);
        showItem.Font = new Font(showItem.Font, FontStyle.Bold);
        menu.Items.Add(showItem);

        menu.Items.Add(new ToolStripSeparator());

        var pauseItem = new ToolStripMenuItem(IsPaused ? "Resume Monitoring" : "Pause Monitoring");
        pauseItem.Click += (s, e) =>
        {
            PauseResumeRequested?.Invoke(this, EventArgs.Empty);
            pauseItem.Text = IsPaused ? "Resume Monitoring" : "Pause Monitoring";
        };
        menu.Items.Add(pauseItem);

        var settingsItem = new ToolStripMenuItem("Settings");
        settingsItem.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(settingsItem);

        var updateItem = new ToolStripMenuItem("Check for Updates...");
        updateItem.Click += (s, e) => CheckUpdatesRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(updateItem);

        menu.Items.Add(new ToolStripSeparator());

        var clearItem = new ToolStripMenuItem("Clear History");
        clearItem.Click += (s, e) => ClearHistoryRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(clearItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitItem);

        return menu;
    }

    /// <summary>
    /// Generates a simple clipboard icon programmatically.
    /// </summary>
    private static Icon CreateDefaultIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);

            // Clipboard body
            using var bodyBrush = new SolidBrush(Color.FromArgb(100, 149, 237)); // Cornflower blue
            using var bodyPen = new Pen(Color.FromArgb(70, 110, 200), 1.5f);
            g.FillRectangle(bodyBrush, 5, 6, 22, 24);
            g.DrawRectangle(bodyPen, 5, 6, 22, 24);

            // Clipboard clip
            using var clipBrush = new SolidBrush(Color.FromArgb(70, 110, 200));
            g.FillRectangle(clipBrush, 10, 2, 12, 8);

            // Lines on clipboard
            using var linePen = new Pen(Color.White, 1.5f);
            g.DrawLine(linePen, 9, 16, 23, 16);
            g.DrawLine(linePen, 9, 20, 23, 20);
            g.DrawLine(linePen, 9, 24, 18, 24);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
