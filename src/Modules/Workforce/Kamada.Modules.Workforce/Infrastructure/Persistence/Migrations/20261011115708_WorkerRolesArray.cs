using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamada.Modules.Workforce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkerRolesArray : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "roles",
                schema: "workforce",
                table: "workers",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.Sql("UPDATE workforce.workers SET roles = ARRAY[role];");

            migrationBuilder.DropColumn(
                name: "role",
                schema: "workforce",
                table: "workers");

            migrationBuilder.AddCheckConstraint(
                name: "ck_workers_roles_not_empty",
                schema: "workforce",
                table: "workers",
                sql: "cardinality(roles) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_workers_roles_not_empty",
                schema: "workforce",
                table: "workers");

            migrationBuilder.AddColumn<string>(
                name: "role",
                schema: "workforce",
                table: "workers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // A worker with several roles keeps the first one (Admin before Courier).
            migrationBuilder.Sql("UPDATE workforce.workers SET role = roles[1];");

            migrationBuilder.DropColumn(
                name: "roles",
                schema: "workforce",
                table: "workers");
        }
    }
}
