namespace BusinessLayer.Printing;

public static class KassaEscPosRenderer
{
    public static List<ReceiptDesignSection> DefaultCashierSections() =>
    [
        new() { Key = "companyName", Enabled = true, Size = "lg", Thickness = "bold", Align = "center" },
        new() { Key = "checkNumber", Enabled = false, Size = "sm", Thickness = "bold", Align = "center" },
        new() { Key = "printDate", Enabled = false, Size = "sm", Thickness = "bold", Align = "center" },
        new() { Key = "waiter", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "customer", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "guests", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "hall", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "table", Enabled = true, Size = "md", Thickness = "bold", Align = "left" },
        new() { Key = "openTime", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "closeTime", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "items", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "itemsTotal", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "service", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "deposit", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "discount", Enabled = true, Size = "sm", Thickness = "normal", Align = "left" },
        new() { Key = "grandTotal", Enabled = true, Size = "lg", Thickness = "bold", Align = "left" },
        new() { Key = "payment", Enabled = true, Size = "sm", Thickness = "bold", Align = "left" },
        new() { Key = "extra", Enabled = true, Size = "sm", Thickness = "bold", Align = "center" },
    ];

    public static byte[] Render(string? receiptDesignSettingsJson, KassaReceiptContext ctx)
    {
        var root = ReceiptDesignParser.Parse(receiptDesignSettingsJson);
        var sections = ReceiptDesignParser.NormalizeCashierSections(root);
        return RenderSections(sections, ctx);
    }

    private static byte[] RenderSections(List<ReceiptDesignSection> sections, KassaReceiptContext ctx)
    {
        var buf = new EscPosBuffer();
        var lineWidth = EscPosHelpers.DefaultWidth;
        buf.WriteInit();

        var company = EscPosHelpers.SafeUpper(ctx.CompanyName);
        var checkLine = EscPosHelpers.SafeUpper(EscPosHelpers.FormatCheckNumber(ctx.CheckNumber));
        var dtLine = EscPosHelpers.SafeUpper(EscPosHelpers.FormatDateTimeDdMmYyyyHm(DateTime.Now));
        var waiter = EscPosHelpers.SafeUpper(ctx.WaiterName);
        var hall = EscPosHelpers.SafeUpper(ctx.HallName);
        var tableCore = EscPosHelpers.NormalizeTableLabel(ctx.TableName);
        var openTime = EscPosHelpers.FormatOpenCloseTime(ctx.OpenTime);
        var closeTime = EscPosHelpers.FormatOpenCloseTime(ctx.CloseTime);
        var thankYou = string.IsNullOrWhiteSpace(ctx.ThankYouText)
            ? "TESEKKUR EDIRIK"
            : EscPosHelpers.SafeUpper(ctx.ThankYouText);
        var extra = (ctx.ExtraText ?? "").Trim();
        var foodTotal = EscPosHelpers.FormatMoney(ctx.FoodTotal);
        var serviceAmount = EscPosHelpers.FormatMoney(ctx.ServiceAmount);
        var discountAmount = EscPosHelpers.FormatMoney(ctx.DiscountAmount);
        var grandTotal = EscPosHelpers.FormatMoney(ctx.GrandTotal);
        var depositDiff = ctx.DepositLimit - ctx.FoodTotal;

        foreach (var row in sections)
        {
            if (!row.Enabled) continue;
            var key = row.Key.ToLowerInvariant();

            switch (key)
            {
                case "companyname":
                    ApplyStyle(buf, row, ref lineWidth);
                    WriteWrapped(buf, company, WrapWidth(row.Size, lineWidth));
                    ResetStyle(buf, ref lineWidth);
                    break;
                case "checknumber":
                    ApplyStyle(buf, row, ref lineWidth);
                    if (!string.IsNullOrEmpty(checkLine)) buf.WriteLine(checkLine, lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    break;
                case "printdate":
                    ApplyStyle(buf, row, ref lineWidth);
                    if (!string.IsNullOrEmpty(dtLine)) buf.WriteLine(dtLine, lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    break;
                case "waiter":
                    ApplyStyle(buf, row, ref lineWidth);
                    if (!string.IsNullOrEmpty(waiter)) buf.WriteLine($"OFISIANT: {waiter}", lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    break;
                case "customer":
                    ApplyStyle(buf, row, ref lineWidth);
                    WriteCustomer(buf, ctx, lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    break;
                case "guests":
                    ApplyStyle(buf, row, ref lineWidth);
                    if (ctx.GuestCount is > 0) buf.WriteLine($"QONAQ SAYI: {ctx.GuestCount}", lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    break;
                case "hall":
                    ApplyStyle(buf, row, ref lineWidth);
                    if (!string.IsNullOrEmpty(hall)) buf.WriteLine($"ZAL: {hall}", lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    break;
                case "table":
                    ApplyStyle(buf, row, ref lineWidth);
                    if (!string.IsNullOrEmpty(tableCore)) buf.WriteLine($"MASA: {tableCore}", lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    WriteSplitLabel(buf, ctx.SplitLabel, lineWidth);
                    break;
                case "opentime":
                    ApplyStyle(buf, row, ref lineWidth);
                    if (!string.IsNullOrEmpty(openTime)) buf.WriteLine($"ACILIS TARIXI: {openTime}", lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    break;
                case "closetime":
                    ApplyStyle(buf, row, ref lineWidth);
                    if (!string.IsNullOrEmpty(closeTime)) buf.WriteLine($"BAGLANIS TARIXI: {closeTime}", lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    break;
                case "items":
                    RenderItemsTable(buf, row, ctx.Items, ref lineWidth);
                    break;
                case "itemstotal":
                    ApplyStyle(buf, row, ref lineWidth);
                    if (ctx.FoodTotal > 0) WriteKv(buf, "CEM:", $"{foodTotal} AZN", lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    break;
                case "service":
                    ApplyStyle(buf, row, ref lineWidth);
                    if (ctx.ServiceAmount > 0) WriteKv(buf, "SERVIS HAQQI:", $"+{serviceAmount} AZN", lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    break;
                case "deposit":
                    ApplyStyle(buf, row, ref lineWidth);
                    if (depositDiff > 0) WriteKv(buf, "DEPOZIT:", $"+{EscPosHelpers.FormatMoney(depositDiff)} AZN", lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    break;
                case "discount":
                    ApplyStyle(buf, row, ref lineWidth);
                    if (ctx.DiscountAmount > 0) WriteKv(buf, "ENDIRIM:", $"-{discountAmount} AZN", lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    break;
                case "grandtotal":
                    RenderGrandTotal(buf, row, grandTotal, ref lineWidth);
                    break;
                case "payment":
                    RenderPayment(buf, row, ctx, grandTotal, ref lineWidth);
                    break;
                case "extra":
                    ApplyStyle(buf, row, ref lineWidth);
                    buf.WriteLine(string.IsNullOrEmpty(extra) ? thankYou : EscPosHelpers.SafeUpper(extra), lineWidth);
                    ResetStyle(buf, ref lineWidth);
                    break;
            }
        }

        buf.WriteRawLine("");
        buf.WriteAlign("center");
        buf.Write(0x1D, 0x21, 0x00);
        buf.WriteBold(false);
        buf.WriteLine("POWERED BY NEOPOS", lineWidth);
        buf.WriteAlign("left");
        buf.WriteRawLine("");
        buf.WriteRawLine("");
        buf.WriteCut();
        return buf.ToArray();
    }

    private static void ApplyStyle(EscPosBuffer buf, ReceiptDesignSection row, ref int lineWidth)
    {
        buf.WriteAlign(row.Align);
        buf.WriteBold(string.Equals(row.Thickness, "bold", StringComparison.OrdinalIgnoreCase));
        buf.WriteSizeKassa(row.Size, ref lineWidth);
    }

    private static void ResetStyle(EscPosBuffer buf, ref int lineWidth)
    {
        buf.Write(0x1D, 0x21, 0x00);
        buf.WriteBold(false);
        lineWidth = EscPosHelpers.DefaultWidth;
        buf.WriteFontA();
    }

    private static int WrapWidth(string? size, int lineWidth)
    {
        return string.Equals(size, "lg", StringComparison.OrdinalIgnoreCase)
            ? Math.Max(12, lineWidth / 2)
            : lineWidth;
    }

    private static void WriteWrapped(EscPosBuffer buf, string text, int wrapW)
    {
        foreach (var ln in WrapWords(text, wrapW))
            buf.WriteLine(ln, wrapW);
    }

    private static void WriteCustomer(EscPosBuffer buf, KassaReceiptContext ctx, int lineWidth)
    {
        var cn = (ctx.CustomerName ?? "").Trim();
        var cp = (ctx.CustomerPhone ?? "").Trim();
        var ca = (ctx.CustomerAddress ?? "").Trim();
        if (string.IsNullOrEmpty(cn) && string.IsNullOrEmpty(cp) && string.IsNullOrEmpty(ca)) return;
        var first = string.Join(" ", new[] { cn, cp }.Where(s => !string.IsNullOrEmpty(s)));
        if (!string.IsNullOrEmpty(first))
        {
            foreach (var ln in WrapWords(EscPosHelpers.SafeUpper(first), lineWidth))
                buf.WriteLine(ln, lineWidth);
        }
        if (!string.IsNullOrEmpty(ca))
        {
            foreach (var ln in WrapWords(EscPosHelpers.SafeUpper(ca), lineWidth))
                buf.WriteLine(ln, lineWidth);
        }
    }

    private static void WriteSplitLabel(EscPosBuffer buf, string? splitLabel, int lineWidth)
    {
        var split = EscPosHelpers.SafeUpper(splitLabel).Trim();
        if (string.IsNullOrEmpty(split)) return;
        buf.WriteAlign("center");
        buf.WriteBold(true);
        foreach (var ln in WrapWords(split, lineWidth))
            buf.WriteLine(ln, lineWidth);
        buf.WriteBold(false);
        buf.WriteAlign("left");
    }

    private static void RenderItemsTable(
        EscPosBuffer buf,
        ReceiptDesignSection row,
        IReadOnlyList<KassaReceiptLineItem> items,
        ref int lineWidth)
    {
        buf.WriteAlign("left");
        buf.WriteBold(string.Equals(row.Thickness, "bold", StringComparison.OrdinalIgnoreCase));
        buf.WriteSizeKassa(row.Size, ref lineWidth);

        var colName = lineWidth >= 60 ? 30 : 22;
        var colQty = 6;
        var colPrice = lineWidth >= 60 ? 14 : 10;
        var colTotal = lineWidth - (colName + colQty + colPrice);

        buf.WriteRawLine(new string('-', lineWidth));
        buf.WriteBold(true);
        var hdr = PadRight("MEHSUL", colName) + PadLeft("MIQ", colQty) + PadLeft("QIYM", colPrice) + PadLeft("CEM", colTotal);
        buf.WriteRawLine(hdr.Length > lineWidth ? hdr[..lineWidth] : hdr);
        buf.WriteBold(false);
        buf.WriteRawLine(new string('-', lineWidth));

        foreach (var it in items)
        {
            var name = EscPosHelpers.SafeUpper(it.Name);
            var qty = EscPosHelpers.FormatQty(it.Qty);
            var price = EscPosHelpers.FormatMoney(it.Price);
            var total = EscPosHelpers.FormatMoney(it.Total);
            var note = (it.Note ?? "").Trim();

            var nameLines = WrapWords(name, colName).Where(x => !string.IsNullOrEmpty(x)).ToList();
            var firstName = nameLines.Count > 0 ? nameLines[0] : "";
            var row1 = PadRight(firstName, colName) + PadLeft(qty, colQty) + PadLeft(price, colPrice) + PadLeft(total, colTotal);
            buf.WriteBold(true);
            buf.WriteRawLine(row1.Length > lineWidth ? row1[..lineWidth] : row1);
            buf.WriteBold(false);

            foreach (var ln in nameLines.Skip(1))
                buf.WriteRawLine(PadRight(ln, colName));

            if (!string.IsNullOrEmpty(note))
            {
                buf.WriteBold(true);
                buf.WriteRawLine("** QEYD **");
                buf.WriteBold(false);
                foreach (var ln in WrapWords(EscPosHelpers.SafeUpper(note), lineWidth))
                    buf.WriteRawLine(ln);
            }

            buf.WriteRawLine(new string('-', lineWidth));
        }

        buf.Write(0x1D, 0x21, 0x00);
        buf.WriteBold(false);
        lineWidth = EscPosHelpers.DefaultWidth;
        buf.WriteFontA();
    }

    private static void RenderGrandTotal(EscPosBuffer buf, ReceiptDesignSection row, string grandTotal, ref int lineWidth)
    {
        if (string.IsNullOrEmpty(grandTotal)) return;
        buf.WriteRawLine(new string('=', lineWidth));
        buf.WriteRawLine("");
        ApplyStyle(buf, row, ref lineWidth);
        var yekun = $"YEKUN: {grandTotal} AZN";
        var wrapW = string.Equals(row.Size, "lg", StringComparison.OrdinalIgnoreCase)
            ? Math.Max(12, lineWidth / 2)
            : lineWidth;
        foreach (var ln in WrapWords(EscPosHelpers.SafeUpper(yekun), wrapW))
            buf.WriteLine(ln, wrapW);
        ResetStyle(buf, ref lineWidth);
        buf.WriteAlign("left");
        buf.WriteRawLine("");
    }

    private static void RenderPayment(
        EscPosBuffer buf,
        ReceiptDesignSection row,
        KassaReceiptContext ctx,
        string grandTotal,
        ref int lineWidth)
    {
        if (!ctx.IsPaid) return;
        ApplyStyle(buf, row, ref lineWidth);
        buf.WriteRawLine("");
        var custom = (ctx.CustomPaymentMethodName ?? "").Trim();
        if (!string.IsNullOrEmpty(custom))
        {
            if (ctx.PaidCash > 0 && ctx.PaidCard > 0)
            {
                WriteKv(buf, "NAGD:", $"{EscPosHelpers.FormatMoney(ctx.PaidCash)} AZN", lineWidth);
                WriteKv(buf, $"{EscPosHelpers.SafeUpper(custom)}:", $"{EscPosHelpers.FormatMoney(ctx.PaidCard)} AZN", lineWidth);
            }
            else if (ctx.PaidCard > 0 && ctx.PaidCash == 0)
                WriteKv(buf, $"{EscPosHelpers.SafeUpper(custom)}:", $"{EscPosHelpers.FormatMoney(ctx.PaidCard)} AZN", lineWidth);
            else if (ctx.PaidCash > 0 && ctx.PaidCard == 0)
                WriteKv(buf, $"{EscPosHelpers.SafeUpper(custom)}:", $"{EscPosHelpers.FormatMoney(ctx.PaidCash)} AZN", lineWidth);
            else
                WriteKv(buf, $"{EscPosHelpers.SafeUpper(custom)}:", $"{grandTotal} AZN", lineWidth);
        }
        else
        {
            if (ctx.PaidCash > 0) WriteKv(buf, "NAGD:", $"{EscPosHelpers.FormatMoney(ctx.PaidCash)} AZN", lineWidth);
            if (ctx.PaidCard > 0) WriteKv(buf, "KART:", $"{EscPosHelpers.FormatMoney(ctx.PaidCard)} AZN", lineWidth);
        }
        ResetStyle(buf, ref lineWidth);
        buf.WriteAlign("left");
    }

    private static void WriteKv(EscPosBuffer buf, string key, string value, int lineWidth)
    {
        var left = key.Trim();
        var right = value.Trim();
        var line = PadRight(left, Math.Max(1, lineWidth - right.Length)) + right;
        buf.WriteLine(line, lineWidth);
    }

    private static string PadRight(string s, int len)
    {
        if (s.Length >= len) return s[..len];
        return s + new string(' ', len - s.Length);
    }

    private static string PadLeft(string s, int len)
    {
        if (s.Length >= len) return s[^len..];
        return new string(' ', len - s.Length) + s;
    }

    private static List<string> WrapWords(string text, int width)
    {
        var t = (text ?? "").Trim();
        if (string.IsNullOrEmpty(t)) return [""];
        var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var cur = "";
        foreach (var w in words)
        {
            if (w.Length > width)
            {
                if (!string.IsNullOrEmpty(cur)) { lines.Add(cur); cur = ""; }
                for (var i = 0; i < w.Length; i += width)
                    lines.Add(w.Substring(i, Math.Min(width, w.Length - i)));
                continue;
            }
            var test = string.IsNullOrEmpty(cur) ? w : $"{cur} {w}";
            if (test.Length <= width) cur = test;
            else { if (!string.IsNullOrEmpty(cur)) lines.Add(cur); cur = w; }
        }
        if (!string.IsNullOrEmpty(cur)) lines.Add(cur);
        return lines.Count > 0 ? lines : [""];
    }
}
