namespace BusinessLayer.DTOs.OrderHeader;

public class OrderDetailSplitAssignmentDto
{
    public Guid OrderDetailId { get; set; }
    /// <summary>1, 2, 3…</summary>
    public int SplitGroup { get; set; }
}

/// <summary>Bir sifariş sətirində miqdarın parçalara bölünməsi (məs: 4 ədəd → P1:2, P2:2).</summary>
public class SplitPartDto
{
    public int SplitGroup { get; set; }
    public double Quantity { get; set; }
}

public class OrderLineSplitDto
{
    public Guid OrderDetailId { get; set; }
    public List<SplitPartDto> Parts { get; set; } = new();
}

public class UpdateOrderSplitsDto
{
    /// <summary>Köhnə: bütün sətir tək parçaya — bütün miqdar.</summary>
    public List<OrderDetailSplitAssignmentDto>? Assignments { get; set; }
    /// <summary>Yeni: hər sətir üçün miqdar üzrə parçalar (cəm = sətir miqdarı).</summary>
    public List<OrderLineSplitDto>? Lines { get; set; }
}

public class PaySplitDto
{
    public Guid OrderId { get; set; }
    public int SplitGroup { get; set; }
    public decimal CashAmount { get; set; }
    public decimal CardAmount { get; set; }
    public string? CashierName { get; set; }
    /// <summary>Parçalı ödənişlə sifariş tam bağlananda (son ödəniş) — əlavə ödəniş üsulu etiketi.</summary>
    public Guid? CustomPaymentMethodId { get; set; }
}

public class OrderSplitPaymentGetDto
{
    public Guid Id { get; set; }
    public int SplitGroup { get; set; }
    public decimal PaidCash { get; set; }
    public decimal PaidCard { get; set; }
}
