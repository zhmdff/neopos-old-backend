using Application.Interfaces;
using AutoMapper;
using BusinessLayer.DTOs.Company;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Entities;
using Domain.Enums;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BusinessLayer.Services.Implementations;

public class CompanyService : ICompanyService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly IWebHostEnvironment _env;
    private readonly ITranslationService _translationService;
    private readonly IConfiguration _configuration;

    public CompanyService(
        AppDbContext context,
        IMapper mapper,
        IWebHostEnvironment env,
        ITranslationService translationService,
        IConfiguration configuration)
    {
        _context = context;
        _mapper = mapper;
        _env = env;
        _translationService = translationService;
        _configuration = configuration;
    }

    public async Task<CompanyGetDto> GetByIdAsync(Guid id)
    {
        var company = await _context.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (company == null) throw new Exception("Şirkət tapılmadı!");

        return _mapper.Map<CompanyGetDto>(company);
    }

    public async Task<bool> UpdateAsync(
        CompanyPutDto dto,
        IFormFile? logoFile,
        IFormFile? posLockScreenFile = null,
        IFormFile? customerDisplayLockScreenFile = null)
    {
        var company = await _context.Companies.FindAsync(dto.Id);
        if (company == null) throw new Exception("Şirkət tapılmadı!");

        company.NameAz = dto.NameAz;
        company.AddressAz = dto.AddressAz;
        company.PhoneNumber1 = dto.PhoneNumber1;
        company.PhoneNumber2 = dto.PhoneNumber2;
        company.PhoneNumber3 = dto.PhoneNumber3;
        company.IsActive = dto.IsActive;

        if (dto.KassaReceiptThankYouText != null)
            company.KassaReceiptThankYouText = string.IsNullOrWhiteSpace(dto.KassaReceiptThankYouText)
                ? null
                : dto.KassaReceiptThankYouText.Trim().Length > 500
                    ? dto.KassaReceiptThankYouText.Trim()[..500]
                    : dto.KassaReceiptThankYouText.Trim();

        if (dto.TablesLayoutMode.HasValue &&
            Enum.IsDefined(typeof(TablesLayoutMode), dto.TablesLayoutMode.Value))
        {
            company.TablesLayoutMode = (TablesLayoutMode)dto.TablesLayoutMode.Value;
        }

        company.EkassamEnabled = dto.EkassamEnabled;
        company.EkassamBaseUrl = string.IsNullOrWhiteSpace(dto.EkassamBaseUrl)
            ? null
            : dto.EkassamBaseUrl.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(dto.EkassamApiKey))
            company.EkassamApiKey = dto.EkassamApiKey.Trim();

        if (dto.IsGuestModeActive.HasValue)
            company.IsGuestModeActive = dto.IsGuestModeActive.Value;

        var nameTrans = await _translationService.TranslateTextAsync(dto.NameAz, new List<string> { "en", "ru" });
        company.NameEn = nameTrans.GetValueOrDefault("en", dto.NameAz);
        company.NameRu = nameTrans.GetValueOrDefault("ru", dto.NameAz);

        var addrTrans = await _translationService.TranslateTextAsync(dto.AddressAz, new List<string> { "en", "ru" });
        company.AddressEn = addrTrans.GetValueOrDefault("en", dto.AddressAz);
        company.AddressRu = addrTrans.GetValueOrDefault("ru", dto.AddressAz);

        if (logoFile != null)
        {
            company.Logo = await UploadCompanyImage(logoFile, "logos", company.Logo);
        }
        else if (dto.ClearCompanyLogo)
        {
            TryDeleteWebRootRelativeFile(company.Logo);
            company.Logo = null;
        }

        if (posLockScreenFile != null)
        {
            company.PosLockScreenImage = await UploadCompanyImage(
                posLockScreenFile,
                "company-screens/pos-lock",
                company.PosLockScreenImage);
        }
        else if (dto.ClearPosLockScreenImage)
        {
            TryDeleteWebRootRelativeFile(company.PosLockScreenImage);
            company.PosLockScreenImage = null;
        }

        if (customerDisplayLockScreenFile != null)
        {
            company.CustomerDisplayLockScreenImage = await UploadCompanyImage(
                customerDisplayLockScreenFile,
                "company-screens/customer-display",
                company.CustomerDisplayLockScreenImage);
        }

        return await _context.SaveChangesAsync() > 0;
    }

    private async Task<string> UploadCompanyImage(IFormFile file, string relativeFolder, string? oldRelativePath)
    {
        string rootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        string folderPath = Path.Combine(rootPath, "uploads", relativeFolder.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        if (!string.IsNullOrEmpty(oldRelativePath))
        {
            string oldFullPath = Path.Combine(rootPath, oldRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(oldFullPath)) File.Delete(oldFullPath);
        }

        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        string fullPath = Path.Combine(folderPath, fileName);

        using (var fileStream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        return $"/uploads/{relativeFolder}/{fileName}".Replace('\\', '/');
    }

    private void TryDeleteWebRootRelativeFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        string rootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        string fullPath = Path.Combine(rootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }

    private static void ValidateAutoShiftHHmm(string? s, string field)
    {
        var v = (s ?? "").Trim();
        if (!Regex.IsMatch(v, @"^\d{1,2}:\d{2}$")) throw new Exception($"{field}: vaxt HH:MM formatında olmalıdır.");
        var p = v.Split(':');
        var h = int.Parse(p[0]);
        var m = int.Parse(p[1]);
        if (h is < 0 or > 23 || m is < 0 or > 59) throw new Exception($"{field}: saat 00:00–23:59 aralığında olmalıdır.");
    }

    public async Task<CompanyGetDto> UpdateAutoCashShiftConfigAsync(Guid companyId, Guid userId, AutoCashShiftConfigPutDto dto)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId);
        if (user == null) throw new Exception("İstifadəçi tapılmadı və ya bu şirkətə aid deyil.");

        var permissions = user.Role?.Permissions ?? [];
        if (user.Role?.IsAdmin != true && !permissions.Contains(20))
            throw new Exception("Bu əməliyyat üçün kassa növbəsi icazəsi (20) və ya admin lazımdır.");

        ValidateAutoShiftHHmm(dto.OpenTime, "Açılış");
        ValidateAutoShiftHHmm(dto.CloseTime, "Bağlanış");
        var openT = dto.OpenTime.Trim();
        var closeT = dto.CloseTime.Trim();
        if (string.Equals(openT, closeT, StringComparison.Ordinal))
            throw new Exception("Açılış və bağlanış eyni ola bilməz.");

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId)
            ?? throw new Exception("Şirkət tapılmadı.");

        company.AutoCashShiftEnabled = dto.Enabled;
        company.AutoCashShiftOpenTime = openT;
        company.AutoCashShiftCloseTime = closeT;
        company.AutoCashShiftForceClose = dto.ForceClose;
        company.CashShiftPromptOpeningDeposit = dto.PromptOpeningDeposit;
        company.CashShiftPrintReportOnClose = dto.PrintReportOnClose;
        await _context.SaveChangesAsync();
        return await GetByIdAsync(companyId);
    }

    public async Task<CompanyGetDto> UpdateReceiptDesignAsync(Guid companyId, Guid userId, CompanyReceiptDesignPutDto dto)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId);
        if (user == null) throw new Exception("İstifadəçi tapılmadı və ya bu şirkətə aid deyil.");

        // Bu hissə “əsas settings” kimidir: admin və ya uyğun icazə ilə.
        // Repo-da printer permission ayrıca deyilsə, admin olmayanlara bloklamayaq.

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId)
            ?? throw new Exception("Şirkət tapılmadı.");

        company.CashierPrinterTarget = string.IsNullOrWhiteSpace(dto.CashierPrinterTarget)
            ? null
            : dto.CashierPrinterTarget.Trim();
        company.KitchenPrinterTarget = string.IsNullOrWhiteSpace(dto.KitchenPrinterTarget)
            ? null
            : dto.KitchenPrinterTarget.Trim();

        company.ReceiptDesignSettingsJson = string.IsNullOrWhiteSpace(dto.ReceiptDesignSettingsJson)
            ? null
            : dto.ReceiptDesignSettingsJson.Trim();

        if (dto.KassaReceiptThankYouText != null)
            company.KassaReceiptThankYouText = string.IsNullOrWhiteSpace(dto.KassaReceiptThankYouText)
                ? null
                : dto.KassaReceiptThankYouText.Trim().Length > 500
                    ? dto.KassaReceiptThankYouText.Trim()[..500]
                    : dto.KassaReceiptThankYouText.Trim();

        await _context.SaveChangesAsync();
        return await GetByIdAsync(companyId);
    }

    public async Task<CompanyGetDto> UpdateTerminalLineDeleteConfirmEnabledAsync(Guid companyId, bool enabled)
    {
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId)
            ?? throw new Exception("Şirkət tapılmadı.");
        company.TerminalLineDeleteConfirmEnabled = enabled;
        await _context.SaveChangesAsync();
        return await GetByIdAsync(companyId);
    }

    public async Task<CompanyGetDto> UpdateMenuFilterByWorkshopAsync(Guid companyId, bool enabled)
    {
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId)
            ?? throw new Exception("Şirkət tapılmadı.");
        company.MenuFilterByWorkshop = enabled;
        await _context.SaveChangesAsync();
        return await GetByIdAsync(companyId);
    }

    public async Task<CompanyGetDto> UpdateTelegramBotTokenFromTerminalAsync(Guid companyId, string? token)
    {
        // Token appsettings «BossTelegram:BotToken»-dədirsə terminal DB-yə yazmır.
        var cfgToken = _configuration["BossTelegram:BotToken"]?.Trim();
        if (!string.IsNullOrEmpty(cfgToken))
            return await GetByIdAsync(companyId);

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId)
            ?? throw new Exception("Şirkət tapılmadı.");
        var t = token?.Trim();
        company.TelegramBotToken = string.IsNullOrEmpty(t) ? null : (t.Length > 512 ? t[..512] : t);
        await _context.SaveChangesAsync();
        return await GetByIdAsync(companyId);
    }

    public async Task UpdateTelegramNotifyPrefsFromTerminalAsync(Guid companyId, Dictionary<string, bool>? prefs)
    {
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId)
            ?? throw new Exception("Şirkət tapılmadı.");
        if (prefs == null || prefs.Count == 0)
        {
            company.TelegramNotifyPrefsJson = null;
        }
        else
        {
            var json = System.Text.Json.JsonSerializer.Serialize(prefs);
            company.TelegramNotifyPrefsJson = json.Length > 4000 ? json[..4000] : json;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<string, bool>?> GetTelegramNotifyPrefsAsync(Guid companyId)
    {
        var json = await _context.Companies
            .AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.TelegramNotifyPrefsJson)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
        }
        catch
        {
            return null;
        }
    }
}