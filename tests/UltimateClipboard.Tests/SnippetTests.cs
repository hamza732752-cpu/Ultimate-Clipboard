using System;
using System.IO;
using System.Threading.Tasks;
using UltimateClipboard.Data;
using UltimateClipboard.Models;
using Xunit;

namespace UltimateClipboard.Tests;

public class SnippetTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ClipboardDatabase _database;

    public SnippetTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"uc_snippet_test_{Guid.NewGuid():N}.db");
        _database = new ClipboardDatabase(_dbPath);
    }

    public void Dispose()
    {
        _database.Dispose();
        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch { }
    }

    [Fact]
    public async Task InsertAndRetrieveSnippet_WorksCorrectly()
    {
        var snippet = new Snippet
        {
            Title = "Email Signature",
            Content = "Best regards,\nHamza",
            Collection = "Work",
            Tags = "email,closing"
        };

        var id = await _database.InsertSnippetAsync(snippet);
        Assert.True(id > 0);

        var list = await _database.GetSnippetsAsync();
        Assert.Single(list);
        Assert.Equal("Email Signature", list[0].Title);
        Assert.Equal("Best regards,\nHamza", list[0].Content);
    }

    [Fact]
    public async Task SearchSnippets_FindsMatches()
    {
        await _database.InsertSnippetAsync(new Snippet { Title = "Git push script", Content = "git push origin main" });
        await _database.InsertSnippetAsync(new Snippet { Title = "Home Address", Content = "123 Main Street" });

        var results = await _database.SearchSnippetsAsync("git");
        Assert.Single(results);
        Assert.Equal("Git push script", results[0].Title);
    }

    [Fact]
    public async Task DeleteSnippet_RemovesItem()
    {
        var id = await _database.InsertSnippetAsync(new Snippet { Title = "Temporary", Content = "delete me" });
        Assert.Single(await _database.GetSnippetsAsync());

        await _database.DeleteSnippetAsync(id);
        Assert.Empty(await _database.GetSnippetsAsync());
    }
}
