using Domain.Enums;

namespace Domain.Common.Entities;

public class Table : AuditableCompanyEntity
{
    public string NameAz { get; set; }
    public string NameEn { get; set; }
    public string NameRu { get; set; }

    public int Capacity { get; set; }
    public int OrderIndex { get; set; }

    public decimal? DepositAmount { get; set; }
    public TimeSpan? DepositStartTime { get; set; }
    public TimeSpan? DepositEndTime { get; set; }

    /// <summary>Masa saat limiti (dəqiqə). Yalnız zalda IsTableHourActive olduqda.</summary>
    public int? TableHourLimitMinutes { get; set; }

    public Guid HallId { get; set; }
    public Hall Hall { get; set; }

    public TableStatus Status { get; set; } = TableStatus.Empty;

    /// <summary>
    /// Xəritə rejimində masa mərkəzinin üfüqi mövqeyi (0–100, faiz).
    /// </summary>
    public decimal? MapPositionX { get; set; }

    /// <summary>
    /// Xəritə rejimində masa mərkəzinin şaquli mövqeyi (0–100, faiz).
    /// </summary>
    public decimal? MapPositionY { get; set; }

    /// <summary>Xəritə üzrə en (0–100, konteyner faizi).</summary>
    public decimal? MapWidthPercent { get; set; }

    /// <summary>Xəritə üzrə hündürlük (0–100, konteyner faizi).</summary>
    public decimal? MapHeightPercent { get; set; }

    public TableMapShape MapShape { get; set; } = TableMapShape.Rectangle;
}
