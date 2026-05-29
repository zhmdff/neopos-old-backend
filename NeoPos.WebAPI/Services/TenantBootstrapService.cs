using DAL.Server.Context;
using Domain.Common;
using Domain.Common.Entities;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;


namespace NeoPos.WebAPI.Services;

public class TenantBootstrapService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantBootstrapService> _logger;

    public TenantBootstrapService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<TenantBootstrapService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task BootstrapAsync()
    {
        var tenantKey = _configuration["NeoPos:TenantKey"];
        if (string.IsNullOrEmpty(tenantKey) || tenantKey == "YOUR_TENANT_KEY_HERE")
        {
            _logger.LogWarning("TenantKey not configured in appsettings.json. Skipping bootstrap.");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var localDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var remoteDb = scope.ServiceProvider.GetRequiredService<RemoteDbContext>();

        var metadata = await localDb.LocalSyncMetadata.FirstOrDefaultAsync();
        if (metadata != null && metadata.TenantKey == tenantKey && metadata.LastSuccessfulSyncAt.HasValue)
        {
            _logger.LogInformation("System already bootstrapped for tenant {TenantKey}. Skipping.", tenantKey);
            return;
        }

        _logger.LogInformation("Starting initial bootstrap for tenant {TenantKey}...", tenantKey);

        string mode = Environment.GetEnvironmentVariable("NEOPOS_MODE") 
                      ?? _configuration["NeoPos:Mode"] 
                      ?? "tenant"; // Default is tenant
        bool usePostgresAsPrimary = mode.Equals("master", StringComparison.OrdinalIgnoreCase);

        try
        {
            // Fix B7: Graceful check for remote connectivity
            _logger.LogInformation("Checking connectivity to Master server...");
            bool isRemoteAvailable = false;
            try
            {
                // Try connecting with a short timeout
                isRemoteAvailable = await remoteDb.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Remote database connectivity check failed: {Message}", ex.Message);
            }

            if (!isRemoteAvailable)
            {
                if (metadata != null && metadata.TenantKey == tenantKey)
                {
                    _logger.LogWarning("Master server is unreachable, but local metadata exists. Continuing in offline mode.");
                    return;
                }
                
                _logger.LogError("Master server is unreachable and no local metadata found. Initial bootstrap requires internet connection.");
                return;
            }

            // 1. Resolve Company from Master
            _logger.LogInformation("Looking for company with TenantKey: {TenantKey}", tenantKey);
            var company = await remoteDb.Companies
                .FirstOrDefaultAsync(x => x.TenantKey == tenantKey);

            if (company == null)
            {
                _logger.LogInformation("Company with TenantKey {TenantKey} not found on Master server. Seeding company...", tenantKey);
                company = new Company
                {
                    Id = Guid.NewGuid(),
                    TenantKey = tenantKey,
                    NameAz = "NeoPos Restoran",
                    NameEn = "NeoPos Restaurant",
                    NameRu = "NeoPos Ресторан",
                    AddressAz = "Baku",
                    AddressEn = "Baku",
                    AddressRu = "Баку",
                    PhoneNumber1 = "+994500000000",
                    Slug = "neopos-restoran",
                    PackageEndDate = DateTime.UtcNow.AddYears(5),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System_Seed",
                    IsSynced = true
                };
                await remoteDb.Companies.AddAsync(company);
                await remoteDb.SaveChangesAsync();
                _logger.LogInformation("New company created on Master: {CompanyId}", company.Id);
            }

            _logger.LogInformation("Target company: {CompanyName} (ID: {CompanyId})", company.NameAz, company.Id);

            // Check if there is any user for this company in remoteDb
            var hasAnyUser = await remoteDb.Users.AnyAsync(u => u.CompanyId == company.Id && !u.IsDeleted);
            if (!hasAnyUser)
            {
                _logger.LogInformation("No users found for company {CompanyName} on Master. Seeding admin...", company.NameAz);
                
                var adminUsername = _configuration["NeoPos:AdminUsername"]?.Trim();
                if (string.IsNullOrEmpty(adminUsername)) adminUsername = "admin_boss";

                var adminPassword = _configuration["NeoPos:AdminPassword"]?.Trim();
                if (string.IsNullOrEmpty(adminPassword)) adminPassword = "AdminPassword123";

                var now = DateTime.UtcNow;
                var roleId = Guid.NewGuid();
                var userId = Guid.NewGuid();

                var role = new Role
                {
                    Id = roleId,
                    CompanyId = company.Id,
                    NameAz = "Admin",
                    NameEn = "Admin",
                    NameRu = "Admin",
                    IsAdmin = true,
                    Permissions = Array.Empty<int>(), // Use empty array instead of null
                    CreatedAt = now,
                    CreatedBy = "System_Seed",
                    IsDeleted = false,
                    IsSynced = true
                };

                var user = new User
                {
                    Id = userId,
                    CompanyId = company.Id,
                    RoleId = roleId,
                    FullName = "System Admin",
                    Username = adminUsername,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword), // Hashed
                    PinCode = "1111",
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = "System_Seed",
                    IsDeleted = false,
                    IsSynced = true
                };

                await remoteDb.Roles.AddAsync(role);
                await remoteDb.Users.AddAsync(user);
                
                // Seed fake/demo data only if enabled in config
                bool seedDemoData = _configuration["NeoPos:SeedDemoData"]?.ToLower() == "true";
                if (seedDemoData)
                {
                    _logger.LogInformation("Seeding demo data for company {CompanyName}...", company.NameAz);
                    await SeedDemoDataAsync(remoteDb, company.Id);
                }

                await remoteDb.SaveChangesAsync();
                _logger.LogInformation("Master Seeding Done. User: {Username}, Pass: {Password}", adminUsername, adminPassword);
            }

            // If we are using Postgres as primary, we don't need to wipe/hydrate "local" (which is the same DB)
            if (usePostgresAsPrimary)
            {
                _logger.LogInformation("Running in Master mode (PostgreSQL primary). Skipping local hydration.");
                return;
            }

            // Open local connection and disable foreign key checks during bulk sync
            await localDb.Database.OpenConnectionAsync();
            await localDb.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

            try
            {
                // 2. Wipe Local DB (Except Metadata)
                // Note: In a real scenario we might want to be more careful, but for bootstrap it's cleaner.
                await ClearLocalDataAsync(localDb);

                // 3. Hydrate Company
                localDb.Companies.Add(company);
                await localDb.SaveChangesAsync();

                // 4. Hydrate All Other Tables
                var dbSetProperties = typeof(AppDbContext)
                    .GetProperties()
                    .Where(p => p.PropertyType.IsGenericType && 
                                p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));

                foreach (var prop in dbSetProperties)
                {
                    var entityType = prop.PropertyType.GetGenericArguments()[0];
                    
                    // Skip Company and Metadata as they are handled or not syncable this way
                    if (entityType == typeof(Company) || entityType == typeof(LocalSyncMetadata))
                        continue;

                    // Only sync entities that belong to a company
                    if (typeof(AuditableCompanyEntity).IsAssignableFrom(entityType))
                    {
                        await HydrateTableAsync(localDb, remoteDb, entityType, company.Id);
                    }
                    else if (typeof(BaseEntity).IsAssignableFrom(entityType) && entityType != typeof(Company))
                    {
                        // For entities like User/Role that might not inherit from AuditableCompanyEntity 
                        // but still need syncing. Need to check if they have CompanyId.
                        var companyIdProp = entityType.GetProperty("CompanyId");
                        if (companyIdProp != null)
                        {
                            await HydrateTableAsync(localDb, remoteDb, entityType, company.Id);
                        }
                    }
                }

                // 5. Update Metadata
                if (metadata == null)
                {
                    metadata = new LocalSyncMetadata { Id = Guid.NewGuid() };
                    localDb.LocalSyncMetadata.Add(metadata);
                }
                metadata.TenantKey = tenantKey;
                metadata.LastSuccessfulSyncAt = DateTime.UtcNow;
                metadata.LastSyncStatus = "Success";
                metadata.IsSynced = true;

                await localDb.SaveChangesAsync();
                
                // Re-enable foreign keys
                await localDb.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
                _logger.LogInformation("Bootstrap completed successfully for tenant {TenantKey}.", tenantKey);
            }
            finally
            {
                await localDb.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bootstrap failed for tenant {TenantKey}.", tenantKey);
        }
    }

    private async Task ClearLocalDataAsync(AppDbContext localDb)
    {
        _logger.LogInformation("Clearing local database tables...");

        // Dynamically get all DbSet tables from AppDbContext
        var dbSetProperties = typeof(AppDbContext)
            .GetProperties()
            .Where(p => p.PropertyType.IsGenericType && 
                        p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));

        // We should delete in an order that respects foreign keys if possible, 
        // but since we disable PRAGMA foreign_keys in the caller, simple deletion is fine.
        foreach (var prop in dbSetProperties)
        {
            var entityType = prop.PropertyType.GetGenericArguments()[0];
            
            // Skip Metadata
            if (entityType == typeof(LocalSyncMetadata))
                continue;

            var tableName = prop.Name;
            try
            {
                await localDb.Database.ExecuteSqlRawAsync($"DELETE FROM {tableName}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not clear table {Table}: {Message}", tableName, ex.Message);
            }
        }
    }

    private async Task HydrateTableAsync(AppDbContext localDb, RemoteDbContext remoteDb, Type entityType, Guid companyId)
    {
        var method = typeof(TenantBootstrapService)
            .GetMethod(nameof(HydrateGenericTableAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.MakeGenericMethod(entityType);

        if (method != null)
        {
            await (Task)method.Invoke(this, new object[] { localDb, remoteDb, companyId })!;
        }
    }

    private async Task HydrateGenericTableAsync<T>(AppDbContext localDb, RemoteDbContext remoteDb, Guid companyId) where T : class
    {
        _logger.LogInformation("Hydrating table {Table}...", typeof(T).Name);

        // Fetch all non-deleted records for this company from Master
        // We use reflection to filter by CompanyId since we can't easily constrain T to IHasCompanyId here
        var remoteData = await remoteDb.Set<T>()
            .AsNoTracking()
            .ToListAsync();

        // Filter manually if needed or use dynamic LINQ
        var filteredData = remoteData.Where(x => {
            var prop = x.GetType().GetProperty("CompanyId");
            if (prop == null) return true; // If no CompanyId, take all (global data)
            var val = prop.GetValue(x);
            return val is Guid id && id == companyId;
        }).ToList();

        if (filteredData.Any())
        {
            // Mark as synced locally
            foreach (var item in filteredData)
            {
                var syncProp = item.GetType().GetProperty("IsSynced");
                syncProp?.SetValue(item, true);
            }

            localDb.Set<T>().AddRange(filteredData);
            await localDb.SaveChangesAsync();
            _logger.LogInformation("Hydrated {Count} items for {Table}.", filteredData.Count, typeof(T).Name);
        }
    }

    private async Task SeedDemoDataAsync(RemoteDbContext remoteDb, Guid companyId)
    {
        var now = DateTime.UtcNow;

        // 1. Workshops (Kitchen, Bar)
        var kitchenWorkshop = new Workshop
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            NameAz = "Mətbəx",
            NameEn = "Kitchen",
            NameRu = "Кухня",
            IsPrinting = true,
            PrinterType = "Network",
            PrinterValue = "192.168.100.97",
            CreatedAt = now,
            CreatedBy = "System_Seed",
            IsDeleted = false
        };

        var barWorkshop = new Workshop
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            NameAz = "Bar",
            NameEn = "Bar",
            NameRu = "Бар",
            IsPrinting = true,
            PrinterType = "Network",
            PrinterValue = "192.168.1.201",
            CreatedAt = now,
            CreatedBy = "System_Seed",
            IsDeleted = false
        };

        await remoteDb.Workshops.AddRangeAsync(kitchenWorkshop, barWorkshop);

        // 2. Halls
        var mainHall = new Hall
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            NameAz = "Əsas Zal",
            NameEn = "Main Hall",
            NameRu = "Главный Зал",
            ServicePercentage = 10,
            OrderIndex = 1,
            IsGuestCountEnabled = true,
            IsTableHourActive = false,
            CreatedAt = now,
            CreatedBy = "System_Seed",
            IsDeleted = false
        };

        var terraceHall = new Hall
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            NameAz = "Teras",
            NameEn = "Terrace",
            NameRu = "Терраса",
            ServicePercentage = 10,
            OrderIndex = 2,
            IsGuestCountEnabled = true,
            IsTableHourActive = false,
            CreatedAt = now,
            CreatedBy = "System_Seed",
            IsDeleted = false
        };

        await remoteDb.Halls.AddRangeAsync(mainHall, terraceHall);

        // 3. Tables
        var tables = new List<Table>();
        for (int i = 1; i <= 6; i++)
        {
            tables.Add(new Table
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                HallId = mainHall.Id,
                NameAz = $"Masa {i}",
                NameEn = $"Table {i}",
                NameRu = $"Стол {i}",
                Capacity = 4,
                OrderIndex = i,
                Status = TableStatus.Empty,
                MapPositionX = 10 + (i * 12),
                MapPositionY = 30,
                MapWidthPercent = 8,
                MapHeightPercent = 8,
                MapShape = TableMapShape.Rectangle,
                CreatedAt = now,
                CreatedBy = "System_Seed",
                IsDeleted = false
            });
        }

        for (int i = 11; i <= 14; i++)
        {
            tables.Add(new Table
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                HallId = terraceHall.Id,
                NameAz = $"Masa {i}",
                NameEn = $"Table {i}",
                NameRu = $"Стол {i}",
                Capacity = 6,
                OrderIndex = i,
                Status = TableStatus.Empty,
                MapPositionX = 15 + ((i - 10) * 15),
                MapPositionY = 40,
                MapWidthPercent = 10,
                MapHeightPercent = 10,
                MapShape = TableMapShape.Circle,
                CreatedAt = now,
                CreatedBy = "System_Seed",
                IsDeleted = false
            });
        }
        await remoteDb.Tables.AddRangeAsync(tables);

        // 4. Categories
        var foodCategory = new Category
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            NameAz = "Yeməklər",
            NameEn = "Foods",
            NameRu = "Блюда",
            OrderIndex = 1,
            CreatedAt = now,
            CreatedBy = "System_Seed",
            IsDeleted = false
        };

        var drinkCategory = new Category
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            NameAz = "İçkilər",
            NameEn = "Drinks",
            NameRu = "Напитки",
            OrderIndex = 2,
            CreatedAt = now,
            CreatedBy = "System_Seed",
            IsDeleted = false
        };

        await remoteDb.Categories.AddRangeAsync(foodCategory, drinkCategory);

        // 5. Products
        var products = new List<Product>
        {
            new Product
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                CategoryId = foodCategory.Id,
                WorkshopId = kitchenWorkshop.Id,
                NameAz = "Mərci Şorbası",
                NameEn = "Lentil Soup",
                NameRu = "Чечевичный Суп",
                Barcode = "100001",
                OrderIndex = 1,
                Unit = SalesUnit.Pcs,
                Stock = 100,
                CostPrice = 1.20m,
                MarkupValue = 150,
                MarkupType = MarkupType.Percentage,
                SalePrice = 3.00m,
                ShowInQr = true,
                ShowInTerminal = true,
                CreatedAt = now,
                CreatedBy = "System_Seed",
                IsDeleted = false
            },
            new Product
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                CategoryId = foodCategory.Id,
                WorkshopId = kitchenWorkshop.Id,
                NameAz = "Sezar Salatı",
                NameEn = "Caesar Salad",
                NameRu = "Салат Цезарь",
                Barcode = "100002",
                OrderIndex = 2,
                Unit = SalesUnit.Pcs,
                Stock = 50,
                CostPrice = 4.00m,
                MarkupValue = 100,
                MarkupType = MarkupType.Percentage,
                SalePrice = 8.00m,
                ShowInQr = true,
                ShowInTerminal = true,
                CreatedAt = now,
                CreatedBy = "System_Seed",
                IsDeleted = false
            },
            new Product
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                CategoryId = foodCategory.Id,
                WorkshopId = kitchenWorkshop.Id,
                NameAz = "Pizza Marqarita",
                NameEn = "Pizza Margherita",
                NameRu = "Пицца Маргарита",
                Barcode = "100003",
                OrderIndex = 3,
                Unit = SalesUnit.Pcs,
                Stock = 80,
                CostPrice = 3.50m,
                MarkupValue = 150,
                MarkupType = MarkupType.Percentage,
                SalePrice = 9.00m,
                ShowInQr = true,
                ShowInTerminal = true,
                CreatedAt = now,
                CreatedBy = "System_Seed",
                IsDeleted = false
            },
            new Product
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                CategoryId = drinkCategory.Id,
                WorkshopId = barWorkshop.Id,
                NameAz = "Coca-Cola 330ml",
                NameEn = "Coca-Cola 330ml",
                NameRu = "Coca-Cola 330мл",
                Barcode = "200001",
                OrderIndex = 1,
                Unit = SalesUnit.Pcs,
                Stock = 500,
                CostPrice = 0.60m,
                MarkupValue = 233,
                MarkupType = MarkupType.Percentage,
                SalePrice = 2.00m,
                ShowInQr = true,
                ShowInTerminal = true,
                CreatedAt = now,
                CreatedBy = "System_Seed",
                IsDeleted = false
            },
            new Product
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                CategoryId = drinkCategory.Id,
                WorkshopId = barWorkshop.Id,
                NameAz = "Təbii Şirə",
                NameEn = "Fresh Juice",
                NameRu = "Свежий Сок",
                Barcode = "200002",
                OrderIndex = 2,
                Unit = SalesUnit.Pcs,
                Stock = 120,
                CostPrice = 1.50m,
                MarkupValue = 200,
                MarkupType = MarkupType.Percentage,
                SalePrice = 4.50m,
                ShowInQr = true,
                ShowInTerminal = true,
                CreatedAt = now,
                CreatedBy = "System_Seed",
                IsDeleted = false
            }
        };

        await remoteDb.Products.AddRangeAsync(products);
    }
}
