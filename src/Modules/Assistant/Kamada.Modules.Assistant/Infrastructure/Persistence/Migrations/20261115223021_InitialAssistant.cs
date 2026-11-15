using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamada.Modules.Assistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAssistant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "assistant");

            migrationBuilder.CreateTable(
                name: "settings",
                schema: "assistant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    text_provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ollama_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    gemini_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    speech_provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settings", x => x.id);
                    table.CheckConstraint("ck_settings_single_row", "id = '00000000-0000-7000-8000-000000000001'");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "settings",
                schema: "assistant");
        }
    }
}
