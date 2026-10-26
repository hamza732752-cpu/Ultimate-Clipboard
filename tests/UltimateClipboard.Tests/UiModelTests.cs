using System;
using System.IO;
using System.Threading.Tasks;
using UltimateClipboard.Data;
using UltimateClipboard.Models;
using UltimateClipboard.Services;
using UltimateClipboard.ViewModels;
using Xunit;

namespace UltimateClipboard.Tests;

public class UiModelTests
{
    [Fact]
    public void ClipboardEntry_DetectsHexColorsCorrectly()
    {
        var entry1 = new ClipboardEntry { PlainText = "#FF00AA" };
        Assert.True(entry1.IsHexColor);

        var entry2 = new ClipboardEntry { PlainText = "#fff" };
        Assert.True(entry2.IsHexColor);

        var entry3 = new ClipboardEntry { PlainText = "#80FF00AA" };
        Assert.True(entry3.IsHexColor);

        var entry4 = new ClipboardEntry { PlainText = "Not a color" };
        Assert.False(entry4.IsHexColor);

        var entry5 = new ClipboardEntry { PlainText = "#12" };
        Assert.False(entry5.IsHexColor);

        var entry6 = new ClipboardEntry { PlainText = "#GGHHII" };
        Assert.False(entry6.IsHexColor);
    }

    [Fact]
    public void ClipboardEntry_DetectsUrlsAndExtractsDomain()
    {
        var entry1 = new ClipboardEntry { PlainText = "https://github.com/MeheReal/clipboardmanager" };
        Assert.True(entry1.IsUrl);
        Assert.Equal("github.com", entry1.UrlDomain);

        var entry2 = new ClipboardEntry { PlainText = "http://localhost:5000/api/status" };
        Assert.True(entry2.IsUrl);
        Assert.Equal("localhost", entry2.UrlDomain);

        var entry3 = new ClipboardEntry { PlainText = "Just random text with https://embedded.com" };
        Assert.False(entry3.IsUrl);
        Assert.Empty(entry3.UrlDomain);
    }

    [Fact]
    public void ClipboardEntry_ParsesDisplayTagsCorrectly()
    {
        var entry1 = new ClipboardEntry { Tags = "discord, chat, mehe, screenshot" };
        Assert.True(entry1.HasTags);
        Assert.Equal(4, entry1.DisplayTags.Count);
        Assert.Contains("discord", entry1.DisplayTags);
        Assert.Contains("mehe", entry1.DisplayTags);

        var entry2 = new ClipboardEntry { Tags = null };
        Assert.False(entry2.HasTags);
        Assert.Empty(entry2.DisplayTags);
    }

    [Fact]
    public async Task MainViewModel_ClearCommands_WorkProperly()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"uc_test_ui_{Guid.NewGuid():N}.db");
        try
        {
            var db = new ClipboardDatabase(dbPath);
            var settings = new SettingsService(Path.Combine(Path.GetTempPath(), $"uc_settings_{Guid.NewGuid():N}.json"));
            var capture = new ClipboardCapture();
            var restore = new ClipboardRestore();

            var vm = new MainViewModel(db, capture, restore, settings);

            // Initially not confirming
            Assert.False(vm.IsConfirmingClear);

            // Prompt clear
            vm.PromptClearCommand.Execute(null);
            Assert.True(vm.IsConfirmingClear);

            // Cancel clear
            vm.CancelClearCommand.Execute(null);
            Assert.False(vm.IsConfirmingClear);

            // Search query and clear search
            vm.SearchQuery = "test search";
            Assert.True(vm.HasSearchQuery);

            vm.ClearSearchCommand.Execute(null);
            Assert.False(vm.HasSearchQuery);
            Assert.Equal(string.Empty, vm.SearchQuery);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }
}
