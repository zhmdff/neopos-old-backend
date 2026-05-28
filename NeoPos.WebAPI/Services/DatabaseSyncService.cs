using DAL.Server.Context;
using Domain.Common;
using Domain.Common.Entities;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

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

        // Fix B2: Initial startup delay
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
                    _failCount = 0; // Reset on success
                }
                catch (Exception ex)
                {
                    _failCount++;
                    _logger.LogError(ex, "Error occurred during database synchronization. Fail count: {FailCount}", _failCount);
                }
            }

            // Fix B2 & B3: Configurable interval + exponential backoff
            var nextDelayMinutes = intervalMinutes;
            if (_failCount > 0)
            {
                // Exponential backoff: 5, 10, 20, 40, 60...
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
        var remoteDb = scope.ServiceProvider.GetRequiredService<RemoteDbContext>();

        // Get Metadata and CompanyId
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

        _logger.LogInformation("Starting bi-directional sync cycle. Last sync: {LastSync}", lastSync);

        // Define a safe sync order to avoid Foreign Key violations (Parents before Children)
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

        // Get all DbSet properties from AppDbContext
        var allDbSetTypes = typeof(AppDbContext)
            .GetProperties()
            .Where(p => p.PropertyType.IsGenericType && 
                        p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .ToList();

        // Combine defined order with any remaining entities
        var finalOrder = syncOrder.Where(t => allDbSetTypes.Contains(t)).ToList();
        finalOrder.AddRange(allDbSetTypes.Except(finalOrder));

        foreach (var entityType in finalOrder)
        {
            // Skip non-syncable entities
            if (entityType == typeof(LocalSyncMetadata))
                continue;

            // Sync entities inheriting from BaseEntity
            if (typeof(BaseEntity).IsAssignableFrom(entityType))
            {
                await InvokeSyncTableAsync(localDb, remoteDb, entityType, company.Id, lastSync, stoppingToken);
            }
        }

        // Update Metadata
        metadata.LastSuccessfulSyncAt = syncStartTime;
        metadata.LastSyncStatus = "Success";
        await localDb.SaveChangesAsync(stoppingToken);

        _logger.LogInformation("Sync cycle completed successfully.");
    }

    private async Task InvokeSyncTableAsync(AppDbContext localDb, RemoteDbContext remoteDb, Type entityType, Guid companyId, DateTime lastSync, CancellationToken stoppingToken)
    {
        var method = typeof(DatabaseSyncService)
            .GetMethod(nameof(SyncTableAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.MakeGenericMethod(entityType);

        if (method != null)
        {
            await (Task)method.Invoke(this, new object[] { localDb, remoteDb, companyId, lastSync, stoppingToken })!;
        }
    }

    private async Task SyncTableAsync<T>(AppDbContext localDb, RemoteDbContext remoteDb, Guid companyId, DateTime lastSync, CancellationToken stoppingToken) where T : BaseEntity
    {
        // PHASE 1: Push (Local -> Master)
        var localItems = await localDb.Set<T>()
            .AsNoTracking()
            .ToListAsync(stoppingToken);

        var unsyncedItems = localItems.Where(x => !x.IsSynced).ToList();

        // Find items that are marked synced locally but are absent from remote
        var syncedLocalIds = localItems.Where(x => x.IsSynced).Select(x => x.Id).ToList();
        
        // Optimization: Fetch existing remote IDs in bulk to minimize round-trips
        var existingRemoteItems = new List<T>();
        if (localItems.Any())
        {
            var allLocalIds = localItems.Select(x => x.Id).ToList();
            existingRemoteItems = await remoteDb.Set<T>()
                .Where(x => allLocalIds.Contains(x.Id))
                .ToListAsync(stoppingToken);
        }

        var existingRemoteIds = existingRemoteItems.Select(x => x.Id).ToHashSet();
        var missingFromRemote = localItems
            .Where(x => x.IsSynced && !existingRemoteIds.Contains(x.Id))
            .ToList();

        var itemsToPush = unsyncedItems.Concat(missingFromRemote).ToList();

        if (itemsToPush.Any())
        {
            _logger.LogInformation("Pushing {Count} items for table {Table} ({Unsynced} unsynced, {Missing} missing from remote)",
                itemsToPush.Count, typeof(T).Name, unsyncedItems.Count, missingFromRemote.Count);

            foreach (var item in itemsToPush)
            {
                var remoteItem = existingRemoteItems.FirstOrDefault(x => x.Id == item.Id);
                if (remoteItem == null)
                {
                    item.IsSynced = true;
                    remoteDb.Set<T>().Add(item);
                }
                else
                {
                    remoteDb.Entry(remoteItem).CurrentValues.SetValues(item);
                }
            }
            
            await remoteDb.SaveChangesAsync(stoppingToken);

            // Mark as synced locally in bulk
            var idsToMarkSynced = itemsToPush.Where(x => !x.IsSynced).Select(x => x.Id).ToList();
            if (idsToMarkSynced.Any())
            {
                await localDb.Set<T>()
                    .Where(x => idsToMarkSynced.Contains(x.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsSynced, true), stoppingToken);
            }
        }

        // PHASE 2: Pull (Master -> Local)
        if (typeof(AuditableEntity).IsAssignableFrom(typeof(T)))
        {
            var remoteModifiedItems = await remoteDb.Set<T>()
                .Cast<AuditableEntity>()
                .Where(x => x.LastModifiedAt > lastSync || x.CreatedAt > lastSync)
                .AsNoTracking()
                .ToListAsync(stoppingToken);

            var filteredRemoteData = remoteModifiedItems.Where(x => {
                var prop = x.GetType().GetProperty("CompanyId");
                if (prop == null) return true; 
                var val = prop.GetValue(x);
                return val is Guid id && id == companyId;
            }).Cast<T>().ToList();

            if (filteredRemoteData.Any())
            {
                _logger.LogInformation("Pulling {Count} updates for table {Table}", filteredRemoteData.Count, typeof(T).Name);
                
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
                    else if (localItem.IsSynced)
                    {
                        localDb.Entry(localItem).CurrentValues.SetValues(remoteItem);
                    }
                }
                await localDb.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
