using System;
using System.IO;
using UltimateClipboard.Models;
using UltimateClipboard.Services;
using Xunit;

namespace UltimateClipboard.Tests;

public class SettingsTests : IDisposable
{
    private readonly string _testDir;
    private readonly SettingsService _settingsService;

    public SettingsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"uc_settings_test_{Guid.NewGuid():N}");
        _settingsService = new SettingsService(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch { }
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var settings = _settingsService.Load();
        Assert.NotNull(settings);
        Assert.Equal("System", settings.Theme);
        Assert.Equal(5000, settings.MaxHistoryEntries);
        Assert.Equal(90, settings.RetentionDays);
        Assert.False(settings.LaunchAtStartup); // must be disabled by default
        Assert.True(settings.StartMinimized);
    }

    [Fact]
    public void SaveAndLoad_PersistsValues()
    {
        _settingsService.Settings.Theme = "Dark";
        _settingsService.Settings.MaxHistoryEntries = 2500;
        _settingsService.Settings.RetentionDays = 30;
        _settingsService.Settings.ExcludedApps = "keepassxc;bitwarden";
        _settingsService.Save();

        var reloaded = new SettingsService(_testDir);
        var loaded = reloaded.Load();

        Assert.Equal("Dark", loaded.Theme);
        Assert.Equal(2500, loaded.MaxHistoryEntries);
        Assert.Equal(30, loaded.RetentionDays);
        Assert.Equal("keepassxc;bitwarden", loaded.ExcludedApps);
    }

    [Fact]
    public void IsAppExcluded_MatchesExcludedProcessesCaseInsensitively()
    {
        _settingsService.Settings.ExcludedApps = "KeePassXC;1Password;mstsc.exe";

        Assert.True(_settingsService.IsAppExcluded("keepassxc"));
        Assert.True(_settingsService.IsAppExcluded("KEEPASSXC"));
        Assert.True(_settingsService.IsAppExcluded("1password"));
        Assert.True(_settingsService.IsAppExcluded("mstsc.exe"));

        Assert.False(_settingsService.IsAppExcluded("chrome"));
        Assert.False(_settingsService.IsAppExcluded("notepad"));
        Assert.False(_settingsService.IsAppExcluded(null));
        Assert.False(_settingsService.IsAppExcluded(""));
    }
}
