namespace BusinessLayer.DTOs.Table;

public class TablePostDto
{
    public string NameAz { get; set; }
    public int Capacity { get; set; }
    public decimal? DepositAmount { get; set; }
    public string? DepositStartTime { get; set; } 
    public string? DepositEndTime { get; set; } 
    public Guid HallId { get; set; }
    public Guid CompanyId { get; set; }
    /// <summary>Verilərsə toplu yaratmada sıra saxlanır; yoxdursa zalda max+1.</summary>
    public int? OrderIndex { get; set; }
    /// <summary>0–100 faiz, xəritə rejimi üçün.</summary>
    public decimal? MapPositionX { get; set; }
    public decimal? MapPositionY { get; set; }
    public decimal? MapWidthPercent { get; set; }
    public decimal? MapHeightPercent { get; set; }
    public int? MapShape { get; set; }
    /// <summary>Masa saat limiti (dəqiqə), məs. 3:00 → 180.</summary>
    public int? TableHourLimitMinutes { get; set; }
}
