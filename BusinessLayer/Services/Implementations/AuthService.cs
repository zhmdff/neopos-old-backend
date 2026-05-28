using AutoMapper;
using BusinessLayer.DTOs.Auth;
using BusinessLayer.ExternalServices.Abstractions;
using BusinessLayer.Services.Abstractions;
using BusinessLayer.Utilities;
using DAL.Server.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BusinessLayer.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly IJwtTokenService _tokenService;

    public AuthService(AppDbContext context, IMapper mapper, IJwtTokenService tokenService)
    {
        _context = context;
        _mapper = mapper;
        _tokenService = tokenService;
    }

    public async Task<LoginResponseDTO> LoginAsync(LoginRequestDTO request)
    {
        var username = (request.Username ?? string.Empty).Trim();
        if (username.Length == 0) throw new Exception("İstifadəçi adı boş ola bilməz.");
        var password = (request.Password ?? string.Empty).Trim();
        if (password.Length == 0) throw new Exception("Şifrə boş ola bilməz.");

        var users = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Company)
            .Where(u => u.Username == username && u.IsActive && !u.IsDeleted)
            .ToListAsync();

        var matches = users
            // Android/WebView klaviatura/autofill bəzən boşluq əlavə edir; server tərəfdə də trim edirik.
            .Where(u => u.PasswordHash == password)
            .ToList();

        if (matches.Count == 0)
            throw new Exception("İstifadəçi adı və ya şifrə yanlışdır.");

        // Default şirkət: admin olanı üstün tut (yoxdursa birinci)
        var primary = matches
            .OrderByDescending(u => u.Role != null && u.Role.IsAdmin)
            .ThenBy(u => u.Company?.NameAz)
            .First();

        if (primary.Company != null && CompanyPackageExpiry.IsExpired(primary.Company.PackageEndDate))
            throw new Exception(CompanyPackageExpiry.ExpiredMessageAz);

        var token = await _tokenService.GenerateJwtToken(primary);

        var response = _mapper.Map<LoginResponseDTO>(primary);
        response.Token = token;

        // Şirkət siyahısı:
        // 1) Əgər linked hesab varsa: həmin LinkedAccountId ilə bağlı bütün user-ların şirkətləri
        // 2) Yoxdursa: eyni username+password ilə match olanlar (köhnə davranış)
        var companyUsers = primary.LinkedAccountId.HasValue
            ? await _context.Users
                .Include(x => x.Company)
                .Where(x =>
                    x.LinkedAccountId == primary.LinkedAccountId &&
                    x.IsActive &&
                    !x.IsDeleted)
                .ToListAsync()
            : matches;

        response.Companies = companyUsers
            .GroupBy(u => u.CompanyId)
            .Select(g =>
            {
                var u = g.First();
                return new UserCompanyBriefDTO
                {
                    CompanyId = u.CompanyId,
                    CompanyName = u.Company?.NameAz ?? string.Empty,
                    CompanyNameEn = u.Company?.NameEn ?? string.Empty,
                    PackageEndDate = u.Company?.PackageEndDate ?? default
                };
            })
            .OrderBy(x => x.CompanyNameEn)
            .ThenBy(x => x.CompanyName)
            .ToList();

        return response;
    }

    public async Task<LoginResponseDTO> PinLoginAsync(PinLoginRequestDTO request)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.CompanyId == request.CompanyId
                                   && u.PinCode == request.PinCode
                                   && u.IsActive
                                   && !u.IsDeleted);

        if (user == null)
        {
            var checkUserByPin = await _context.Users.AnyAsync(u => u.PinCode == request.PinCode);

            if (!checkUserByPin)
                throw new Exception("Bu PIN-ə sahib istifadəçi bazada tapılmadı.");

            throw new Exception("PIN düzdür, amma istifadəçinin Şirkət ID-si (CompanyId) terminalın ID-si ilə uyğun gəlmir!");
        }

        if (user.Company != null && CompanyPackageExpiry.IsExpired(user.Company.PackageEndDate))
            throw new Exception(CompanyPackageExpiry.ExpiredMessageAz);

        var token = await _tokenService.GenerateJwtToken(user);
        var response = _mapper.Map<LoginResponseDTO>(user);
        response.Token = token;

        // Eyni PIN ilə bir neçə obyekt (linked və ya eyni username) — terminalda obyekt seçici üçün.
        var companyUsers = user.LinkedAccountId.HasValue
            ? await _context.Users
                .Include(x => x.Company)
                .Where(x =>
                    x.LinkedAccountId == user.LinkedAccountId &&
                    x.IsActive &&
                    !x.IsDeleted)
                .ToListAsync()
            : await _context.Users
                .Include(x => x.Company)
                .Where(x =>
                    x.Username == user.Username &&
                    x.PinCode == request.PinCode &&
                    x.IsActive &&
                    !x.IsDeleted)
                .ToListAsync();

        response.Companies = companyUsers
            .GroupBy(x => x.CompanyId)
            .Select(g =>
            {
                var x = g.First();
                return new UserCompanyBriefDTO
                {
                    CompanyId = x.CompanyId,
                    CompanyName = x.Company?.NameAz ?? string.Empty,
                    CompanyNameEn = x.Company?.NameEn ?? string.Empty,
                    PackageEndDate = x.Company?.PackageEndDate ?? default
                };
            })
            .OrderBy(x => x.CompanyNameEn)
            .ThenBy(x => x.CompanyName)
            .ToList();

        return response;
    }

    public async Task<LoginResponseDTO> WaiterShiftLoginAsync(WaiterShiftLoginRequestDTO request)
    {
        var code = (request.AccessCode ?? "").Trim().Replace(" ", "");
        if (request.CompanyId == Guid.Empty)
            throw new Exception("Şirkət mütləqdir.");

        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CompanyId && !c.IsDeleted);

        if (company == null)
            throw new Exception("Şirkət tapılmadı.");

        if (CompanyPackageExpiry.IsExpired(company.PackageEndDate))
            throw new Exception(CompanyPackageExpiry.ExpiredMessageAz);

        var shift = await _context.CashShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == request.CompanyId && !s.IsClosed);

        if (shift == null)
            throw new Exception("Açıq növbə yoxdur. Əvvəl kassada növbə açılmalıdır.");

        // Kod boşdursa — sadə QR / ofisiant telefonu; kod varsa — əvvəlki yoxlama.
        if (code.Length > 0 &&
            (string.IsNullOrWhiteSpace(shift.WaiterAccessCode) || shift.WaiterAccessCode != code))
            throw new Exception("Ofisiant kodu yanlışdır və ya köhnəlib.");

        var token = await _tokenService.GenerateWaiterSessionToken(
            request.CompanyId,
            shift.Id,
            company.NameAz);

        return new LoginResponseDTO
        {
            Id = Guid.Empty,
            Token = token,
            FullName = "Ofisiant",
            RoleName = "Waiter",
            CompanyId = request.CompanyId,
            CompanyName = company.NameAz,
            PackageEndDate = company.PackageEndDate,
            Permissions = new List<int>(),
            RoleIsAdmin = false
        };
    }

    public async Task<LoginResponseDTO> SwitchCompanyAsync(Guid currentUserId, Guid companyId)
    {
        if (currentUserId == Guid.Empty) throw new Exception("İstifadəçi tapılmadı.");
        if (companyId == Guid.Empty) throw new Exception("Şirkət düzgün deyil.");

        var current = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive && !u.IsDeleted);

        if (current == null) throw new Exception("İstifadəçi tapılmadı.");

        // Linked hesab varsa: həmin linked hesabdan seçilmiş şirkətin user row-sunu tap
        // Yoxdursa: eyni username ilə (köhnə davranış)
        var target = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u =>
                u.CompanyId == companyId &&
                u.IsActive &&
                !u.IsDeleted &&
                (current.LinkedAccountId.HasValue
                    ? u.LinkedAccountId == current.LinkedAccountId
                    : u.Username == current.Username));

        if (target == null) throw new Exception("Bu istifadəçinin seçilən şirkətə girişi yoxdur.");

        if (target.Company != null && CompanyPackageExpiry.IsExpired(target.Company.PackageEndDate))
            throw new Exception(CompanyPackageExpiry.ExpiredMessageAz);

        var token = await _tokenService.GenerateJwtToken(target);
        var response = _mapper.Map<LoginResponseDTO>(target);
        response.Token = token;

        // selector üçün şirkət siyahısı
        var all = current.LinkedAccountId.HasValue
            ? await _context.Users
                .Include(x => x.Company)
                .Where(x => x.LinkedAccountId == current.LinkedAccountId && x.IsActive && !x.IsDeleted)
                .ToListAsync()
            : await _context.Users
                .Include(x => x.Company)
                .Where(x => x.Username == current.Username && x.IsActive && !x.IsDeleted)
                .ToListAsync();

        response.Companies = all
            .GroupBy(x => x.CompanyId)
            .Select(g =>
            {
                var x = g.First();
                return new UserCompanyBriefDTO
                {
                    CompanyId = x.CompanyId,
                    CompanyName = x.Company?.NameAz ?? string.Empty,
                    CompanyNameEn = x.Company?.NameEn ?? string.Empty,
                    PackageEndDate = x.Company?.PackageEndDate ?? default
                };
            })
            .OrderBy(x => x.CompanyNameEn)
            .ThenBy(x => x.CompanyName)
            .ToList();

        return response;
    }

    public async Task<LoginResponseDTO> LinkAccountsAsync(Guid currentUserId, Guid otherUserId)
    {
        if (currentUserId == Guid.Empty || otherUserId == Guid.Empty)
            throw new Exception("İstifadəçi düzgün deyil.");
        if (currentUserId == otherUserId)
            throw new Exception("Eyni istifadəçini link etmək olmaz.");

        var current = await _context.Users
            .Include(u => u.Role).Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive && !u.IsDeleted);
        var other = await _context.Users
            .Include(u => u.Role).Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Id == otherUserId && u.IsActive && !u.IsDeleted);

        if (current == null || other == null)
            throw new Exception("İstifadəçi tapılmadı.");

        var linkId = current.LinkedAccountId ?? other.LinkedAccountId ?? Guid.NewGuid();
        current.LinkedAccountId = linkId;
        other.LinkedAccountId = linkId;

        _context.Users.Update(current);
        _context.Users.Update(other);
        await _context.SaveChangesAsync();

        // Cari user şirkətində yenilənmiş token və şirkət siyahısı qaytar
        var token = await _tokenService.GenerateJwtToken(current);
        var response = _mapper.Map<LoginResponseDTO>(current);
        response.Token = token;

        var all = await _context.Users
            .Include(x => x.Company)
            .Where(x => x.LinkedAccountId == linkId && x.IsActive && !x.IsDeleted)
            .ToListAsync();

        response.Companies = all
            .GroupBy(x => x.CompanyId)
            .Select(g =>
            {
                var x = g.First();
                return new UserCompanyBriefDTO
                {
                    CompanyId = x.CompanyId,
                    CompanyName = x.Company?.NameAz ?? string.Empty,
                    CompanyNameEn = x.Company?.NameEn ?? string.Empty,
                    PackageEndDate = x.Company?.PackageEndDate ?? default
                };
            })
            .OrderBy(x => x.CompanyNameEn)
            .ThenBy(x => x.CompanyName)
            .ToList();

        return response;
    }
}