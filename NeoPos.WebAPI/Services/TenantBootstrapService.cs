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
        string mode = Environment.GetEnvironmentVariable("NEOPOS_MODE")
                      ?? _configuration["NeoPos:Mode"]
                      ?? "tenant";
        bool isMaster = mode.Equals("master", StringComparison.OrdinalIgnoreCase);

        if (isMaster)
            await BootstrapMasterAsync();
        else
            await BootstrapTenantLocalAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MASTER bootstrap: seed company / admin into PostgreSQL (RemoteDbContext)
    // ─────────────────────────────────────────────────────────────────────────
    private async Task BootstrapMasterAsync()
    {
        var tenantKey = _configuration["NeoPos:TenantKey"];
        if (string.IsNullOrEmpty(tenantKey) || tenantKey == "YOUR_TENANT_KEY_HERE")
        {
            _logger.LogWarning("[Master] TenantKey not configured. Skipping master bootstrap.");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var remoteDb = scope.ServiceProvider.GetRequiredService<RemoteDbContext>();

        try
        {
            _logger.LogInformation("[Master] Checking connectivity to PostgreSQL...");
            bool canConnect = false;
            try { canConnect = await remoteDb.Database.CanConnectAsync(); }
            catch (Exception ex) { _logger.LogWarning("[Master] PostgreSQL connectivity check failed: {Message}", ex.Message); }

            if (!canConnect)
            {
                _logger.LogError("[Master] Cannot connect to PostgreSQL. Bootstrap skipped.");
                return;
            }

            // 1. Resolve or create Company
            var company = await remoteDb.Companies.FirstOrDefaultAsync(x => x.TenantKey == tenantKey);
            if (company == null)
            {
                _logger.LogInformation("[Master] Creating company for TenantKey {TenantKey}...", tenantKey);
                company = new Company
                {
                    Id = Guid.NewGuid(),
                    TenantKey = tenantKey,
                    NameAz = "NeoPos Restoran",
                    NameEn = "NeoPos Restaurant",
                    NameRu = "NeoPos Ресторан",
                    AddressAz = "Baku", AddressEn = "Baku", AddressRu = "Баку",
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
                _logger.LogInformation("[Master] Company created: {CompanyId}", company.Id);
            }

            _logger.LogInformation("[Master] Company: {Name} (ID: {Id})", company.NameAz, company.Id);

            // 2. Seed admin user if none exists
            var hasAnyUser = await remoteDb.Users.AnyAsync(u => u.CompanyId == company.Id && !u.IsDeleted);
            if (!hasAnyUser)
            {
                var adminUsername = _configuration["NeoPos:AdminUsername"]?.Trim() ?? "admin_boss";
                var adminPassword = _configuration["NeoPos:AdminPassword"]?.Trim() ?? "AdminPassword123";

                var now = DateTime.UtcNow;
                var roleId = Guid.NewGuid();
                var role = new Role
                {
                    Id = roleId, CompanyId = company.Id,
                    NameAz = "Admin", NameEn = "Admin", NameRu = "Admin",
                    IsAdmin = true, Permissions = Array.Empty<int>(),
                    CreatedAt = now, CreatedBy = "System_Seed",
                    IsDeleted = false, IsSynced = true
                };
                var user = new User
                {
                    Id = Guid.NewGuid(), CompanyId = company.Id, RoleId = roleId,
                    FullName = "System Admin", Username = adminUsername,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                    PinCode = "1111", IsActive = true,
                    CreatedAt = now, CreatedBy = "System_Seed",
                    IsDeleted = false, IsSynced = true
                };

                bool seedDemoData = _configuration["NeoPos:SeedDemoData"]?.ToLower() == "true";
                if (seedDemoData)
                {
                    _logger.LogInformation("[Master] Seeding demo data...");
                    await SeedDemoDataAsync(remoteDb, company.Id);
                }

                await remoteDb.Roles.AddAsync(role);
                await remoteDb.Users.AddAsync(user);
                await remoteDb.SaveChangesAsync();
                _logger.LogInformation("[Master] Admin seeded. User: {Username}", adminUsername);
            }

            _logger.LogInformation("[Master] Bootstrap complete.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Master] Bootstrap failed.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TENANT bootstrap: seed local SQLite only — NO PostgreSQL access at all.
    // Runs every startup: checks if the configured admin user exists, creates
    // or updates them, then saves. Real catalog data comes via DatabaseSyncService.
    // ─────────────────────────────────────────────────────────────────────────
    private async Task BootstrapTenantLocalAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var localDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantKey = _configuration["NeoPos:TenantKey"];
        if (string.IsNullOrEmpty(tenantKey) || tenantKey == "YOUR_TENANT_KEY_HERE")
        {
            _logger.LogWarning("[Tenant] TenantKey not configured. Skipping local bootstrap.");
            return;
        }

        var adminUsername = _configuration["NeoPos:AdminUsername"]?.Trim() ?? "admin";
        var adminPassword = _configuration["NeoPos:AdminPassword"]?.Trim() ?? "Admin123";

        _logger.LogInformation("[Tenant] Checking local bootstrap for TenantKey={TenantKey}, User={Username}...", tenantKey, adminUsername);

        try
        {
            await localDb.Database.OpenConnectionAsync();
            await localDb.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

            var now = DateTime.UtcNow;
            bool anythingChanged = false;

            // 1. Ensure company exists locally
            var company = await localDb.Companies.FirstOrDefaultAsync(c => c.TenantKey == tenantKey);
            if (company == null)
            {
                _logger.LogInformation("[Tenant] Company not found locally. Creating...");
                company = new Company
                {
                    Id = Guid.NewGuid(),
                    TenantKey = tenantKey,
                    NameAz = _configuration["NeoPos:CompanyName"] ?? "NeoPos Restoran",
                    NameEn = _configuration["NeoPos:CompanyName"] ?? "NeoPos Restaurant",
                    NameRu = _configuration["NeoPos:CompanyName"] ?? "NeoPos Ресторан",
                    AddressAz = "Baku", AddressEn = "Baku", AddressRu = "Баку",
                    PhoneNumber1 = "+994500000000",
                    Slug = tenantKey.ToLower(),
                    PackageEndDate = now.AddYears(5),
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = "System_Seed",
                    IsSynced = false
                };
                localDb.Companies.Add(company);
                await localDb.SaveChangesAsync();
                anythingChanged = true;
                _logger.LogInformation("[Tenant] Company created locally: {CompanyId}", company.Id);
            }

            // 2. Ensure admin role exists
            var adminRole = await localDb.Roles
                .FirstOrDefaultAsync(r => r.CompanyId == company.Id && r.IsAdmin && !r.IsDeleted);
            if (adminRole == null)
            {
                _logger.LogInformation("[Tenant] Admin role not found locally. Creating...");
                adminRole = new Role
                {
                    Id = Guid.NewGuid(), CompanyId = company.Id,
                    NameAz = "Admin", NameEn = "Admin", NameRu = "Admin",
                    IsAdmin = true, Permissions = Array.Empty<int>(),
                    CreatedAt = now, CreatedBy = "System_Seed",
                    IsDeleted = false, IsSynced = false
                };
                localDb.Roles.Add(adminRole);
                await localDb.SaveChangesAsync();
                anythingChanged = true;
            }

            // 3. Check if the configured admin user exists by username
            var existingUser = await localDb.Users
                .FirstOrDefaultAsync(u => u.Username == adminUsername && u.CompanyId == company.Id && !u.IsDeleted);

            if (existingUser == null)
            {
                // User not found — create them
                _logger.LogInformation("[Tenant] User '{Username}' not found in SQLite. Creating from appsettings...", adminUsername);
                var newUser = new User
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    RoleId = adminRole.Id,
                    FullName = "Admin",
                    Username = adminUsername,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                    PinCode = "1111",
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = "System_Seed",
                    IsDeleted = false,
                    IsSynced = false
                };
                localDb.Users.Add(newUser);
                await localDb.SaveChangesAsync();
                anythingChanged = true;
                _logger.LogInformation("[Tenant] User '{Username}' created successfully.", adminUsername);
            }
            else
            {
                // User exists — verify password still matches; update hash if config changed
                bool passwordMatches = BCrypt.Net.BCrypt.Verify(adminPassword, existingUser.PasswordHash);
                if (!passwordMatches)
                {
                    _logger.LogInformation("[Tenant] Password for '{Username}' changed in appsettings. Updating hash...", adminUsername);
                    existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
                    existingUser.LastModifiedAt = now;
                    await localDb.SaveChangesAsync();
                    anythingChanged = true;
                }
                else
                {
                    _logger.LogInformation("[Tenant] User '{Username}' already exists and password matches.", adminUsername);
                }
            }

            // 4. Upsert bootstrap metadata
            var metadata = await localDb.LocalSyncMetadata.FirstOrDefaultAsync();
            if (metadata == null)
            {
                metadata = new LocalSyncMetadata { Id = Guid.NewGuid() };
                localDb.LocalSyncMetadata.Add(metadata);
            }
            metadata.TenantKey = tenantKey;
            metadata.LastSuccessfulSyncAt = now;
            metadata.LastSyncStatus = anythingChanged ? "LocalBootstrapUpdated" : "LocalBootstrapOk";
            metadata.IsSynced = false;
            await localDb.SaveChangesAsync();

            await localDb.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
            _logger.LogInformation("[Tenant] Bootstrap complete. Changed={Changed}. Sync will follow via DatabaseSyncService.", anythingChanged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tenant] Local bootstrap failed for {TenantKey}.", tenantKey);
        }
        finally
        {
            await localDb.Database.CloseConnectionAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Demo data seeder — only used by master bootstrap
    // ─────────────────────────────────────────────────────────────────────────
    private async Task SeedDemoDataAsync(RemoteDbContext remoteDb, Guid companyId)
    {
        var now = DateTime.UtcNow;

        var kitchenWorkshop = new Workshop { Id = Guid.NewGuid(), CompanyId = companyId, NameAz = "Mətbəx", NameEn = "Kitchen", NameRu = "Кухня", IsPrinting = true, PrinterType = "Network", PrinterValue = "192.168.100.97", CreatedAt = now, CreatedBy = "System_Seed", IsDeleted = false };
        var barWorkshop = new Workshop { Id = Guid.NewGuid(), CompanyId = companyId, NameAz = "Bar", NameEn = "Bar", NameRu = "Бар", IsPrinting = true, PrinterType = "Network", PrinterValue = "192.168.1.201", CreatedAt = now, CreatedBy = "System_Seed", IsDeleted = false };
        await remoteDb.Workshops.AddRangeAsync(kitchenWorkshop, barWorkshop);

        var mainHall = new Hall { Id = Guid.NewGuid(), CompanyId = companyId, NameAz = "Əsas Zal", NameEn = "Main Hall", NameRu = "Главный Зал", ServicePercentage = 10, OrderIndex = 1, IsGuestCountEnabled = true, IsTableHourActive = false, CreatedAt = now, CreatedBy = "System_Seed", IsDeleted = false };
        var terraceHall = new Hall { Id = Guid.NewGuid(), CompanyId = companyId, NameAz = "Teras", NameEn = "Terrace", NameRu = "Терраса", ServicePercentage = 10, OrderIndex = 2, IsGuestCountEnabled = true, IsTableHourActive = false, CreatedAt = now, CreatedBy = "System_Seed", IsDeleted = false };
        await remoteDb.Halls.AddRangeAsync(mainHall, terraceHall);

        var tables = new List<Table>();
        for (int i = 1; i <= 6; i++)
            tables.Add(new Table { Id = Guid.NewGuid(), CompanyId = companyId, HallId = mainHall.Id, NameAz = $"Masa {i}", NameEn = $"Table {i}", NameRu = $"Стол {i}", Capacity = 4, OrderIndex = i, Status = TableStatus.Empty, MapPositionX = 10 + (i * 12), MapPositionY = 30, MapWidthPercent = 8, MapHeightPercent = 8, MapShape = TableMapShape.Rectangle, CreatedAt = now, CreatedBy = "System_Seed", IsDeleted = false });
        for (int i = 11; i <= 14; i++)
            tables.Add(new Table { Id = Guid.NewGuid(), CompanyId = companyId, HallId = terraceHall.Id, NameAz = $"Masa {i}", NameEn = $"Table {i}", NameRu = $"Стол {i}", Capacity = 6, OrderIndex = i, Status = TableStatus.Empty, MapPositionX = 15 + ((i - 10) * 15), MapPositionY = 40, MapWidthPercent = 10, MapHeightPercent = 10, MapShape = TableMapShape.Circle, CreatedAt = now, CreatedBy = "System_Seed", IsDeleted = false });
        await remoteDb.Tables.AddRangeAsync(tables);

        var foodCategory = new Category { Id = Guid.NewGuid(), CompanyId = companyId, NameAz = "Yeməklər", NameEn = "Foods", NameRu = "Блюда", OrderIndex = 1, CreatedAt = now, CreatedBy = "System_Seed", IsDeleted = false };
        var drinkCategory = new Category { Id = Guid.NewGuid(), CompanyId = companyId, NameAz = "İçkilər", NameEn = "Drinks", NameRu = "Напитки", OrderIndex = 2, CreatedAt = now, CreatedBy = "System_Seed", IsDeleted = false };
        await remoteDb.Categories.AddRangeAsync(foodCategory, drinkCategory);

        var products = new List<Product>
        {
            new Product { Id = Guid.NewGuid(), CompanyId = companyId, CategoryId = foodCategory.Id, WorkshopId = kitchenWorkshop.Id, NameAz = "Mərci Şorbası", NameEn = "Lentil Soup", NameRu = "Чечевичный Суп", Barcode = "100001", OrderIndex = 1, Unit = SalesUnit.Pcs, Stock = 100, CostPrice = 1.20m, MarkupValue = 150, MarkupType = MarkupType.Percentage, SalePrice = 3.00m, ShowInQr = true, ShowInTerminal = true, CreatedAt = now, CreatedBy = "System_Seed", IsDeleted = false },
            new Product { Id = Guid.NewGuid(), CompanyId = companyId, CategoryId = foodCategory.Id, WorkshopId = kitchenWorkshop.Id, NameAz = "Sezar Salatı", NameEn = "Caesar Salad", NameRu = "Салат Цезарь", Barcode = "100002", OrderIndex = 2, Unit = SalesUnit.Pcs, Stock = 50, CostPrice = 4.00m, MarkupValue = 100, MarkupType = MarkupType.Percentage, SalePrice = 8.00m, ShowInQr = true, ShowInTerminal = true, CreatedAt = now, CreatedBy = "System_Seed", IsDeleted = false },
            new Product { Id = Guid.NewGuid(), CompanyId = companyId, CategoryId = foodCategory.Id, WorkshopId = kitchenWorkshop.Id, NameAz = "Pizza Marqarita", NameEn = "Pizza Margherita", NameRu = "Пицца Маргарита", Barcode = "100003", OrderIndex = 3, Unit = SalesUnit.Pcs, Stock = 80, CostPrice = 3.50m, MarkupValue = 150, MarkupType = MarkupType.Percentage, SalePrice = 9.00m, ShowInQr = true, ShowInTerminal = true, CreatedAt = now, CreatedBy = "System_Seed", IsDeleted = false },
            new Product { Id = Guid.NewGuid(), CompanyId = companyId, CategoryId = drinkCategory.Id, WorkshopId = barWorkshop.Id, NameAz = "Coca-Cola 330ml", NameEn = "Coca-Cola 330ml", NameRu = "Coca-Cola 330мл", Barcode = "200001", OrderIndex = 1, Unit = SalesUnit.Pcs, Stock = 500, CostPrice = 0.60m, MarkupValue = 233, MarkupType = MarkupType.Percentage, SalePrice = 2.00m, ShowInQr = true, ShowInTerminal = true, CreatedAt = now, CreatedBy = "System_Seed", IsDeleted = false },
            new Product { Id = Guid.NewGuid(), CompanyId = companyId, CategoryId = drinkCategory.Id, WorkshopId = barWorkshop.Id, NameAz = "Təbii Şirə", NameEn = "Fresh Juice", NameRu = "Свежий Сок", Barcode = "200002", OrderIndex = 2, Unit = SalesUnit.Pcs, Stock = 120, CostPrice = 1.50m, MarkupValue = 200, MarkupType = MarkupType.Percentage, SalePrice = 4.50m, ShowInQr = true, ShowInTerminal = true, CreatedAt = now, CreatedBy = "System_Seed", IsDeleted = false }
        };
        await remoteDb.Products.AddRangeAsync(products);
    }
}
