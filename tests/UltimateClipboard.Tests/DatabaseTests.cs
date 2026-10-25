using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UltimateClipboard.Data;
using UltimateClipboard.Helpers;
using UltimateClipboard.Models;
using Xunit;

namespace UltimateClipboard.Tests;

public class DatabaseTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ClipboardDatabase _database;

    public DatabaseTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"uc_test_{Guid.NewGuid():N}.db");
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
    public async Task InsertEntry_PersistsAndRetrievesEntry()
    {
        var entry = new ClipboardEntry
        {
            ContentType = ClipboardContentType.Text,
            PlainText = "Hello, UltimateClipboard!",
            ContentHash = ContentHasher.ComputeHash("Hello, UltimateClipboard!"),
            CapturedAt = DateTime.UtcNow,
            SourceApplication = "notepad"
        };

        var id = await _database.InsertEntryAsync(entry);
        Assert.True(id > 0);

        var byId = await _database.GetEntryByIdAsync(id);
        Assert.NotNull(byId);
        Assert.Equal("Hello, UltimateClipboard!", byId.PlainText);

        var recent = await _database.GetRecentEntriesAsync(10);
        Assert.Single(recent);
        Assert.Equal("Hello, UltimateClipboard!", recent[0].PlainText);
        Assert.Equal("notepad", recent[0].SourceApplication);
    }

    [Fact]
    public async Task InsertEntry_DuplicateHash_UpdatesExistingInsteadOfAddingRow()
    {
        var hash = ContentHasher.ComputeHash("Duplicate Test");
        var entry1 = new ClipboardEntry
        {
            ContentType = ClipboardContentType.Text,
            PlainText = "Duplicate Test",
            ContentHash = hash,
            CapturedAt = DateTime.UtcNow.AddMinutes(-10),
            SourceApplication = "app1"
        };

        var id1 = await _database.InsertEntryAsync(entry1);

        var entry2 = new ClipboardEntry
        {
            ContentType = ClipboardContentType.Text,
            PlainText = "Duplicate Test",
            ContentHash = hash,
            CapturedAt = DateTime.UtcNow,
            SourceApplication = "app2"
        };

        var id2 = await _database.InsertEntryAsync(entry2);

        Assert.Equal(id1, id2);
        var count = await _database.GetEntryCountAsync();
        Assert.Equal(1, count);

        var recent = await _database.GetRecentEntriesAsync();
        Assert.Equal("app2", recent[0].SourceApplication);
    }

    [Fact]
    public async Task SearchEntries_MatchesPartialQuery()
    {
        await _database.InsertEntryAsync(new ClipboardEntry
        {
            ContentType = ClipboardContentType.Text,
            PlainText = "The quick brown fox jumps over the lazy dog",
            ContentHash = ContentHasher.ComputeHash("1")
        });

        await _database.InsertEntryAsync(new ClipboardEntry
        {
            ContentType = ClipboardContentType.Text,
            PlainText = "Pack my box with five dozen liquor jugs",
            ContentHash = ContentHasher.ComputeHash("2")
        });

        var results = await _database.SearchEntriesAsync(query: "quick");
        Assert.Single(results);
        Assert.Contains("fox", results[0].PlainText);

        var allMatches = await _database.SearchEntriesAsync(query: "box");
        Assert.Single(allMatches);
    }

    [Fact]
    public async Task SearchEntries_MatchesImageByAiTags_WhenSearchingForCat()
    {
        // Insert an image entry that was tagged by AI with "cat, tabby cat, feline, pet"
        await _database.InsertEntryAsync(new ClipboardEntry
        {
            ContentType = ClipboardContentType.Image,
            PlainText = "SnippingTool Image (800 × 600) — cat, tabby cat",
            Tags = "cat, tabby cat, feline, pet, domestic animal",
            ContentHash = ContentHasher.ComputeHash("image_with_cat_content")
        });

        // Insert unrelated image
        await _database.InsertEntryAsync(new ClipboardEntry
        {
            ContentType = ClipboardContentType.Image,
            PlainText = "Discord Image (1920 × 1080) — sports car, vehicle",
            Tags = "car, sports car, vehicle",
            ContentHash = ContentHasher.ComputeHash("image_with_car_content")
        });

        // Search for "cat"
        var catResults = await _database.SearchEntriesAsync(query: "cat");
        Assert.Single(catResults);
        Assert.Equal(ClipboardContentType.Image, catResults[0].ContentType);
        Assert.Contains("cat", catResults[0].Tags);

        // Search for "feline" (matched via AI tags)
        var felineResults = await _database.SearchEntriesAsync(query: "feline");
        Assert.Single(felineResults);
        Assert.Equal(ClipboardContentType.Image, felineResults[0].ContentType);
    }

    [Fact]
    public async Task FilterByType_ReturnsMatchingEntriesOnly()
    {
        await _database.InsertEntryAsync(new ClipboardEntry
        {
            ContentType = ClipboardContentType.Text,
            PlainText = "Just text",
            ContentHash = ContentHasher.ComputeHash("text")
        });

        await _database.InsertEntryAsync(new ClipboardEntry
        {
            ContentType = ClipboardContentType.Image,
            ImageData = new byte[] { 1, 2, 3 },
            ContentHash = ContentHasher.ComputeHash(new byte[] { 1, 2, 3 })
        });

        var textOnly = await _database.SearchEntriesAsync(typeFilter: ClipboardContentType.Text);
        Assert.Single(textOnly);
        Assert.Equal(ClipboardContentType.Text, textOnly[0].ContentType);

        var imagesOnly = await _database.SearchEntriesAsync(typeFilter: ClipboardContentType.Image);
        Assert.Single(imagesOnly);
        Assert.Equal(ClipboardContentType.Image, imagesOnly[0].ContentType);
    }

    [Fact]
    public async Task TogglePin_PreservesPinnedAcrossClearHistory()
    {
        var id1 = await _database.InsertEntryAsync(new ClipboardEntry
        {
            ContentType = ClipboardContentType.Text,
            PlainText = "Pinned Item",
            ContentHash = ContentHasher.ComputeHash("p")
        });

        var id2 = await _database.InsertEntryAsync(new ClipboardEntry
        {
            ContentType = ClipboardContentType.Text,
            PlainText = "Regular Item",
            ContentHash = ContentHasher.ComputeHash("r")
        });

        await _database.TogglePinAsync(id1);

        var recentBefore = await _database.GetRecentEntriesAsync();
        Assert.True(recentBefore.First(e => e.Id == id1).IsPinned);

        // Clear history should delete only unpinned
        await _database.ClearHistoryAsync();

        var recentAfter = await _database.GetRecentEntriesAsync();
        Assert.Single(recentAfter);
        Assert.Equal(id1, recentAfter[0].Id);
    }

    [Fact]
    public async Task EnforceHistoryLimit_RemovesOldestEntries()
    {
        for (int i = 1; i <= 5; i++)
        {
            await _database.InsertEntryAsync(new ClipboardEntry
            {
                ContentType = ClipboardContentType.Text,
                PlainText = $"Item {i}",
                ContentHash = ContentHasher.ComputeHash($"item_{i}"),
                CapturedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }

        Assert.Equal(5, await _database.GetEntryCountAsync());

        await _database.EnforceHistoryLimitAsync(3);

        var count = await _database.GetEntryCountAsync();
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task EnforceRetention_DeletesItemsOlderThanDays()
    {
        await _database.InsertEntryAsync(new ClipboardEntry
        {
            ContentType = ClipboardContentType.Text,
            PlainText = "Old Item",
            ContentHash = ContentHasher.ComputeHash("old"),
            CapturedAt = DateTime.UtcNow.AddDays(-10)
        });

        await _database.InsertEntryAsync(new ClipboardEntry
        {
            ContentType = ClipboardContentType.Text,
            PlainText = "New Item",
            ContentHash = ContentHasher.ComputeHash("new"),
            CapturedAt = DateTime.UtcNow.AddDays(-1)
        });

        await _database.EnforceRetentionAsync(5);

        var remaining = await _database.GetRecentEntriesAsync();
        Assert.Single(remaining);
        Assert.Equal("New Item", remaining[0].PlainText);
    }
}
