using System.Net.Sockets;
using System.Text;
using BusinessLayer.DTOs.Kitchen;
using BusinessLayer.Printing;
using Domain.Common.Entities;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace BusinessLayer.Services.Implementations;

public interface ITcpPrinterService
{
    Task<bool> SendToPrinterAsync(string ipAddress, int port, byte[] data);
    byte[] GenerateKitchenEscPos(
        string? receiptDesignSettingsJson,
        string workshopName,
        string hallName,
        string tableName,
        string waiterName,
        DateTime? openTime,
        List<KitchenPrintItemDto> items,
        string beepMode = "default");
    byte[] GenerateKassaReceiptEscPos(Company company, OrderHeader order, List<OrderDetail> details);
    byte[] GenerateShiftReportEscPos(Company company, CashShift shift, decimal totalCash, decimal totalCard, decimal totalRevenue, int orderCount);
}

public class TcpPrinterService : ITcpPrinterService
{
    private readonly ILogger<TcpPrinterService> _logger;

    public TcpPrinterService(ILogger<TcpPrinterService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> SendToPrinterAsync(string ipAddress, int port, byte[] data)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ipAddress, port);

            if (await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask)
            {
                await connectTask;
                using var stream = client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
                return true;
            }

            _logger.LogWarning("Printer connection timeout: {IP}", ipAddress);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to print to {IP}", ipAddress);
            return false;
        }
    }

    public byte[] GenerateKitchenEscPos(
        string? receiptDesignSettingsJson,
        string workshopName,
        string hallName,
        string tableName,
        string waiterName,
        DateTime? openTime,
        List<KitchenPrintItemDto> items,
        string beepMode = "default")
    {
        return KitchenEscPosRenderer.Render(
            receiptDesignSettingsJson,
            workshopName,
            hallName,
            tableName,
            waiterName,
            openTime,
            items,
            beepMode);
    }

    public byte[] GenerateKassaReceiptEscPos(Company company, OrderHeader order, List<OrderDetail> details)
    {
        var ctx = KassaReceiptContextFactory.From(company, order, details);
        return KassaEscPosRenderer.Render(company.ReceiptDesignSettingsJson, ctx);
    }

    public byte[] GenerateShiftReportEscPos(
        Company company,
        CashShift shift,
        decimal totalCash,
        decimal totalCard,
        decimal totalRevenue,
        int orderCount)
    {
        var ms = new MemoryStream();

        byte[] AlignCenter = { 0x1B, 0x61, 0x01 };
        byte[] AlignLeft = { 0x1B, 0x61, 0x00 };
        byte[] FontDouble = { 0x1D, 0x21, 0x11 };
        byte[] FontNormal = { 0x1D, 0x21, 0x00 };
        byte[] FontBoldOn = { 0x1B, 0x45, 0x01 };
        byte[] FontBoldOff = { 0x1B, 0x45, 0x00 };
        byte[] Cut = { 0x1D, 0x56, 0x41, 0x03 };
        byte[] Initialize = { 0x1B, 0x40 };

        void Write(byte[] bytes) => ms.Write(bytes, 0, bytes.Length);
        void WriteText(string text, bool newline = true)
        {
            var cleaned = EscPosHelpers.CleanAzChars(text);
            var bytes = Encoding.ASCII.GetBytes(cleaned + (newline ? "\n" : ""));
            ms.Write(bytes, 0, bytes.Length);
        }

        Write(Initialize);
        Write(AlignCenter);
        Write(FontDouble);
        WriteText("NOVBE HESABATI");
        Write(FontNormal);
        WriteText(company.NameAz);
        WriteText("--------------------------------");

        Write(AlignLeft);
        WriteText($"ACILIS: {shift.StartTime:dd.MM.yyyy HH:mm}");
        if (shift.EndTime.HasValue) WriteText($"BAGLANIS: {shift.EndTime:dd.MM.yyyy HH:mm}");
        WriteText($"MESUL: {shift.LastModifiedBy ?? "Admin"}");
        WriteText("--------------------------------");

        void WriteLine(string label, string value)
        {
            int pad = 32 - label.Length - value.Length;
            if (pad < 1) pad = 1;
            WriteText(label + new string(' ', pad) + value);
        }

        WriteLine("CEK SAYI:", orderCount.ToString());
        WriteLine("ILKIN DEPOZIT:", $"{shift.OpeningDepositAmount:0.00} AZN");
        WriteText("--------------------------------");

        Write(FontBoldOn);
        WriteLine("NAGD SATIS:", $"{totalCash:0.00} AZN");
        WriteLine("KART SATIS:", $"{totalCard:0.00} AZN");
        WriteText("--------------------------------");

        Write(FontDouble);
        WriteLine("TOPLAM:", $"{totalRevenue:0.00} AZN");
        Write(FontNormal);
        Write(FontBoldOff);

        Write(AlignCenter);
        WriteText("--------------------------------");
        WriteText($"CAP TARIXI: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        Write(new byte[] { 0x0A, 0x0A, 0x0A, 0x0A });
        Write(Cut);

        return ms.ToArray();
    }
}
