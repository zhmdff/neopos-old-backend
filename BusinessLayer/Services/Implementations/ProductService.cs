using Application.Interfaces;
using AutoMapper;
using BusinessLayer.DTOs.Product;
using DAL.Server.Context;
using Domain.Common.Entities;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

public class ProductService : IProductService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly IWebHostEnvironment _env;
    private readonly ITranslationService _translationService;

    public ProductService(AppDbContext context, IMapper mapper, IWebHostEnvironment env, ITranslationService translationService)
    {
        _context = context;
        _mapper = mapper;
        _env = env;
        _translationService = translationService;
    }

    public async Task<IEnumerable<ProductGetDto>> GetAllAsync(Guid companyId, int skip, int take, string? search, Guid? categoryId, Guid? workshopId, bool uncategorizedOnly = false)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Workshop)
            .Include(p => p.AdditionalWorkshops)
            .Include(p => p.Variants)
            .Where(p => !p.IsDeleted && p.CompanyId == companyId);

        if (uncategorizedOnly)
            query = query.Where(p => p.CategoryId == null);
        else if (categoryId.HasValue)
        {
            var subCategoryIds = await _context.Categories
                .Where(c => c.ParentCategoryId == categoryId.Value && !c.IsDeleted)
                .Select(c => c.Id)
                .ToListAsync();

            subCategoryIds.Add(categoryId.Value);

            query = query.Where(p => p.CategoryId.HasValue && subCategoryIds.Contains(p.CategoryId.Value));
        }

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.NameAz.ToLower().Contains(search.ToLower()) || (p.Barcode != null && p.Barcode.Contains(search)));

        if (workshopId.HasValue)
            query = query.Where(p => p.WorkshopId == workshopId.Value);

        var products = await query
            .OrderByDescending(p => p.CreatedAt)
            .OrderBy(c => c.OrderIndex)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        var productIds = products.Select(p => p.Id).ToList();
        var setRows = await _context.ProductSets
            .AsNoTracking()
            .Include(ps => ps.SetItems)
            .ThenInclude(si => si.Product)
            .Include(ps => ps.ChoiceGroups)
            .ThenInclude(g => g.Options)
            .ThenInclude(o => o.Product)
            .Where(ps => productIds.Contains(ps.ProductId))
            .ToListAsync();

        var setsByProductId = setRows
            .GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => g.First());

        var list = _mapper.Map<List<ProductGetDto>>(products);
        foreach (var dto in list)
        {
            if (!setsByProductId.TryGetValue(dto.Id, out var ps))
                continue;

            dto.SetDescription = ps.Description;
            dto.SetComposition = ps.SetItems
                .OrderBy(si => si.Product?.NameAz)
                .Select(si => new ProductSetCompositionLineDto
                {
                    ProductName = si.Product?.NameAz ?? string.Empty,
                    Quantity = si.Quantity
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.ProductName))
                .ToList();

            dto.SetChoiceGroups = ps.ChoiceGroups
                .OrderBy(g => g.SortOrder)
                .ThenBy(g => g.NameAz)
                .Select(g => new ProductSetChoiceGroupLineDto
                {
                    NameAz = g.NameAz,
                    MinChoices = g.MinChoices,
                    MaxChoices = g.MaxChoices,
                    SortOrder = g.SortOrder,
                    Options = g.Options
                        .OrderBy(o => o.SortOrder)
                        .ThenBy(o => o.Product?.NameAz)
                        .Select(o => new ProductSetChoiceOptionLineDto
                        {
                            ProductId = o.ProductId,
                            ProductName = o.Product?.NameAz ?? string.Empty,
                            Quantity = o.Quantity,
                            SortOrder = o.SortOrder,
                        })
                        .Where(o => !string.IsNullOrWhiteSpace(o.ProductName))
                        .ToList(),
                })
                .Where(g => g.Options.Count > 0)
                .ToList();

            dto.HasBusinessLunch = dto.SetChoiceGroups.Count > 0;
        }

        return list;
    }
    public async Task<Guid> CreateAsync(ProductPostDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NameAz))
            throw new Exception("Məhsul adı (AZ) boş ola bilməz.");

        var nameNorm = dto.NameAz.Trim().ToLowerInvariant();
        bool exists = await _context.Products.AnyAsync(p =>
            !p.IsDeleted &&
            p.CompanyId == dto.CompanyId &&
            (p.NameAz.ToLower() == nameNorm || (!string.IsNullOrEmpty(dto.Barcode) && p.Barcode == dto.Barcode)));

        if (exists) throw new Exception("Bu adda və ya barkodda məhsul artıq mövcuddur!");


        var orderScope = _context.Products
            .Where(p => !p.IsDeleted && p.CompanyId == dto.CompanyId);
        if (dto.CategoryId.HasValue && dto.CategoryId.Value != Guid.Empty)
            orderScope = orderScope.Where(p => p.CategoryId == dto.CategoryId);
        else
            orderScope = orderScope.Where(p => p.CategoryId == null);

        int maxOrder = await orderScope
            .Select(p => (int?)p.OrderIndex)
            .MaxAsync() ?? 0;

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == dto.CompanyId) ?? throw new Exception("Şirkət tapılmadı!");
        var product = _mapper.Map<Product>(dto);
        if (product.CategoryId == Guid.Empty)
            product.CategoryId = null;

        // Default: true (Boss-dan gəlməsə də)
        product.ShowInQr = dto.ShowInQr ?? true;
        product.ShowInTerminal = dto.ShowInTerminal ?? true;

        decimal cost = dto.CostPrice;
        decimal markup = dto.MarkupValue;
        product.SalePrice = dto.MarkupType == MarkupType.Percentage
            ? cost + (cost * markup / 100)
            : cost + markup;

        if (company.IsDeliveryPriceEnabled)
        {
            product.DeliveryPrice = dto.DeliveryPrice.HasValue && dto.DeliveryPrice.Value > 0
                ? dto.DeliveryPrice.Value
                : product.SalePrice;
        }
        else product.DeliveryPrice = null;
        

        product.OrderIndex = maxOrder + 1;
        product.CompanyId = dto.CompanyId; 

        var nameTranslations = await _translationService.TranslateTextAsync(dto.NameAz, new List<string> { "en", "ru" });
        product.NameEn = nameTranslations.GetValueOrDefault("en", dto.NameAz);
        product.NameRu = nameTranslations.GetValueOrDefault("ru", dto.NameAz);

        product.CreatedAt = DateTime.UtcNow;
        product.CreatedBy = "Admin";
        product.IsDeleted = false;

        if (dto.ImageFile != null)
            product.ImageUrl = await UploadImage(dto.ImageFile);

        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // Additional workshops (mətbəx çapı)
        if (dto.AdditionalWorkshopIds != null && dto.AdditionalWorkshopIds.Count > 0)
        {
            var extras = dto.AdditionalWorkshopIds
                .Where(x => x != Guid.Empty && x != product.WorkshopId)
                .Distinct()
                .Select(wid => new ProductWorkshop
                {
                    Id = Guid.NewGuid(),
                    CompanyId = dto.CompanyId,
                    ProductId = product.Id,
                    WorkshopId = wid,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Admin",
                    IsDeleted = false
                })
                .ToList();
            if (extras.Count > 0)
            {
                await _context.ProductWorkshops.AddRangeAsync(extras);
                await _context.SaveChangesAsync();
            }
        }

        return product.Id;
    }

    public async Task UpdateAsync(ProductPutDto dto)
    {
        if (dto.Id == Guid.Empty)
            throw new Exception("Məhsul ID-si göndərilməyib.");

        if (string.IsNullOrWhiteSpace(dto.NameAz))
            throw new Exception("Məhsul adı (AZ) boş ola bilməz.");

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == dto.Id && p.CompanyId == dto.CompanyId && !p.IsDeleted) ?? throw new Exception("Məhsul tapılmadı və ya giriş icazəniz yoxdur!");
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == dto.CompanyId);

        var nameNorm = dto.NameAz.Trim().ToLowerInvariant();
        bool exists = await _context.Products.AnyAsync(p =>
            p.Id != dto.Id &&
            !p.IsDeleted &&
            p.CompanyId == dto.CompanyId &&
            (p.NameAz.ToLower() == nameNorm || (!string.IsNullOrEmpty(dto.Barcode) && p.Barcode == dto.Barcode)));

        if (exists) throw new Exception("Daxil etdiyiniz ad və ya barkod başqa bir məhsul tərəfindən istifadə edilir!");

        _mapper.Map(dto, product);
        if (product.CategoryId == Guid.Empty)
            product.CategoryId = null;

        // Nullable flag-lar: göndərilməyibsə köhnəni saxla (default artıq DB-də true olacaq)
        if (dto.ShowInQr.HasValue) product.ShowInQr = dto.ShowInQr.Value;
        if (dto.ShowInTerminal.HasValue) product.ShowInTerminal = dto.ShowInTerminal.Value;

        // Additional workshops update: replace set
        var newExtraIds = (dto.AdditionalWorkshopIds ?? new List<Guid>())
            .Where(x => x != Guid.Empty && x != product.WorkshopId)
            .Distinct()
            .ToHashSet();

        var existing = await _context.ProductWorkshops
            .Where(x => x.CompanyId == dto.CompanyId && x.ProductId == product.Id)
            .ToListAsync();

        var toRemove = existing.Where(x => !newExtraIds.Contains(x.WorkshopId)).ToList();
        if (toRemove.Count > 0) _context.ProductWorkshops.RemoveRange(toRemove);

        var existingIds = existing.Select(x => x.WorkshopId).ToHashSet();
        var toAdd = newExtraIds
            .Where(id => !existingIds.Contains(id))
            .Select(wid => new ProductWorkshop
            {
                Id = Guid.NewGuid(),
                CompanyId = dto.CompanyId,
                ProductId = product.Id,
                WorkshopId = wid,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Admin",
                IsDeleted = false
            })
            .ToList();
        if (toAdd.Count > 0) await _context.ProductWorkshops.AddRangeAsync(toAdd);

        decimal cost = dto.CostPrice;
        decimal markup = dto.MarkupValue;
        product.SalePrice = dto.MarkupType == MarkupType.Percentage
            ? cost + (cost * markup / 100)
            : cost + markup;

        if (company != null && company.IsDeliveryPriceEnabled)
        {
            product.DeliveryPrice = dto.DeliveryPrice.HasValue && dto.DeliveryPrice.Value > 0
                ? dto.DeliveryPrice.Value
                : product.SalePrice;
        }
        else product.DeliveryPrice = null;
        

        var nameTranslations = await _translationService.TranslateTextAsync(dto.NameAz, new List<string> { "en", "ru" });
        product.NameEn = nameTranslations.GetValueOrDefault("en", dto.NameAz);
        product.NameRu = nameTranslations.GetValueOrDefault("ru", dto.NameAz);

        product.LastModifiedAt = DateTime.UtcNow;
        product.LastModifiedBy = "Admin";

        if (dto.ImageFile != null)
            product.ImageUrl = await UploadImage(dto.ImageFile);

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id, Guid companyId)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId);

        if (product != null)
        {
            var baku = TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
            var nowBaku = DateTime.SpecifyKind(
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, baku),
                DateTimeKind.Unspecified);
            product.IsDeleted = true;
            product.DeletedAt = nowBaku;
            product.DeletedBy = "delete";
            await _context.SaveChangesAsync();
        }
    }

    public async Task<DeletedProductsReportDto> GetDeletedReportAsync(DateTime start, DateTime end, Guid companyId)
    {
        var items = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Workshop)
            .Where(p => p.CompanyId == companyId && p.IsDeleted)
            .Where(p => p.DeletedAt != null || p.LastModifiedAt != null)
            .Where(p =>
                (p.DeletedAt ?? p.LastModifiedAt)!.Value >= start &&
                (p.DeletedAt ?? p.LastModifiedAt)!.Value <= end)
            .OrderByDescending(p => p.DeletedAt ?? p.LastModifiedAt)
            .Select(p => new DeletedProductReportItemDto
            {
                Id = p.Id,
                NameAz = p.NameAz,
                CategoryName = p.Category != null ? p.Category.NameAz : null,
                WorkshopName = p.Workshop != null ? p.Workshop.NameAz : null,
                SalePrice = p.SalePrice,
                DeletedAt = p.DeletedAt ?? p.LastModifiedAt,
                DeletedBy = p.DeletedBy ?? p.LastModifiedBy
            })
            .ToListAsync();

        return new DeletedProductsReportDto
        {
            Start = start,
            End = end,
            TotalCount = items.Count,
            Items = items,
            OrderLineDeletions = new List<OrderLineDeletionItemDto>()
        };
    }

    public async Task UpdateOrdersAsync(Guid companyId, List<ProductOrderUpdateDto> dtos)
    {
        var ids = dtos.Select(x => x.Id).ToList();
        var products = await _context.Products
            .Where(p => ids.Contains(p.Id) && p.CompanyId == companyId)
            .ToListAsync();

        foreach (var item in dtos)
        {
            var product = products.FirstOrDefault(p => p.Id == item.Id);
            if (product != null)
            {
                product.OrderIndex = item.OrderIndex;
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task<string> UploadImage(IFormFile file)
    {
        string rootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        string folderPath = Path.Combine(rootPath, "uploads", "products");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        string fullPath = Path.Combine(folderPath, fileName);

        using (var fileStream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }
        return $"/uploads/products/{fileName}";
    }


    public async Task<(IEnumerable<ProductStockStatusDto> items, int totalCount)> GetStockStatusAsync(Guid companyId, int skip, int take, string? search)
    {
        var query = _context.Products
            .Where(p => !p.IsDeleted && p.CompanyId == companyId);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.NameAz.ToLower().Contains(search.ToLower()));

        int totalCount = await query.CountAsync();

        var products = await query
            .OrderBy(p => p.NameAz)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        var productIds = products.Select(p => p.Id).ToList();

        var warehouseStocks = await _context.ProductStockHistories
            .Include(h => h.Warehouse)
            .Where(h => productIds.Contains(h.ProductId) && !h.IsDeleted)
            .GroupBy(h => new { h.ProductId, WarehouseName = h.Warehouse.Name })
            .Select(g => new
            {
                ProductId = g.Key.ProductId,
                WarehouseName = g.Key.WarehouseName,    
                Quantity = g.Sum(x => x.ChangeAmount) 
            })
            .ToListAsync();

        var result = products.Select(p => new ProductStockStatusDto
        {
            Id = p.Id,
            NameAz = p.NameAz,
            Stock = p.Stock, 
            CostPrice = p.CostPrice,
            UnitName = p.Unit.ToString(),
            WarehouseDetails = warehouseStocks
                .Where(ws => ws.ProductId == p.Id)
                .Select(ws => new WarehouseStockDto
                {
                    WarehouseName = ws.WarehouseName,
                    Quantity = ws.Quantity
                }).ToList()
        }).ToList();

        return (result, totalCount);
    }
}