using BusinessLayer.DTOs.ProductVariant;

namespace BusinessLayer.Services.Abstractions;

public interface IProductVariantService
{
    Task<IEnumerable<ProductVariantGetDto>> GetByProductAsync(Guid productId, Guid companyId);
    Task<ProductVariantGetDto> CreateAsync(ProductVariantPostDto dto);
    Task<ProductVariantGetDto> UpdateAsync(ProductVariantPutDto dto);
    Task DeleteAsync(Guid id, Guid companyId);
}

