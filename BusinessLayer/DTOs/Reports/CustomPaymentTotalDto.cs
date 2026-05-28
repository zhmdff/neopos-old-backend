namespace BusinessLayer.DTOs.Reports;

/// <summary>Bağlı çeklər üzrə şirkətin əlavə ödəniş üsullarına düşən məbləğlər (hesabat).</summary>
public class CustomPaymentTotalDto
{
    public Guid MethodId { get; set; }
    public string MethodName { get; set; } = "";
    public decimal Amount { get; set; }
    public int OrderCount { get; set; }
}
