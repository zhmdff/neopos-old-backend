using Domain.Common.Entities;
using Npgsql;

namespace NeoPos.Migration.Legacy;

/// <summary>
/// Reads legacy NeoPos PostgreSQL (schema in neopos_schema.sql — no TenantKey / IsSynced).
/// </summary>
internal sealed class LegacyDataReader
{
    private readonly string _connectionString;
    private readonly string _schema;

    public LegacyDataReader(string connectionString, string schema = "public")
    {
        _connectionString = connectionString;
        _schema = string.IsNullOrWhiteSpace(schema) ? "public" : schema.Trim();
    }

    public async Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $"""SELECT 1 FROM "{_schema}"."Companies" WHERE "Id" = @id LIMIT 1""", conn);
        cmd.Parameters.AddWithValue("id", companyId);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    public async Task<Company?> ReadCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand($"""
            SELECT
                "Id", "Logo", "NameAz", "NameRu", "NameEn",
                "AddressAz", "AddressRu", "AddressEn",
                "PhoneNumber1", "PhoneNumber2", "PhoneNumber3",
                "Slug", "PackageEndDate", "IsActive", "IsDeleted",
                "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy",
                "DeletedAt", "DeletedBy",
                "IsDeliveryPriceEnabled", "IsUserModeActive", "IsGuestModeActive",
                "TablesLayoutMode", "EkassamEnabled", "EkassamBaseUrl", "EkassamApiKey",
                "AutoCashShiftEnabled", "AutoCashShiftOpenTime", "AutoCashShiftCloseTime",
                "AutoCashShiftForceClose", "CashShiftPromptOpeningDeposit", "CashShiftPrintReportOnClose",
                "CashierPrinterTarget", "KitchenPrinterTarget", "ReceiptDesignSettingsJson",
                "KassaReceiptThankYouText", "PosLockScreenImage", "CustomerDisplayLockScreenImage",
                "MenuFilterByWorkshop", "TerminalLineDeleteConfirmEnabled",
                "TelegramBotToken", "TelegramNotifyPrefsJson"
            FROM "{_schema}"."Companies"
            WHERE "Id" = @id
            """, conn);
        cmd.Parameters.AddWithValue("id", companyId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new Company
        {
            Id = reader.GetGuid(0),
            Logo = reader.IsDBNull(1) ? null : reader.GetString(1),
            NameAz = reader.GetString(2),
            NameRu = reader.GetString(3),
            NameEn = reader.GetString(4),
            AddressAz = reader.GetString(5),
            AddressRu = reader.GetString(6),
            AddressEn = reader.GetString(7),
            PhoneNumber1 = reader.GetString(8),
            PhoneNumber2 = reader.IsDBNull(9) ? null : reader.GetString(9),
            PhoneNumber3 = reader.IsDBNull(10) ? null : reader.GetString(10),
            Slug = reader.IsDBNull(11) ? "" : reader.GetString(11),
            PackageEndDate = reader.GetDateTime(12),
            IsActive = reader.GetBoolean(13),
            IsDeleted = reader.GetBoolean(14),
            CreatedAt = reader.GetDateTime(15),
            CreatedBy = reader.GetString(16),
            LastModifiedAt = reader.IsDBNull(17) ? null : reader.GetDateTime(17),
            LastModifiedBy = reader.IsDBNull(18) ? null : reader.GetString(18),
            DeletedAt = reader.IsDBNull(19) ? null : reader.GetDateTime(19),
            DeletedBy = reader.IsDBNull(20) ? null : reader.GetString(20),
            IsDeliveryPriceEnabled = reader.GetBoolean(21),
            IsUserModeActive = reader.GetBoolean(22),
            IsGuestModeActive = reader.GetBoolean(23),
            TablesLayoutMode = (Domain.Enums.TablesLayoutMode)reader.GetInt32(24),
            EkassamEnabled = reader.GetBoolean(25),
            EkassamBaseUrl = reader.IsDBNull(26) ? null : reader.GetString(26),
            EkassamApiKey = reader.IsDBNull(27) ? null : reader.GetString(27),
            AutoCashShiftEnabled = reader.GetBoolean(28),
            AutoCashShiftOpenTime = reader.GetString(29),
            AutoCashShiftCloseTime = reader.GetString(30),
            AutoCashShiftForceClose = reader.GetBoolean(31),
            CashShiftPromptOpeningDeposit = reader.GetBoolean(32),
            CashShiftPrintReportOnClose = reader.GetBoolean(33),
            CashierPrinterTarget = reader.IsDBNull(34) ? null : reader.GetString(34),
            KitchenPrinterTarget = reader.IsDBNull(35) ? null : reader.GetString(35),
            ReceiptDesignSettingsJson = reader.IsDBNull(36) ? null : reader.GetString(36),
            KassaReceiptThankYouText = reader.IsDBNull(37) ? null : reader.GetString(37),
            PosLockScreenImage = reader.IsDBNull(38) ? null : reader.GetString(38),
            CustomerDisplayLockScreenImage = reader.IsDBNull(39) ? null : reader.GetString(39),
            MenuFilterByWorkshop = reader.GetBoolean(40),
            TerminalLineDeleteConfirmEnabled = reader.GetBoolean(41),
            TelegramBotToken = reader.IsDBNull(42) ? null : reader.GetString(42),
            TelegramNotifyPrefsJson = reader.IsDBNull(43) ? null : reader.GetString(43),
            IsSynced = true
        };
    }

    public async Task<IReadOnlyList<Role>> ReadRolesAsync(Guid companyId, CancellationToken ct = default)
    {
        var list = new List<Role>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand($"""
            SELECT
                "Id", "NameAz", "NameRu", "NameEn", "IsAdmin", "IsDeleted",
                "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy",
                "DeletedAt", "DeletedBy", "CompanyId", "Permissions"
            FROM "{_schema}"."Roles"
            WHERE "CompanyId" = @companyId
            ORDER BY "CreatedAt"
            """, conn);
        cmd.Parameters.AddWithValue("companyId", companyId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Role
            {
                Id = reader.GetGuid(0),
                NameAz = reader.GetString(1),
                NameRu = reader.GetString(2),
                NameEn = reader.GetString(3),
                IsAdmin = reader.GetBoolean(4),
                IsDeleted = reader.GetBoolean(5),
                CreatedAt = reader.GetDateTime(6),
                CreatedBy = reader.GetString(7),
                LastModifiedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                LastModifiedBy = reader.IsDBNull(9) ? null : reader.GetString(9),
                DeletedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                DeletedBy = reader.IsDBNull(11) ? null : reader.GetString(11),
                CompanyId = reader.GetGuid(12),
                Permissions = reader.IsDBNull(13) ? null : reader.GetFieldValue<int[]>(13),
                IsSynced = true
            });
        }

        return list;
    }

    public async Task<IReadOnlyList<User>> ReadUsersAsync(Guid companyId, CancellationToken ct = default)
    {
        var list = new List<User>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand($"""
            SELECT
                "Id", "FullName", "Username", "PasswordHash", "PinCode", "IsActive", "RoleId",
                "IsDeleted", "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy",
                "DeletedAt", "DeletedBy", "CompanyId", "LinkedAccountId"
            FROM "{_schema}"."Users"
            WHERE "CompanyId" = @companyId
            ORDER BY "CreatedAt"
            """, conn);
        cmd.Parameters.AddWithValue("companyId", companyId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new User
            {
                Id = reader.GetGuid(0),
                FullName = reader.GetString(1),
                Username = reader.GetString(2),
                PasswordHash = reader.GetString(3),
                PinCode = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsActive = reader.GetBoolean(5),
                RoleId = reader.GetGuid(6),
                IsDeleted = reader.GetBoolean(7),
                CreatedAt = reader.GetDateTime(8),
                CreatedBy = reader.GetString(9),
                LastModifiedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                LastModifiedBy = reader.IsDBNull(11) ? null : reader.GetString(11),
                DeletedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                DeletedBy = reader.IsDBNull(13) ? null : reader.GetString(13),
                CompanyId = reader.GetGuid(14),
                LinkedAccountId = reader.IsDBNull(15) ? null : reader.GetGuid(15),
                IsSynced = true
            });
        }

        return list;
    }
}
