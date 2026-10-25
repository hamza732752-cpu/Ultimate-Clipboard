using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using UltimateClipboard.Data;
using UltimateClipboard.Models;
using UltimateClipboard.Services;
using Application = System.Windows.Application;

namespace UltimateClipboard.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ClipboardDatabase _database;
    private readonly ClipboardCapture _capture;
    private readonly ClipboardRestore _restore;
    private readonly SettingsService _settingsService;
    private readonly ImageRecognitionService _imageRecognitionService;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<ClipboardEntry> Entries { get; } = new();

    private ClipboardEntry? _selectedEntry;
    public ClipboardEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => SetProperty(ref _selectedEntry, value);
    }

    private int _selectedIndex = -1;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, value);
    }

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                _ = SearchAsync();
            }
        }
    }

    private string _activeFilter = "All";
    public string ActiveFilter
    {
        get => _activeFilter;
        set
        {
            if (SetProperty(ref _activeFilter, value))
            {
                _ = RefreshAsync();
            }
        }
    }

    private bool _isMonitoringPaused;
    public bool IsMonitoringPaused
    {
        get => _isMonitoringPaused;
        set
        {
            if (SetProperty(ref _isMonitoringPaused, value))
            {
                OnPropertyChanged(nameof(MonitoringStatusText));
            }
        }
    }

    public string MonitoringStatusText => IsMonitoringPaused ? "Monitoring Paused" : "Monitoring Active";

    public bool IsEmpty => Entries.Count == 0;

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private long _entryCount;
    public long EntryCount
    {
        get => _entryCount;
        set
        {
            if (SetProperty(ref _entryCount, value))
            {
                StatusText = $"{value} items";
            }
        }
    }

    // Commands
    public ICommand SelectAndCopyCommand { get; }
    public ICommand DeleteEntryCommand { get; }
    public ICommand TogglePinCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SetFilterCommand { get; }
    public ICommand OpenInExplorerCommand { get; }

    public MainViewModel(ClipboardDatabase database, ClipboardCapture capture, ClipboardRestore restore, SettingsService settingsService, ImageRecognitionService? imageRecognitionService = null)
    {
        _database = database;
        _capture = capture;
        _restore = restore;
        _settingsService = settingsService;
        _imageRecognitionService = imageRecognitionService ?? new ImageRecognitionService();

        SelectAndCopyCommand = new AsyncRelayCommand(SelectAndCopyAsync);
        DeleteEntryCommand = new AsyncRelayCommand(DeleteEntryAsync);
        TogglePinCommand = new AsyncRelayCommand(TogglePinAsync);
        ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync);
        ClearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        SetFilterCommand = new RelayCommand(p => ActiveFilter = p as string ?? "All");
        OpenInExplorerCommand = new RelayCommand(OpenInExplorer);
    }

    /// <summary>
    /// Loads the initial history from the database.
    /// </summary>
    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    /// <summary>
    /// Called by the clipboard monitor when new content is detected.
    /// </summary>
    public async Task OnClipboardChangedAsync()
    {
        if (IsMonitoringPaused) return;

        var entry = _capture.CaptureCurrentClipboard();
        if (entry is null) return;

        // Check excluded apps
        if (_settingsService.IsAppExcluded(entry.SourceApplication))
            return;

        // Skip empty content
        if (string.IsNullOrEmpty(entry.ContentHash))
            return;

        // Generate thumbnail for images and analyze visual content with on-device AI & OCR
        if (entry.ContentType == ClipboardContentType.Image && entry.ImageData is not null)
        {
            entry.ThumbnailData = await Helpers.ImageHelper.CreateThumbnailAsync(entry.ImageData);

            if (_settingsService.Settings.EnableAiImageRecognition)
            {
                var analysis = await _imageRecognitionService.AnalyzeImageAsync(entry.ImageData);
                if (analysis.AllSearchTags.Count > 0)
                {
                    entry.Tags = string.Join(", ", analysis.AllSearchTags);
                }

                string baseTitle = !string.IsNullOrWhiteSpace(entry.PlainText) ? entry.PlainText : "Image";
                if (!string.IsNullOrWhiteSpace(analysis.DisplaySummary))
                {
                    entry.PlainText = $"{baseTitle} — {analysis.DisplaySummary}";
                }

                if (!string.IsNullOrWhiteSpace(analysis.OcrText))
                {
                    entry.PlainText = $"{entry.PlainText}\n{analysis.OcrText}";
                }
            }
        }

        // Process file entries: extract tags, thumbnail for image files, and snippet for text files
        if (entry.ContentType == ClipboardContentType.Files)
        {
            await ProcessFilesEntryAsync(entry);
        }

        // Insert (handles duplicate detection internally)
        entry.Id = await _database.InsertEntryAsync(entry);

        // Enforce limits
        await _database.EnforceHistoryLimitAsync(_settingsService.Settings.MaxHistoryEntries);

        // Refresh the list
        await RefreshAsync();
    }

    /// <summary>
    /// Refreshes the entries list from the database.
    /// </summary>
    public async Task RefreshAsync()
    {
        ClipboardContentType? typeFilter = _activeFilter switch
        {
            "Text" => ClipboardContentType.Text,
            "Images" => ClipboardContentType.Image,
            "Files" => ClipboardContentType.Files,
            _ => null
        };

        bool? pinnedOnly = _activeFilter == "Pinned" ? true : null;
        bool? favoritesOnly = _activeFilter == "Favorites" ? true : null;

        var entries = await _database.SearchEntriesAsync(
            query: string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery,
            typeFilter: typeFilter,
            pinnedOnly: pinnedOnly,
            favoritesOnly: favoritesOnly);

        Application.Current?.Dispatcher.Invoke(() =>
        {
            Entries.Clear();
            foreach (var entry in entries)
            {
                // Fall back to full image data if thumbnail wasn't generated
                if (entry.ContentType == ClipboardContentType.Image && entry.ThumbnailData is null && entry.ImageData is not null)
                {
                    entry.ThumbnailData = entry.ImageData;
                }
                // Don't load full image data into the list - use thumbnails
                if (entry.ContentType == ClipboardContentType.Image)
                    entry.ImageData = null; // Free memory, keep ThumbnailData
                Entries.Add(entry);
            }
            OnPropertyChanged(nameof(IsEmpty));
        });

        EntryCount = await _database.GetEntryCountAsync();
    }

    private async Task SearchAsync()
    {
        // Cancel any pending search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            // Small debounce
            await Task.Delay(150, token);
            if (token.IsCancellationRequested) return;

            await RefreshAsync();
        }
        catch (OperationCanceledException)
        {
            // Expected when typing fast
        }
    }

    private async Task SelectAndCopyAsync(object? parameter)
    {
        var entry = parameter as ClipboardEntry ?? SelectedEntry;
        if (entry is null) return;

        // Fetch full entry from DB if it is an image or if data was deferred
        if (entry.ContentType == ClipboardContentType.Image)
        {
            var fullEntry = await _database.GetEntryByIdAsync(entry.Id);
            if (fullEntry is not null)
                entry = fullEntry;
        }

        bool success = _restore.RestoreToClipboard(entry);
        if (success)
        {
            StatusText = "✓ Copied to clipboard!";
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                StatusText = $"{EntryCount} items";
            });
        }
    }

    private async Task DeleteEntryAsync(object? parameter)
    {
        var entry = parameter as ClipboardEntry ?? SelectedEntry;
        if (entry is null) return;

        await _database.DeleteEntryAsync(entry.Id);
        await RefreshAsync();
    }

    private async Task TogglePinAsync(object? parameter)
    {
        var entry = parameter as ClipboardEntry ?? SelectedEntry;
        if (entry is null) return;

        await _database.TogglePinAsync(entry.Id);
        await RefreshAsync();
    }

    private async Task ToggleFavoriteAsync(object? parameter)
    {
        var entry = parameter as ClipboardEntry ?? SelectedEntry;
        if (entry is null) return;

        await _database.ToggleFavoriteAsync(entry.Id);
        await RefreshAsync();
    }

    private async Task ClearHistoryAsync()
    {
        await _database.ClearHistoryAsync();
        await RefreshAsync();
    }

    private void OpenInExplorer(object? parameter)
    {
        var entry = parameter as ClipboardEntry ?? SelectedEntry;
        if (entry is null || entry.ContentType != ClipboardContentType.Files)
            return;

        var paths = entry.ParsedFilePaths;
        if (paths.Length == 0) return;

        string targetPath = paths[0];
        try
        {
            if (File.Exists(targetPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{targetPath}\"",
                    UseShellExecute = true
                });
            }
            else if (Directory.Exists(targetPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{targetPath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch { }
    }

    private async Task ProcessFilesEntryAsync(ClipboardEntry entry)
    {
        var paths = entry.ParsedFilePaths;
        if (paths.Length == 0) return;

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Collect tags from file names and extensions
        foreach (var p in paths)
        {
            string name = Path.GetFileNameWithoutExtension(p);
            string ext = Path.GetExtension(p).TrimStart('.').ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(name))
            {
                tags.Add(name);
                var words = name.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var w in words)
                    if (w.Length >= 2) tags.Add(w);
            }
            if (!string.IsNullOrWhiteSpace(ext))
                tags.Add(ext);
        }

        // Look for an image file among copied files to generate thumbnail and AI classification
        string? imageFile = paths.FirstOrDefault(p =>
        {
            string ext = Path.GetExtension(p).ToLowerInvariant();
            return (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".webp") && File.Exists(p);
        });

        if (imageFile is not null)
        {
            try
            {
                byte[] imageBytes = await File.ReadAllBytesAsync(imageFile);
                if (imageBytes.Length <= _settingsService.Settings.MaxImageSizeKB * 1024)
                {
                    entry.ThumbnailData = await Helpers.ImageHelper.CreateThumbnailAsync(imageBytes);

                    if (_settingsService.Settings.EnableAiImageRecognition)
                    {
                        var analysis = await _imageRecognitionService.AnalyzeImageAsync(imageBytes);
                        foreach (var tag in analysis.AllSearchTags)
                            tags.Add(tag);
                    }
                }
            }
            catch { }
        }
        else
        {
            // If not an image, check for a text file to read preview snippet
            string? textFile = paths.FirstOrDefault(p =>
            {
                string ext = Path.GetExtension(p).ToLowerInvariant();
                return (ext == ".txt" || ext == ".md" || ext == ".cs" || ext == ".json" || ext == ".py" ||
                        ext == ".js" || ext == ".ts" || ext == ".html" || ext == ".css" || ext == ".xml" ||
                        ext == ".csv" || ext == ".log" || ext == ".ini" || ext == ".yaml" || ext == ".yml" ||
                        ext == ".sql" || ext == ".sh" || ext == ".ps1") && File.Exists(p);
            });

            if (textFile is not null)
            {
                try
                {
                    using var stream = new FileStream(textFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    char[] buffer = new char[200];
                    int read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        string snippet = new string(buffer, 0, read).Trim();
                        string fileName = Path.GetFileName(textFile);
                        entry.PlainText = $"{fileName}\n{snippet}";

                        var words = snippet.Split(new[] { ' ', '\r', '\n', '\t', ',', '.', ':', ';', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var w in words.Take(25))
                            if (w.Length >= 3) tags.Add(w);
                    }
                }
                catch { }
            }
        }

        if (tags.Count > 0)
        {
            entry.Tags = string.Join(", ", tags);
        }
    }
}
