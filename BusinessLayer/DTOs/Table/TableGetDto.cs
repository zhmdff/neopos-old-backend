using BusinessLayer.DTOs.OrderHeader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Table;

public class TableGetDto
{
    public Guid Id { get; set; }
    public string NameAz { get; set; }
    public string NameEn { get; set; }
    public string NameRu { get; set; }
    public int OrderIndex { get; set; }
    public int Capacity { get; set; }
    public decimal? DepositAmount { get; set; }
    public string DepositStartTime { get; set; }
    public string DepositEndTime { get; set; }
    public int? TableHourLimitMinutes { get; set; }
    public string HallNameAz { get; set; }
    public int Status { get; set; }
    public decimal? MapPositionX { get; set; }
    public decimal? MapPositionY { get; set; }
    public decimal? MapWidthPercent { get; set; }
    public decimal? MapHeightPercent { get; set; }
    /// <summary>0 düzbucaqlı, 1 dairəvi.</summary>
    public int MapShape { get; set; }
    public OrderHeaderGetDto? ActiveOrder { get; set; }
}
