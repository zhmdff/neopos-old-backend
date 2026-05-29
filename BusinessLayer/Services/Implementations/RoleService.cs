using Application.Interfaces;
using AutoMapper;
using BusinessLayer.DTOs.Role;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class RoleService : IRoleService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly ITranslationService _translationService;

    public RoleService(AppDbContext context, IMapper mapper, ITranslationService translationService)
    {
        _context = context;
        _mapper = mapper;
        _translationService = translationService;
    }

    public async Task<IEnumerable<RoleGetDto>> GetAllAsync(Guid companyId)
    {
        var roles = await _context.Roles
            .Where(r => !r.IsDeleted && r.CompanyId == companyId) // Şirkət filtri
            .ToListAsync();
        return _mapper.Map<IEnumerable<RoleGetDto>>(roles);
    }

    public async Task<RoleGetDto> GetByIdAsync(Guid id, Guid companyId)
    {
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == companyId && !r.IsDeleted);

        if (role == null) throw new Exception("Vəzifə tapılmadı və ya giriş icazəniz yoxdur!");

        return _mapper.Map<RoleGetDto>(role);
    }

    public async Task CreateAsync(RolePostDto dto)
    {
        // Eyni şirkət daxilində eyni adlı vəzifə yoxlanışı
        bool exists = await _context.Roles.AnyAsync(r =>
            r.CompanyId == dto.CompanyId &&
            r.NameAz.ToLower() == dto.NameAz.ToLower() &&
            !r.IsDeleted);

        if (exists) throw new Exception($"'{dto.NameAz}' adlı vəzifə artıq mövcuddur!");

        var role = _mapper.Map<Role>(dto);

        var translations = await _translationService.TranslateTextAsync(dto.NameAz, new List<string> { "ru", "en" });
        role.NameRu = translations.GetValueOrDefault("ru", dto.NameAz);
        role.NameEn = translations.GetValueOrDefault("en", dto.NameAz);

        role.CreatedAt = DateTime.UtcNow;
        role.CreatedBy = "System";
        role.CompanyId = dto.CompanyId; // Məcburi mənimsətmə

        await _context.Roles.AddAsync(role);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(RolePutDto dto)
    {
        // Yoxlama üçün əvvəlcə yalnız ID ilə tapmağa çalışaq (debug məqsədli)
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Id == dto.Id && !r.IsDeleted);

        if (role == null)
            throw new Exception("Vəzifə bazada ümumiyyətlə yoxdur!");

        // İndi şirkət ID-sini yoxlayaq
        if (role.CompanyId != dto.CompanyId)
            throw new Exception($"Giriş qadağandır! Rolun şirkəti: {role.CompanyId}, Gələn şirkət: {dto.CompanyId}");

        // Məlumatları yeniləyirik
        role.NameAz = dto.NameAz;

        var translations = await _translationService.TranslateTextAsync(dto.NameAz, new List<string> { "ru", "en" });
        role.NameRu = translations.GetValueOrDefault("ru", dto.NameAz);
        role.NameEn = translations.GetValueOrDefault("en", dto.NameAz);

        role.Permissions = dto.Permissions?.ToArray() ?? Array.Empty<int>();
        role.LastModifiedAt = DateTime.UtcNow;
        role.LastModifiedBy = "System";

        _context.Roles.Update(role);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id, Guid companyId)
    {
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == companyId);

        if (role == null) throw new Exception("Vəzifə tapılmadı!");

        role.IsDeleted = true;
        await _context.SaveChangesAsync();
    }
}