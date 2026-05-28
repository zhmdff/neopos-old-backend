using Domain.Common;
using Domain.Common.Entities;

namespace Domain.Entities;

public class ProductSetItem : AuditableCompanyEntity
{
    public Guid ProductSetId { get; set; }
    public virtual ProductSet ProductSet { get; set; }

    public Guid ProductId { get; set; }
    public virtual Product Product { get; set; }

    public double Quantity { get; set; }
}   