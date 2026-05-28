namespace BusinessLayer.Utilities;

/// <summary>Boss / Telegram / Web Push üçün kritik audit hadisələrinin təsnifatı.</summary>
public static class BossCriticalAuditKindClassifier
{
    public enum Kind
    {
        None,
        ProductLineDeleted,
        ArchiveCheckReopened,
        TableTransferred
    }

    public static Kind Classify(string? action)
    {
        if (IsProductDeletion(action)) return Kind.ProductLineDeleted;
        if (IsArchiveCheckReopened(action)) return Kind.ArchiveCheckReopened;
        if (IsTableTransferred(action)) return Kind.TableTransferred;
        return Kind.None;
    }

    public static string NormalizeAuditActionForMatch(string? action)
    {
        if (string.IsNullOrEmpty(action)) return string.Empty;
        return action.ToUpperInvariant()
            .Replace("İ", "I", StringComparison.Ordinal)
            .Replace("İ", "I", StringComparison.Ordinal)
            .Replace("Ə", "E", StringComparison.Ordinal)
            .Replace("ı", "I", StringComparison.Ordinal)
            .Replace("Ç", "C", StringComparison.Ordinal)
            .Replace("Ş", "S", StringComparison.Ordinal)
            .Replace("Ö", "O", StringComparison.Ordinal)
            .Replace("Ü", "U", StringComparison.Ordinal)
            .Replace("Ğ", "G", StringComparison.Ordinal);
    }

    public static bool IsProductDeletion(string? action)
    {
        var n = NormalizeAuditActionForMatch(action);
        return n.Contains("MEHSUL", StringComparison.Ordinal) && n.Contains("SILINDI", StringComparison.Ordinal);
    }

    public static bool IsArchiveCheckReopened(string? action)
    {
        var n = NormalizeAuditActionForMatch(action);
        return n.Contains("ARXIV", StringComparison.Ordinal)
               && n.Contains("CEK", StringComparison.Ordinal)
               && n.Contains("YENILENDI", StringComparison.Ordinal);
    }

    /// <summary>OrderService: «MASA KÖÇÜRÜLDÜ 🔄».</summary>
    public static bool IsTableTransferred(string? action)
    {
        var n = NormalizeAuditActionForMatch(action);
        return n.Contains("MASA", StringComparison.Ordinal) && n.Contains("KOCURULDU", StringComparison.Ordinal);
    }
}
