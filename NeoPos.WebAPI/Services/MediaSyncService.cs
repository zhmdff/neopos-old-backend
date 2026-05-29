using DAL.Server.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace NeoPos.WebAPI.Services;

public sealed class MediaSyncRequest
{
    public required string MasterWebBaseUrl { get; init; }
    public string? UploadSecret { get; init; }
    public IEnumerable<string?> DbReferencedPaths { get; init; } = Array.Empty<string?>();
    public bool ScanLocalUploadsFolder { get; init; } = true;
}

public interface IMediaSyncService
{
    Task SyncUploadsAsync(MediaSyncRequest request, CancellationToken ct = default);

    [Obsolete("Use SyncUploadsAsync")]
    Task SyncMissingMediaAsync(string masterBaseUrl, IEnumerable<string?> relativePaths, CancellationToken ct)
        => SyncUploadsAsync(new MediaSyncRequest
        {
            MasterWebBaseUrl = masterBaseUrl,
            DbReferencedPaths = relativePaths,
        }, ct);
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

    public async Task SyncUploadsAsync(MediaSyncRequest request, CancellationToken ct = default)
    {
        var masterBase = NormalizeWebBase(request.MasterWebBaseUrl);
        if (string.IsNullOrEmpty(masterBase))
        {
            _logger.LogWarning("Media sync skipped: MasterWebBaseUrl is not configured.");
            return;
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var paths = MediaPathCollector.NormalizePaths(request.DbReferencedPaths);

        if (request.ScanLocalUploadsFolder)
            paths.UnionWith(MediaPathCollector.EnumerateUploadsFiles(webRoot));

        if (paths.Count == 0)
        {
            _logger.LogInformation("Media sync: no upload paths to process.");
            return;
        }

        using var client = _httpClientFactory.CreateClient("NeoPosMediaSync");
        client.Timeout = TimeSpan.FromMinutes(10);

        var pulled = 0;
        var pushed = 0;

        foreach (var path in paths)
        {
            if (ct.IsCancellationRequested) break;

            var localPath = MediaPathCollector.ToLocalFilePath(webRoot, path);
            var localExists = File.Exists(localPath);
            var onMaster = await ExistsOnMasterAsync(client, masterBase, path, ct);

            if (!localExists)
            {
                if (await TryPullFromMasterAsync(client, masterBase, path, localPath, ct))
                    pulled++;
                continue;
            }

            if (!onMaster)
            {
                if (await TryPushToMasterAsync(client, masterBase, request.UploadSecret, path, localPath, ct))
                    pushed++;
            }
        }

        _logger.LogInformation("Media sync finished. Paths={Count}, pulled={Pulled}, pushed={Pushed}", paths.Count, pulled, pushed);
    }

    private async Task<bool> TryPullFromMasterAsync(
        HttpClient client,
        string masterBase,
        string relativePath,
        string localPath,
        CancellationToken ct)
    {
        try
        {
            var url = $"{masterBase}{relativePath}";
            _logger.LogDebug("Pull media from master: {Url}", url);
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Pull skipped {Path}: {Status}", relativePath, response.StatusCode);
                return false;
            }

            var dir = Path.GetDirectoryName(localPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var file = File.Create(localPath);
            await stream.CopyToAsync(file, ct);
            _logger.LogInformation("Pulled media: {Path}", relativePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to pull media: {Path}", relativePath);
            return false;
        }
    }

    private static async Task<bool> ExistsOnMasterAsync(HttpClient client, string masterBase, string relativePath, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, $"{masterBase}{relativePath}");
            using var response = await client.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TryPushToMasterAsync(
        HttpClient client,
        string masterBase,
        string? uploadSecret,
        string relativePath,
        string localPath,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(uploadSecret))
        {
            _logger.LogDebug("Push skipped {Path}: Sync:MediaUploadSecret not configured.", relativePath);
            return false;
        }

        try
        {
            var url =
                $"{masterBase}/api/System/sync-media?path={Uri.EscapeDataString(relativePath)}";
            var bytes = await File.ReadAllBytesAsync(localPath, ct);
            using var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = content };
            request.Headers.TryAddWithoutValidation("X-NeoPos-Sync-Secret", uploadSecret.Trim());

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Push failed {Path}: {Status}", relativePath, response.StatusCode);
                return false;
            }

            _logger.LogInformation("Pushed media to master: {Path}", relativePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push media: {Path}", relativePath);
            return false;
        }
    }

    public static string NormalizeWebBase(string? raw)
    {
        var s = (raw ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(s)) return "";
        if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            s = "https://" + s;
        return s;
    }
}

/// <summary>Collect /uploads/... paths from DB and disk.</summary>
public static class MediaPathCollector
{
    public static async Task<HashSet<string>> CollectFromDatabasesAsync(
        AppDbContext localDb,
        RemoteDbContext? remoteDb,
        Guid localCompanyId,
        Guid remoteCompanyId,
        CancellationToken ct)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await AddCompanyMediaAsync(localDb, localCompanyId, paths, ct);
        foreach (var url in await localDb.Categories.AsNoTracking()
                     .Where(c => c.CompanyId == localCompanyId)
                     .Select(c => c.ImageUrl)
                     .ToListAsync(ct))
            AddPath(paths, url);
        foreach (var url in await localDb.Products.AsNoTracking()
                     .Where(p => p.CompanyId == localCompanyId)
                     .Select(p => p.ImageUrl)
                     .ToListAsync(ct))
            AddPath(paths, url);
        await AddQrGalleryAsync(localDb, localCompanyId, paths, ct);

        if (remoteDb != null)
        {
            await AddCompanyMediaAsync(remoteDb, remoteCompanyId, paths, ct);
            foreach (var url in await remoteDb.Categories.AsNoTracking()
                         .Where(c => c.CompanyId == remoteCompanyId)
                         .Select(c => c.ImageUrl)
                         .ToListAsync(ct))
                AddPath(paths, url);
            foreach (var url in await remoteDb.Products.AsNoTracking()
                         .Where(p => p.CompanyId == remoteCompanyId)
                         .Select(p => p.ImageUrl)
                         .ToListAsync(ct))
                AddPath(paths, url);
            await AddQrGalleryAsync(remoteDb, remoteCompanyId, paths, ct);
        }

        return NormalizePaths(paths);
    }

    public static HashSet<string> NormalizePaths(IEnumerable<string?> raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in raw)
        {
            var n = NormalizeOne(p);
            if (n != null) set.Add(n);
        }
        return set;
    }

    public static string? NormalizeOne(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var s = path.Trim().Replace('\\', '/');
        if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(s, UriKind.Absolute, out var uri))
            {
                s = uri.AbsolutePath;
            }
            else return null;
        }

        if (!s.StartsWith('/')) s = "/" + s;
        if (!s.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)) return null;
        if (s.Contains("..", StringComparison.Ordinal)) return null;
        return s;
    }

    public static HashSet<string> EnumerateUploadsFiles(string webRoot)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uploadsDir = Path.Combine(webRoot, "uploads");
        if (!Directory.Exists(uploadsDir)) return set;

        foreach (var file in Directory.EnumerateFiles(uploadsDir, "*", SearchOption.AllDirectories))
        {
            var rel = "/" + Path.GetRelativePath(webRoot, file).Replace('\\', '/');
            var n = NormalizeOne(rel);
            if (n != null) set.Add(n);
        }

        return set;
    }

    public static string ToLocalFilePath(string webRoot, string normalizedRelativePath)
        => Path.Combine(webRoot, normalizedRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    private static async Task AddCompanyMediaAsync(
        AppDbContext db,
        Guid companyId,
        HashSet<string> paths,
        CancellationToken ct)
    {
        var c = await db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == companyId, ct);
        if (c == null) return;
        AddPath(paths, c.Logo);
        AddPath(paths, c.PosLockScreenImage);
        AddPath(paths, c.CustomerDisplayLockScreenImage);
    }

    private static async Task AddQrGalleryAsync(AppDbContext db, Guid companyId, HashSet<string> paths, CancellationToken ct)
    {
        var qr = await db.QRMenuSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId, ct);
        if (qr == null) return;

        foreach (var img in qr.GalleryImages)
            AddPath(paths, img);

        if (!string.IsNullOrWhiteSpace(qr.GalleryImagesJson))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(qr.GalleryImagesJson);
                if (list != null)
                    foreach (var img in list)
                        AddPath(paths, img);
            }
            catch
            {
                /* ignore malformed json */
            }
        }
    }

    private static void AddPath(HashSet<string> paths, string? p)
    {
        var n = NormalizeOne(p);
        if (n != null) paths.Add(n);
    }
}
