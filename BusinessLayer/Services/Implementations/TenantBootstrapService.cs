using AutoMapper;
using BusinessLayer.DTOs.Auth;
using BusinessLayer.ExternalServices.Abstractions;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace BusinessLayer.Services.Implementations;

public class TenantBootstrapService : ITenantBootstrapService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IMapper _mapper;

    public TenantBootstrapService(
        AppDbContext context,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        IMapper mapper)
    {
        _context = context;
        _configuration = configuration;
        _jwtTokenService = jwtTokenService;
        _mapper = mapper;
    }

    public async Task<LoginResponseDTO> BootstrapAsync(TenantBootstrapRequestDto request)
    {
        var expected = _configuration["NeoPos:TenantBootstrapSecret"]?.Trim();
        if (string.IsNullOrEmpty(expected))
            throw new Exception("Tenant bootstrap serverdə aktiv deyil (NeoPos:TenantBootstrapSecret boşdur).");

        var secret = (request.SetupSecret ?? string.Empty).Trim();
        if (!FixedTimeUtf8Equals(secret, expected))
            throw new Exception("Yanlış təhlükəsizlik açarı.");

        var companyName = (request.CompanyName ?? string.Empty).Trim();
        if (companyName.Length < 2)
            throw new Exception("Şirkət adı çox qısadır.");

        if (companyName.Length > 200)
            companyName = companyName[..200];

        var roleName = (request.AdminRoleName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(roleName))
            roleName = "Administrator";
        if (roleName.Length > 100)
            roleName = roleName[..100];

        var fullName = (request.AdminFullName ?? string.Empty).Trim();
        if (fullName.Length < 2)
            throw new Exception("Tam ad daxil edin.");
        if (fullName.Length > 150)
            fullName = fullName[..150];

        var username = (request.AdminUsername ?? string.Empty).Trim();
        if (username.Length < 2)
            throw new Exception("İstifadəçi adı daxil edin.");
        if (username.Length > 50)
            username = username[..50];

        var password = request.AdminPassword ?? string.Empty;
        if (password.Length < 4)
            throw new Exception("Şifrə çox qısadır.");

        string? pin = string.IsNullOrWhiteSpace(request.AdminPinCode)
            ? null
            : request.AdminPinCode.Trim();
        if (pin != null)
        {
            if (pin.Length is < 4 or > 10)
                throw new Exception("PIN 4–10 simvol olmalıdır.");
            if (!pin.All(char.IsDigit))
                throw new Exception("PIN yalnız rəqəmlərdən ibarət olmalıdır.");
        }

        if (await _context.Users.AnyAsync(u => u.Username == username && !u.IsDeleted))
            throw new Exception("Bu istifadəçi adı artıq mövcuddur.");

        var slugBase = BuildSlugBase(companyName);
        var slug = await MakeUniqueSlugAsync(slugBase);

        var now = DateTime.UtcNow;
        var companyId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var tenantKey = (request.TenantKey ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(tenantKey))
            tenantKey = slug; // fallback: use slug as tenant key if not provided

        var company = new Company
        {
            Id = companyId,
            TenantKey = tenantKey,
            NameAz = companyName,
            NameEn = companyName,
            NameRu = companyName,
            AddressAz = "-",
            AddressEn = "-",
            AddressRu = "-",
            PhoneNumber1 = "-",
            Slug = slug,
            PackageEndDate = now.AddYears(5),
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "TenantBootstrap",
            IsSynced = true,
        };

        var role = new Role
        {
            Id = roleId,
            CompanyId = companyId,
            NameAz = roleName,
            NameEn = roleName,
            NameRu = roleName,
            IsAdmin = true,
            Permissions = [],
            CreatedAt = now,
            CreatedBy = "TenantBootstrap",
            IsSynced = true,
        };

        var user = new User
        {
            Id = userId,
            CompanyId = companyId,
            RoleId = roleId,
            FullName = fullName,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            PinCode = pin,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "TenantBootstrap",
            IsSynced = true,
        };

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.Companies.AddAsync(company);
            await _context.Roles.AddAsync(role);
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        var created = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Company)
            .FirstAsync(u => u.Id == userId);

        var response = _mapper.Map<LoginResponseDTO>(created);
        response.Token = await _jwtTokenService.GenerateJwtToken(created);
        response.Companies =
        [
            new UserCompanyBriefDTO
            {
                CompanyId = created.CompanyId,
                CompanyName = created.Company?.NameAz ?? companyName,
                CompanyNameEn = created.Company?.NameEn ?? companyName,
                PackageEndDate = created.Company?.PackageEndDate ?? company.PackageEndDate,
            }
        ];
        response.Permissions ??= [];
        return response;
    }

    private static bool FixedTimeUtf8Equals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static string BuildSlugBase(string name)
    {
        var sb = new StringBuilder();
        foreach (var ch in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
            else if (ch is ' ' or '-' or '_')
                sb.Append('-');
        }
        var s = sb.ToString().Trim('-');
        while (s.Contains("--"))
            s = s.Replace("--", "-", StringComparison.Ordinal);
        if (s.Length > 40)
            s = s[..40];
        return string.IsNullOrEmpty(s) ? "company" : s;
    }

    private async Task<string> MakeUniqueSlugAsync(string baseSlug)
    {
        for (var i = 0; i < 20; i++)
        {
            var suffix = i == 0 ? string.Empty : $"-{Guid.NewGuid().ToString("N")[..6]}";
            var candidate = baseSlug + suffix;
            var exists = await _context.Companies.AnyAsync(c => c.Slug == candidate);
            if (!exists)
                return candidate;
        }
        return $"{baseSlug}-{Guid.NewGuid():N}";
    }
}
