namespace BusinessLayer.DTOs.Company;

public class CompanyPaymentMethodDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string NameAz { get; set; } = "";
    public int SortOrder { get; set; }
}

public class CompanyPaymentMethodPostDto
{
    public string NameAz { get; set; } = "";
    public int SortOrder { get; set; }
}

public class CompanyPaymentMethodPutDto
{
    public string NameAz { get; set; } = "";
    public int SortOrder { get; set; }
}
