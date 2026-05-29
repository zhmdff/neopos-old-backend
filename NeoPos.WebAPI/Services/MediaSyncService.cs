using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Http;

namespace NeoPos.WebAPI.Services;

public interface IMediaSyncService
{
    Task SyncMissingMediaAsync(string masterBaseUrl, IEnumerable<string?> relativePaths, CancellationToken ct);
}

public class MediaSyncService : IMediaSyncService
{
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MediaSyncService> _logger;

    public MediaSyncService(IWebHostEnvironment env, IHttpClientFactory httpClientFactory, ILogger<MediaSyncService> logger)
    {
        _env = env;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SyncMissingMediaAsync(string masterBaseUrl, IEnumerable<string?> relativePaths, CancellationToken ct)
    {
        var cleanPaths = relativePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList();

        if (!cleanPaths.Any()) return;

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(masterBaseUrl.TrimEnd('/') + "/");

        foreach (var path in cleanPaths)
        {
            if (ct.IsCancellationRequested) break;

            // Ensure the path is relative to wwwroot
            var localPath = Path.Combine(_env.WebRootPath, path!.TrimStart('/'));
            
            if (File.Exists(localPath)) continue;

            try
            {
                _logger.LogInformation("Downloading missing media: {Path}", path);
                
                // Ensure directory exists
                var dir = Path.GetDirectoryName(localPath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var response = await client.GetAsync(path, ct);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                    await File.WriteAllBytesAsync(localPath, bytes, ct);
                    _logger.LogInformation("Successfully synced: {Path}", path);
                }
                else
                {
                    _logger.LogWarning("Failed to download media {Path}: {Status}", path, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing media file: {Path}", path);
            }
        }
    }
}
