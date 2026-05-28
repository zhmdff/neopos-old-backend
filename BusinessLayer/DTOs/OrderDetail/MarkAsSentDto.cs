namespace BusinessLayer.DTOs.OrderDetail;

public class MarkAsSentDto
{
    public List<Guid> OrderDetailIds { get; set; } = new();
}
