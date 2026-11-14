using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamada.Modules.Workforce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TwoFactorAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pending_two_factor_secret",
                schema: "workforce",
                table: "workers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "two_factor_enabled",
                schema: "workforce",
                table: "workers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "two_factor_last_step",
                schema: "workforce",
                table: "workers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_secret",
                schema: "workforce",
                table: "workers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recovery_codes",
                schema: "workforce",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recovery_codes", x => x.id);
                    table.ForeignKey(
                        name: "fk_recovery_codes_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "workforce",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recovery_codes_worker_id_code_hash",
                schema: "workforce",
                table: "recovery_codes",
                columns: new[] { "worker_id", "code_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recovery_codes",
                schema: "workforce");

            migrationBuilder.DropColumn(
                name: "pending_two_factor_secret",
                schema: "workforce",
                table: "workers");

            migrationBuilder.DropColumn(
                name: "two_factor_enabled",
                schema: "workforce",
                table: "workers");

            migrationBuilder.DropColumn(
                name: "two_factor_last_step",
                schema: "workforce",
                table: "workers");

            migrationBuilder.DropColumn(
                name: "two_factor_secret",
                schema: "workforce",
                table: "workers");
        }
    }
}
