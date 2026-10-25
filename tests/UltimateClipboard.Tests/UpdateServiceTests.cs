using System;
using System.Threading.Tasks;
using UltimateClipboard.Services;
using Xunit;

namespace UltimateClipboard.Tests;

public class UpdateServiceTests
{
    [Fact]
    public void Constructor_InitializesWithValidCurrentVersion()
    {
        var service = new UpdateService();
        Assert.NotNull(service.CurrentVersion);
        Assert.NotEmpty(service.CurrentVersion);
        Assert.True(Version.TryParse(service.CurrentVersion, out _));
    }

    [Fact]
    public async Task CheckForUpdates_ReturnsGracefulResult_WithoutThrowing()
    {
        var service = new UpdateService("NonExistentOwner12345", "NonExistentRepo12345");
        var result = await service.CheckForUpdatesAsync();

        Assert.NotNull(result);
        Assert.NotEmpty(result.StatusMessage);
        Assert.False(result.IsUpdateAvailable);
    }
}
