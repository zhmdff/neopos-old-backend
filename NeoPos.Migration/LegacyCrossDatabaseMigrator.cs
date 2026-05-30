using Npgsql;
using BusinessLayer.Utilities;

namespace NeoPos.Migration;

/// <summary>
/// Copies one tenant from a legacy PostgreSQL database (no TenantKey/IsSynced) into neopos_sync
/// using postgres_fdw on the same server.
/// </summary>
public static class LegacyCrossDatabaseMigrator
{
    public sealed class Options
    {
        public required string TargetConnectionString { get; init; }
        public required string SourceDatabase { get; init; }
        public required string SourceUser { get; init; }
        public required string SourcePassword { get; init; }
        public required Guid CompanyId { get; init; }
        public required string TenantKey { get; init; }
    }

    public static async Task RunAsync(Options options, CancellationToken ct = default)
    {
        var target = new NpgsqlConnectionStringBuilder(options.TargetConnectionString);
        await using var conn = new NpgsqlConnection(target.ConnectionString);
        await conn.OpenAsync(ct);

        var companyId = options.CompanyId.ToString("D");
        var tenantKey = options.TenantKey.Replace("'", "''");
        var serverName = "neopos_legacy_import_srv";
        var schemaName = "legacy_import";

        async Task Exec(string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await Exec("CREATE EXTENSION IF NOT EXISTS postgres_fdw;");
        await Exec($"DROP SERVER IF EXISTS {serverName} CASCADE;");
        await Exec($"""
            CREATE SERVER {serverName}
            FOREIGN DATA WRAPPER postgres_fdw
            OPTIONS (host 'localhost', dbname '{options.SourceDatabase.Replace("'", "''")}', port '5432');
            """);
        await Exec($"""
            CREATE USER MAPPING IF NOT EXISTS FOR CURRENT_USER
            SERVER {serverName}
            OPTIONS (user '{options.SourceUser.Replace("'", "''")}', password '{options.SourcePassword.Replace("'", "''")}');
            """);
        await Exec($"DROP SCHEMA IF EXISTS {schemaName} CASCADE;");
        await Exec($"CREATE SCHEMA {schemaName};");

        var tables = new[]
        {
            "Companies", "Roles", "Users", "Halls", "Tables", "Categories", "Workshops", "Products",
            "ProductVariants", "CashShifts", "OrderHeaders", "OrderDetails", "HallTimeDiscountRules",
            "Customers", "AuditLogs"
        };

        await Exec($"""
            IMPORT FOREIGN SCHEMA public
            LIMIT TO ({string.Join(", ", tables.Select(t => $"\"{t}\""))})
            FROM SERVER {serverName}
            INTO {schemaName};
            """);

        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            await Exec($"""
                INSERT INTO public."Companies" (
                    "Id", "Logo", "NameAz", "NameRu", "NameEn", "AddressAz", "AddressRu", "AddressEn",
                    "PhoneNumber1", "PhoneNumber2", "PhoneNumber3", "Slug", "PackageEndDate", "IsActive",
                    "IsDeliveryPriceEnabled", "IsUserModeActive", "IsGuestModeActive", "TablesLayoutMode",
                    "EkassamEnabled", "EkassamBaseUrl", "EkassamApiKey", "AutoCashShiftEnabled",
                    "AutoCashShiftOpenTime", "AutoCashShiftCloseTime", "AutoCashShiftForceClose",
                    "CashShiftPromptOpeningDeposit", "CashShiftPrintReportOnClose", "CashierPrinterTarget",
                    "KitchenPrinterTarget", "ReceiptDesignSettingsJson", "KassaReceiptThankYouText",
                    "PosLockScreenImage", "CustomerDisplayLockScreenImage", "MenuFilterByWorkshop",
                    "TerminalLineDeleteConfirmEnabled", "TelegramBotToken", "TelegramNotifyPrefsJson",
                    "IsDeleted", "IsSynced", "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy",
                    "DeletedAt", "DeletedBy", "TenantKey"
                )
                SELECT
                    "Id", "Logo", "NameAz", "NameRu", "NameEn", "AddressAz", "AddressRu", "AddressEn",
                    "PhoneNumber1", "PhoneNumber2", "PhoneNumber3", "Slug", "PackageEndDate", "IsActive",
                    "IsDeliveryPriceEnabled", "IsUserModeActive", "IsGuestModeActive", "TablesLayoutMode",
                    "EkassamEnabled", "EkassamBaseUrl", "EkassamApiKey", "AutoCashShiftEnabled",
                    "AutoCashShiftOpenTime", "AutoCashShiftCloseTime", "AutoCashShiftForceClose",
                    "CashShiftPromptOpeningDeposit", "CashShiftPrintReportOnClose", "CashierPrinterTarget",
                    "KitchenPrinterTarget", "ReceiptDesignSettingsJson", "KassaReceiptThankYouText",
                    "PosLockScreenImage", "CustomerDisplayLockScreenImage", "MenuFilterByWorkshop",
                    "TerminalLineDeleteConfirmEnabled", "TelegramBotToken", "TelegramNotifyPrefsJson",
                    "IsDeleted", TRUE, "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy",
                    "DeletedAt", "DeletedBy", '{tenantKey}'
                FROM {schemaName}."Companies"
                WHERE "Id" = '{companyId}'
                ON CONFLICT ("Id") DO UPDATE SET "TenantKey" = EXCLUDED."TenantKey", "IsSynced" = TRUE;
                """);

            await CopySimpleAsync(conn, schemaName, "Roles",
                "\"Id\", \"NameAz\", \"NameRu\", \"NameEn\", \"IsAdmin\", \"IsDeleted\", \"IsSynced\", \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"Permissions\"",
                "\"Id\", \"NameAz\", \"NameRu\", \"NameEn\", \"IsAdmin\", \"IsDeleted\", TRUE, \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"Permissions\"",
                companyId, ct);

            await CopySimpleAsync(conn, schemaName, "Users",
                "\"Id\", \"FullName\", \"Username\", \"PasswordHash\", \"PinCode\", \"IsActive\", \"RoleId\", \"IsDeleted\", \"IsSynced\", \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"LinkedAccountId\"",
                "\"Id\", \"FullName\", \"Username\", \"PasswordHash\", \"PinCode\", \"IsActive\", \"RoleId\", \"IsDeleted\", TRUE, \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"LinkedAccountId\"",
                companyId, ct);

            await UpgradePlaintextUserPasswordsAsync(conn, companyId, ct);

            await CopySimpleAsync(conn, schemaName, "Halls",
                "\"Id\", \"NameAz\", \"NameEn\", \"NameRu\", \"ServicePercentage\", \"IsDeleted\", \"IsSynced\", \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"OrderIndex\", \"IsGuestCountEnabled\", \"IsTableHourActive\"",
                "\"Id\", \"NameAz\", \"NameEn\", \"NameRu\", \"ServicePercentage\", \"IsDeleted\", TRUE, \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"OrderIndex\", \"IsGuestCountEnabled\", \"IsTableHourActive\"",
                companyId, ct);

            await CopySimpleAsync(conn, schemaName, "Tables",
                "\"Id\", \"NameAz\", \"NameEn\", \"NameRu\", \"Capacity\", \"DepositAmount\", \"DepositStartTime\", \"DepositEndTime\", \"HallId\", \"Status\", \"IsDeleted\", \"IsSynced\", \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"OrderIndex\", \"MapPositionX\", \"MapPositionY\", \"MapWidthPercent\", \"MapHeightPercent\", \"MapShape\", \"TableHourLimitMinutes\"",
                "\"Id\", \"NameAz\", \"NameEn\", \"NameRu\", \"Capacity\", \"DepositAmount\", \"DepositStartTime\", \"DepositEndTime\", \"HallId\", \"Status\", \"IsDeleted\", TRUE, \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"OrderIndex\", \"MapPositionX\", \"MapPositionY\", \"MapWidthPercent\", \"MapHeightPercent\", \"MapShape\", \"TableHourLimitMinutes\"",
                companyId, ct);

            await CopySimpleAsync(conn, schemaName, "Categories",
                "\"Id\", \"NameAz\", \"NameEn\", \"NameRu\", \"OrderIndex\", \"ImageUrl\", \"ParentCategoryId\", \"IsDeleted\", \"IsSynced\", \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"OrderIndexByQrMenu\"",
                "\"Id\", \"NameAz\", \"NameEn\", \"NameRu\", \"OrderIndex\", \"ImageUrl\", \"ParentCategoryId\", \"IsDeleted\", TRUE, \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"OrderIndexByQrMenu\"",
                companyId, ct);

            await CopySimpleAsync(conn, schemaName, "Workshops",
                "\"Id\", \"NameAz\", \"NameEn\", \"NameRu\", \"IsPrinting\", \"IsDeleted\", \"IsSynced\", \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"PrinterType\", \"PrinterValue\"",
                "\"Id\", \"NameAz\", \"NameEn\", \"NameRu\", \"IsPrinting\", \"IsDeleted\", TRUE, \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"PrinterType\", \"PrinterValue\"",
                companyId, ct);

            await CopySimpleAsync(conn, schemaName, "Products",
                "\"Id\", \"NameAz\", \"NameEn\", \"NameRu\", \"Barcode\", \"Unit\", \"CostPrice\", \"MarkupValue\", \"MarkupType\", \"SalePrice\", \"ImageUrl\", \"CategoryId\", \"WorkshopId\", \"CookingProcess\", \"IsDeleted\", \"IsSynced\", \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"OrderIndex\", \"OrderIndexByQrMenu\", \"Stock\", \"DeliveryPrice\", \"ShowInQr\", \"ShowInTerminal\"",
                "\"Id\", \"NameAz\", \"NameEn\", \"NameRu\", \"Barcode\", \"Unit\", \"CostPrice\", \"MarkupValue\", \"MarkupType\", \"SalePrice\", \"ImageUrl\", \"CategoryId\", \"WorkshopId\", \"CookingProcess\", \"IsDeleted\", TRUE, \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"OrderIndex\", \"OrderIndexByQrMenu\", \"Stock\", \"DeliveryPrice\", \"ShowInQr\", \"ShowInTerminal\"",
                companyId, ct);

            await CopySimpleAsync(conn, schemaName, "ProductVariants",
                "\"Id\", \"NameAz\", \"NameEn\", \"NameRu\", \"Price\", \"Barcode\", \"OrderIndex\", \"ProductId\", \"IsDeleted\", \"IsSynced\", \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"DeliveryPrice\"",
                "\"Id\", \"NameAz\", \"NameEn\", \"NameRu\", \"Price\", \"Barcode\", \"OrderIndex\", \"ProductId\", \"IsDeleted\", TRUE, \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"DeliveryPrice\"",
                companyId, ct);

            await CopySimpleAsync(conn, schemaName, "CashShifts",
                "\"Id\", \"StartTime\", \"EndTime\", \"OpenedByUserId\", \"ClosedByUserId\", \"IsClosed\", \"IsDeleted\", \"IsSynced\", \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"WaiterAccessCode\", \"OpeningDepositAmount\"",
                "\"Id\", \"StartTime\", \"EndTime\", \"OpenedByUserId\", \"ClosedByUserId\", \"IsClosed\", \"IsDeleted\", TRUE, \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"WaiterAccessCode\", \"OpeningDepositAmount\"",
                companyId, ct);

            await CopySimpleAsync(conn, schemaName, "OrderHeaders",
                "\"Id\", \"CheckNumber\", \"IsClosed\", \"Note\", \"TableId\", \"WaiterName\", \"CashierName\", \"OpenTime\", \"CloseTime\", \"TotalAmount\", \"PaymentMethod\", \"IsDeleted\", \"IsSynced\", \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"ServiceAmount\", \"ServicePercentage\", \"DepositAmount\", \"DepositEndTime\", \"DepositStartTime\", \"DiscountAmount\", \"DiscountPercentage\", \"IsPercentageDiscount\", \"PaidCard\", \"PaidCash\", \"CustomerId\", \"BehAmount\", \"CashShiftId\", \"CustomPaymentMethodId\", \"GuestCount\", \"TableHourBonusMinutes\"",
                "\"Id\", \"CheckNumber\", \"IsClosed\", \"Note\", \"TableId\", \"WaiterName\", \"CashierName\", \"OpenTime\", \"CloseTime\", \"TotalAmount\", \"PaymentMethod\", \"IsDeleted\", TRUE, \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"ServiceAmount\", \"ServicePercentage\", \"DepositAmount\", \"DepositEndTime\", \"DepositStartTime\", \"DiscountAmount\", \"DiscountPercentage\", \"IsPercentageDiscount\", \"PaidCard\", \"PaidCash\", \"CustomerId\", \"BehAmount\", \"CashShiftId\", \"CustomPaymentMethodId\", \"GuestCount\", \"TableHourBonusMinutes\"",
                companyId, ct);

            await CopySimpleAsync(conn, schemaName, "OrderDetails",
                "\"Id\", \"OrderHeaderId\", \"ProductId\", \"ProductName\", \"Price\", \"Quantity\", \"ItemNote\", \"TotalPrice\", \"IsDeleted\", \"IsSynced\", \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"IsSent\", \"SplitGroup\", \"ProductVariantId\", \"ProductVariantName\", \"KitchenCompositionNote\"",
                "\"Id\", \"OrderHeaderId\", \"ProductId\", \"ProductName\", \"Price\", \"Quantity\", \"ItemNote\", \"TotalPrice\", \"IsDeleted\", TRUE, \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"IsSent\", \"SplitGroup\", \"ProductVariantId\", \"ProductVariantName\", \"KitchenCompositionNote\"",
                companyId, ct);

            await CopySimpleAsync(conn, schemaName, "Customers",
                "\"Id\", \"FullName\", \"Phone\", \"Address\", \"BirthDay\", \"IsDeleted\", \"IsSynced\", \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\"",
                "\"Id\", \"FullName\", \"Phone\", \"Address\", \"BirthDay\", \"IsDeleted\", TRUE, \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\"",
                companyId, ct);

            await CopySimpleAsync(conn, schemaName, "AuditLogs",
                "\"Id\", \"UserId\", \"UserName\", \"Action\", \"TableName\", \"HallName\", \"Description\", \"IsDeleted\", \"IsSynced\", \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"LineProductName\", \"LineQuantity\", \"LineTotal\", \"LineUnitPrice\"",
                "\"Id\", \"UserId\", \"UserName\", \"Action\", \"TableName\", \"HallName\", \"Description\", \"IsDeleted\", TRUE, \"CreatedAt\", \"CreatedBy\", \"LastModifiedAt\", \"LastModifiedBy\", \"DeletedAt\", \"DeletedBy\", \"CompanyId\", \"LineProductName\", \"LineQuantity\", \"LineTotal\", \"LineUnitPrice\"",
                companyId, ct);

            await tx.CommitAsync(ct);
            Console.WriteLine($"Legacy import completed for {companyId} → TenantKey={options.TenantKey}");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            await Exec($"DROP SCHEMA IF EXISTS {schemaName} CASCADE;");
            await Exec($"DROP SERVER IF EXISTS {serverName} CASCADE;");
        }
    }

    private static async Task UpgradePlaintextUserPasswordsAsync(
        NpgsqlConnection conn,
        string companyId,
        CancellationToken ct)
    {
        var users = new List<(Guid Id, string PasswordHash)>();
        await using (var read = new NpgsqlCommand(
            "SELECT \"Id\", \"PasswordHash\" FROM public.\"Users\" WHERE \"CompanyId\" = @cid AND NOT \"IsDeleted\"",
            conn))
        {
            read.Parameters.AddWithValue("cid", Guid.Parse(companyId));
            await using var r = await read.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var hash = r.GetString(1);
                if (PasswordHashHelper.IsLegacyPlaintext(hash))
                    users.Add((r.GetGuid(0), hash));
            }
        }

        foreach (var (id, plain) in users)
        {
            var bcrypt = PasswordHashHelper.NormalizeToBcrypt(plain);
            await using var upd = new NpgsqlCommand(
                """UPDATE public."Users" SET "PasswordHash" = @hash WHERE "Id" = @id""",
                conn);
            upd.Parameters.AddWithValue("hash", bcrypt);
            upd.Parameters.AddWithValue("id", id);
            await upd.ExecuteNonQueryAsync(ct);
        }

        if (users.Count > 0)
            Console.WriteLine($"Upgraded {users.Count} legacy plaintext password(s) to BCrypt.");
    }

    private static async Task CopySimpleAsync(
        NpgsqlConnection conn,
        string schemaName,
        string table,
        string destCols,
        string selectCols,
        string companyId,
        CancellationToken ct)
    {
        var sql = $"""
            INSERT INTO public."{table}" ({destCols})
            SELECT {selectCols}
            FROM {schemaName}."{table}"
            WHERE "CompanyId" = '{companyId}'
            ON CONFLICT ("Id") DO NOTHING;
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        var n = await cmd.ExecuteNonQueryAsync(ct);
        Console.WriteLine($"{table}: {n} rows");
    }
}
