using BusinessLayer.Utilities;
using DAL.Server.Context;
using Domain.Common.Entities;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace NeoPos.WebAPI.Services;

/// <summary>
/// Terminal admin login: verify user on master (Neon), then pull full tenant into local SQLite.
/// </summary>
public sealed class TenantMasterLoginBootstrapService
{
    private readonly AppDbContext _localDb;
    private readonly RemoteDbContext? _remoteDb;
    private readonly DatabaseSyncService? _syncService;
    private readonly ILogger<TenantMasterLoginBootstrapService> _logger;

    public TenantMasterLoginBootstrapService(
        AppDbContext localDb,
        IServiceProvider serviceProvider,
        ILogger<TenantMasterLoginBootstrapService> logger)
    {
        _localDb = localDb;
        _remoteDb = serviceProvider.GetService<RemoteDbContext>();
        _syncService = serviceProvider.GetService<DatabaseSyncService>();
        _logger = logger;
    }

    /// <summary>
    /// If credentials match a user on master, ensure local tenant shell exists and sync data down.
    /// </summary>
    public async Task TryPrepareLocalFromMasterAsync(
        string username,
        string password,
        CancellationToken ct = default)
    {
        if (_remoteDb == null || _syncService == null)
            return;

        username = (username ?? string.Empty).Trim();
        password = (password ?? string.Empty).Trim();
        if (username.Length == 0 || password.Length == 0)
            return;

        try
        {
            if (!await _remoteDb.Database.CanConnectAsync(ct))
            {
                _logger.LogDebug("Master DB unreachable — skipping login bootstrap.");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Master DB connectivity check failed — skipping login bootstrap.");
            return;
        }

        var masterUsers = await _remoteDb.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Where(u => u.Username.ToLower() == username.ToLower() && u.IsActive && !u.IsDeleted)
            .ToListAsync(ct);

        if (masterUsers.Count == 0)
            return;

        var matches = masterUsers
            .Where(u => PasswordHashHelper.Verify(password, u.PasswordHash))
            .ToList();

        if (matches.Count == 0)
            return;

        var primary = matches
            .OrderByDescending(u => u.Role != null && u.Role.IsAdmin)
            .ThenBy(u => u.CompanyId)
            .First();

        var masterCompany = await _remoteDb.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == primary.CompanyId && !c.IsDeleted, ct);

        if (masterCompany == null || string.IsNullOrWhiteSpace(masterCompany.TenantKey))
            return;

        _logger.LogInformation(
            "Master login bootstrap: {Username} → tenant {TenantKey} ({CompanyName})",
            username,
            masterCompany.TenantKey,
            masterCompany.NameAz);

        await EnsureLocalTenantShellAsync(masterCompany, ct);

        _logger.LogInformation(
            "Pulling login essentials (company, roles, users) for tenant {TenantKey}. Full catalog sync runs in background.",
            masterCompany.TenantKey);

        await _syncService.PullLoginEssentialsAsync(ct);
        _syncService.ScheduleBackgroundSync();
    }

    private async Task EnsureLocalTenantShellAsync(Company masterCompany, CancellationToken ct)
    {
        var tenantKey = masterCompany.TenantKey!.Trim();
        var now = DateTime.UtcNow;

        var localCompany = await _localDb.Companies
            .FirstOrDefaultAsync(c => c.TenantKey == tenantKey, ct);

        if (localCompany == null)
        {
            localCompany = new Company
            {
                Id = Guid.NewGuid(),
                TenantKey = tenantKey,
                CreatedAt = now,
                CreatedBy = "MasterLoginBootstrap",
                IsSynced = false,
            };
            _localDb.Companies.Add(localCompany);
        }

        ApplyMasterCompanyFields(localCompany, masterCompany);
        localCompany.TenantKey = tenantKey;
        localCompany.IsSynced = false;

        var metadata = await _localDb.LocalSyncMetadata.FirstOrDefaultAsync(ct);
        if (metadata == null)
        {
            metadata = new LocalSyncMetadata
            {
                Id = Guid.NewGuid(),
                TenantKey = tenantKey,
                LastSyncStatus = "LoginBootstrapPending",
                IsSynced = false,
            };
            _localDb.LocalSyncMetadata.Add(metadata);
        }
        else
        {
            metadata.TenantKey = tenantKey;
        }

        await _localDb.SaveChangesAsync(ct);
    }

    private static void ApplyMasterCompanyFields(Company local, Company master)
    {
        local.Logo = master.Logo;
        local.NameAz = master.NameAz;
        local.NameRu = master.NameRu;
        local.NameEn = master.NameEn;
        local.AddressAz = master.AddressAz;
        local.AddressRu = master.AddressRu;
        local.AddressEn = master.AddressEn;
        local.PhoneNumber1 = master.PhoneNumber1;
        local.PhoneNumber2 = master.PhoneNumber2;
        local.PhoneNumber3 = master.PhoneNumber3;
        local.Slug = master.Slug;
        local.PackageEndDate = master.PackageEndDate;
        local.IsActive = master.IsActive;
        local.IsDeliveryPriceEnabled = master.IsDeliveryPriceEnabled;
        local.IsUserModeActive = master.IsUserModeActive;
        local.IsGuestModeActive = master.IsGuestModeActive;
        local.TablesLayoutMode = master.TablesLayoutMode;
        local.EkassamEnabled = master.EkassamEnabled;
        local.EkassamBaseUrl = master.EkassamBaseUrl;
        local.EkassamApiKey = master.EkassamApiKey;
        local.AutoCashShiftEnabled = master.AutoCashShiftEnabled;
        local.AutoCashShiftOpenTime = master.AutoCashShiftOpenTime;
        local.AutoCashShiftCloseTime = master.AutoCashShiftCloseTime;
        local.AutoCashShiftForceClose = master.AutoCashShiftForceClose;
        local.CashShiftPromptOpeningDeposit = master.CashShiftPromptOpeningDeposit;
        local.CashShiftPrintReportOnClose = master.CashShiftPrintReportOnClose;
        local.CashierPrinterTarget = master.CashierPrinterTarget;
        local.KitchenPrinterTarget = master.KitchenPrinterTarget;
        local.ReceiptDesignSettingsJson = master.ReceiptDesignSettingsJson;
        local.KassaReceiptThankYouText = master.KassaReceiptThankYouText;
        local.PosLockScreenImage = master.PosLockScreenImage;
        local.CustomerDisplayLockScreenImage = master.CustomerDisplayLockScreenImage;
        local.MenuFilterByWorkshop = master.MenuFilterByWorkshop;
        local.TerminalLineDeleteConfirmEnabled = master.TerminalLineDeleteConfirmEnabled;
        local.TelegramBotToken = master.TelegramBotToken;
        local.TelegramNotifyPrefsJson = master.TelegramNotifyPrefsJson;
        local.IsDeleted = master.IsDeleted;
        local.LastModifiedAt = master.LastModifiedAt;
    }
}
