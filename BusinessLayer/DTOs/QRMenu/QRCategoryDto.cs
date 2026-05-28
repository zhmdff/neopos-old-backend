namespace BusinessLayer.DTOs.QRMenu;

public class CategoryQRDto
{
    public Guid Id { get; set; }
    public string NameAz { get; set; }
    public string NameEn { get; set; }
    public string NameRu { get; set; }
    public string? ImageUrl { get; set; }
    public int? OrderIndexByQrMenu { get; set; }
    public List<ProductQRDto> Products { get; set; }
}