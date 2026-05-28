using DAL.Server.Configurations;
using DAL.Server.Service;
using Domain.Common;
using Domain.Common.Entities;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DAL.Server.Context;

public class AppDbContext : DbContext
{

    private readonly Guid? _currentCompanyId;
    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUserService) : base(options)
    {
        _currentCompanyId = currentUserService.CompanyId;
    }

    protected AppDbContext(DbContextOptions options, ICurrentUserService currentUserService) : base(options)
    {
        _currentCompanyId = currentUserService.CompanyId;
    }

    public DbSet<Company> Companies { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Hall> Halls { get; set; }
    public DbSet<Table> Tables { get; set; }
    public DbSet<Workshop> Workshops { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<ProductWorkshop> ProductWorkshops { get; set; }
    public DbSet<CashShift> CashShifts { get; set; }
    public DbSet<CashShiftExpense> CashShiftExpenses { get; set; }
    public DbSet<OrderDetail> OrderDetails { get; set; }
    public DbSet<OrderHeader> OrderHeaders { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<OrderSplitPayment> OrderSplitPayments { get; set; }
    public DbSet<ProductSet> ProductSets { get; set; }
    public DbSet<ProductSetItem> ProductSetItems { get; set; }
    public DbSet<ProductSetChoiceGroup> ProductSetChoiceGroups { get; set; }
    public DbSet<ProductSetChoiceOption> ProductSetChoiceOptions { get; set; }
    public DbSet<QRMenuSetting> QRMenuSettings { get; set; }
    public DbSet<KitchenOperation> KitchenOperations{ get; set; }
    public DbSet<Warehouse> Warehouses{ get; set; }
    public DbSet<Supplier> Suppliers{ get; set; }
    public DbSet<Purchase> Purchases{ get; set; }
    public DbSet<ProductStockHistory> ProductStockHistories{ get; set; }
    public DbSet<AuditLog> AuditLogs{ get; set; }
    public DbSet<BossWebPushSubscription> BossWebPushSubscriptions { get; set; }
    public DbSet<BossTelegramChat> BossTelegramChats { get; set; }
    public DbSet<PendingOrderLineDeleteConfirm> PendingOrderLineDeleteConfirms { get; set; }
    public DbSet<CompanyPaymentMethod> CompanyPaymentMethods { get; set; }
    public DbSet<HallTimeDiscountRule> HallTimeDiscountRules { get; set; }
    public DbSet<LocalSyncMetadata> LocalSyncMetadata { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<AuditableEntity>();

        var bakuTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        var bakuTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, bakuTimeZone);

        var finalTime = DateTime.SpecifyKind(bakuTime, DateTimeKind.Unspecified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = finalTime;
                entry.Entity.IsSynced = false;

                if (entry is { Entity: AuditableCompanyEntity ace } && ace.CompanyId == Guid.Empty && _currentCompanyId.HasValue)
                {
                    ace.CompanyId = _currentCompanyId.Value;
                }

                if (string.IsNullOrEmpty(entry.Entity.CreatedBy))
                {
                    entry.Entity.CreatedBy = "System_Admin";
                }
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.LastModifiedAt = finalTime;
                entry.Entity.IsSynced = false;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KitchenOperationConfiguration).Assembly);

    }
}