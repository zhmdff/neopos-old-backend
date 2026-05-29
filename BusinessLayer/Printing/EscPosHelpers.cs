using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BusinessLayer.Printing;

internal static class EscPosHelpers
{
    public const int DefaultWidth = 48;

    public static string CleanAzChars(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text
            .Replace('×', 'X').Replace('✕', 'X').Replace('✖', 'X').Replace('⋅', 'X')
            .Replace("ə", "e").Replace("Ə", "E")
            .Replace("ç", "c").Replace("Ç", "C")
            .Replace("ğ", "g").Replace("Ğ", "G")
            .Replace("İ", "I").Replace("ı", "i")
            .Replace("ö", "o").Replace("Ö", "O")
            .Replace("ş", "s").Replace("Ş", "S")
            .Replace("ü", "u").Replace("Ü", "U")
            .Replace("₼", "AZN")
            .Replace('\u00A0', ' ');
    }

    public static string SafeUpper(string? text) => CleanAzChars(text).ToUpperInvariant();

    public static string FormatMoney(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    public static string FormatQty(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 1e-6) return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static string FormatCheckNumber(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (string.IsNullOrEmpty(s)) return "";
        if (s.StartsWith("CH-", StringComparison.OrdinalIgnoreCase)) s = s[3..].Trim();
        return string.IsNullOrEmpty(s) ? "" : $"Cek No : {s}";
    }

    public static string FormatDateTimeDdMmYyyyHm(DateTime dt)
        => dt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string FormatOpenCloseTime(DateTime? value)
    {
        if (!value.HasValue) return "";
        return FormatDateTimeDdMmYyyyHm(value.Value);
    }

    public static string NormalizeTableLabel(string? tableUpper)
    {
        var t = SafeUpper(tableUpper).Trim();
        if (string.IsNullOrEmpty(t)) return "";
        var cleaned = Regex.Replace(t, @"^MASA\s*:?\s*", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"^TABLE\s*:?\s*", "", RegexOptions.IgnoreCase).Trim();
        return string.IsNullOrEmpty(cleaned) ? t : cleaned;
    }

    public static string PickPayMethodLabel(decimal paidCash, decimal paidCard, string? customName)
    {
        if (!string.IsNullOrWhiteSpace(customName)) return customName.Trim();
        var cashOk = paidCash > 0;
        var cardOk = paidCard > 0;
        if (cashOk && cardOk) return "NAGD + KART";
        if (cardOk) return "KART";
        if (cashOk) return "NAGD";
        return "";
    }
}

internal sealed class EscPosBuffer
{
    private readonly List<byte> _bytes = [];

    public void Write(params byte[] data) => _bytes.AddRange(data);

    public void WriteInit() => Write(0x1B, 0x40);

    public void WriteAlign(string? align)
    {
        var a = (align ?? "left").Trim().ToLowerInvariant();
        Write(a switch
        {
            "center" => new byte[] { 0x1B, 0x61, 0x01 },
            "right" => new byte[] { 0x1B, 0x61, 0x02 },
            _ => new byte[] { 0x1B, 0x61, 0x00 },
        });
    }

    public void WriteSize(string? size)
    {
        var s = (size ?? "sm").Trim().ToLowerInvariant();
        Write(s switch
        {
            "lg" => new byte[] { 0x1D, 0x21, 0x11 },
            "md" => new byte[] { 0x1D, 0x21, 0x10 },
            _ => new byte[] { 0x1D, 0x21, 0x00 },
        });
    }

    public void WriteBold(bool on) => Write(on ? new byte[] { 0x1B, 0x45, 0x01 } : new byte[] { 0x1B, 0x45, 0x00 });

    public void WriteFontA() => Write(0x1B, 0x4D, 0x00);

    public void WriteFontB() => Write(0x1B, 0x4D, 0x01);

    public void WriteSizeKassa(string? size, ref int lineWidth)
    {
        var x = (size ?? "sm").Trim().ToLowerInvariant();
        if (x == "xs")
        {
            WriteFontB();
            lineWidth = 64;
            Write(0x1D, 0x21, 0x00);
            return;
        }
        WriteFontA();
        lineWidth = EscPosHelpers.DefaultWidth;
        Write(x switch
        {
            "lg" => new byte[] { 0x1D, 0x21, 0x11 },
            "md" => new byte[] { 0x1D, 0x21, 0x01 },
            _ => new byte[] { 0x1D, 0x21, 0x00 },
        });
    }

    public void WriteLine(string? text, int maxWidth = EscPosHelpers.DefaultWidth)
    {
        var ln = EscPosHelpers.SafeUpper(text?.Trim());
        if (string.IsNullOrEmpty(ln)) return;
        if (ln.Length > maxWidth) ln = ln[..maxWidth];
        _bytes.AddRange(Encoding.ASCII.GetBytes(ln));
        _bytes.Add(0x0A);
    }

    public void WriteRawLine(string text)
    {
        _bytes.AddRange(Encoding.ASCII.GetBytes(text));
        _bytes.Add(0x0A);
    }

    public void WriteCut() => Write(0x1D, 0x56, 0x41, 0x03);

    public void WriteBeep(string? beepMode)
    {
        var mode = (beepMode ?? "default").Trim().ToLowerInvariant();
        if (mode == "off" || mode == "default") return;
        if (mode == "long")
        {
            Write(0x1B, 0x42, 0x02, 0x02);
            Write(0x1B, 0x42, 0x02, 0x02);
        }
        else if (mode == "short")
        {
            Write(0x1B, 0x42, 0x02, 0x02);
        }
    }

    public byte[] ToArray() => _bytes.ToArray();
}
