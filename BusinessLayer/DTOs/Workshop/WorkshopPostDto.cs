namespace BusinessLayer.DTOs.Workshop;

public class WorkshopPostDto
{
    public string NameAz { get; set; }
    public bool IsPrinting { get; set; }
    public Guid CompanyId { get; set; }
    public string PrinterType { get; set; }
    public string PrinterValue { get; set; }
}