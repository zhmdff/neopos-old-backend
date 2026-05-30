using System.Text.RegularExpressions;

namespace NeoPos.Migration;

public static class TenantKeyGenerator
{
    private static readonly Regex NonSlug = new(@"[^a-z0-9\-]+", RegexOptions.Compiled);

    public static string FromCompanyName(string? nameAz, Guid companyId)
    {
        var raw = (nameAz ?? "").Trim().ToLowerInvariant();
        raw = raw
            .Replace('ə', 'e').Replace('ı', 'i').Replace('ö', 'o').Replace('ü', 'u')
            .Replace('ş', 's').Replace('ç', 'c').Replace('ğ', 'g')
            .Replace('İ', 'i').Replace('Ə', 'e');

        var slug = NonSlug.Replace(raw, "-").Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(slug))
            slug = "tenant";

        var suffix = companyId.ToString("N")[..8];
        var maxBase = Math.Max(8, 40 - suffix.Length - 1);
        if (slug.Length > maxBase)
            slug = slug[..maxBase].Trim('-');

        return $"{slug}-{suffix}";
    }
}
