using Domain.Common;
using Domain.Common.Entities;

namespace Domain.Entities;

public class ProductSetChoiceGroup : AuditableCompanyEntity
{
    public Guid ProductSetId { get; set; }
    public virtual ProductSet ProductSet { get; set; } = null!;

    public string NameAz { get; set; } = string.Empty;
    public int MinChoices { get; set; } = 1;
    public int MaxChoices { get; set; } = 1;
    public int SortOrder { get; set; }

    public virtual ICollection<ProductSetChoiceOption> Options { get; set; } = new List<ProductSetChoiceOption>();
}
