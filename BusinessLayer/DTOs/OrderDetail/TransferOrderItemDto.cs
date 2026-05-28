namespace BusinessLayer.DTOs.OrderDetail;

public class TransferOrderItemDto
{
    public Guid SourceDetailId { get; set; }
    public Guid TargetTableId { get; set; }
    public double Quantity { get; set; }
    public Guid CompanyId { get; set; }
}

