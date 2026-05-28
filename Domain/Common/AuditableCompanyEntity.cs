using Domain.Common.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Common;

public abstract class AuditableCompanyEntity : AuditableEntity
{
    public Guid CompanyId { get; set; }
    [ForeignKey("CompanyId")]
    public Company Company { get; set; }
}
