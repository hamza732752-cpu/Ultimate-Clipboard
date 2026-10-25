using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using UltimateClipboard.Data;
using UltimateClipboard.Models;
using UltimateClipboard.Services;

namespace UltimateClipboard.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly ClipboardDatabase _database;
    private readonly UpdateService _updateService;

    private string _theme;
    public string Theme
    {
        get => _theme;
        set
        {
            if (SetProperty(ref _theme, value))
            {
                App.Instance.ApplyTheme(value);
            }
        }
    }

    private int _maxHistoryEntries;
    public int MaxHistoryEntries
    {
        get => _maxHistoryEntries;
        set => SetProperty(ref _maxHistoryEntries, value);
    }

    private int _retentionDays;
    public int RetentionDays
    {
        get => _retentionDays;
        set => SetProperty(ref _retentionDays, value);
    }

    private int _maxImageSizeKB;
    public int MaxImageSizeKB
    {
        get => _maxImageSizeKB;
        set => SetProperty(ref _maxImageSizeKB, value);
    }

    private bool _startMinimized;
    public bool StartMinimized
    {
        get => _startMinimized;
        set => SetProperty(ref _startMinimized, value);
    }

    private bool _launchAtStartup;
    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set => SetProperty(ref _launchAtStartup, value);
    }

    private string _excludedApps;
    public string ExcludedApps
    {
        get => _excludedApps;
        set => SetProperty(ref _excludedApps, value);
    }

    private string _databasePath;
    public string DatabasePath
    {
        get => _databasePath;
        set => SetProperty(ref _databasePath, value);
    }

    private bool _autoCheckUpdates;
    public bool AutoCheckUpdates
    {
        get => _autoCheckUpdates;
        set => SetProperty(ref _autoCheckUpdates, value);
    }

    private bool _enableAiImageRecognition;
    public bool EnableAiImageRecognition
    {
        get => _enableAiImageRecognition;
        set => SetProperty(ref _enableAiImageRecognition, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string CurrentVersion => _updateService.CurrentVersion;

    private string _updateStatus = string.Empty;
    public string UpdateStatus
    {
        get => _updateStatus;
        set => SetProperty(ref _updateStatus, value);
    }

    private bool _isCheckingForUpdate;
    public bool IsCheckingForUpdate
    {
        get => _isCheckingForUpdate;
        set => SetProperty(ref _isCheckingForUpdate, value);
    }

    private bool _isUpdateAvailable;
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => SetProperty(ref _isUpdateAvailable, value);
    }

    private string _downloadUrl = string.Empty;
    public string DownloadUrl
    {
        get => _downloadUrl;
        set => SetProperty(ref _downloadUrl, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand ResetDefaultsCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand DownloadUpdateCommand { get; }

    public SettingsViewModel(SettingsService settingsService, ClipboardDatabase database, UpdateService? updateService = null)
    {
        _settingsService = settingsService;
        _database = database;
        _updateService = updateService ?? new UpdateService();

        var s = _settingsService.Settings;
        _theme = s.Theme;
        _maxHistoryEntries = s.MaxHistoryEntries;
        _retentionDays = s.RetentionDays;
        _maxImageSizeKB = s.MaxImageSizeKB;
        _startMinimized = s.StartMinimized;
        _launchAtStartup = s.LaunchAtStartup;
        _excludedApps = s.ExcludedApps;
        _databasePath = s.DatabasePath ?? _settingsService.GetDatabasePath();
        _autoCheckUpdates = s.AutoCheckUpdates;
        _enableAiImageRecognition = s.EnableAiImageRecognition;

        SaveCommand = new RelayCommand(Save);
        ResetDefaultsCommand = new RelayCommand(ResetDefaults);
        ClearHistoryCommand = new AsyncRelayCommand(async () =>
        {
            await _database.ClearHistoryAsync();
            StatusMessage = "History cleared successfully.";
        });

        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync);
        DownloadUpdateCommand = new RelayCommand(() => _updateService.OpenReleaseUrl(DownloadUrl));
    }

    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdate = true;
        UpdateStatus = "Checking for updates...";

        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            IsUpdateAvailable = result.IsUpdateAvailable;
            DownloadUrl = result.DownloadUrl;
            UpdateStatus = result.StatusMessage;

            _settingsService.Settings.LastUpdateCheck = DateTime.UtcNow;
            _settingsService.Save();
        }
        finally
        {
            IsCheckingForUpdate = false;
        }
    }

    private void Save()
    {
        var s = _settingsService.Settings;
        s.Theme = Theme;
        s.MaxHistoryEntries = MaxHistoryEntries;
        s.RetentionDays = RetentionDays;
        s.MaxImageSizeKB = MaxImageSizeKB;
        s.StartMinimized = StartMinimized;
        s.LaunchAtStartup = LaunchAtStartup;
        s.ExcludedApps = ExcludedApps;
        s.DatabasePath = string.IsNullOrWhiteSpace(DatabasePath) ? null : DatabasePath;
        s.AutoCheckUpdates = AutoCheckUpdates;
        s.EnableAiImageRecognition = EnableAiImageRecognition;

        _settingsService.Save();
        StatusMessage = "Settings saved successfully.";
    }

    private void ResetDefaults()
    {
        var d = new AppSettings();
        Theme = d.Theme;
        MaxHistoryEntries = d.MaxHistoryEntries;
        RetentionDays = d.RetentionDays;
        MaxImageSizeKB = d.MaxImageSizeKB;
        StartMinimized = d.StartMinimized;
        LaunchAtStartup = d.LaunchAtStartup;
        ExcludedApps = d.ExcludedApps;
        DatabasePath = _settingsService.GetDatabasePath();
        AutoCheckUpdates = d.AutoCheckUpdates;
        EnableAiImageRecognition = d.EnableAiImageRecognition;

        Save();
        StatusMessage = "Reset to default settings.";
    }
}
