using AutoMapper;
using BusinessLayer.DTOs.QRMenu;
using DAL.Server.Context;
using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Concrete;

public class QRMenuService : IQRMenuService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public QRMenuService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<QRMenuFullDto?> GetFullMenuBySlugAsync(string slug)
    {
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive && !x.IsDeleted);

        if (company == null) return null;

        var setting = await _context.QRMenuSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == company.Id);

        var categories = await _context.Categories
            .AsNoTracking()
            .Include(c => c.Products)
                .ThenInclude(p => p.Variants)
            .Where(c => c.CompanyId == company.Id && !c.IsDeleted)
            .OrderBy(c => c.OrderIndexByQrMenu ?? 999)
            .ToListAsync();

        foreach (var cat in categories)
        {
            cat.Products = cat.Products
                .Where(p => !p.IsDeleted && p.ShowInQr)
                .OrderBy(p => p.OrderIndexByQrMenu ?? 999)
                .ToList();
        }

        var fullMenuDto = _mapper.Map<QRMenuFullDto>(company);
        fullMenuDto.Settings = _mapper.Map<QRMenuSettingDto>(setting);
        fullMenuDto.Categories = categories
            .Select(c =>
            {
                var dto = _mapper.Map<CategoryQRDto>(c);
                dto.Products = BuildQrMenuProducts(c.Products);
                return dto;
            })
            .ToList();

        return fullMenuDto;
    }

    /// <summary>
    /// Variantlı məhsul: QR-də yalnız variantlar (şəkil — əsas məhsuldan). Variantsız — əsas məhsul.
    /// </summary>
    private static List<ProductQRDto> BuildQrMenuProducts(IEnumerable<Product> products)
    {
        var list = new List<ProductQRDto>();
        foreach (var p in products)
        {
            var variants = (p.Variants ?? [])
                .Where(v => !v.IsDeleted)
                .OrderBy(v => v.OrderIndex)
                .ThenBy(v => v.NameAz)
                .ToList();

            if (variants.Count > 0)
            {
                var baseOrder = p.OrderIndexByQrMenu ?? 999;
                foreach (var v in variants)
                {
                    list.Add(new ProductQRDto
                    {
                        Id = v.Id,
                        ProductId = p.Id,
                        ProductVariantId = v.Id,
                        NameAz = FormatQrVariantDisplayName(p.NameAz, v.NameAz),
                        NameRu = FormatQrVariantDisplayName(p.NameRu, v.NameRu),
                        NameEn = FormatQrVariantDisplayName(p.NameEn, v.NameEn),
                        SalePrice = v.Price,
                        ImageUrl = p.ImageUrl,
                        CookingProcess = p.CookingProcess,
                        OrderIndexByQrMenu = baseOrder * 1000 + v.OrderIndex,
                    });
                }
            }
            else
            {
                list.Add(new ProductQRDto
                {
                    Id = p.Id,
                    ProductId = p.Id,
                    ProductVariantId = null,
                    NameAz = p.NameAz,
                    NameRu = p.NameRu,
                    NameEn = p.NameEn,
                    SalePrice = p.SalePrice,
                    ImageUrl = p.ImageUrl,
                    CookingProcess = p.CookingProcess,
                    OrderIndexByQrMenu = p.OrderIndexByQrMenu,
                });
            }
        }

        return list;
    }

    private static string FormatQrVariantDisplayName(string? productName, string? variantName)
    {
        var pn = (productName ?? "").Trim();
        var vn = (variantName ?? "").Trim();
        if (string.IsNullOrEmpty(vn)) return pn;
        if (string.IsNullOrEmpty(pn)) return vn;
        if (vn.Contains(pn, StringComparison.InvariantCultureIgnoreCase)) return vn;
        return $"{pn} - {vn}";
    }

    public async Task<QRMenuSettingDto> GetSettingsByCompanyIdAsync(Guid companyId)
    {
        var setting = await _context.QRMenuSettings
            .FirstOrDefaultAsync(x => x.CompanyId == companyId);

        return _mapper.Map<QRMenuSettingDto>(setting);
    }

    public async Task<bool> UpdateSettingsAsync(Guid companyId, QRMenuSettingDto settingsDto)
    {
        var setting = await _context.QRMenuSettings
            .FirstOrDefaultAsync(x => x.CompanyId == companyId);

        if (setting == null)
        {
            setting = _mapper.Map<QRMenuSetting>(settingsDto);
            setting.CompanyId = companyId;
            await _context.QRMenuSettings.AddAsync(setting);
        }
        else
        {
            _mapper.Map(settingsDto, setting);
            setting.CompanyId = companyId;
            _context.QRMenuSettings.Update(setting);
        }

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateCategoryOrdersAsync(List<OrderUpdateDto> dtos)
    {
        foreach (var item in dtos)
        {
            var category = await _context.Categories.FindAsync(item.Id);
            if (category != null)
            {
                category.OrderIndexByQrMenu = item.OrderIndex;
            }
        }
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateProductOrdersAsync(List<OrderUpdateDto> dtos)
    {
        foreach (var item in dtos)
        {
            var product = await _context.Products.FindAsync(item.Id);
            if (product != null)
            {
                product.OrderIndexByQrMenu = item.OrderIndex;
            }
        }
        return await _context.SaveChangesAsync() > 0;
    }
}