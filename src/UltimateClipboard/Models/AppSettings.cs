namespace UltimateClipboard.Models;

public class AppSettings
{
    public string Theme { get; set; } = "System"; // "Dark", "Light", "System"
    public string GlobalHotkey { get; set; } = "Win+Shift+V";
    public int MaxHistoryEntries { get; set; } = 5000;
    public int RetentionDays { get; set; } = 90;
    public int MaxImageSizeKB { get; set; } = 5120; // 5 MB
    public bool StartMinimized { get; set; } = true;
    public bool LaunchAtStartup { get; set; } = false; // never enabled without user approval
    public bool MonitoringEnabled { get; set; } = true;
    public string ExcludedApps { get; set; } = string.Empty; // semicolon-separated process names
    public string? DatabasePath { get; set; } // null = default %LOCALAPPDATA%\UltimateClipboard
    public double PanelWidth { get; set; } = 420;
    public double PanelHeight { get; set; } = 580;
    public bool AutoCheckUpdates { get; set; } = true;
    public bool EnableAiImageRecognition { get; set; } = true;
    public DateTime? LastUpdateCheck { get; set; }
}
