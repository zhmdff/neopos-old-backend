using DAL.Server.Context;
using Domain.Common.Entities;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NeoPos.Migration.Legacy;

namespace NeoPos.Migration;

public sealed class UserMigrationOptions
{
    public required string SourceConnectionString { get; init; }
    public string SourceSchema { get; init; } = "public";
    public required string TargetConnectionString { get; init; }
    /// <summary>postgres | sqlite</summary>
    public required string TargetProvider { get; init; }
    public required Guid CompanyId { get; init; }
    public required string TenantKey { get; init; }
    public bool DryRun { get; init; }
    /// <summary>When true, update TenantKey on existing company instead of skipping.</summary>
    public bool UpdateTenantKey { get; init; }
}

public sealed class UserMigrationResult
{
    public int CompaniesInserted { get; set; }
    public int CompaniesSkipped { get; set; }
    public int CompaniesUpdated { get; set; }
    public int RolesInserted { get; set; }
    public int RolesSkipped { get; set; }
    public int UsersInserted { get; set; }
    public int UsersSkipped { get; set; }
    public int UsersRejected { get; set; }
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];
}

public sealed class UserMigrationRunner
{
    public async Task<UserMigrationResult> RunAsync(UserMigrationOptions options, CancellationToken ct = default)
    {
        var result = new UserMigrationResult();
        var reader = new LegacyDataReader(options.SourceConnectionString, options.SourceSchema);

        if (!await reader.CompanyExistsAsync(options.CompanyId, ct))
        {
            result.Errors.Add($"Company {options.CompanyId} not found in legacy database (schema: {options.SourceSchema}).");
            return result;
        }

        var legacyCompany = await reader.ReadCompanyAsync(options.CompanyId, ct);
        if (legacyCompany == null)
        {
            result.Errors.Add("Failed to read legacy company row.");
            return result;
        }

        legacyCompany.TenantKey = options.TenantKey.Trim();

        var legacyRoles = await reader.ReadRolesAsync(options.CompanyId, ct);
        var legacyUsers = await reader.ReadUsersAsync(options.CompanyId, ct);

        var roleIds = legacyRoles.Select(r => r.Id).ToHashSet();
        foreach (var user in legacyUsers)
        {
            if (!roleIds.Contains(user.RoleId))
            {
                result.UsersRejected++;
                result.Errors.Add(
                    $"User {user.Username} ({user.Id}) references missing RoleId {user.RoleId} — fix legacy data or migrate roles first.");
            }
        }

        if (result.UsersRejected > 0)
            return result;

        await using var db = CreateTargetContext(options);

        if (options.DryRun)
        {
            result.Warnings.Add("DRY RUN — no writes performed.");
            await SummarizeDryRunAsync(db, legacyCompany, legacyRoles, legacyUsers, result, ct);
            return result;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await MigrateCompanyAsync(db, legacyCompany, options, result, ct);
            await MigrateRolesAsync(db, legacyRoles, result, ct);
            await MigrateUsersAsync(db, legacyUsers, result, ct);

            if (options.TargetProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
                await EnsureLocalSyncMetadataAsync(db, options.TenantKey, ct);

            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            result.Errors.Add($"Migration rolled back: {ex.Message}");
            throw;
        }

        return result;
    }

    private static AppDbContext CreateTargetContext(UserMigrationOptions options)
    {
        var currentUser = new MigrationCurrentUserService();
        if (options.TargetProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var sqlite = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(options.TargetConnectionString)
                .Options;
            return new AppDbContext(sqlite, currentUser);
        }

        var pg = new DbContextOptionsBuilder<RemoteDbContext>()
            .UseNpgsql(options.TargetConnectionString)
            .Options;
        return new RemoteDbContext(pg, currentUser);
    }

    private static async Task MigrateCompanyAsync(
        AppDbContext db,
        Company legacy,
        UserMigrationOptions options,
        UserMigrationResult result,
        CancellationToken ct)
    {
        var existing = await db.Companies.FirstOrDefaultAsync(c => c.Id == legacy.Id, ct);
        if (existing == null)
        {
            db.Companies.Add(legacy);
            await db.SaveChangesAsync(ct);
            await MarkSyncedAsync(db, legacy.Id, ct);
            result.CompaniesInserted++;
            return;
        }

        result.CompaniesSkipped++;
        if (options.UpdateTenantKey && existing.TenantKey != options.TenantKey)
        {
            existing.TenantKey = options.TenantKey;
            await db.SaveChangesAsync(ct);
            await MarkSyncedAsync(db, existing.Id, ct);
            result.CompaniesUpdated++;
            result.Warnings.Add($"Updated TenantKey on company {existing.Id} to '{options.TenantKey}'.");
        }
        else if (string.IsNullOrEmpty(existing.TenantKey))
        {
            result.Warnings.Add(
                $"Company {existing.Id} already exists without TenantKey. Re-run with --update-tenant-key or set TenantKey manually.");
        }
    }

    private static async Task MigrateRolesAsync(
        AppDbContext db,
        IReadOnlyList<Role> legacyRoles,
        UserMigrationResult result,
        CancellationToken ct)
    {
        var existingIds = await db.Roles
            .Where(r => legacyRoles.Select(x => x.Id).Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(ct);
        var existingSet = existingIds.ToHashSet();

        foreach (var role in legacyRoles)
        {
            if (existingSet.Contains(role.Id))
            {
                result.RolesSkipped++;
                continue;
            }

            db.Roles.Add(role);
            result.RolesInserted++;
        }

        if (result.RolesInserted > 0)
        {
            await db.SaveChangesAsync(ct);
            var insertedIds = legacyRoles.Select(r => r.Id).Except(existingSet).ToList();
            await MarkSyncedAsync(db, insertedIds, ct);
        }
    }

    private static async Task MigrateUsersAsync(
        AppDbContext db,
        IReadOnlyList<User> legacyUsers,
        UserMigrationResult result,
        CancellationToken ct)
    {
        var legacyIds = legacyUsers.Select(u => u.Id).ToList();
        var existingById = await db.Users
            .Where(u => legacyIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(ct);
        var existingIdSet = existingById.ToHashSet();

        var usernames = legacyUsers.Select(u => u.Username).ToList();
        var existingByUsername = await db.Users
            .Where(u => usernames.Contains(u.Username) && !u.IsDeleted)
            .Select(u => new { u.Id, u.Username })
            .ToListAsync(ct);

        foreach (var user in legacyUsers)
        {
            if (existingIdSet.Contains(user.Id))
            {
                result.UsersSkipped++;
                continue;
            }

            var usernameConflict = existingByUsername.FirstOrDefault(x =>
                x.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase) && x.Id != user.Id);
            if (usernameConflict != null)
            {
                result.UsersRejected++;
                result.Errors.Add(
                    $"Username '{user.Username}' already used by user {usernameConflict.Id}. Legacy user {user.Id} not imported.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                result.UsersRejected++;
                result.Errors.Add($"User '{user.Username}' has empty PasswordHash — skipped.");
                continue;
            }

            db.Users.Add(user);
            result.UsersInserted++;
        }

        if (result.UsersRejected > 0)
            throw new InvalidOperationException("User migration aborted due to conflicts. Transaction will roll back.");

        if (result.UsersInserted > 0)
        {
            await db.SaveChangesAsync(ct);
            var insertedIds = legacyUsers
                .Where(u => !existingIdSet.Contains(u.Id))
                .Select(u => u.Id)
                .ToList();
            await MarkSyncedAsync(db, insertedIds, ct);
        }
    }

    private static async Task EnsureLocalSyncMetadataAsync(AppDbContext db, string tenantKey, CancellationToken ct)
    {
        var meta = await db.LocalSyncMetadata.FirstOrDefaultAsync(ct);
        if (meta == null)
        {
            meta = new LocalSyncMetadata
            {
                Id = Guid.NewGuid(),
                TenantKey = tenantKey,
                LastSuccessfulSyncAt = DateTime.UtcNow,
                LastSyncStatus = "LegacyUserMigration",
                IsSynced = true
            };
            db.LocalSyncMetadata.Add(meta);
        }
        else
        {
            meta.TenantKey = tenantKey;
            meta.LastSyncStatus = "LegacyUserMigration";
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>AppDbContext.SaveChanges forces IsSynced=false on insert — restore for migrated rows.</summary>
    private static async Task MarkSyncedAsync(AppDbContext db, Guid id, CancellationToken ct)
    {
        await db.Companies.Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsSynced, true), ct);
    }

    private static async Task MarkSyncedAsync(AppDbContext db, IReadOnlyList<Guid> roleOrUserIds, CancellationToken ct)
    {
        if (roleOrUserIds.Count == 0) return;

        await db.Roles.Where(r => roleOrUserIds.Contains(r.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsSynced, true), ct);

        await db.Users.Where(u => roleOrUserIds.Contains(u.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsSynced, true), ct);
    }

    private static async Task SummarizeDryRunAsync(
        AppDbContext db,
        Company company,
        IReadOnlyList<Role> roles,
        IReadOnlyList<User> users,
        UserMigrationResult result,
        CancellationToken ct)
    {
        var companyExists = await db.Companies.AnyAsync(c => c.Id == company.Id, ct);
        result.CompaniesInserted = companyExists ? 0 : 1;
        result.CompaniesSkipped = companyExists ? 1 : 0;

        var roleIds = await db.Roles.Where(r => roles.Select(x => x.Id).Contains(r.Id)).Select(r => r.Id).ToListAsync(ct);
        result.RolesInserted = roles.Count - roleIds.Count;
        result.RolesSkipped = roleIds.Count;

        var userIds = await db.Users.Where(u => users.Select(x => x.Id).Contains(u.Id)).Select(u => u.Id).ToListAsync(ct);
        result.UsersInserted = users.Count - userIds.Count;
        result.UsersSkipped = userIds.Count;
    }
}
