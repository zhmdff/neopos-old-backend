using Domain.Common;
using Domain.Common.Entities;
using Domain.Entities;
using Domain.Enums;

namespace Domain.Entities;

public class KitchenOperation : AuditableCompanyEntity
{
    public Guid OrderDetailId { get; set; }
    public OrderDetail OrderDetail { get; set; }

    public double Quantity { get; set; } // Göndərilən fərq miqdarı
    public KitchenOperationType OperationType { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsPrinted { get; set; } = false;

    // Əgər OrderDetail bazadan silinərsə, məlumatın itməməsi üçün:
    public string ProductName { get; set; }
    public Guid OrderHeaderId { get; set; }
    public string Note { get; set; }
}