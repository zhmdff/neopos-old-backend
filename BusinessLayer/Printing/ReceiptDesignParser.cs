using System.Text.Json;

namespace BusinessLayer.Printing;

public static class ReceiptDesignParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static ReceiptDesignRoot? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<ReceiptDesignRoot>(json.Trim(), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static List<ReceiptDesignSection> NormalizeKitchenSections(ReceiptDesignRoot? root)
    {
        var defaults = KitchenEscPosRenderer.DefaultKitchenSections();
        var fromJson = root?.Kitchen?.Sections;
        return MergeSections(defaults, fromJson);
    }

    public static List<ReceiptDesignSection> NormalizeCashierSections(ReceiptDesignRoot? root)
    {
        var defaults = KassaEscPosRenderer.DefaultCashierSections();
        return MergeSections(defaults, root?.Cashier?.Sections);
    }

    private static List<ReceiptDesignSection> MergeSections(
        List<ReceiptDesignSection> defaults,
        List<ReceiptDesignSection>? fromJson)
    {
        var map = defaults.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
        if (fromJson != null)
        {
            foreach (var row in fromJson)
            {
                if (string.IsNullOrWhiteSpace(row.Key)) continue;
                map[row.Key] = new ReceiptDesignSection
                {
                    Key = row.Key,
                    Enabled = row.Enabled,
                    Size = NormalizeSize(row.Size),
                    Thickness = NormalizeThickness(row.Thickness),
                    Align = NormalizeAlign(row.Align),
                };
            }
        }

        return defaults
            .Select(d => map.TryGetValue(d.Key, out var merged) ? merged : d)
            .ToList();
    }

    public static string NormalizeSize(string? s)
        => s is "xs" or "sm" or "md" or "lg" ? s : "md";

    public static string NormalizeThickness(string? s)
        => s is "normal" or "bold" ? s : "normal";

    public static string NormalizeAlign(string? s)
        => s is "left" or "center" or "right" ? s : "left";
}
