using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using UltimateClipboard.Data;
using UltimateClipboard.Models;
using UltimateClipboard.Services;

namespace UltimateClipboard.ViewModels;

public class SnippetViewModel : ViewModelBase
{
    private readonly ClipboardDatabase _database;
    private readonly ClipboardRestore _restore;

    public ObservableCollection<Snippet> Snippets { get; } = new();

    private Snippet? _selectedSnippet;
    public Snippet? SelectedSnippet
    {
        get => _selectedSnippet;
        set => SetProperty(ref _selectedSnippet, value);
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

    public ICommand NewSnippetCommand { get; }
    public ICommand SaveSnippetCommand { get; }
    public ICommand DeleteSnippetCommand { get; }
    public ICommand CopySnippetCommand { get; }

    public SnippetViewModel(ClipboardDatabase database, ClipboardRestore restore)
    {
        _database = database;
        _restore = restore;

        NewSnippetCommand = new RelayCommand(NewSnippet);
        SaveSnippetCommand = new AsyncRelayCommand(SaveSnippetAsync);
        DeleteSnippetCommand = new AsyncRelayCommand(DeleteSnippetAsync);
        CopySnippetCommand = new RelayCommand(CopySnippet);
    }

    public async Task LoadSnippetsAsync()
    {
        var list = await _database.GetSnippetsAsync();
        Snippets.Clear();
        foreach (var item in list)
        {
            Snippets.Add(item);
        }
    }

    private async Task SearchAsync()
    {
        var list = string.IsNullOrWhiteSpace(SearchQuery)
            ? await _database.GetSnippetsAsync()
            : await _database.SearchSnippetsAsync(SearchQuery);

        Snippets.Clear();
        foreach (var item in list)
        {
            Snippets.Add(item);
        }
    }

    private void NewSnippet()
    {
        var snippet = new Snippet
        {
            Title = "New Snippet",
            Content = "",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        Snippets.Insert(0, snippet);
        SelectedSnippet = snippet;
    }

    private async Task SaveSnippetAsync(object? parameter)
    {
        var snippet = parameter as Snippet ?? SelectedSnippet;
        if (snippet is null) return;

        snippet.ModifiedAt = DateTime.UtcNow;
        if (snippet.Id == 0)
        {
            snippet.Id = await _database.InsertSnippetAsync(snippet);
        }
        else
        {
            await _database.UpdateSnippetAsync(snippet);
        }
    }

    private async Task DeleteSnippetAsync(object? parameter)
    {
        var snippet = parameter as Snippet ?? SelectedSnippet;
        if (snippet is null) return;

        await _database.DeleteSnippetAsync(snippet.Id);
        Snippets.Remove(snippet);
        if (SelectedSnippet == snippet)
        {
            SelectedSnippet = null;
        }
    }

    private void CopySnippet(object? parameter)
    {
        var snippet = parameter as Snippet ?? SelectedSnippet;
        if (snippet is null) return;

        var entry = new ClipboardEntry
        {
            ContentType = ClipboardContentType.Text,
            PlainText = snippet.Content
        };
        _restore.RestoreToClipboard(entry);
    }
}
