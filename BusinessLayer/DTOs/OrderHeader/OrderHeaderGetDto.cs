using BusinessLayer.DTOs.Customer;
using BusinessLayer.DTOs.OrderDetail;

namespace BusinessLayer.DTOs.OrderHeader;

public class OrderHeaderGetDto
{
    public Guid Id { get; set; }
    public string? CheckNumber { get; set; }
    public bool IsClosed { get; set; }
    public string? Note { get; set; }
    public Guid? CashShiftId { get; set; }
    public int? GuestCount { get; set; }
    /// <summary>Masa saat limiti bitdikdən sonra uzadılmış bonus dəqiqələr.</summary>
    public int TableHourBonusMinutes { get; set; }

    public Guid TableId { get; set; }
    public string? TableName { get; set; }
    public string? HallName { get; set; }
    public string? WaiterName { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public decimal ServicePercentage { get; set; }
    public decimal ServiceAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public decimal DepositAmount { get; set; } 
    public TimeSpan? DepositStartTime { get; set; }
    public TimeSpan? DepositEndTime { get; set; }

    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public bool IsPercentageDiscount { get; set; }
    /// <summary>Beh məbləği (çap və ödənişdə TotalAmount-dan çıxılır).</summary>
    public decimal BehAmount { get; set; }
    public List<OrderDetailGetDto> OrderDetails { get; set; } = new();
    public int PaymentMethod { get; set; }

    public Guid? CustomPaymentMethodId { get; set; }
    public string? CustomPaymentMethodName { get; set; }

    public decimal PaidCash { get; set; }
    public decimal PaidCard { get; set; }
    public string CreatedBy { get; set; }
    public string? CashierName { get; set; }

    public List<OrderSplitPaymentGetDto> SplitPayments { get; set; } = new();

    public Guid? CustomerId { get; set; }
    public CustomerGetDto? Customer { get; set; }
}