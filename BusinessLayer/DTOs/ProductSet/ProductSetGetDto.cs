namespace BusinessLayer.DTOs.ProductSet;

public class ProductSetGetDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductNameAz { get; set; }
    public decimal SetSalePrice { get; set; }
    public string? Description { get; set; }
    public string? CategoryName { get; set; }
    public Guid? CategoryId { get; set; }
    public string WorkshopName { get; set; }
    public Guid WorkshopId { get; set; }
    public List<ProductSetItemGetDto> SetItems { get; set; } = [];

    public List<ProductSetChoiceGroupGetDto> ChoiceGroups { get; set; } = [];
}