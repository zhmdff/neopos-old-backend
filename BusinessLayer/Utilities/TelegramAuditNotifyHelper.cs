using System.Globalization;
using System.Text.RegularExpressions;

namespace BusinessLayer.Utilities;

/// <summary>Terminal <c>telegramNotifyPrefs.js</c> ilə uyğun audit → Telegram sinifləndirməsi və mətn.</summary>
public static class TelegramAuditNotifyHelper
{
    private static readonly Regex AfterKitchenTagRe = new(@"\[\[NeoPos:afterKitchen:([01])\]\]\s*$", RegexOptions.Compiled);
    private static readonly Regex AfterKitchenTagStripRe = new(@"\s*\[\[NeoPos:afterKitchen:[01]\]\]\s*", RegexOptions.Compiled);

    /// <summary>Audit təsvirindəki daxili NeoPos teqləri (mətbəx flag) — bildiriş/UI üçün silinir.</summary>
    public static string StripInternalTags(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return AfterKitchenTagStripRe.Replace(text, " ").Trim();
    }
    private static readonly Regex PriceArrowRe = new(@"QIYMET:\s*([\d.,]+)\s*[→\-]\s*([\d.,]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex QtyArrowRe = new(@"MIQDAR:\s*([\d.,]+)\s*[→\-]\s*([\d.,]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WaiterChangeRe = new(@"[«""]([^""»]+)[»""]\s*[→\-]\s*[«""]([^""»]+)[»""]", RegexOptions.Compiled);

    public static readonly IReadOnlyDictionary<string, bool> DefaultPrefs = new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        ["newCheck"] = false,
        ["productIncreaseBeforeKitchen"] = true,
        ["productIncreaseAfterKitchen"] = true,
        ["productDecreaseBeforeKitchen"] = true,
        ["productDecreaseAfterKitchen"] = true,
        ["paymentDone"] = false,
        ["priceChange"] = true,
        ["mergeChecks"] = false,
        ["guestCount"] = true,
        ["discount"] = false,
        ["serviceFee"] = false,
        ["waiterChange"] = false,
        ["shiftStart"] = false,
        ["shiftEnd"] = false,
        ["archiveUpdate"] = true,
        ["depositChange"] = false,
        ["tableTransfer"] = true,
    };

    public static string? ClassifyKind(string? title, string? body)
    {
        var t = NormAuditText(title);
        var b = NormAuditText(body);
        var bodyRaw = body ?? string.Empty;

        if (t.Contains("MEHSUL", StringComparison.Ordinal) && t.Contains("SILINDI", StringComparison.Ordinal))
        {
            var tag = ParseAfterKitchenTag(bodyRaw);
            return tag == 1 ? "productDecreaseAfterKitchen" : "productDecreaseBeforeKitchen";
        }

        if (t.Contains("SIFARIS", StringComparison.Ordinal) && t.Contains("ACILDI", StringComparison.Ordinal)) return "newCheck";
        if (t.Contains("SIFARIS", StringComparison.Ordinal) && t.Contains("BAGLANDI", StringComparison.Ordinal)) return "paymentDone";
        if (t.Contains("CEKLER", StringComparison.Ordinal) && t.Contains("BIRLESDIRILDI", StringComparison.Ordinal)) return "mergeChecks";

        if ((t.Contains("QONAQ", StringComparison.Ordinal) || t.Contains("QONAG", StringComparison.Ordinal)) &&
            (t.Contains("SAYI", StringComparison.Ordinal) || t.Contains("SAY", StringComparison.Ordinal)) &&
            t.Contains("DEYISDI", StringComparison.Ordinal))
            return "guestCount";
        if ((b.Contains("QONAQ", StringComparison.Ordinal) || b.Contains("QONAG", StringComparison.Ordinal)) &&
            b.Contains("SAYI", StringComparison.Ordinal) && (b.Contains('→') || b.Contains("->")))
            return "guestCount";

        if (t.Contains("ENDIRIM", StringComparison.Ordinal)) return "discount";
        if (t.Contains("SERVIS", StringComparison.Ordinal) && t.Contains("HAQQI", StringComparison.Ordinal) && t.Contains("DEYISIKLIYI", StringComparison.Ordinal))
            return "serviceFee";
        if (t.Contains("OFISIANT", StringComparison.Ordinal) && t.Contains("DEYISDI", StringComparison.Ordinal)) return "waiterChange";
        if (t.Contains("DEPOZIT", StringComparison.Ordinal) && t.Contains("DEYISIKLIYI", StringComparison.Ordinal)) return "depositChange";

        if (t.Contains("MEHSUL", StringComparison.Ordinal) && t.Contains("ELAVESI", StringComparison.Ordinal))
        {
            var tag = ParseAfterKitchenTag(bodyRaw);
            return tag == 1 ? "productIncreaseAfterKitchen" : "productIncreaseBeforeKitchen";
        }

        if (t.Contains("MASA", StringComparison.Ordinal) && t.Contains("KOCURULDU", StringComparison.Ordinal)) return "tableTransfer";

        if (t.Contains("NOVBE", StringComparison.Ordinal))
        {
            if (t.Contains("BAGLANDI", StringComparison.Ordinal)) return "shiftEnd";
            if (t.Contains("ACILDI", StringComparison.Ordinal)) return "shiftStart";
        }

        if (t.Contains("ARXIV", StringComparison.Ordinal) && t.Contains("CEK", StringComparison.Ordinal) &&
            (t.Contains("YENIL", StringComparison.Ordinal) || t.Contains("YENILENDI", StringComparison.Ordinal)))
            return "archiveUpdate";
        if (b.Contains("NOVBE", StringComparison.Ordinal) && b.Contains("TARIXCESINDEN", StringComparison.Ordinal) && b.Contains("YENIL", StringComparison.Ordinal))
            return "archiveUpdate";

        var isMehsulRedakte = t.Contains("MEHSUL", StringComparison.Ordinal) &&
                              (t.Contains("REDAKTE", StringComparison.Ordinal) || t.Contains("REDAKTESI", StringComparison.Ordinal) || t.Contains("REDAKT", StringComparison.Ordinal));
        if (isMehsulRedakte)
        {
            var tag = ParseAfterKitchenTag(bodyRaw);
            var dir = QuantityDirection(body);
            if (dir == "down")
                return tag == 1 ? "productDecreaseAfterKitchen" : "productDecreaseBeforeKitchen";
            if (dir == "up")
                return tag == 1 ? "productIncreaseAfterKitchen" : "productIncreaseBeforeKitchen";
            if (HasPriceChange(body)) return "priceChange";
            return null;
        }

        if (AfterKitchenTagRe.IsMatch(bodyRaw))
        {
            var br = NormAuditText(body);
            if (br.Contains("MASASINA", StringComparison.Ordinal) && br.Contains("ELAVE", StringComparison.Ordinal) &&
                br.Contains("EDILDI", StringComparison.Ordinal) && !br.Contains("REDAKTE", StringComparison.Ordinal))
            {
                var tag = ParseAfterKitchenTag(bodyRaw);
                return tag == 1 ? "productIncreaseAfterKitchen" : "productIncreaseBeforeKitchen";
            }
        }

        return null;
    }

    public static bool IsKindEnabled(string? kind, string? prefsJson)
    {
        if (string.IsNullOrEmpty(kind)) return false;
        var prefs = ParsePrefs(prefsJson);
        return prefs.TryGetValue(kind, out var on) && on;
    }

    public static Dictionary<string, bool> ParsePrefs(string? prefsJson)
    {
        var merged = new Dictionary<string, bool>(DefaultPrefs, StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(prefsJson)) return merged;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(prefsJson);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return merged;
            foreach (var kv in merged.Keys.ToList())
            {
                if (doc.RootElement.TryGetProperty(kv, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.True)
                    merged[kv] = true;
                else if (doc.RootElement.TryGetProperty(kv, out el) && el.ValueKind == System.Text.Json.JsonValueKind.False)
                    merged[kv] = false;
            }
        }
        catch
        {
            /* */
        }

        return merged;
    }

    public static string BuildMessage(
        string kind,
        string? title,
        string? body,
        string? userName,
        string? tableName,
        string? hallName,
        string? timeHHmm,
        DateTime whenLocal)
    {
        var cleanBody = StripInternalTags(body);
        var titleUp = (title ?? string.Empty).Trim().ToUpperInvariant();
        var user = string.IsNullOrWhiteSpace(userName) ? "—" : userName.Trim();
        var tbl = tableName?.Trim() ?? string.Empty;
        var hall = hallName?.Trim() ?? string.Empty;
        var timePart = string.IsNullOrWhiteSpace(timeHHmm)
            ? whenLocal.ToString("HH:mm", CultureInfo.InvariantCulture)
            : timeHHmm.Trim();
        var head = $"{whenLocal.Day:00}.{whenLocal.Month:00}.{whenLocal.Year} -- {timePart}";

        string TableLoc()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(hall)) parts.Add(hall);
            if (!string.IsNullOrEmpty(tbl)) parts.Add(tbl);
            return parts.Count == 0 ? string.Empty : $" {string.Join(" — ", parts)} —";
        }

        string Tail(string s)
        {
            var x = (s ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(x)) return string.Empty;
            if (!x.EndsWith('.')) x += ".";
            return " " + x;
        }

        return kind switch
        {
            "newCheck" => $"{head}\n{user} ✅ {titleUp} ✅{TableLoc()}{Tail(cleanBody)}".Trim(),
            "paymentDone" => $"{head}\n{user} 💳 {titleUp} 💳{TableLoc()}{Tail(cleanBody)}".Trim(),
            "mergeChecks" => $"{head}\n{user} 🙏🏻 {titleUp} 🙏🏻{Tail(cleanBody)}".Trim(),
            "waiterChange" => BuildWaiterChange(head, user, titleUp, cleanBody),
            "productIncreaseBeforeKitchen" or "productIncreaseAfterKitchen" =>
                $"{head}\n{user} ➕ {titleUp} ➕{(string.IsNullOrEmpty(tbl) ? string.Empty : $" {tbl} —")}{Tail(cleanBody)}".Trim(),
            "productDecreaseBeforeKitchen" or "productDecreaseAfterKitchen" =>
                $"{head}\n{user} ❗ {titleUp} ❗{(string.IsNullOrEmpty(tbl) ? string.Empty : $" {tbl} —")}{Tail(cleanBody)}".Trim(),
            "priceChange" => $"{head}\n{user} 🟡 {titleUp} 🟡{Tail(cleanBody)}".Trim(),
            "guestCount" => $"{head}\n{user} 👥 {titleUp} 👥{Tail(cleanBody)}".Trim(),
            "discount" => $"{head}\n{user} 📉 {titleUp} 📉{Tail(cleanBody)}".Trim(),
            "serviceFee" => $"{head}\n{user} ⚙️ {titleUp} ⚙️{Tail(cleanBody)}".Trim(),
            "shiftStart" => $"{head}\n{user} 🟢 {titleUp} 🟢{Tail(cleanBody)}".Trim(),
            "shiftEnd" => $"{head}\n{user} 🔴 {titleUp} 🔴{Tail(cleanBody)}".Trim(),
            "archiveUpdate" => $"{head}\n{user} 🔄 {titleUp} 🔄{Tail(cleanBody)}".Trim(),
            "depositChange" => $"{head}\n{user} 💰 {titleUp} 💰{Tail(cleanBody)}".Trim(),
            "tableTransfer" => $"{head}\n{user} 🔁 {titleUp} 🔁{Tail(cleanBody)}".Trim(),
            _ => $"{head}\n{user} 📣 {titleUp}{Tail(cleanBody)}".Trim(),
        };
    }

    private static string BuildWaiterChange(string head, string user, string titleUp, string body)
    {
        var m = WaiterChangeRe.Match(body);
        if (m.Success)
            return $"{head}\n{user} 🧑‍🧒{titleUp} 🧑‍🧒: «{m.Groups[1].Value.Trim()}» → «{m.Groups[2].Value.Trim()}»".Trim();
        var tail = string.IsNullOrWhiteSpace(body) ? string.Empty : (body.EndsWith('.') ? " " + body : " " + body + ".");
        return $"{head}\n{user} 🧑‍🧒{titleUp} 🧑‍🧒{tail}".Trim();
    }

    private static string NormAuditText(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.ToUpperInvariant()
            .Replace("İ", "I", StringComparison.Ordinal)
            .Replace("İ", "I", StringComparison.Ordinal)
            .Replace("Ə", "E", StringComparison.Ordinal)
            .Replace("ı", "I", StringComparison.Ordinal)
            .Replace("Ç", "C", StringComparison.Ordinal)
            .Replace("Ş", "S", StringComparison.Ordinal)
            .Replace("Ö", "O", StringComparison.Ordinal)
            .Replace("Ü", "U", StringComparison.Ordinal)
            .Replace("Ğ", "G", StringComparison.Ordinal)
            .Replace("·", " ", StringComparison.Ordinal)
            .Replace("\u0130", "I", StringComparison.Ordinal)
            .Replace("\u0131", "I", StringComparison.Ordinal);
    }

    private static int? ParseAfterKitchenTag(string body)
    {
        var m = AfterKitchenTagRe.Match(body);
        if (!m.Success) return null;
        return m.Groups[1].Value == "1" ? 1 : 0;
    }

    private static bool HasPriceChange(string? description)
    {
        var m = PriceArrowRe.Match(NormAuditText(description));
        if (!m.Success) return false;
        if (!double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var a)) return false;
        if (!double.TryParse(m.Groups[2].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var b)) return false;
        return Math.Abs(a - b) > 0.0001;
    }

    private static string? QuantityDirection(string? description)
    {
        var m = QtyArrowRe.Match(NormAuditText(description));
        if (!m.Success) return null;
        if (!double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var a)) return null;
        if (!double.TryParse(m.Groups[2].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var b)) return null;
        if (Math.Abs(a - b) < 0.0001) return null;
        return b > a ? "up" : "down";
    }
}
