using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using UltimateClipboard.Models;

namespace UltimateClipboard.Services;

/// <summary>
/// Loads and saves application settings to a JSON file.
/// </summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _settingsPath;

    public AppSettings Settings { get; private set; } = new();

    public SettingsService(string? appDataDir = null)
    {
        string dir = appDataDir ?? GetDefaultAppDataDir();
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    /// <summary>
    /// Gets the default application data directory: %LOCALAPPDATA%\UltimateClipboard
    /// </summary>
    public static string GetDefaultAppDataDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UltimateClipboard");
    }

    /// <summary>
    /// Gets the database file path based on current settings.
    /// </summary>
    public string GetDatabasePath()
    {
        if (!string.IsNullOrEmpty(Settings.DatabasePath))
            return Settings.DatabasePath;

        return Path.Combine(GetDefaultAppDataDir(), "clipboard.db");
    }

    /// <summary>
    /// Loads settings from disk. Returns default settings if the file doesn't exist.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
        return Settings;
    }

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Settings save failure is non-fatal
        }
    }

    /// <summary>
    /// Checks if a process name is in the excluded apps list.
    /// </summary>
    public bool IsAppExcluded(string? processName)
    {
        if (string.IsNullOrEmpty(processName) || string.IsNullOrEmpty(Settings.ExcludedApps))
            return false;

        var excluded = Settings.ExcludedApps.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var app in excluded)
        {
            if (processName.Equals(app, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
