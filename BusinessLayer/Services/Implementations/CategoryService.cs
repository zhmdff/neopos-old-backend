using AutoMapper;
using BusinessLayer.DTOs.Category;
using BusinessLayer.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using DAL.Server.Context;
using Domain.Common.Entities;
using Microsoft.AspNetCore.Hosting;
using Application.Interfaces;

namespace BusinessLayer.Services.Implementations;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly IWebHostEnvironment _env;
    private readonly ITranslationService _translationService;

    public CategoryService(AppDbContext context, IMapper mapper, IWebHostEnvironment env, ITranslationService translationService)
    {
        _context = context;
        _mapper = mapper;
        _env = env;
        _translationService = translationService;
    }

    public async Task<IEnumerable<CategoryGetDto>> GetAllAsync(Guid companyId, int skip, int take, string? search, Guid? parentId = null)
    {
        var query = _context.Categories
            .Where(c => !c.IsDeleted && c.CompanyId == companyId);

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c => c.NameAz.ToLower().Contains(search.ToLower()));
        }
        else
        {
            if (take <= 100) // POS rejimi
            {
                query = query.Where(c => c.ParentCategoryId == parentId);
            }
        }

        var categories = await query
            .OrderBy(c => c.OrderIndex)
            .Select(c => new CategoryGetDto
            {
                Id = c.Id,
                NameAz = c.NameAz,
                NameEn = c.NameEn,
                NameRu = c.NameRu,
                OrderIndex = c.OrderIndex,
                ImageUrl = c.ImageUrl,
                ParentCategoryId = c.ParentCategoryId,
                // 🔥 Alt kateqoriya sayını tapırıq
                SubCategoryCount = _context.Categories.Count(sc => sc.ParentCategoryId == c.Id && !sc.IsDeleted),
                // 🔥 Bu kateqoriyanın özünə aid məhsul sayını tapırıq
                ProductCount = _context.Products.Count(p => p.CategoryId == c.Id && !p.IsDeleted)
            })
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return categories;
    }

    public async Task UpdateOrdersAsync(Guid companyId, List<CategoryOrderUpdateDto> dtos)
    {
        var ids = dtos.Select(x => x.Id).ToList();

        // Yalnız həmin şirkətə aid kateqoriyaları yeniləyirik
        var categories = await _context.Categories
            .Where(c => ids.Contains(c.Id) && c.CompanyId == companyId)
            .ToListAsync();

        foreach (var category in categories)
        {
            var dto = dtos.First(x => x.Id == category.Id);
            category.OrderIndex = dto.OrderIndex;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<Guid> CreateAsync(CategoryPostDto dto)
    {
        var category = _mapper.Map<Category>(dto);

        // Şirkət daxilində OrderIndex hesablanması
        int maxOrder = await _context.Categories
            .Where(c => c.ParentCategoryId == dto.ParentCategoryId && !c.IsDeleted && c.CompanyId == dto.CompanyId)
            .Select(c => (int?)c.OrderIndex)
            .MaxAsync() ?? 0;

        category.OrderIndex = maxOrder + 1;

        var translations = await _translationService.TranslateTextAsync(dto.NameAz, new List<string> { "en", "ru" });
        category.NameEn = translations.GetValueOrDefault("en", dto.NameAz);
        category.NameRu = translations.GetValueOrDefault("ru", dto.NameAz);

        category.CreatedAt = DateTime.UtcNow;
        category.CreatedBy = "Admin";
        category.IsDeleted = false;
        category.CompanyId = dto.CompanyId; // Mütləq set edilməlidir

        if (dto.ImageFile != null)
        {
            category.ImageUrl = await UploadImage(dto.ImageFile);
        }

        await _context.Categories.AddAsync(category);
        await _context.SaveChangesAsync();
        return category.Id;
    }

    public async Task UpdateAsync(CategoryPutDto dto)
    {
        // Təhlükəsizlik: Həm ID, həm CompanyId yoxlanılır
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == dto.Id && c.CompanyId == dto.CompanyId && !c.IsDeleted);

        if (category == null) throw new Exception("Kateqoriya tapılmadı və ya giriş icazəniz yoxdur!");

        _mapper.Map(dto, category);

        var translations = await _translationService.TranslateTextAsync(dto.NameAz, new List<string> { "en", "ru" });
        category.NameEn = translations.GetValueOrDefault("en", dto.NameAz);
        category.NameRu = translations.GetValueOrDefault("ru", dto.NameAz);

        category.LastModifiedAt = DateTime.UtcNow;
        category.LastModifiedBy = "Admin";

        if (dto.ImageFile != null)
        {
            category.ImageUrl = await UploadImage(dto.ImageFile);
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id, Guid companyId)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId);

        if (category != null)
        {
            category.IsDeleted = true;
            await _context.SaveChangesAsync();
        }
    }

    private async Task<string> UploadImage(Microsoft.AspNetCore.Http.IFormFile file)
    {
        string rootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        string folderPath = Path.Combine(rootPath, "uploads", "categories");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        string fullPath = Path.Combine(folderPath, fileName);

        using (var fileStream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }
        return $"/uploads/categories/{fileName}";
    }
}