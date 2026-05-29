using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BusinessLayer.Services.Implementations;

public interface ITcpPrinterService
{
    Task<bool> SendToPrinterAsync(string ipAddress, int port, byte[] data);
    byte[] GenerateKitchenEscPos(string workshopName, string hallName, string tableName, string waiterName, List<BusinessLayer.DTOs.Kitchen.KitchenPrintItemDto> items);
    byte[] GenerateKassaReceiptEscPos(Domain.Common.Entities.Company company, Domain.Entities.OrderHeader order, List<Domain.Entities.OrderDetail> details);
    byte[] GenerateShiftReportEscPos(Domain.Common.Entities.Company company, Domain.Common.Entities.CashShift shift, decimal totalCash, decimal totalCard, decimal totalRevenue, int orderCount);
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
            
            // 5 second timeout for printer connection
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

    public byte[] GenerateKitchenEscPos(string workshopName, string hallName, string tableName, string waiterName, List<BusinessLayer.DTOs.Kitchen.KitchenPrintItemDto> items)
    {
        var ms = new MemoryStream();
        
        // ESC/POS Commands
        byte[] Initialize = { 0x1B, 0x40 };
        byte[] AlignCenter = { 0x1B, 0x61, 0x01 };
        byte[] AlignLeft = { 0x1B, 0x61, 0x00 };
        byte[] FontDouble = { 0x1D, 0x21, 0x11 };
        byte[] FontNormal = { 0x1D, 0x21, 0x00 };
        byte[] Cut = { 0x1D, 0x56, 0x41, 0x03 };
        byte[] Beep = { 0x1B, 0x42, 0x02, 0x02 };

        void Write(byte[] bytes) => ms.Write(bytes, 0, bytes.Length);
        void WriteText(string text, bool newline = true)
        {
            var cleaned = CleanAzChars(text).ToUpper();
            var bytes = Encoding.ASCII.GetBytes(cleaned + (newline ? "\n" : ""));
            ms.Write(bytes, 0, bytes.Length);
        }

        Write(Initialize);
        Write(Beep);
        Write(AlignCenter);
        Write(FontDouble);
        WriteText(workshopName);
        Write(FontNormal);
        WriteText($"{hallName} / {tableName}");
        WriteText($"OFISIANT: {waiterName}");
        WriteText("================================");
        
        Write(AlignLeft);
        foreach (var item in items)
        {
            string qtyStr = item.Qty > 0 ? "+" + item.Qty : item.Qty.ToString();
            Write(FontDouble);
            WriteText($"{item.Name}  {qtyStr}");
            Write(FontNormal);
            if (!string.IsNullOrEmpty(item.Note))
            {
                WriteText($"  QEYD: {item.Note}");
            }
            if (!string.IsNullOrEmpty(item.CompositionNote))
            {
                WriteText($"  TERKIB: {item.CompositionNote}");
            }
            WriteText("--------------------------------");
        }

        Write(AlignCenter);
        WriteText($"SAAT: {DateTime.Now:HH:mm:ss}");
        Write(new byte[] { 0x0A, 0x0A, 0x0A });
        Write(Cut);

        return ms.ToArray();
    }

    public byte[] GenerateKassaReceiptEscPos(Domain.Common.Entities.Company company, Domain.Entities.OrderHeader order, List<Domain.Entities.OrderDetail> details)
    {
        var ms = new MemoryStream();
        
        byte[] Initialize = { 0x1B, 0x40 };
        byte[] AlignCenter = { 0x1B, 0x61, 0x01 };
        byte[] AlignLeft = { 0x1B, 0x61, 0x00 };
        byte[] AlignRight = { 0x1B, 0x61, 0x02 };
        byte[] FontDouble = { 0x1D, 0x21, 0x11 };
        byte[] FontNormal = { 0x1D, 0x21, 0x00 };
        byte[] FontBoldOn = { 0x1B, 0x45, 0x01 };
        byte[] FontBoldOff = { 0x1B, 0x45, 0x00 };
        byte[] Cut = { 0x1D, 0x56, 0x41, 0x03 };

        void Write(byte[] bytes) => ms.Write(bytes, 0, bytes.Length);
        void WriteText(string text, bool newline = true)
        {
            var cleaned = CleanAzChars(text);
            var bytes = Encoding.ASCII.GetBytes(cleaned + (newline ? "\n" : ""));
            ms.Write(bytes, 0, bytes.Length);
        }

        Write(Initialize);
        Write(AlignCenter);
        Write(FontDouble);
        WriteText(company.NameAz);
        Write(FontNormal);
        if (!string.IsNullOrEmpty(company.AddressAz)) WriteText(company.AddressAz);
        if (!string.IsNullOrEmpty(company.PhoneNumber1)) WriteText(company.PhoneNumber1);
        WriteText("--------------------------------");
        
        Write(AlignLeft);
        WriteText($"CEK: {order.CheckNumber}");
        WriteText($"MASA: {order.Table?.NameAz}");
        WriteText($"OFISIANT: {order.WaiterName}");
        WriteText($"TARIX: {order.OpenTime:dd.MM.yyyy HH:mm}");
        if (order.CloseTime.HasValue) WriteText($"BAGLANIS: {order.CloseTime:HH:mm}");
        WriteText("================================");

        foreach (var item in details.Where(d => d.Quantity > 0))
        {
            WriteText(item.ProductName);
            string line = $"{item.Quantity} x {item.Price:0.00}";
            string total = $"{item.TotalPrice:0.00} AZN";
            
            // Padding for price line
            int pad = 32 - line.Length - total.Length;
            if (pad < 1) pad = 1;
            WriteText(line + new string(' ', pad) + total);
        }
        WriteText("================================");

        Write(AlignRight);
        decimal subtotal = details.Sum(d => d.TotalPrice);
        WriteText($"CEMI: {subtotal:0.00} AZN");
        if (order.ServiceAmount > 0) WriteText($"XIDMET ({order.ServicePercentage}%): {order.ServiceAmount:0.00} AZN");
        if (order.DiscountAmount > 0) WriteText($"ENDIRIM: -{order.DiscountAmount:0.00} AZN");
        if (order.BehAmount > 0) WriteText($"BEH: -{order.BehAmount:0.00} AZN");
        
        Write(FontDouble);
        Write(FontBoldOn);
        WriteText($"YEKUN: {order.TotalAmount - order.BehAmount:0.00} AZN");
        Write(FontBoldOff);
        Write(FontNormal);
        
        Write(AlignCenter);
        WriteText("--------------------------------");
        WriteText(company.KassaReceiptThankYouText ?? "TESEKKUR EDIRIK!");
        Write(new byte[] { 0x0A, 0x0A, 0x0A, 0x0A });
        Write(Cut);

        return ms.ToArray();
    }

    public byte[] GenerateShiftReportEscPos(Domain.Common.Entities.Company company, Domain.Common.Entities.CashShift shift, decimal totalCash, decimal totalCard, decimal totalRevenue, int orderCount)
    {
        var ms = new MemoryStream();
        
        byte[] Initialize = { 0x1B, 0x40 };
        byte[] AlignCenter = { 0x1B, 0x61, 0x01 };
        byte[] AlignLeft = { 0x1B, 0x61, 0x00 };
        byte[] AlignRight = { 0x1B, 0x61, 0x02 };
        byte[] FontDouble = { 0x1D, 0x21, 0x11 };
        byte[] FontNormal = { 0x1D, 0x21, 0x00 };
        byte[] FontBoldOn = { 0x1B, 0x45, 0x01 };
        byte[] FontBoldOff = { 0x1B, 0x45, 0x00 };
        byte[] Cut = { 0x1D, 0x56, 0x41, 0x03 };

        void Write(byte[] bytes) => ms.Write(bytes, 0, bytes.Length);
        void WriteText(string text, bool newline = true)
        {
            var cleaned = CleanAzChars(text);
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

    private string CleanAzChars(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text
            .Replace("Ç", "C").Replace("ç", "c")
            .Replace("ə", "e").Replace("Ə", "E")
            .Replace("ğ", "g").Replace("Ğ", "G")
            .Replace("İ", "I").Replace("ı", "i")
            .Replace("ö", "o").Replace("Ö", "O")
            .Replace("ş", "s").Replace("Ş", "S")
            .Replace("ü", "u").Replace("Ü", "U")
            .Replace("₼", "AZN");
    }
}
