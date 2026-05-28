using Domain.Common;
using Domain.Common.Entities;
using Domain.Enums;

namespace Domain.Entities;

public class OrderHeader : AuditableCompanyEntity
{
    public string? CheckNumber { get; set; }
    public bool IsClosed { get; set; }
    public string? Note { get; set; }

    public Guid TableId { get; set; }
    public virtual Table Table { get; set; }

    /// <summary>Hesabat: satış hansı kassa növbəsinə aiddir (adətən ödəniş/çek bağlanan anda aktiv növbə).</summary>
    public Guid? CashShiftId { get; set; }
    public virtual CashShift? CashShift { get; set; }

    public string? WaiterName { get; set; }
    public string? CashierName { get; set; }

    public DateTime OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }

    public decimal ServicePercentage { get; set; }
    public decimal ServiceAmount { get; set; }

    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public bool IsPercentageDiscount { get; set; }

    /// <summary>Çek üzrə beh (məbləğ); detal sətri deyil. Ödəniləcək cəm = TotalAmount - Beh (0–Total aralığında).</summary>
    public decimal BehAmount { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public TimeSpan? DepositStartTime { get; set; }
    public TimeSpan? DepositEndTime { get; set; }

    public PaymentType? PaymentMethod { get; set; }
    public decimal PaidCash { get; set; } 
    public decimal PaidCard { get; set; } 

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = [];
    public virtual ICollection<OrderSplitPayment> SplitPayments { get; set; } = [];

    public Guid? CustomerId { get; set; }
    public virtual Customer? Customer { get; set; }

    /// <summary>Boss tərəfindən əlavə edilmiş ödəniş üsulu (məs. Wolt); null = yalnız nağd/kart/qarışıq etiketi.</summary>
    public Guid? CustomPaymentMethodId { get; set; }
    public virtual CompanyPaymentMethod? CustomPaymentMethod { get; set; }

    /// <summary>Terminal: masa üçün qonaq sayı (opsional).</summary>
    public int? GuestCount { get; set; }

    /// <summary>Masa saat limiti bitdikdən sonra məhsul əlavə ediləndə uzadılan bonus dəqiqələr (terminal/ofisiant sinxron).</summary>
    public int TableHourBonusMinutes { get; set; }
}