using AutoMapper;
using BusinessLayer.DTOs.ProductSet;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class ProductSetService : IProductSetService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public ProductSetService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<ProductSetGetDto> CreateSetAsync(ProductSetPostDto dto)
    {
        // Ana məhsulun bu şirkətə aid olduğunu yoxlayırıq
        var mainProduct = await _context.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.CompanyId == dto.CompanyId);

        if (mainProduct == null) throw new Exception("Məhsul tapılmadı və ya giriş icazəniz yoxdur!");

        await EnsureChoiceGroupProductsExistAsync(dto.CompanyId, dto);

        var existingSet = await _context.ProductSets
            .Include(x => x.SetItems)
            .Include(x => x.ChoiceGroups)
            .ThenInclude(g => g.Options)
            .FirstOrDefaultAsync(x => x.ProductId == dto.ProductId && x.CompanyId == dto.CompanyId);

        if (existingSet != null)
        {
            // Köhnə tərkibi təmizləyirik
            _context.ProductSetItems.RemoveRange(existingSet.SetItems);
            _context.ProductSetChoiceGroups.RemoveRange(existingSet.ChoiceGroups.ToList());

            existingSet.Description = dto.Description;
            existingSet.LastModifiedBy = "Admin";
            existingSet.LastModifiedAt = DateTime.UtcNow;

            if (dto.SetItems != null)
            {
                foreach (var itemDto in dto.SetItems)
                {
                    existingSet.SetItems.Add(new ProductSetItem
                    {
                        ProductId = itemDto.ProductId,
                        Quantity = itemDto.Quantity,
                        CompanyId = dto.CompanyId, // Şirkət ID-si keçirilir
                        CreatedBy = "Admin",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            AddChoiceGroupsToSet(existingSet, dto.ChoiceGroups, dto.CompanyId);
            _context.ProductSets.Update(existingSet);
        }
        else
        {
            var productSet = _mapper.Map<ProductSet>(dto);
            productSet.CompanyId = dto.CompanyId;
            productSet.CreatedBy = "Admin";
            productSet.CreatedAt = DateTime.UtcNow;

            if (productSet.SetItems != null)
            {
                foreach (var item in productSet.SetItems)
                {
                    item.CompanyId = dto.CompanyId;
                    item.CreatedBy = "Admin";
                    item.CreatedAt = DateTime.UtcNow;
                }
            }

            AddChoiceGroupsToSet(productSet, dto.ChoiceGroups, dto.CompanyId);
            await _context.ProductSets.AddAsync(productSet);
        }

        await _context.SaveChangesAsync();

        // GetSetByIdAsync artıq companyId tələb edir
        return await GetSetByIdAsync(existingSet?.Id ?? (await _context.ProductSets.FirstAsync(x => x.ProductId == dto.ProductId)).Id, dto.CompanyId);
    }

    public async Task<List<ProductSetGetDto>> GetAllSetsAsync(Guid companyId, int skip, int take, string? search, Guid? categoryId, Guid? workshopId)
    {
        var query = _context.ProductSets
            .Include(ps => ps.Product).ThenInclude(p => p.Category)
            .Include(ps => ps.Product).ThenInclude(p => p.Workshop)
            .Include(ps => ps.SetItems).ThenInclude(psi => psi.Product)
            .Include(ps => ps.ChoiceGroups).ThenInclude(g => g.Options).ThenInclude(o => o.Product)
            .Where(ps => ps.CompanyId == companyId) // Şirkət filtri mütləqdir
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(x => x.Product.NameAz.ToLower().Contains(search.ToLower()));

        if (categoryId != null)
            query = query.Where(x => x.Product.CategoryId == categoryId);

        if (workshopId != null)
            query = query.Where(x => x.Product.WorkshopId == workshopId);

        var sets = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return _mapper.Map<List<ProductSetGetDto>>(sets);
    }

    public async Task<ProductSetGetDto> GetSetByIdAsync(Guid id, Guid companyId)
    {
        var set = await _context.ProductSets
            .Include(ps => ps.Product)
            .Include(ps => ps.SetItems)
                .ThenInclude(psi => psi.Product)
            .Include(ps => ps.ChoiceGroups)
                .ThenInclude(g => g.Options)
                .ThenInclude(o => o.Product)
            .FirstOrDefaultAsync(ps => ps.Id == id && ps.CompanyId == companyId);

        if (set == null) throw new Exception("Set tapılmadı!");

        return _mapper.Map<ProductSetGetDto>(set);
    }

    public async Task DeleteSetAsync(Guid id, Guid companyId)
    {
        var set = await _context.ProductSets
            .FirstOrDefaultAsync(ps => ps.Id == id && ps.CompanyId == companyId);

        if (set == null) throw new Exception("Set tapılmadı!");

        _context.ProductSets.Remove(set);
        await _context.SaveChangesAsync();
    }

    private static void ValidateChoiceGroup(ProductSetChoiceGroupPostDto g)
    {
        if (string.IsNullOrWhiteSpace(g.NameAz))
            throw new Exception("Seçim qrupunun adı boş ola bilməz.");
        var opts = g.Options ?? [];
        if (opts.Count == 0)
            throw new Exception($"«{g.NameAz.Trim()}» qrupunda ən azı bir variant olmalıdır.");
        if (g.MinChoices < 0 || g.MaxChoices < 1)
            throw new Exception("MinChoices / MaxChoices düzgün deyil.");
        if (g.MinChoices > g.MaxChoices)
            throw new Exception($"«{g.NameAz.Trim()}»: minimum seçim maksimumdan çox ola bilməz.");
        if (g.MaxChoices > opts.Count)
            throw new Exception($"«{g.NameAz.Trim()}»: maksimum seçim variant sayından çox ola bilməz.");
    }

    private static void AddChoiceGroupsToSet(ProductSet set, List<ProductSetChoiceGroupPostDto>? groupDtos, Guid companyId)
    {
        if (groupDtos == null || groupDtos.Count == 0) return;

        foreach (var gDto in groupDtos.OrderBy(g => g.SortOrder))
        {
            ValidateChoiceGroup(gDto);
            var g = new ProductSetChoiceGroup
            {
                NameAz = gDto.NameAz.Trim(),
                MinChoices = gDto.MinChoices,
                MaxChoices = gDto.MaxChoices,
                SortOrder = gDto.SortOrder,
                CompanyId = companyId,
                CreatedBy = "Admin",
                CreatedAt = DateTime.UtcNow,
            };
            foreach (var oDto in (gDto.Options ?? []).OrderBy(o => o.SortOrder))
            {
                g.Options.Add(new ProductSetChoiceOption
                {
                    ProductId = oDto.ProductId,
                    Quantity = oDto.Quantity,
                    SortOrder = oDto.SortOrder,
                    CompanyId = companyId,
                    CreatedBy = "Admin",
                    CreatedAt = DateTime.UtcNow,
                });
            }

            set.ChoiceGroups.Add(g);
        }
    }

    private async Task EnsureChoiceGroupProductsExistAsync(Guid companyId, ProductSetPostDto dto)
    {
        var ids = dto.ChoiceGroups?
            .SelectMany(g => (g.Options ?? []).Select(o => o.ProductId))
            .Distinct()
            .ToList() ?? [];
        if (ids.Count == 0) return;

        var ok = await _context.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.CompanyId == companyId && !p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync();

        if (ok.Count != ids.Count)
            throw new Exception("Seçim qrupunda mövcud olmayan və ya silinmiş məhsul ID-si var.");
    }
}