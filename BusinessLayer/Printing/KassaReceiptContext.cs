namespace BusinessLayer.Printing;

public sealed class KassaReceiptContext
{
    public string CompanyName { get; init; } = "";
    public string? CheckNumber { get; init; }
    public string? TableName { get; init; }
    public string? HallName { get; init; }
    public string? WaiterName { get; init; }
    public string? KassirName { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public string? CustomerAddress { get; init; }
    public int? GuestCount { get; init; }
    public DateTime? OpenTime { get; init; }
    public DateTime? CloseTime { get; init; }
    public string? ExtraText { get; init; }
    public string? ThankYouText { get; init; }
    public string? SplitLabel { get; init; }
    public decimal FoodTotal { get; init; }
    public decimal ServiceAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal GrandTotal { get; init; }
    public decimal DepositLimit { get; init; }
    public bool IsPaid { get; init; }
    public decimal PaidCash { get; init; }
    public decimal PaidCard { get; init; }
    public string? CustomPaymentMethodName { get; init; }
    public IReadOnlyList<KassaReceiptLineItem> Items { get; init; } = [];
}

public sealed class KassaReceiptLineItem
{
    public string Name { get; init; } = "";
    public double Qty { get; init; }
    public decimal Price { get; init; }
    public decimal Total { get; init; }
    public string? Note { get; init; }
}
