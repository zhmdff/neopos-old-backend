using Domain.Common;
using Domain.Common.Entities;

namespace Domain.Entities;

public class ProductSet : AuditableCompanyEntity
{
    public Guid ProductId { get; set; }
    public virtual Product Product { get; set; }

    public string? Description { get; set; }

    public virtual ICollection<ProductSetItem> SetItems { get; set; } = new List<ProductSetItem>();

    /// <summary>Business lunch / iftar: hər qrupdan min–max seçim (məs. şorba, əsas yemək).</summary>
    public virtual ICollection<ProductSetChoiceGroup> ChoiceGroups { get; set; } = new List<ProductSetChoiceGroup>();
}