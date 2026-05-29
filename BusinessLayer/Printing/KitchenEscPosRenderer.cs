using BusinessLayer.DTOs.Kitchen;

namespace BusinessLayer.Printing;

public static class KitchenEscPosRenderer
{
    public static List<ReceiptDesignSection> DefaultKitchenSections() =>
    [
        new() { Key = "printDate", Enabled = true, Size = "sm", Thickness = "bold", Align = "center" },
        new() { Key = "workshopName", Enabled = true, Size = "lg", Thickness = "bold", Align = "center" },
        new() { Key = "waiter", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "hall", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "table", Enabled = true, Size = "md", Thickness = "bold", Align = "left" },
        new() { Key = "openTime", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "items", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
    ];

    public static byte[] Render(
        string? receiptDesignSettingsJson,
        string workshopName,
        string hallName,
        string tableName,
        string waiterName,
        DateTime? openTime,
        IReadOnlyList<KitchenPrintItemDto> items,
        string beepMode = "default")
    {
        var root = ReceiptDesignParser.Parse(receiptDesignSettingsJson);
        var sections = ReceiptDesignParser.NormalizeKitchenSections(root);
        var fontMode = root?.Kitchen?.Lan?.EscPosCompact == true ? "normal" : "double";
        var printDate = EscPosHelpers.FormatDateTimeDdMmYyyyHm(DateTime.Now);
        var openTimeStr = EscPosHelpers.FormatOpenCloseTime(openTime);

        var buf = new EscPosBuffer();
        buf.WriteInit();
        buf.WriteBeep(beepMode);

        var defaultItemEsc = fontMode == "normal"
            ? new byte[] { 0x1D, 0x21, 0x00 }
            : new byte[] { 0x1D, 0x21, 0x11 };

        foreach (var row in sections)
        {
            if (!row.Enabled) continue;

            if (string.Equals(row.Key, "items", StringComparison.OrdinalIgnoreCase))
            {
                WriteItemsSection(buf, row, items, defaultItemEsc);
                continue;
            }

            var line = SectionLineText(row.Key, workshopName, hallName, tableName, waiterName, printDate, openTimeStr);
            if (string.IsNullOrEmpty(line)) continue;

            buf.WriteAlign(row.Align);
            buf.WriteSize(row.Size);
            buf.WriteBold(string.Equals(row.Thickness, "bold", StringComparison.OrdinalIgnoreCase));
            buf.WriteLine(line);
            buf.WriteBold(false);
            buf.Write(0x1D, 0x21, 0x00);
            buf.WriteAlign("left");
        }

        buf.Write(0x1D, 0x21, 0x00);
        buf.WriteRawLine("");
        buf.WriteRawLine("");
        buf.WriteCut();
        return buf.ToArray();
    }

    private static string SectionLineText(
        string key,
        string workshopName,
        string hallName,
        string tableName,
        string waiterName,
        string printDate,
        string openTimeStr)
    {
        switch (key.ToLowerInvariant())
        {
            case "printdate":
                return string.IsNullOrEmpty(printDate) ? "" : printDate;
            case "workshopname":
                return string.IsNullOrEmpty(workshopName) ? "" : EscPosHelpers.SafeUpper(workshopName);
            case "waiter":
                return string.IsNullOrEmpty(waiterName) ? "" : $"OFSIYANT: {EscPosHelpers.SafeUpper(waiterName)}";
            case "hall":
                return string.IsNullOrEmpty(hallName) ? "" : $"ZAL: {EscPosHelpers.SafeUpper(hallName)}";
            case "table":
                return string.IsNullOrEmpty(tableName) ? "" : $"MASA: {EscPosHelpers.SafeUpper(tableName)}";
            case "opentime":
                return string.IsNullOrEmpty(openTimeStr) ? "" : $"ACILIS: {openTimeStr}";
            default:
                return "";
        }
    }

    private static void WriteItemsSection(
        EscPosBuffer buf,
        ReceiptDesignSection row,
        IReadOnlyList<KitchenPrintItemDto> items,
        byte[] defaultItemEsc)
    {
        buf.Write(0x1D, 0x21, 0x00);
        buf.WriteAlign("center");
        buf.WriteRawLine("============================");
        buf.WriteAlign("left");

        var itemEsc = ItemEscFromRow(row, defaultItemEsc);

        foreach (var item in items)
        {
            buf.WriteAlign("left");
            buf.Write(itemEsc);

            var name = EscPosHelpers.SafeUpper(item.Name);
            var st = NormKitchenStatus(item.Status);
            var isFirstTime = Math.Abs(item.Total - item.Qty) < 1e-6;
            var isCancelled = st is "LEGVE" or "CANCELLED";
            var isReduced = (st is "AZALDI" or "REDUCED") && Math.Abs(item.Qty) > 1e-9;

            if (isCancelled) buf.WriteRawLine("[LEGV OLUNDU]");
            else if (isReduced) buf.WriteRawLine("[AZALDI]");
            else if (st == "YENI" && !isFirstTime) buf.WriteRawLine("[ARTDI]");

            if (isCancelled) buf.WriteRawLine(name);
            else
            {
                var sign = st == "YENI" && !isFirstTime ? "+" : isReduced ? "-" : "";
                buf.WriteRawLine($"{name}  {sign}{EscPosHelpers.FormatQty(item.Qty)}");
            }

            buf.WriteAlign("left");
            WriteKitchenItemNotes(buf, item, isCancelled);
            buf.Write(0x1D, 0x21, 0x00);
            buf.WriteRawLine("------------------------");
        }
    }

    private static byte[] ItemEscFromRow(ReceiptDesignSection row, byte[] defaultBuf)
    {
        return (row.Size ?? "sm").Trim().ToLowerInvariant() switch
        {
            "lg" => new byte[] { 0x1D, 0x21, 0x11 },
            "md" => new byte[] { 0x1D, 0x21, 0x10 },
            "xs" or "sm" => new byte[] { 0x1D, 0x21, 0x00 },
            _ => defaultBuf,
        };
    }

    private static void WriteKitchenItemNotes(EscPosBuffer buf, KitchenPrintItemDto item, bool isCancelled)
    {
        if (isCancelled) return;

        var compRaw = (item.CompositionNote ?? "").Trim();
        if (!string.IsNullOrEmpty(compRaw))
        {
            var comp = EscPosHelpers.SafeUpper(compRaw
                .Replace('×', 'X')
                .Replace("×", "X", StringComparison.OrdinalIgnoreCase));
            foreach (var line in comp.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var t = line.Trim();
                if (!string.IsNullOrEmpty(t)) buf.WriteRawLine($"Terkib : {t}");
            }
        }

        var noteRaw = (item.Note ?? "").Trim();
        if (!string.IsNullOrEmpty(noteRaw))
            buf.WriteRawLine($"Qeyd : {EscPosHelpers.SafeUpper(noteRaw)}");
    }

    private static string NormKitchenStatus(string? s)
    {
        var t = EscPosHelpers.CleanAzChars(s ?? "").ToUpperInvariant();
        return t;
    }
}
