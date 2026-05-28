using AutoMapper;
using BusinessLayer.DTOs.User;
using BusinessLayer.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using DAL.Server.Context;
using Domain.Common.Entities;
using Application.Interfaces;

namespace BusinessLayer.Services.Implementations;

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public UserService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task CreateAsync(UserPostDto dto)
    {
        // Username bütün sistemdə təkrarolunmaz olmalıdır (Login üçün)
        if (await _context.Users.AnyAsync(u => u.Username == dto.Username && !u.IsDeleted))
            throw new Exception("Bu istifadəçi adı artıq istifadə olunub!");

        // Ad və Soyad yalnız bu şirkət daxilində yoxlanılır
        if (await _context.Users.AnyAsync(u => u.FullName == dto.FullName && u.CompanyId == dto.CompanyId && !u.IsDeleted))
            throw new Exception("Bu şirkətdə bu adda istifadəçi artıq mövcuddur!");

        var user = _mapper.Map<User>(dto);

        user.PasswordHash = dto.Password; // Qeyd: Real layihədə BCrypt və ya Argon2 istifadə etməlisən
        user.CreatedAt = DateTime.UtcNow;
        user.CreatedBy = "System";
        user.IsActive = true;
        user.CompanyId = dto.CompanyId;

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserPutDto dto)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == dto.Id && u.CompanyId == dto.CompanyId && !u.IsDeleted);

        if (user == null) throw new Exception("İstifadəçi tapılmadı!");

        // Username unikal olmalıdır
        if (await _context.Users.AnyAsync(u => u.Username == dto.Username && u.Id != dto.Id && !u.IsDeleted))
            throw new Exception("Bu istifadəçi adı başqa biri tərəfindən istifadə olunur!");

        _mapper.Map(dto, user);

        if (!string.IsNullOrWhiteSpace(dto.Password))
            user.PasswordHash = dto.Password.Trim();

        user.LastModifiedAt = DateTime.UtcNow;
        user.LastModifiedBy = "System";

        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<UserGetDto>> GetAllAsync(Guid companyId)
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Company)
            .Where(u => !u.IsDeleted && u.CompanyId == companyId) // Şirkət filtri
            .ToListAsync();

        return _mapper.Map<IEnumerable<UserGetDto>>(users);
    }

    public async Task<UserGetDto> GetByIdAsync(Guid id, Guid companyId, Guid? viewerUserId = null)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == companyId && !u.IsDeleted);

        if (user == null) throw new Exception("İstifadəçi tapılmadı!");

        var dto = _mapper.Map<UserGetDto>(user);
        if (viewerUserId.HasValue && viewerUserId.Value == id)
            dto.PanelPassword = string.IsNullOrEmpty(user.PasswordHash) ? null : user.PasswordHash;
        return dto;
    }

    public async Task DeleteAsync(Guid id, Guid companyId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == companyId);
        if (user == null) throw new Exception("İstifadəçi tapılmadı!");

        user.IsDeleted = true;
        user.IsActive = false;

        await _context.SaveChangesAsync();
    }
}