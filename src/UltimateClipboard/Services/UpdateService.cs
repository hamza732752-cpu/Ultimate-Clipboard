using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace UltimateClipboard.Services;

public class UpdateCheckResult
{
    public bool Success { get; set; }
    public bool IsUpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = "1.1.0";
    public string LatestVersion { get; set; } = "1.1.0";
    public string ReleaseNotes { get; set; } = string.Empty;
    public string ReleaseUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
}

/// <summary>
/// Service to check for application updates from GitHub Releases.
/// Safe, privacy-conscious, and non-blocking.
/// </summary>
public class UpdateService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly string _repoOwner;
    private readonly string _repoName;

    public string CurrentVersion { get; }

    public UpdateService(string repoOwner = "Hamza", string repoName = "UltimateClipboard")
    {
        _repoOwner = repoOwner;
        _repoName = repoName;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        CurrentVersion = version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.1.0";
    }

    /// <summary>
    /// Checks if a newer release is available on GitHub.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        var result = new UpdateCheckResult
        {
            CurrentVersion = CurrentVersion,
            LatestVersion = CurrentVersion
        };

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{_repoOwner}/{_repoName}/releases/latest");
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("UltimateClipboard", CurrentVersion));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            using var response = await HttpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                result.Success = true;
                result.IsUpdateAvailable = false;
                result.StatusMessage = $"You are running the latest version (v{CurrentVersion}).";
                return result;
            }

            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                result.StatusMessage = "Could not check for updates. Please try again later.";
                return result;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";
            string body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";

            string cleanTag = tagName.TrimStart('v', 'V');
            result.LatestVersion = cleanTag;
            result.ReleaseUrl = htmlUrl;
            result.ReleaseNotes = body;

            // Extract first .exe or .zip download URL if available in assets
            if (root.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("browser_download_url", out var downloadProp))
                    {
                        result.DownloadUrl = downloadProp.GetString() ?? "";
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(result.DownloadUrl))
            {
                result.DownloadUrl = htmlUrl;
            }

            if (Version.TryParse(cleanTag, out var remoteVer) && Version.TryParse(CurrentVersion, out var currentVer))
            {
                result.IsUpdateAvailable = remoteVer > currentVer;
            }
            else
            {
                result.IsUpdateAvailable = !string.IsNullOrEmpty(cleanTag) && !cleanTag.Equals(CurrentVersion, StringComparison.OrdinalIgnoreCase);
            }

            result.Success = true;
            result.StatusMessage = result.IsUpdateAvailable
                ? $"🎉 Update v{result.LatestVersion} is available!"
                : $"✓ You are up to date! (v{CurrentVersion})";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.StatusMessage = "Network error while checking for updates.";
            Debug.WriteLine($"[UpdateService] Check failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Opens the release download page in the default web browser.
    /// </summary>
    public void OpenReleaseUrl(string? url = null)
    {
        string targetUrl = !string.IsNullOrEmpty(url)
            ? url
            : $"https://github.com/{_repoOwner}/{_repoName}/releases";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetUrl,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
