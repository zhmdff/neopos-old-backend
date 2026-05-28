using DAL.Server.Context;
using Microsoft.EntityFrameworkCore;

namespace DAL.Server.SchemaPatches;

/// <summary>
/// Köhnə manual migration cədvəli DeletedAt/DeletedBy sütunları olmadan yaradılıbsa — idempotent düzəliş.
/// </summary>
public static class HallTimeDiscountRulesSchemaPatch
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            const string sqliteSql = """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                SELECT '20260516130000_HallTimeDiscountRulesDeletedColumns', '8.0.0'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" = '20260516130000_HallTimeDiscountRulesDeletedColumns'
                );
                """;
            await db.Database.ExecuteSqlRawAsync(sqliteSql, cancellationToken);
            return;
        }

        const string sql = """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = 'HallTimeDiscountRules'
                ) THEN
                    ALTER TABLE "HallTimeDiscountRules"
                        ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp without time zone;
                    ALTER TABLE "HallTimeDiscountRules"
                        ADD COLUMN IF NOT EXISTS "DeletedBy" character varying(256);
                END IF;
            END $$;

            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260516130000_HallTimeDiscountRulesDeletedColumns', '8.0.0'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20260516130000_HallTimeDiscountRulesDeletedColumns'
            );
            """;

        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
