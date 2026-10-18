using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace myView.Services;

public class UpdateInfo
{
    public bool IsUpdateAvailable { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string ReleaseName { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string ReleaseUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public long AssetSize { get; set; }
}

public static class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/pliroforikos/myViewer/releases/latest";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("myView", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        return client;
    }

    /// <summary>
    /// Checks GitHub Releases API for a newer release than currentVersion.
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdatesAsync(string currentVersionStr, CancellationToken ct = default)
    {
        try
        {
            using var response = await HttpClient.GetAsync(GitHubApiUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
            string releaseName = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
            string releaseNotes = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";

            string cleanLatestVer = tagName.TrimStart('v', 'V').Trim();
            string cleanCurrentVer = currentVersionStr.TrimStart('v', 'V').Trim();

            if (!Version.TryParse(cleanLatestVer, out var latestVer) ||
                !Version.TryParse(cleanCurrentVer, out var currentVer))
            {
                return null;
            }

            bool isNewer = latestVer > currentVer;

            // Find lightweight setup asset (or fallback to any .exe setup)
            string downloadUrl = string.Empty;
            string assetName = string.Empty;
            long assetSize = 0;

            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                JsonElement? chosenAsset = null;

                // Priority 1: lightweight-setup.exe
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    string aName = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (aName.Contains("lightweight", StringComparison.OrdinalIgnoreCase) &&
                        aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        chosenAsset = asset;
                        break;
                    }
                }

                // Priority 2: any setup.exe
                if (chosenAsset == null)
                {
                    foreach (var asset in assetsProp.EnumerateArray())
                    {
                        string aName = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        if (aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            chosenAsset = asset;
                            break;
                        }
                    }
                }

                if (chosenAsset.HasValue)
                {
                    var a = chosenAsset.Value;
                    assetName = a.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                    downloadUrl = a.TryGetProperty("browser_download_url", out var du) ? du.GetString() ?? "" : "";
                    assetSize = a.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                }
            }

            return new UpdateInfo
            {
                IsUpdateAvailable = isNewer,
                LatestVersion = cleanLatestVer,
                CurrentVersion = cleanCurrentVer,
                ReleaseName = releaseName,
                ReleaseNotes = releaseNotes,
                ReleaseUrl = htmlUrl,
                DownloadUrl = downloadUrl,
                AssetName = assetName,
                AssetSize = assetSize
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Downloads the installer to destinationPath with progress reporting (0.0 to 1.0).
    /// </summary>
    public static async Task DownloadInstallerAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
            throw new ArgumentException("Download URL is empty", nameof(downloadUrl));

        // Use custom client with larger timeout for download
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("myView", "1.0"));

        using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;

        // Ensure target directory exists
        string? dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string tempFile = destinationPath + ".downloading";

        try
        {
            await using (var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
            {
                byte[] buffer = new byte[65536];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    totalRead += read;

                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        double p = (double)totalRead / totalBytes.Value;
                        progress?.Report(p);
                    }
                }
            }

            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            File.Move(tempFile, destinationPath);
            progress?.Report(1.0);
        }
        catch
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
            throw;
        }
    }

    /// <summary>
    /// Launches the downloaded installer process.
    /// </summary>
    public static void LaunchInstaller(string installerPath)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException("Installer file was not found", installerPath);

        var startInfo = new ProcessStartInfo(installerPath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(installerPath) ?? string.Empty
        };

        Process.Start(startInfo);
    }
}
