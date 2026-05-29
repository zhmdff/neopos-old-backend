using DAL.Server.Context;
using Domain.Common;
using Domain.Common.Entities;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace NeoPos.WebAPI.Services;

public class DatabaseSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSyncService> _logger;

    private int _failCount = 0;
    private const int MaxBackoffMinutes = 60;

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
                    await SyncDataAsync(stoppingToken);
                    _failCount = 0; 
                }
                catch (Exception ex)
                {
                    _failCount++;
                    _logger.LogError(ex, "Error occurred during database synchronization. Fail count: {FailCount}", _failCount);
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
        _logger.LogInformation("Manual sync triggered via API.");
        await SyncDataAsync(ct);
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
        if (metadata == null || !metadata.LastSuccessfulSyncAt.HasValue)
        {
            _logger.LogWarning("System not bootstrapped. Skipping background sync.");
            return;
        }

        var company = await localDb.Companies.FirstOrDefaultAsync(stoppingToken);
        if (company == null)
        {
            _logger.LogError("No company found in local database. Cannot sync.");
            return;
        }

        var lastSync = metadata.LastSuccessfulSyncAt.Value;
        var syncStartTime = DateTime.UtcNow;

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

                    await UpsertOnRemoteAsync(remoteDb, item, existingRemoteItems, stoppingToken);
                }

                try
                {
                    await remoteDb.SaveChangesAsync(stoppingToken);
                }
                catch (DbUpdateException ex) when (IsPostgresUniqueViolation(ex))
                {
                    _logger.LogWarning(ex, "Push conflict for {Table}; retrying row-by-row upsert.", type.Name);
                    remoteDb.ChangeTracker.Clear();
                    foreach (var item in itemsToPush)
                    {
                        if (item is AuditableCompanyEntity companyEntity
                            && companyEntity.CompanyId == localCompanyId
                            && remoteCompanyId != localCompanyId)
                        {
                            companyEntity.CompanyId = remoteCompanyId;
                        }

                        await UpsertOnRemoteAsync(remoteDb, item, null, stoppingToken);
                        await remoteDb.SaveChangesAsync(stoppingToken);
                        remoteDb.ChangeTracker.Clear();
                    }
                }

                pushedThisCycle = itemsToPush.Select(x => x.Id).ToHashSet();
                await localDb.Set<T>()
                    .Where(x => pushedThisCycle.Contains(x.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsSynced, true), stoppingToken);
            }
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
}
