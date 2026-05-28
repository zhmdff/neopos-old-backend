using System.ComponentModel.DataAnnotations;

namespace Domain.Common.Entities;

/// <summary>Şirkətə məxsus əlavə ödəniş üsulları (ad); nağd/kart/qarışıq enum-də qalır.</summary>
public class CompanyPaymentMethod : AuditableCompanyEntity
{
    [MaxLength(120)]
    public string NameAz { get; set; } = "";

    public int SortOrder { get; set; }
}
