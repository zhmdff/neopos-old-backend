using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Server.Migrations.RemoteDb
{
    /// <inheritdoc />
    public partial class AddTenantSyncMetadataRemote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantKey",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LocalSyncMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSuccessfulSyncAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "text", nullable: true),
                    TenantKey = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsSynced = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalSyncMetadata", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalSyncMetadata");

            migrationBuilder.DropColumn(
                name: "TenantKey",
                table: "Companies");
        }
    }
}
