using Application.Interfaces;
using AutoMapper;
using BusinessLayer.DTOs.ProductVariant;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class ProductVariantService : IProductVariantService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly ITranslationService _translationService;

    public ProductVariantService(AppDbContext context, IMapper mapper, ITranslationService translationService)
    {
        _context = context;
        _mapper = mapper;
        _translationService = translationService;
    }

    public async Task<IEnumerable<ProductVariantGetDto>> GetByProductAsync(Guid productId, Guid companyId)
    {
        var variants = await _context.ProductVariants
            .Where(v => v.ProductId == productId && v.CompanyId == companyId && !v.IsDeleted)
            .OrderBy(v => v.OrderIndex)
            .ThenBy(v => v.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return _mapper.Map<List<ProductVariantGetDto>>(variants);
    }

    public async Task<ProductVariantGetDto> CreateAsync(ProductVariantPostDto dto)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.CompanyId == dto.CompanyId && !p.IsDeleted)
            ?? throw new Exception("Məhsul tapılmadı!");

        var company = await _context.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dto.CompanyId)
            ?? throw new Exception("Şirkət tapılmadı!");

        bool exists = await _context.ProductVariants.AnyAsync(v =>
            v.ProductId == dto.ProductId &&
            v.CompanyId == dto.CompanyId &&
            !v.IsDeleted &&
            (v.NameAz.ToLower() == dto.NameAz.ToLower() ||
             (!string.IsNullOrWhiteSpace(dto.Barcode) && v.Barcode == dto.Barcode)));

        if (exists) throw new Exception("Bu adda və ya barkodda variant artıq mövcuddur!");

        int maxIndex = await _context.ProductVariants
            .Where(v => v.ProductId == dto.ProductId && v.CompanyId == dto.CompanyId && !v.IsDeleted)
            .Select(v => (int?)v.OrderIndex)
            .MaxAsync() ?? 0;

        var v = new ProductVariant
        {
            ProductId = product.Id,
            CompanyId = dto.CompanyId,
            NameAz = dto.NameAz,
            Price = dto.Price,
            Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? null : dto.Barcode.Trim(),
            OrderIndex = maxIndex + 1,
            CreatedBy = "Admin",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        };

        if (company.IsDeliveryPriceEnabled)
        {
            v.DeliveryPrice = dto.DeliveryPrice.HasValue && dto.DeliveryPrice.Value > 0
                ? dto.DeliveryPrice.Value
                : dto.Price;
        }
        else v.DeliveryPrice = null;

        var tr = await _translationService.TranslateTextAsync(dto.NameAz, new List<string> { "en", "ru" });
        v.NameEn = tr.GetValueOrDefault("en", dto.NameAz);
        v.NameRu = tr.GetValueOrDefault("ru", dto.NameAz);

        await _context.ProductVariants.AddAsync(v);
        await _context.SaveChangesAsync();

        return _mapper.Map<ProductVariantGetDto>(v);
    }

    public async Task<ProductVariantGetDto> UpdateAsync(ProductVariantPutDto dto)
    {
        var v = await _context.ProductVariants
            .FirstOrDefaultAsync(x => x.Id == dto.Id && x.CompanyId == dto.CompanyId && !x.IsDeleted)
            ?? throw new Exception("Variant tapılmadı!");

        var company = await _context.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dto.CompanyId)
            ?? throw new Exception("Şirkət tapılmadı!");

        if (v.ProductId != dto.ProductId)
            throw new Exception("Variant məhsula uyğun deyil!");

        bool exists = await _context.ProductVariants.AnyAsync(x =>
            x.Id != dto.Id &&
            x.ProductId == dto.ProductId &&
            x.CompanyId == dto.CompanyId &&
            !x.IsDeleted &&
            (x.NameAz.ToLower() == dto.NameAz.ToLower() ||
             (!string.IsNullOrWhiteSpace(dto.Barcode) && x.Barcode == dto.Barcode)));

        if (exists) throw new Exception("Bu adda və ya barkodda başqa variant artıq mövcuddur!");

        v.NameAz = dto.NameAz;
        v.Price = dto.Price;
        if (company.IsDeliveryPriceEnabled)
        {
            v.DeliveryPrice = dto.DeliveryPrice.HasValue && dto.DeliveryPrice.Value > 0
                ? dto.DeliveryPrice.Value
                : dto.Price;
        }
        else v.DeliveryPrice = null;

        v.Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? null : dto.Barcode.Trim();
        v.LastModifiedAt = DateTime.UtcNow;

        var tr = await _translationService.TranslateTextAsync(dto.NameAz, new List<string> { "en", "ru" });
        v.NameEn = tr.GetValueOrDefault("en", dto.NameAz);
        v.NameRu = tr.GetValueOrDefault("ru", dto.NameAz);

        await _context.SaveChangesAsync();
        return _mapper.Map<ProductVariantGetDto>(v);
    }

    public async Task DeleteAsync(Guid id, Guid companyId)
    {
        var v = await _context.ProductVariants
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId);
        if (v == null) return;
        v.IsDeleted = true;
        await _context.SaveChangesAsync();
    }
}

