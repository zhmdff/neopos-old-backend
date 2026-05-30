using DAL.Server.Context;
using Domain.Common;
using Domain.Common.Entities;
using Domain.Entities;
using BusinessLayer.Utilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;

namespace NeoPos.WebAPI.Services;

public class DatabaseSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSyncService> _logger;

    private int _failCount = 0;
    private const int MaxBackoffMinutes = 60;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public static bool IsOutageSimulated { get; set; } = false;

    public DatabaseSyncService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<DatabaseSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var initialDelaySeconds = _configuration.GetValue<int>("Sync:InitialDelaySeconds", 30);
        var intervalMinutes = _configuration.GetValue<int>("Sync:IntervalMinutes", 5);

        _logger.LogInformation("Database Sync Service is starting with {InitialDelay}s initial delay.", initialDelaySeconds);

        await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (IsOutageSimulated)
            {
                _logger.LogWarning("Database synchronization is skipped (Outage Simulation Active).");
            }
            else
            {
                try
                {
                    await RunSerializedSyncAsync(stoppingToken);
                    _failCount = 0; 
                }
                catch (Exception ex)
                {
                    _failCount++;
                    if (IsMasterDbUnreachable(ex))
                    {
                        _logger.LogWarning(
                            "Master database unreachable — sync skipped (internet outage or wrong Neon host in connection string). Fail count: {FailCount}. {Reason}",
                            _failCount,
                            GetConnectivityFailureSummary(ex));
                    }
                    else
                    {
                        _logger.LogError(ex, "Error occurred during database synchronization. Fail count: {FailCount}", _failCount);
                    }
                }
            }

            var nextDelayMinutes = intervalMinutes;
            if (_failCount > 0)
            {
                nextDelayMinutes = (int)Math.Min(intervalMinutes * Math.Pow(2, _failCount - 1), MaxBackoffMinutes);
                _logger.LogInformation("Next sync attempt in {Minutes} minutes due to backoff.", nextDelayMinutes);
            }

            await Task.Delay(TimeSpan.FromMinutes(nextDelayMinutes), stoppingToken);
        }
    }

    public async Task TriggerSyncAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Manual sync triggered via API — waiting for any in-progress sync to finish.");
        await RunSerializedSyncAsync(ct);
    }

    /// <summary>
    /// Fast path for login bootstrap: company + roles + users only (no catalog/orders/media).
    /// </summary>
    public async Task PullLoginEssentialsAsync(CancellationToken ct = default)
    {
        await _syncLock.WaitAsync(ct);
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var localDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var remoteDb = scope.ServiceProvider.GetService<RemoteDbContext>();
            if (remoteDb == null)
            {
                _logger.LogWarning("RemoteDbContext not registered — skipping login essentials pull.");
                return;
            }

            var metadata = await localDb.LocalSyncMetadata.FirstOrDefaultAsync(ct);
            if (metadata == null)
            {
                _logger.LogWarning("System not bootstrapped — skipping login essentials pull.");
                return;
            }

            var company = !string.IsNullOrWhiteSpace(metadata.TenantKey)
                ? await localDb.Companies.FirstOrDefaultAsync(c => c.TenantKey == metadata.TenantKey, ct)
                  ?? await localDb.Companies.FirstOrDefaultAsync(ct)
                : await localDb.Companies.FirstOrDefaultAsync(ct);
            if (company == null)
            {
                _logger.LogError("No company found in local database — cannot pull login essentials.");
                return;
            }

            var masterCompany = await remoteDb.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantKey == company.TenantKey, ct);
            var remoteCompanyId = masterCompany?.Id ?? company.Id;
            var localCompanyId = company.Id;

            _logger.LogInformation(
                "Login essentials pull for tenant {TenantKey} (local company {LocalId}, master {MasterId}).",
                company.TenantKey,
                localCompanyId,
                remoteCompanyId);

            await SyncTableAsync<Company>(localDb, remoteDb, localCompanyId, remoteCompanyId, DateTime.MinValue, ct);
            await SyncAllUsersToLocalAsync(localDb, remoteDb, localCompanyId, remoteCompanyId, ct);

            metadata.LastSyncStatus = "LoginEssentials";
            await localDb.SaveChangesAsync(ct);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Non-blocking full sync — used after login so the HTTP request can return quickly.
    /// </summary>
    public void ScheduleBackgroundSync()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await TriggerSyncAsync();
            }
            catch (Exception ex) when (IsMasterDbUnreachable(ex))
            {
                _logger.LogDebug(ex, "Background sync skipped — master unreachable.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background sync after login failed.");
            }
        });
    }

    private async Task RunSerializedSyncAsync(CancellationToken ct)
    {
        await _syncLock.WaitAsync(ct);
        try
        {
            await SyncDataAsync(ct);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task SyncDataAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var localDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var remoteDb = scope.ServiceProvider.GetService<RemoteDbContext>();
        var mediaSync = scope.ServiceProvider.GetRequiredService<IMediaSyncService>();

        if (remoteDb == null)
        {
            _logger.LogWarning("RemoteDbContext is not registered (tenant mode). Skipping DB-level sync — data is managed via master HTTP API.");
            return;
        }

        var metadata = await localDb.LocalSyncMetadata.FirstOrDefaultAsync(stoppingToken);
        if (metadata == null)
        {
            _logger.LogWarning("System not bootstrapped. Skipping background sync.");
            return;
        }

        var company = !string.IsNullOrWhiteSpace(metadata.TenantKey)
            ? await localDb.Companies.FirstOrDefaultAsync(c => c.TenantKey == metadata.TenantKey, stoppingToken)
              ?? await localDb.Companies.FirstOrDefaultAsync(stoppingToken)
            : await localDb.Companies.FirstOrDefaultAsync(stoppingToken);
        if (company == null)
        {
            _logger.LogError("No company found in local database. Cannot sync.");
            return;
        }

        var lastSync = metadata.LastSuccessfulSyncAt ?? DateTime.MinValue;
        var syncStartTime = DateTime.UtcNow;
        if (!metadata.LastSuccessfulSyncAt.HasValue)
        {
            _logger.LogInformation("First sync cycle — pulling all master data modified since epoch.");
        }

        // Master company row (e.g. from create_tenant.py) may use a different Id than local bootstrap.
        var masterCompany = await remoteDb.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantKey == company.TenantKey, stoppingToken);
        var remoteCompanyId = masterCompany?.Id ?? company.Id;
        if (masterCompany != null && masterCompany.Id != company.Id)
        {
            _logger.LogInformation(
                "Sync uses master CompanyId {MasterId} (local {LocalId}, TenantKey {TenantKey}).",
                masterCompany.Id, company.Id, company.TenantKey);
        }

        _logger.LogInformation("Starting bi-directional sync cycle. Last sync: {LastSync}", lastSync);

        var syncOrder = new List<Type>
        {
            typeof(Company),
            typeof(Role),
            typeof(User),
            typeof(Hall),
            typeof(Table),
            typeof(Workshop),
            typeof(Category),
            typeof(Product),
            typeof(ProductWorkshop),
            typeof(ProductVariant),
            typeof(CashShift),
            typeof(CashShiftExpense),
            typeof(Customer),
            typeof(OrderHeader),
            typeof(OrderDetail),
            typeof(KitchenOperation),
            typeof(OrderSplitPayment),
            typeof(ProductSet),
            typeof(ProductSetItem),
            typeof(ProductSetChoiceGroup),
            typeof(ProductSetChoiceOption),
            typeof(QRMenuSetting),
            typeof(Warehouse),
            typeof(Supplier),
            typeof(Purchase),
            typeof(ProductStockHistory),
            typeof(AuditLog),
            typeof(BossWebPushSubscription),
            typeof(BossTelegramChat),
            typeof(PendingOrderLineDeleteConfirm),
            typeof(CompanyPaymentMethod),
            typeof(HallTimeDiscountRule)
        };

        var allDbSetTypes = typeof(AppDbContext)
            .GetProperties()
            .Where(p => p.PropertyType.IsGenericType && 
                        p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .ToList();

        var finalOrder = syncOrder.Where(t => allDbSetTypes.Contains(t)).ToList();
        finalOrder.AddRange(allDbSetTypes.Except(finalOrder));

        foreach (var entityType in finalOrder)
        {
            if (entityType == typeof(LocalSyncMetadata))
                continue;

            if (typeof(BaseEntity).IsAssignableFrom(entityType))
            {
                await InvokeSyncTableAsync(localDb, remoteDb, entityType, company.Id, remoteCompanyId, lastSync, stoppingToken);
            }
        }

        // --- MEDIA SYNC: wwwroot/uploads (pull from master + push new local files) ---
        try
        {
            var masterWebBase = ResolveMasterWebBaseUrl();
            var mediaPaths = await MediaPathCollector.CollectFromDatabasesAsync(
                localDb, remoteDb, company.Id, remoteCompanyId, stoppingToken);
            var uploadSecret = _configuration["Sync:MediaUploadSecret"]?.Trim();
            if (string.IsNullOrEmpty(uploadSecret))
                uploadSecret = _configuration["NeoPos:TenantBootstrapSecret"]?.Trim();

            await mediaSync.SyncUploadsAsync(new MediaSyncRequest
            {
                MasterWebBaseUrl = masterWebBase,
                UploadSecret = uploadSecret,
                DbReferencedPaths = mediaPaths.ToList(),
                ScanLocalUploadsFolder = _configuration.GetValue("Sync:ScanLocalUploadsFolder", true),
            }, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Media sync failed but data sync continued.");
        }

        metadata.LastSuccessfulSyncAt = syncStartTime;
        metadata.LastSyncStatus = "Success";
        await localDb.SaveChangesAsync(stoppingToken);

        _logger.LogInformation("Sync cycle completed successfully.");
    }

    private async Task InvokeSyncTableAsync(AppDbContext localDb, RemoteDbContext remoteDb, Type entityType, Guid localCompanyId, Guid remoteCompanyId, DateTime lastSync, CancellationToken stoppingToken)
    {
        var method = typeof(DatabaseSyncService)
            .GetMethod(nameof(SyncTableAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.MakeGenericMethod(entityType);

        if (method != null)
        {
            await (Task)method.Invoke(this, new object[] { localDb, remoteDb, localCompanyId, remoteCompanyId, lastSync, stoppingToken })!;
        }
    }

    private async Task SyncTableAsync<T>(AppDbContext localDb, RemoteDbContext remoteDb, Guid localCompanyId, Guid remoteCompanyId, DateTime lastSync, CancellationToken stoppingToken) where T : BaseEntity
    {
        var type = typeof(T);

        var bossManagedConfig = new HashSet<Type> { 
            typeof(Company), typeof(Role), typeof(User), typeof(Hall), typeof(Table), 
            typeof(Workshop), typeof(Category), typeof(Product), typeof(ProductWorkshop), 
            typeof(ProductVariant), typeof(ProductSet), typeof(ProductSetItem), 
            typeof(ProductSetChoiceGroup), typeof(ProductSetChoiceOption), 
            typeof(QRMenuSetting), typeof(Warehouse), typeof(Supplier),
            typeof(CompanyPaymentMethod), typeof(HallTimeDiscountRule)
        };

        var staffManagedTransactions = new HashSet<Type> {
            typeof(CashShift), typeof(CashShiftExpense), typeof(OrderHeader), 
            typeof(OrderDetail), typeof(KitchenOperation), typeof(OrderSplitPayment), 
            typeof(Purchase), typeof(ProductStockHistory), typeof(AuditLog),
            typeof(BossWebPushSubscription), typeof(BossTelegramChat), 
            typeof(PendingOrderLineDeleteConfirm)
        };

        // Normally Boss-managed = pull-only; staff-created users/roles must still push to Neon.
        var tenantCreatableBossEntities = new HashSet<Type> { typeof(User), typeof(Role) };
        bool allowPush = !bossManagedConfig.Contains(type)
                         || type == typeof(Customer)
                         || tenantCreatableBossEntities.Contains(type);

        var pushedThisCycle = new HashSet<Guid>();

        if (allowPush)
        {
            var localItems = await localDb.Set<T>()
                .AsNoTracking()
                .ToListAsync(stoppingToken);

            var unsyncedItems = localItems.Where(x => !x.IsSynced).ToList();
            var allLocalIds = localItems.Select(x => x.Id).ToList();

            var existingRemoteItems = new List<T>();
            if (allLocalIds.Any())
            {
                existingRemoteItems = await remoteDb.Set<T>()
                    .Where(x => allLocalIds.Contains(x.Id))
                    .ToListAsync(stoppingToken);
            }

            var existingRemoteIds = existingRemoteItems.Select(x => x.Id).ToHashSet();
            var missingFromRemote = localItems
                .Where(x => x.IsSynced && !existingRemoteIds.Contains(x.Id))
                .ToList();

            // Staff POS data: only push rows still marked unsynced (avoid re-INSERT duplicate PK on Neon).
            var itemsToPush = staffManagedTransactions.Contains(type)
                ? unsyncedItems
                : unsyncedItems.Concat(missingFromRemote).ToList();

            if (itemsToPush.Any())
            {
                _logger.LogInformation("Pushing {Count} items for {Table}", itemsToPush.Count, type.Name);

                foreach (var item in itemsToPush)
                {
                    if (item is AuditableCompanyEntity companyEntity
                        && companyEntity.CompanyId == localCompanyId
                        && remoteCompanyId != localCompanyId)
                    {
                        companyEntity.CompanyId = remoteCompanyId;
                    }

                    if (item is CashShift openShift && !openShift.IsClosed)
                        await CloseStaleRemoteOpenShiftsAsync(remoteDb, openShift, remoteCompanyId, stoppingToken);

                    await UpsertOnRemoteAsync(remoteDb, item, existingRemoteItems, stoppingToken);
                }

                try
                {
                    await remoteDb.SaveChangesAsync(stoppingToken);
                    pushedThisCycle = itemsToPush.Select(x => x.Id).ToHashSet();
                }
                catch (DbUpdateException ex) when (IsPostgresUniqueViolation(ex))
                {
                    _logger.LogWarning(ex, "Push conflict for {Table}; retrying row-by-row upsert.", type.Name);
                    remoteDb.ChangeTracker.Clear();
                    pushedThisCycle = await PushItemsRowByRowAsync(
                        remoteDb, localDb, type, itemsToPush, localCompanyId, remoteCompanyId, stoppingToken);
                }

                if (pushedThisCycle.Count > 0)
                {
                    await localDb.Set<T>()
                        .Where(x => pushedThisCycle.Contains(x.Id))
                        .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsSynced, true), stoppingToken);
                }
            }
        }

        if (type == typeof(User))
        {
            await SyncAllUsersToLocalAsync(localDb, remoteDb, localCompanyId, remoteCompanyId, stoppingToken);
            return;
        }

        if (!staffManagedTransactions.Contains(type) || type == typeof(Customer))
        {
            if (typeof(AuditableEntity).IsAssignableFrom(type))
            {
                var remoteModifiedItems = await remoteDb.Set<T>()
                    .Cast<AuditableEntity>()
                    .Where(x => x.LastModifiedAt > lastSync || x.CreatedAt > lastSync)
                    .AsNoTracking()
                    .ToListAsync(stoppingToken);

                var filteredRemoteData = remoteModifiedItems.Where(x => {
                    if (pushedThisCycle.Contains(x.Id))
                        return false;
                    var prop = x.GetType().GetProperty("CompanyId");
                    if (prop == null) return true; 
                    var val = prop.GetValue(x);
                    return val is Guid id && (id == remoteCompanyId || id == localCompanyId);
                }).Cast<T>().ToList();

                if (filteredRemoteData.Any())
                {
                    _logger.LogInformation("Pulling {Count} updates for {Table}", filteredRemoteData.Count, type.Name);
                    
                    var remoteIds = filteredRemoteData.Select(x => x.Id).ToList();
                    var localItemsToUpdate = await localDb.Set<T>()
                        .Where(x => remoteIds.Contains(x.Id))
                        .ToListAsync(stoppingToken);

                    foreach (var remoteItem in filteredRemoteData)
                    {
                        var localItem = localItemsToUpdate.FirstOrDefault(x => x.Id == remoteItem.Id);
                        remoteItem.IsSynced = true;

                        if (type == typeof(Company))
                        {
                            var remoteCompany = (Company)(object)remoteItem;
                            var localCompany = await localDb.Companies
                                .FirstOrDefaultAsync(c => c.TenantKey == remoteCompany.TenantKey, stoppingToken);
                            if (localCompany != null)
                            {
                                MergeRemoteCompanyIntoLocal(localCompany, remoteCompany);
                                localCompany.IsSynced = true;
                                continue;
                            }
                        }

                        if (remoteItem is AuditableCompanyEntity pulledCompanyEntity
                            && pulledCompanyEntity.CompanyId == remoteCompanyId
                            && remoteCompanyId != localCompanyId)
                        {
                            pulledCompanyEntity.CompanyId = localCompanyId;
                        }

                        if (localItem == null)
                        {
                            localDb.Set<T>().Add(remoteItem);
                        }
                        else
                        {
                            localDb.Entry(localItem).CurrentValues.SetValues(remoteItem);
                        }
                    }
                    await localDb.SaveChangesAsync(stoppingToken);
                }
            }
        }
    }

    private async Task<HashSet<Guid>> PushItemsRowByRowAsync<T>(
        RemoteDbContext remoteDb,
        AppDbContext localDb,
        Type type,
        List<T> itemsToPush,
        Guid localCompanyId,
        Guid remoteCompanyId,
        CancellationToken stoppingToken) where T : BaseEntity
    {
        var pushed = new HashSet<Guid>();
        foreach (var item in itemsToPush)
        {
            try
            {
                if (item is AuditableCompanyEntity companyEntity
                    && companyEntity.CompanyId == localCompanyId
                    && remoteCompanyId != localCompanyId)
                {
                    companyEntity.CompanyId = remoteCompanyId;
                }

                if (item is CashShift openShift && !openShift.IsClosed)
                    await CloseStaleRemoteOpenShiftsAsync(remoteDb, openShift, remoteCompanyId, stoppingToken);

                await UpsertOnRemoteAsync(remoteDb, item, null, stoppingToken);
                await remoteDb.SaveChangesAsync(stoppingToken);
                pushed.Add(item.Id);
            }
            catch (DbUpdateException ex) when (IsPostgresUniqueViolation(ex))
            {
                _logger.LogWarning(ex, "Skipping push for {Table} row {Id} due to remote conflict.", type.Name, item.Id);
            }
            finally
            {
                remoteDb.ChangeTracker.Clear();
            }
        }

        return pushed;
    }

    private async Task SyncAllUsersToLocalAsync(
        AppDbContext localDb,
        RemoteDbContext remoteDb,
        Guid localCompanyId,
        Guid remoteCompanyId,
        CancellationToken stoppingToken)
    {
        var tenantUsers = await remoteDb.Users.AsNoTracking()
            .Where(u => u.CompanyId == remoteCompanyId && !u.IsDeleted)
            .ToListAsync(stoppingToken);

        var linkedIds = tenantUsers
            .Where(u => u.LinkedAccountId.HasValue)
            .Select(u => u.LinkedAccountId!.Value)
            .ToHashSet();

        var usernameSet = tenantUsers
            .Select(u => u.Username)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allUsers = tenantUsers.ToDictionary(u => u.Id);

        if (linkedIds.Count > 0)
        {
            var linked = await remoteDb.Users.AsNoTracking()
                .Where(u => !u.IsDeleted && u.LinkedAccountId != null && linkedIds.Contains(u.LinkedAccountId.Value))
                .ToListAsync(stoppingToken);
            foreach (var u in linked)
                allUsers[u.Id] = u;
        }

        if (usernameSet.Count > 0)
        {
            var others = await remoteDb.Users.AsNoTracking()
                .Where(u => !u.IsDeleted && u.CompanyId != remoteCompanyId)
                .ToListAsync(stoppingToken);
            foreach (var u in others)
            {
                if (usernameSet.Contains(u.Username))
                    allUsers[u.Id] = u;
            }
        }

        var userList = allUsers.Values.ToList();
        _logger.LogInformation(
            "Full user sync: pulling {Count} user(s) into SQLite (tenant + linked accounts)",
            userList.Count);

        var companyIds = userList.Select(u => u.CompanyId).Distinct().ToList();
        var remoteCompanies = await remoteDb.Companies.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .ToListAsync(stoppingToken);

        var companyIdMap = new Dictionary<Guid, Guid> { [remoteCompanyId] = localCompanyId };

        foreach (var remoteCompany in remoteCompanies)
        {
            var localCompany = await localDb.Companies
                .FirstOrDefaultAsync(c => c.Id == remoteCompany.Id || c.TenantKey == remoteCompany.TenantKey, stoppingToken);

            if (localCompany == null)
            {
                remoteCompany.IsSynced = true;
                localDb.Companies.Add(remoteCompany);
                companyIdMap[remoteCompany.Id] = remoteCompany.Id;
                continue;
            }

            if (localCompany.Id == remoteCompany.Id)
            {
                localDb.Entry(localCompany).CurrentValues.SetValues(remoteCompany);
                companyIdMap[remoteCompany.Id] = localCompany.Id;
            }
            else
            {
                MergeRemoteCompanyIntoLocal(localCompany, remoteCompany);
                companyIdMap[remoteCompany.Id] = localCompany.Id;
            }

            localCompany.IsSynced = true;
        }

        await localDb.SaveChangesAsync(stoppingToken);

        var roleIds = userList.Select(u => u.RoleId).Distinct().ToList();
        var remoteRoles = await remoteDb.Roles.AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .ToListAsync(stoppingToken);

        foreach (var remoteRole in remoteRoles)
        {
            remoteRole.IsSynced = true;
            remoteRole.CompanyId = ResolveLocalCompanyId(
                companyIdMap, remoteRole.CompanyId, localCompanyId, remoteCompanyId);

            var localRole = await localDb.Roles.FindAsync([remoteRole.Id], stoppingToken);
            if (localRole == null)
                localDb.Roles.Add(remoteRole);
            else
                localDb.Entry(localRole).CurrentValues.SetValues(remoteRole);
        }

        var upgradedOnRemote = 0;
        foreach (var remoteUser in userList)
        {
            var normalizedHash = PasswordHashHelper.NormalizeToBcrypt(remoteUser.PasswordHash);
            if (PasswordHashHelper.IsLegacyPlaintext(remoteUser.PasswordHash))
            {
                var masterRow = await remoteDb.Users.FirstOrDefaultAsync(u => u.Id == remoteUser.Id, stoppingToken);
                if (masterRow != null && PasswordHashHelper.IsLegacyPlaintext(masterRow.PasswordHash))
                {
                    masterRow.PasswordHash = normalizedHash;
                    masterRow.IsSynced = true;
                    upgradedOnRemote++;
                }
            }

            remoteUser.PasswordHash = normalizedHash;
            remoteUser.IsSynced = true;
            remoteUser.CompanyId = ResolveLocalCompanyId(
                companyIdMap, remoteUser.CompanyId, localCompanyId, remoteCompanyId);

            var localUser = await localDb.Users.FindAsync([remoteUser.Id], stoppingToken);
            if (localUser == null)
                localDb.Users.Add(remoteUser);
            else
                localDb.Entry(localUser).CurrentValues.SetValues(remoteUser);
        }

        await localDb.SaveChangesAsync(stoppingToken);
        if (upgradedOnRemote > 0)
        {
            await remoteDb.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Upgraded {Count} legacy plaintext password(s) on master.", upgradedOnRemote);
        }
    }

    private static Guid ResolveLocalCompanyId(
        IReadOnlyDictionary<Guid, Guid> companyIdMap,
        Guid remoteCompanyId,
        Guid localTenantCompanyId,
        Guid remoteTenantCompanyId)
    {
        if (companyIdMap.TryGetValue(remoteCompanyId, out var mapped))
            return mapped;
        if (remoteCompanyId == remoteTenantCompanyId)
            return localTenantCompanyId;
        return remoteCompanyId;
    }

    private static async Task CloseStaleRemoteOpenShiftsAsync(
        RemoteDbContext remoteDb,
        CashShift incoming,
        Guid remoteCompanyId,
        CancellationToken stoppingToken)
    {
        var staleOpen = await remoteDb.CashShifts
            .Where(s => s.CompanyId == remoteCompanyId && !s.IsClosed && s.Id != incoming.Id)
            .ToListAsync(stoppingToken);

        foreach (var stale in staleOpen)
        {
            stale.IsClosed = true;
            stale.EndTime ??= incoming.StartTime;
        }
    }

    private static void MergeRemoteCompanyIntoLocal(Company local, Company remote)
    {
        local.Logo = remote.Logo;
        local.NameAz = remote.NameAz;
        local.NameRu = remote.NameRu;
        local.NameEn = remote.NameEn;
        local.AddressAz = remote.AddressAz;
        local.AddressRu = remote.AddressRu;
        local.AddressEn = remote.AddressEn;
        local.PhoneNumber1 = remote.PhoneNumber1;
        local.PhoneNumber2 = remote.PhoneNumber2;
        local.PhoneNumber3 = remote.PhoneNumber3;
        local.Slug = remote.Slug;
        local.PackageEndDate = remote.PackageEndDate;
        local.IsActive = remote.IsActive;
        local.IsDeliveryPriceEnabled = remote.IsDeliveryPriceEnabled;
        local.IsUserModeActive = remote.IsUserModeActive;
        local.IsGuestModeActive = remote.IsGuestModeActive;
        local.TablesLayoutMode = remote.TablesLayoutMode;
        local.EkassamEnabled = remote.EkassamEnabled;
        local.EkassamBaseUrl = remote.EkassamBaseUrl;
        local.EkassamApiKey = remote.EkassamApiKey;
        local.AutoCashShiftEnabled = remote.AutoCashShiftEnabled;
        local.AutoCashShiftOpenTime = remote.AutoCashShiftOpenTime;
        local.AutoCashShiftCloseTime = remote.AutoCashShiftCloseTime;
        local.AutoCashShiftForceClose = remote.AutoCashShiftForceClose;
        local.CashShiftPromptOpeningDeposit = remote.CashShiftPromptOpeningDeposit;
        local.CashShiftPrintReportOnClose = remote.CashShiftPrintReportOnClose;
        local.CashierPrinterTarget = remote.CashierPrinterTarget;
        local.KitchenPrinterTarget = remote.KitchenPrinterTarget;
        local.ReceiptDesignSettingsJson = remote.ReceiptDesignSettingsJson;
        local.KassaReceiptThankYouText = remote.KassaReceiptThankYouText;
        local.PosLockScreenImage = remote.PosLockScreenImage;
        local.CustomerDisplayLockScreenImage = remote.CustomerDisplayLockScreenImage;
        local.MenuFilterByWorkshop = remote.MenuFilterByWorkshop;
        local.TerminalLineDeleteConfirmEnabled = remote.TerminalLineDeleteConfirmEnabled;
        local.TelegramBotToken = remote.TelegramBotToken;
        local.TelegramNotifyPrefsJson = remote.TelegramNotifyPrefsJson;
        local.LastModifiedAt = remote.LastModifiedAt;
        local.IsDeleted = remote.IsDeleted;
    }

    private string ResolveMasterWebBaseUrl()
    {
        var configured = _configuration["Sync:MasterWebBaseUrl"]?.Trim();
        if (!string.IsNullOrEmpty(configured))
            return MediaSyncService.NormalizeWebBase(configured);

        var env = Environment.GetEnvironmentVariable("NEOPOS_MASTER_WEB_URL")?.Trim();
        if (!string.IsNullOrEmpty(env))
            return MediaSyncService.NormalizeWebBase(env);

        _logger.LogWarning(
            "Sync:MasterWebBaseUrl is not set; media pull/push may fail. Example: https://neopos.runasp.net");
        return MediaSyncService.NormalizeWebBase("https://neopos.runasp.net");
    }

    private static bool IsPostgresUniqueViolation(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
        {
            if (inner is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
                return true;
        }
        return false;
    }

    private static async Task UpsertOnRemoteAsync<T>(
        RemoteDbContext remoteDb,
        T item,
        List<T>? cachedRemoteRows,
        CancellationToken stoppingToken) where T : BaseEntity
    {
        var remoteItem = cachedRemoteRows?.FirstOrDefault(x => x.Id == item.Id);
        if (remoteItem == null)
            remoteItem = await remoteDb.Set<T>().FindAsync(new object[] { item.Id }, stoppingToken);

        item.IsSynced = true;
        if (remoteItem == null)
            remoteDb.Set<T>().Add(item);
        else
            remoteDb.Entry(remoteItem).CurrentValues.SetValues(item);
    }

    private static bool IsMasterDbUnreachable(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SocketException or TimeoutException or HttpRequestException)
                return true;

            if (e is NpgsqlException npg && IsNpgsqlConnectivityFailure(npg))
                return true;

            if (e is IOException io && io.InnerException is SocketException)
                return true;
        }

        return false;
    }

    private static bool IsNpgsqlConnectivityFailure(NpgsqlException ex)
    {
        if (ex.IsTransient)
            return true;

        var sqlState = ex.SqlState;
        return sqlState is "08000" or "08001" or "08003" or "08004" or "08006" or "08007" or "57P01" or "53300";
    }

    private static string GetConnectivityFailureSummary(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SocketException se)
                return se.SocketErrorCode switch
                {
                    SocketError.HostNotFound => "Host not found — check Neon hostname in RemoteDb connection string.",
                    SocketError.TimedOut => "Connection timed out — master database did not respond in time.",
                    SocketError.ConnectionRefused => "Connection refused — master database endpoint rejected the connection.",
                    _ => se.Message,
                };

            if (e is TimeoutException)
                return "Connection timed out — master database did not respond in time.";

            if (e is HttpRequestException http)
                return string.IsNullOrWhiteSpace(http.Message)
                    ? "Network error while reaching master services."
                    : http.Message;
        }

        return ex.Message;
    }
}
