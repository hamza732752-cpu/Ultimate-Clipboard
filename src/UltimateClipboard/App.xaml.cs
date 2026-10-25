using System;
using System.IO;
using System.Threading;
using System.Windows;
using UltimateClipboard.Data;
using UltimateClipboard.Helpers;
using UltimateClipboard.Models;
using UltimateClipboard.Services;
using UltimateClipboard.ViewModels;
using UltimateClipboard.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace UltimateClipboard;

public partial class App : Application
{
    private const string MutexName = @"Local\UltimateClipboard_SingleInstance_Mutex";
    private const string EventName = @"Local\UltimateClipboard_Wakeup_Event";
    private static Mutex? _singleInstanceMutex;
    private static EventWaitHandle? _wakeupEvent;
    private static RegisteredWaitHandle? _registeredWaitHandle;

    private SettingsService _settingsService = null!;
    private ClipboardDatabase _database = null!;
    private ClipboardCapture _capture = null!;
    private ClipboardRestore _restore = null!;
    private MainViewModel _mainViewModel = null!;
    private ClipboardPanel _clipboardPanel = null!;
    private ClipboardMonitor _monitor = null!;
    private HotkeyService _hotkeyService = null!;
    private TrayIconService _trayIconService = null!;
    private UpdateService _updateService = null!;
    private ImageRecognitionService _imageRecognitionService = null!;
    private SettingsWindow? _settingsWindow;

    public static App Instance => (App)Current;
    public SettingsService SettingsService => _settingsService;
    public ClipboardDatabase Database => _database;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance enforcement: if another instance is already running, activate it and exit immediately
        bool createdNew;
        try
        {
            _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);
        }
        catch (AbandonedMutexException)
        {
            createdNew = true;
        }
        catch
        {
            createdNew = true;
        }

        if (!createdNew)
        {
            try
            {
                using var existingEvent = EventWaitHandle.OpenExisting(EventName);
                existingEvent.Set();
            }
            catch { }

            Shutdown(0);
            return;
        }

        // We are the primary instance. Create the wakeup event so future instances can signal us
        try
        {
            _wakeupEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            _registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                _wakeupEvent,
                (state, timedOut) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        PositionPanel();
                        _clipboardPanel.ShowAndActivate();
                    });
                },
                null,
                -1,
                false);
        }
        catch { }

        try
        {
            // 1. Settings
            _settingsService = new SettingsService();
            _settingsService.Load();

            // 2. Database
            string dbPath = _settingsService.GetDatabasePath();
            _database = new ClipboardDatabase(dbPath);

            // 3. Apply retention policy at startup
            await _database.EnforceRetentionAsync(_settingsService.Settings.RetentionDays);
            await _database.EnforceHistoryLimitAsync(_settingsService.Settings.MaxHistoryEntries);

            // 4. Capture, Restore, AI and Update services
            _capture = new ClipboardCapture(_settingsService.Settings.MaxImageSizeKB);
            _restore = new ClipboardRestore();
            _imageRecognitionService = new ImageRecognitionService();
            _updateService = new UpdateService();

            // 5. Main ViewModel & Panel View
            _mainViewModel = new MainViewModel(_database, _capture, _restore, _settingsService, _imageRecognitionService);
            _clipboardPanel = new ClipboardPanel(_mainViewModel);
            _clipboardPanel.SettingsRequested += (s, ev) => ShowSettingsWindow();

            // 6. Force HWND creation immediately so monitor & hotkey register even if starting minimized
            var helper = new System.Windows.Interop.WindowInteropHelper(_clipboardPanel);
            helper.EnsureHandle();

            _monitor = new ClipboardMonitor();
            _monitor.ClipboardChanged += async (s, ev) =>
            {
                await _mainViewModel.OnClipboardChangedAsync();
            };
            _monitor.Start(_clipboardPanel);

            _hotkeyService = new HotkeyService();
            _hotkeyService.HotkeyPressed += (s, ev) =>
            {
                TogglePanel();
            };
            _hotkeyService.Register(_clipboardPanel);

            // 7. System Tray Icon
            _trayIconService = new TrayIconService();
            _trayIconService.Initialize();
            _trayIconService.ShowPanelRequested += (s, ev) => Dispatcher.Invoke(TogglePanel);
            _trayIconService.PauseResumeRequested += (s, ev) => Dispatcher.Invoke(TogglePauseResume);
            _trayIconService.ClearHistoryRequested += (s, ev) => Dispatcher.Invoke(ConfirmAndClearHistory);
            _trayIconService.SettingsRequested += (s, ev) => Dispatcher.Invoke(ShowSettingsWindow);
            _trayIconService.CheckUpdatesRequested += (s, ev) => Dispatcher.Invoke(CheckForUpdatesFromTray);
            _trayIconService.ExitRequested += (s, ev) => Dispatcher.Invoke(ShutdownApp);

            // 8. Apply Theme
            ApplyTheme(_settingsService.Settings.Theme);

            // 9. Initial history load
            await _mainViewModel.InitializeAsync();

            // 10. Background update check if enabled
            if (_settingsService.Settings.AutoCheckUpdates)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await _updateService.CheckForUpdatesAsync();
                        if (result.IsUpdateAvailable)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                _trayIconService.UpdateTooltip($"Ultimate Clipboard (Update v{result.LatestVersion} available!)");
                            });
                        }
                    }
                    catch { }
                });
            }

            // 11. Start state
            if (!_settingsService.Settings.StartMinimized)
            {
                _clipboardPanel.ShowAndActivate();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup error: {ex.Message}", "Ultimate Clipboard", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void TogglePanel()
    {
        if (_clipboardPanel.IsVisible)
        {
            _clipboardPanel.Hide();
        }
        else
        {
            PositionPanel();
            _clipboardPanel.ShowAndActivate();
        }
    }

    private void PositionPanel()
    {
        try
        {
            var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var workingArea = screen.WorkingArea;

            // Center horizontally and vertically on the active screen
            _clipboardPanel.Left = workingArea.Left + (workingArea.Width - _clipboardPanel.Width) / 2;
            _clipboardPanel.Top = workingArea.Top + (workingArea.Height - _clipboardPanel.Height) / 2;
        }
        catch
        {
            _clipboardPanel.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public void TogglePauseResume()
    {
        bool newPaused = !_monitor.IsPaused;
        _monitor.IsPaused = newPaused;
        _trayIconService.IsPaused = newPaused;
        _mainViewModel.IsMonitoringPaused = newPaused;
    }

    public async void ConfirmAndClearHistory()
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear all unpinned clipboard history? This cannot be undone.",
            "Clear History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            await _database.ClearHistoryAsync();
            await _mainViewModel.RefreshAsync();
        }
    }

    public void ShowSettingsWindow()
    {
        if (_settingsWindow is null || !_settingsWindow.IsLoaded)
        {
            var vm = new SettingsViewModel(_settingsService, _database, _updateService);
            _settingsWindow = new SettingsWindow(vm);
            _settingsWindow.Closed += (s, ev) => _settingsWindow = null;
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void CheckForUpdatesFromTray()
    {
        ShowSettingsWindow();
        if (_settingsWindow?.DataContext is SettingsViewModel vm)
        {
            vm.CheckForUpdatesCommand.Execute(null);
        }
    }

    public void ApplyTheme(string theme)
    {
        var themeUri = theme.Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? new Uri("Themes/LightTheme.xaml", UriKind.Relative)
            : new Uri("Themes/DarkTheme.xaml", UriKind.Relative);

        var dict = new ResourceDictionary { Source = themeUri };
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(dict);
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("Themes/SharedStyles.xaml", UriKind.Relative) });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _registeredWaitHandle?.Unregister(null);
            _wakeupEvent?.Dispose();
            if (_singleInstanceMutex is not null)
            {
                try { _singleInstanceMutex.ReleaseMutex(); } catch { }
                _singleInstanceMutex.Dispose();
            }
        }
        catch { }

        _trayIconService?.Dispose();
        _hotkeyService?.Dispose();
        _monitor?.Dispose();
        _imageRecognitionService?.Dispose();
        _database?.Dispose();
        base.OnExit(e);
    }

    public void ShutdownApp()
    {
        Shutdown();
    }
}
