namespace Domain.Common.Entities;

public class Workshop : AuditableCompanyEntity
{
    public string NameAz { get; set; }
    public string NameEn { get; set; }
    public string NameRu { get; set; }
    public bool IsPrinting { get; set; } = true;

    public string PrinterType { get; set; }
    public string PrinterValue { get; set; } 
}
