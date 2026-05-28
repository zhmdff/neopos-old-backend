using Domain.Enums;

public class OrderCloseDto
{
    public Guid OrderId { get; set; }
    public decimal CashAmount { get; set; }
    public decimal CardAmount { get; set; }
    public string? CashierName { get; set; }
    public PaymentType PaymentMethod { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Boss tərəfindən yaradılmış əlavə ödəniş üsulu (opsional).</summary>
    public Guid? CustomPaymentMethodId { get; set; }
}