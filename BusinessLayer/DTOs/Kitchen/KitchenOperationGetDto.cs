namespace BusinessLayer.DTOs.Kitchen;

public class KitchenOperationGetDto
{
    public Guid Id { get; set; }
    public Guid OrderDetailId { get; set; }
    public string ProductName { get; set; }
    public double Quantity { get; set; } // Göndərilən fərq (məs: 2)
    public string OperationType { get; set; } // "New", "Reduced", "Cancelled"
    public DateTime SentAt { get; set; }

    // Printer qruplaşdırması üçün vacib olanlar
    public Guid WorkshopId { get; set; }
    public string PrinterType { get; set; }
    public string PrinterValue { get; set; }
}