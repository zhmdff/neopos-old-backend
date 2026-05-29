using System.Text.RegularExpressions;

namespace BusinessLayer.Printing;

/// <summary>
/// LAN printer targets: "192.168.0.10", "192.168.0.10:9100", "192.168.0.10|beep=short".
/// </summary>
public static partial class PrinterTargetParser
{
    private static readonly Regex Ipv4WithPort = new(
        @"^((?:\d{1,3}\.){3}\d{1,3}):(\d{1,5})$",
        RegexOptions.Compiled);

    public static bool TryParseNetworkTarget(
        string? printerValue,
        out string host,
        out int port,
        out string beepMode)
    {
        host = "";
        port = 9100;
        beepMode = "default";

        var raw = (printerValue ?? "").Trim();
        if (string.IsNullOrEmpty(raw)) return false;

        var segments = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0) return false;

        if (!TryParseHostPort(segments[0], out host, out port)) return false;

        for (var i = 1; i < segments.Length; i++)
        {
            var eq = segments[i].IndexOf('=');
            if (eq <= 0) continue;
            var key = segments[i][..eq].Trim().ToLowerInvariant();
            var val = segments[i][(eq + 1)..].Trim().ToLowerInvariant();
            if (key != "beep") continue;
            beepMode = val switch
            {
                "long" => "long",
                "short" => "short",
                "off" or "0" or "false" => "off",
                _ => "default",
            };
        }

        return true;
    }

    private static bool TryParseHostPort(string firstSegment, out string host, out int port)
    {
        host = "";
        port = 9100;
        var t = firstSegment.Trim();
        if (string.IsNullOrEmpty(t)) return false;

        var m = Ipv4WithPort.Match(t);
        if (m.Success)
        {
            host = m.Groups[1].Value;
            if (!int.TryParse(m.Groups[2].Value, out var p) || p <= 0 || p > 65535) p = 9100;
            port = p;
            return IsIpv4(host);
        }

        if (IsIpv4(t))
        {
            host = t;
            return true;
        }

        return false;
    }

    private static bool IsIpv4(string value)
    {
        if (!Ipv4Regex().IsMatch(value)) return false;
        foreach (var part in value.Split('.'))
        {
            if (!int.TryParse(part, out var n) || n < 0 || n > 255) return false;
        }
        return true;
    }

    [GeneratedRegex(@"^(?:\d{1,3}\.){3}\d{1,3}$")]
    private static partial Regex Ipv4Regex();
}
