namespace BusinessLayer.DTOs.Print;

public class RenderKassaEscPosDto
{
    public Guid CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? CheckNumber { get; set; }
    public string? TableName { get; set; }
    public string? HallName { get; set; }
    public string? WaiterName { get; set; }
    public string? Kassir { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public int? GuestCount { get; set; }
    public string? OpenTime { get; set; }
    public string? CloseTime { get; set; }
    public string? ExtraText { get; set; }
    public string? SplitLabel { get; set; }
    public decimal FoodTotal { get; set; }
    public decimal ServiceAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal DepositLimit { get; set; }
    public bool IsPaid { get; set; }
    public decimal PaidCash { get; set; }
    public decimal PaidCard { get; set; }
    public string? CustomPaymentMethodName { get; set; }
    public List<RenderKassaLineItemDto> Items { get; set; } = [];
}

public class RenderKassaLineItemDto
{
    public string? Name { get; set; }
    public double Qty { get; set; }
    public decimal Price { get; set; }
    public decimal Total { get; set; }
    public string? Note { get; set; }
}
