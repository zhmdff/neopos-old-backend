using Domain.Common;
using Domain.Common.Entities;

namespace Domain.Entities;

public class ProductSetChoiceOption : AuditableCompanyEntity
{
    public Guid ProductSetChoiceGroupId { get; set; }
    public virtual ProductSetChoiceGroup ChoiceGroup { get; set; } = null!;

    public Guid ProductId { get; set; }
    public virtual Product Product { get; set; } = null!;

    public double Quantity { get; set; } = 1;
    public int SortOrder { get; set; }
}
