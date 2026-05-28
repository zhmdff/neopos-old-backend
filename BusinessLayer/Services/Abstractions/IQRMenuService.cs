using BusinessLayer.DTOs.QRMenu;

public interface IQRMenuService
{
    Task<QRMenuFullDto?> GetFullMenuBySlugAsync(string slug);
    Task<QRMenuSettingDto> GetSettingsByCompanyIdAsync(Guid companyId);
    Task<bool> UpdateSettingsAsync(Guid companyId, QRMenuSettingDto settingsDto);
    Task<bool> UpdateCategoryOrdersAsync(List<OrderUpdateDto> dtos);
    Task<bool> UpdateProductOrdersAsync(List<OrderUpdateDto> dtos);
}