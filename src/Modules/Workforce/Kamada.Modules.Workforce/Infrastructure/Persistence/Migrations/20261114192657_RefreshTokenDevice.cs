using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamada.Modules.Workforce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefreshTokenDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                schema: "workforce",
                table: "refresh_tokens",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "user_agent",
                schema: "workforce",
                table: "refresh_tokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ip_address",
                schema: "workforce",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "user_agent",
                schema: "workforce",
                table: "refresh_tokens");
        }
    }
}
