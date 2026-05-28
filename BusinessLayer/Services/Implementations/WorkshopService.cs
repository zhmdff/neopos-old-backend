using AutoMapper;
using BusinessLayer.DTOs.Workshop;
using BusinessLayer.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using DAL.Server.Context;
using Application.Interfaces;
using Domain.Common.Entities;

namespace BusinessLayer.Services.Implementations;

public class WorkshopService : IWorkshopService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly ITranslationService _translationService;

    public WorkshopService(AppDbContext context, IMapper mapper, ITranslationService translationService)
    {
        _context = context;
        _mapper = mapper;
        _translationService = translationService;
    }

    public async Task<IEnumerable<WorkshopGetDto>> GetAllAsync(Guid companyId)
    {
        var workshops = await _context.Workshops
            .Where(w => !w.IsDeleted && w.CompanyId == companyId) // Şirkət filtri mütləqdir
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        return _mapper.Map<IEnumerable<WorkshopGetDto>>(workshops);
    }

    public async Task CreateAsync(WorkshopPostDto dto)
    {
        // Unikallıq yoxlanışı yalnız həmin şirkət daxilində
        bool exists = await _context.Workshops.AnyAsync(w =>
            w.NameAz.ToLower() == dto.NameAz.ToLower() &&
            w.CompanyId == dto.CompanyId &&
            !w.IsDeleted);

        if (exists)
            throw new Exception($"'{dto.NameAz}' adlı emalatxana artıq mövcuddur!");

        var workshop = _mapper.Map<Workshop>(dto);

        var translations = await _translationService.TranslateTextAsync(dto.NameAz, new List<string> { "en", "ru" });
        workshop.NameEn = translations.GetValueOrDefault("en", dto.NameAz);
        workshop.NameRu = translations.GetValueOrDefault("ru", dto.NameAz);

        workshop.CreatedAt = DateTime.UtcNow;
        workshop.CreatedBy = "Admin";
        workshop.CompanyId = dto.CompanyId; // DTO-dan gələn ID-ni mənimsədirik

        await _context.Workshops.AddAsync(workshop);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(WorkshopPutDto dto)
    {
        // Təhlükəsizlik üçün həm ID, həm CompanyId yoxlanılır
        var workshop = await _context.Workshops.FirstOrDefaultAsync(w =>
            w.Id == dto.Id &&
            w.CompanyId == dto.CompanyId &&
            !w.IsDeleted);

        if (workshop == null) throw new Exception("Emalatxana tapılmadı və ya giriş icazəniz yoxdur!");

        if (workshop.NameAz != dto.NameAz)
        {
            bool exists = await _context.Workshops.AnyAsync(w =>
                w.NameAz.ToLower() == dto.NameAz.ToLower() &&
                w.CompanyId == dto.CompanyId &&
                w.Id != dto.Id &&
                !w.IsDeleted);

            if (exists) throw new Exception("Bu adda emalatxana artıq mövcuddur!");

            var translations = await _translationService.TranslateTextAsync(dto.NameAz, new List<string> { "en", "ru" });
            workshop.NameEn = translations.GetValueOrDefault("en", dto.NameAz);
            workshop.NameRu = translations.GetValueOrDefault("ru", dto.NameAz);
        }

        workshop.PrinterType = dto.PrinterType;
        workshop.PrinterValue = dto.PrinterValue;
        workshop.IsPrinting = dto.IsPrinting;

        _mapper.Map(dto, workshop);

        workshop.LastModifiedAt = DateTime.UtcNow;
        _context.Workshops.Update(workshop);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id, Guid companyId)
    {
        // Başqasının emalatxanasını silə bilməsin deyə companyId ilə tapırıq
        var workshop = await _context.Workshops.FirstOrDefaultAsync(w =>
            w.Id == id &&
            w.CompanyId == companyId);

        if (workshop == null) throw new Exception("Emalatxana tapılmadı!");

        workshop.IsDeleted = true;
        workshop.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}