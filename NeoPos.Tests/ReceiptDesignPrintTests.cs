using System.Text;
using BusinessLayer.DTOs.Kitchen;
using BusinessLayer.Printing;

namespace NeoPos.Tests;

public class ReceiptDesignPrintTests
{
    [Fact]
    public void PrinterTargetParser_parses_ipv4_with_port_and_beep()
    {
        Assert.True(PrinterTargetParser.TryParseNetworkTarget(
            "192.168.1.50:9100|beep=short", out var host, out var port, out var beep));
        Assert.Equal("192.168.1.50", host);
        Assert.Equal(9100, port);
        Assert.Equal("short", beep);
    }

    [Fact]
    public void KitchenEscPos_contains_workshop_name_from_design()
    {
        const string json = """
            {
              "kitchen": {
                "sections": [
                  { "key": "workshopName", "enabled": true, "size": "lg", "thickness": "bold", "align": "center" },
                  { "key": "items", "enabled": true, "size": "sm", "thickness": "normal", "align": "left" }
                ]
              }
            }
            """;

        var bytes = KitchenEscPosRenderer.Render(
            json,
            "BAR",
            "ZAL 1",
            "MASA 5",
            "Ali",
            new DateTime(2026, 5, 30, 12, 0, 0),
            [new KitchenPrintItemDto { Name = "Cola", Qty = 2, Status = "YENI", Total = 2 }],
            "off");

        var text = Encoding.ASCII.GetString(bytes);
        Assert.Contains("BAR", text);
    }

    [Fact]
    public void KassaEscPos_uses_check_number_section_when_enabled()
    {
        const string json = """
            {
              "cashier": {
                "sections": [
                  { "key": "companyName", "enabled": true, "size": "lg", "thickness": "bold", "align": "center" },
                  { "key": "checkNumber", "enabled": true, "size": "sm", "thickness": "bold", "align": "center" }
                ]
              }
            }
            """;

        var ctx = new KassaReceiptContext
        {
            CompanyName = "Test Restoran",
            CheckNumber = "CH-202605301200",
        };

        var bytes = KassaEscPosRenderer.Render(json, ctx);
        var text = Encoding.ASCII.GetString(bytes);
        Assert.Contains("CEK NO : 202605301200", text);
    }
}
