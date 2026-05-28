using BusinessLayer.DTOs.Company;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace BusinessLayer.Services.Abstractions;

public interface ICompanyService
{
    Task<CompanyGetDto> GetByIdAsync(Guid id);
    Task<bool> UpdateAsync(
        CompanyPutDto dto,
        IFormFile? logoFile,
        IFormFile? posLockScreenFile = null,
        IFormFile? customerDisplayLockScreenFile = null);
    Task<CompanyGetDto> UpdateAutoCashShiftConfigAsync(Guid companyId, Guid userId, AutoCashShiftConfigPutDto dto);
    Task<CompanyGetDto> UpdateReceiptDesignAsync(Guid companyId, Guid userId, CompanyReceiptDesignPutDto dto);
    Task<CompanyGetDto> UpdateTerminalLineDeleteConfirmEnabledAsync(Guid companyId, bool enabled);
    Task<CompanyGetDto> UpdateMenuFilterByWorkshopAsync(Guid companyId, bool enabled);
    Task<CompanyGetDto> UpdateTelegramBotTokenFromTerminalAsync(Guid companyId, string? token);
    Task UpdateTelegramNotifyPrefsFromTerminalAsync(Guid companyId, Dictionary<string, bool>? prefs);
    Task<Dictionary<string, bool>?> GetTelegramNotifyPrefsAsync(Guid companyId);
}