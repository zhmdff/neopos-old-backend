using DAL.Server.Context;
using Domain.Common;
using Domain.Common.Entities;
using Domain.Entities;
using BusinessLayer.Utilities;
using Microsoft.EntityFrameworkCore;
using NeoPos.Migration.Legacy;
using Npgsql;

namespace NeoPos.Migration;

public sealed class FullCompanyMigrationOptions
{
    public required string SourceConnectionString { get; init; }
    public required string TargetConnectionString { get; init; }
    public required Guid CompanyId { get; init; }
    /// <summary>When empty, keeps TenantKey from source company row.</summary>
    public string? TenantKey { get; init; }
    public bool DryRun { get; init; }
}

public sealed class FullCompanyMigrationResult
{
    public List<string> Steps { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];
}

public sealed class FullCompanyMigrationRunner
{
    public async Task<FullCompanyMigrationResult> RunAsync(
        FullCompanyMigrationOptions options,
        CancellationToken ct = default)
    {
        var result = new FullCompanyMigrationResult();

        await using var source = CreateContext(options.SourceConnectionString);
        await using var target = CreateContext(options.TargetConnectionString);

        var company = await LoadSourceCompanyAsync(source, options, ct);
        if (company == null)
        {
            result.Errors.Add($"Company {options.CompanyId} not found in source database.");
            return result;
        }

        if (!string.IsNullOrWhiteSpace(options.TenantKey))
            company.TenantKey = options.TenantKey.Trim();
        else if (string.IsNullOrWhiteSpace(company.TenantKey))
            result.Warnings.Add("Source company has no TenantKey — set Migration:TenantKey before tenant bootstrap.");

        if (options.DryRun)
        {
            result.Warnings.Add("DRY RUN — no writes performed.");
            await SummarizeDryRunAsync(source, target, options.CompanyId, result, ct);
            return result;
        }

        await using var tx = await target.Database.BeginTransactionAsync(ct);
        try
        {
            await UpsertCompanyAsync(target, company, result, ct);

            await CopyCompanyRowsAsync<Role>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<Workshop>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<Category>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<Hall>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<Warehouse>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<Supplier>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<CompanyPaymentMethod>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<QRMenuSetting>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<Customer>(source, target, options.CompanyId, result, ct);
            await CopyUsersUpsertAsync(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<Table>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<Product>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<ProductVariant>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<ProductWorkshop>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<ProductSet>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<ProductSetItem>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<ProductSetChoiceGroup>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<ProductSetChoiceOption>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<HallTimeDiscountRule>(source, target, options.CompanyId, result, ct);
            await CopyPurchasesAsync(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<ProductStockHistory>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<CashShift>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<CashShiftExpense>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<OrderHeader>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<OrderDetail>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<KitchenOperation>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<OrderSplitPayment>(source, target, options.CompanyId, result, ct);
            await CopyCompanyRowsAsync<AuditLog>(source, target, options.CompanyId, result, ct);
            await CopyBossTelegramChatsAsync(source, target, options.CompanyId, result, ct);
            await CopyBossWebPushSubscriptionsAsync(source, target, options.CompanyId, result, ct);
            await CopyPendingDeletesAsync(source, target, options.CompanyId, result, ct);

            await tx.CommitAsync(ct);
            result.Steps.Add($"Company '{company.NameAz}' ({options.CompanyId}) migrated successfully.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            result.Errors.Add($"Migration rolled back: {ex.Message}");
            throw;
        }

        return result;
    }

    private static RemoteDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<RemoteDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new RemoteDbContext(options, new MigrationCurrentUserService());
    }

    private static async Task UpsertCompanyAsync(
        RemoteDbContext target,
        Company company,
        FullCompanyMigrationResult result,
        CancellationToken ct)
    {
        var existing = await target.Companies.FirstOrDefaultAsync(c => c.Id == company.Id, ct);
        company.IsSynced = true;
        if (existing == null)
        {
            target.Companies.Add(company);
            await target.SaveChangesAsync(ct);
            result.Steps.Add("Companies: +1 inserted");
            return;
        }

        target.Entry(existing).CurrentValues.SetValues(company);
        await target.SaveChangesAsync(ct);
        await target.Companies.Where(c => c.Id == company.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsSynced, true), ct);
        result.Steps.Add("Companies: 1 updated");
    }

    private static void NormalizeUserPasswords(IReadOnlyList<User> users)
    {
        foreach (var user in users)
            user.PasswordHash = PasswordHashHelper.NormalizeToBcrypt(user.PasswordHash);
    }

    private static async Task CopyUsersUpsertAsync(
        RemoteDbContext source,
        RemoteDbContext target,
        Guid companyId,
        FullCompanyMigrationResult result,
        CancellationToken ct)
    {
        var items = await source.Users.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(ct);
        if (items.Count == 0)
        {
            result.Steps.Add("Users: 0 rows (skipped)");
            return;
        }

        NormalizeUserPasswords(items);

        var ids = items.Select(x => x.Id).ToList();
        var existing = await target.Users.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        var existingById = existing.ToDictionary(x => x.Id);

        var inserted = 0;
        var updated = 0;
        foreach (var item in items)
        {
            item.IsSynced = true;
            if (existingById.TryGetValue(item.Id, out var local))
            {
                target.Entry(local).CurrentValues.SetValues(item);
                updated++;
            }
            else
            {
                target.Users.Add(item);
                inserted++;
            }
        }

        if (inserted > 0 || updated > 0)
            await target.SaveChangesAsync(ct);

        result.Steps.Add($"Users: +{inserted} inserted, {updated} updated");
    }

    private static async Task CopyCompanyRowsAsync<T>(
        RemoteDbContext source,
        RemoteDbContext target,
        Guid companyId,
        FullCompanyMigrationResult result,
        CancellationToken ct,
        Action<IReadOnlyList<T>>? beforeInsert = null) where T : AuditableCompanyEntity
    {
        var table = typeof(T).Name;
        var items = await source.Set<T>().AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(ct);
        if (items.Count == 0)
        {
            result.Steps.Add($"{table}: 0 rows (skipped)");
            return;
        }

        beforeInsert?.Invoke(items);

        var ids = items.Select(x => x.Id).ToList();
        var existingIds = await target.Set<T>()
            .Where(x => ids.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(ct);
        var existing = existingIds.ToHashSet();

        var inserted = 0;
        foreach (var item in items)
        {
            if (existing.Contains(item.Id)) continue;
            if (item is BaseEntity be) be.IsSynced = true;
            target.Set<T>().Add(item);
            inserted++;
        }

        if (inserted > 0)
        {
            await target.SaveChangesAsync(ct);
            var newIds = items.Where(x => !existing.Contains(x.Id)).Select(x => x.Id).ToList();
            await target.Set<T>()
                .Where(x => newIds.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsSynced, true), ct);
        }

        result.Steps.Add($"{table}: +{inserted} inserted, {items.Count - inserted} skipped");
    }

    private static async Task CopyPurchasesAsync(
        RemoteDbContext source,
        RemoteDbContext target,
        Guid companyId,
        FullCompanyMigrationResult result,
        CancellationToken ct)
    {
        var purchases = await source.Purchases.AsNoTracking()
            .Include(p => p.PurchaseItems)
            .Where(p => p.CompanyId == companyId)
            .ToListAsync(ct);

        if (purchases.Count == 0)
        {
            result.Steps.Add("Purchases: 0 rows (skipped)");
            return;
        }

        var ids = purchases.Select(p => p.Id).ToList();
        var existing = (await target.Purchases.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct))
            .ToHashSet();

        var inserted = 0;
        foreach (var purchase in purchases)
        {
            if (existing.Contains(purchase.Id)) continue;
            purchase.IsSynced = true;
            foreach (var line in purchase.PurchaseItems)
                line.IsSynced = true;
            target.Purchases.Add(purchase);
            inserted++;
        }

        if (inserted > 0)
            await target.SaveChangesAsync(ct);

        result.Steps.Add($"Purchases: +{inserted} inserted, {purchases.Count - inserted} skipped");
    }

    private static async Task CopyBossTelegramChatsAsync(
        RemoteDbContext source,
        RemoteDbContext target,
        Guid companyId,
        FullCompanyMigrationResult result,
        CancellationToken ct)
    {
        var items = await source.BossTelegramChats.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(ct);
        if (items.Count == 0)
        {
            result.Steps.Add("BossTelegramChats: 0 rows (skipped)");
            return;
        }

        var ids = items.Select(x => x.Id).ToList();
        var existing = (await target.BossTelegramChats.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct))
            .ToHashSet();
        var inserted = 0;
        foreach (var row in items)
        {
            if (existing.Contains(row.Id)) continue;
            target.BossTelegramChats.Add(row);
            inserted++;
        }

        if (inserted > 0)
            await target.SaveChangesAsync(ct);

        result.Steps.Add($"BossTelegramChats: +{inserted} inserted, {items.Count - inserted} skipped");
    }

    private static async Task CopyBossWebPushSubscriptionsAsync(
        RemoteDbContext source,
        RemoteDbContext target,
        Guid companyId,
        FullCompanyMigrationResult result,
        CancellationToken ct)
    {
        var items = await source.BossWebPushSubscriptions.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(ct);
        if (items.Count == 0)
        {
            result.Steps.Add("BossWebPushSubscriptions: 0 rows (skipped)");
            return;
        }

        var endpoints = items.Select(x => x.Endpoint).ToList();
        var existing = (await target.BossWebPushSubscriptions
                .Where(x => endpoints.Contains(x.Endpoint))
                .Select(x => x.Endpoint)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var inserted = 0;
        foreach (var row in items)
        {
            if (existing.Contains(row.Endpoint)) continue;
            target.BossWebPushSubscriptions.Add(row);
            inserted++;
        }

        if (inserted > 0)
            await target.SaveChangesAsync(ct);

        result.Steps.Add($"BossWebPushSubscriptions: +{inserted} inserted, {items.Count - inserted} skipped");
    }

    private static async Task CopyPendingDeletesAsync(
        RemoteDbContext source,
        RemoteDbContext target,
        Guid companyId,
        FullCompanyMigrationResult result,
        CancellationToken ct)
    {
        var items = await source.PendingOrderLineDeleteConfirms.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(ct);
        if (items.Count == 0)
        {
            result.Steps.Add("PendingOrderLineDeleteConfirms: 0 rows (skipped)");
            return;
        }

        var ids = items.Select(x => x.Id).ToList();
        var existing = (await target.PendingOrderLineDeleteConfirms.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct))
            .ToHashSet();
        var inserted = 0;
        foreach (var row in items)
        {
            if (existing.Contains(row.Id)) continue;
            target.PendingOrderLineDeleteConfirms.Add(row);
            inserted++;
        }

        if (inserted > 0)
            await target.SaveChangesAsync(ct);

        result.Steps.Add($"PendingOrderLineDeleteConfirms: +{inserted} inserted, {items.Count - inserted} skipped");
    }

    private static async Task SummarizeDryRunAsync(
        RemoteDbContext source,
        RemoteDbContext target,
        Guid companyId,
        FullCompanyMigrationResult result,
        CancellationToken ct)
    {
        var companyExists = await target.Companies.AnyAsync(c => c.Id == companyId, ct);
        result.Steps.Add($"Companies: target has row = {companyExists}");

        async Task Count<T>(string name) where T : AuditableCompanyEntity
        {
            var src = await source.Set<T>().CountAsync(x => x.CompanyId == companyId, ct);
            var dst = await target.Set<T>().CountAsync(x => x.CompanyId == companyId, ct);
            result.Steps.Add($"{name}: source={src}, target={dst}, to copy≈{Math.Max(0, src - dst)}");
        }

        await Count<Role>(nameof(Role));
        await Count<User>(nameof(User));
        await Count<Hall>(nameof(Hall));
        await Count<Table>(nameof(Table));
        await Count<Product>(nameof(Product));
        await Count<OrderHeader>(nameof(OrderHeader));
    }

    private static async Task<Company?> LoadSourceCompanyAsync(
        RemoteDbContext source,
        FullCompanyMigrationOptions options,
        CancellationToken ct)
    {
        try
        {
            return await source.Companies.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == options.CompanyId, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            var reader = new LegacyDataReader(options.SourceConnectionString);
            if (!await reader.CompanyExistsAsync(options.CompanyId, ct))
                return null;
            return await reader.ReadCompanyAsync(options.CompanyId, ct);
        }
    }
}
