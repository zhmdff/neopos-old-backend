namespace BusinessLayer.DTOs.Workshop;

public class WorkshopGetDto
{
    public Guid Id { get; set; }
    public string NameAz { get; set; }
    public bool IsPrinting { get; set; }
    public string PrinterType { get; set; }  
    public string PrinterValue { get; set; } 
}