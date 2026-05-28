using Domain.Common;
using Domain.Common.Entities;

namespace Domain.Entities;

public class OrderDetail : AuditableCompanyEntity
{
    public Guid OrderHeaderId { get; set; }
    public virtual OrderHeader OrderHeader { get; set; }

    public Guid ProductId { get; set; }
    public virtual Product Product { get; set; }

    public Guid? ProductVariantId { get; set; }
    public string? ProductVariantName { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public double Quantity { get; set; }
    public string? ItemNote { get; set; }

    /// <summary>Set / Business Lunch tərkibi — mətbəxə gedir; ofisiantın ItemNote qeydinə yazılmır.</summary>
    public string? KitchenCompositionNote { get; set; }

    public decimal TotalPrice { get; set; }
    public bool IsSent { get; set; } = false;

    /// <summary>0 = təyin yoxdur (tək çek kimi); 1+ parçalı çek qrupu.</summary>
    public int SplitGroup { get; set; }
}