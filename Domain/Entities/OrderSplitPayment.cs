using Domain.Common;

namespace Domain.Entities;

/// <summary>Parçalı çek üzrə ödəniş qeydləri (tək OrderHeader üzrə toplanır).</summary>
public class OrderSplitPayment : AuditableCompanyEntity
{
    public Guid OrderHeaderId { get; set; }
    public virtual OrderHeader OrderHeader { get; set; } = null!;

    /// <summary>1, 2, 3… — OrderDetail.SplitGroup ilə uyğun.</summary>
    public int SplitGroup { get; set; }

    public decimal PaidCash { get; set; }
    public decimal PaidCard { get; set; }
}
