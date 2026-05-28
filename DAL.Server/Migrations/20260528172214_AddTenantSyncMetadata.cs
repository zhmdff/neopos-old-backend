using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSyncMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantKey",
                table: "Companies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LocalSyncMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastSuccessfulSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "TEXT", nullable: true),
                    TenantKey = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSynced = table.Column<bool>(type: "INTEGER", nullable: false)
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
