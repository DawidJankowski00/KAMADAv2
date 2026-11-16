using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamada.Modules.Workforce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Workers get a unique e-mail address. Accounts that exist already get a placeholder (username + <c>@kamada.test</c>)
    /// (the demo domain) for an administrator to replace; the column has no default.
    /// </summary>
    public partial class WorkerEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "workforce",
                table: "workers",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.Sql("UPDATE workforce.workers SET email = username || '@kamada.test' WHERE email IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "workforce",
                table: "workers",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_workers_email",
                schema: "workforce",
                table: "workers",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workers_email",
                schema: "workforce",
                table: "workers");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "workforce",
                table: "workers");
        }
    }
}
