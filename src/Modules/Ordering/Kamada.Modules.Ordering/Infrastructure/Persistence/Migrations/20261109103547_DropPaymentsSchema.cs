using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamada.Modules.Ordering.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// After <see cref="MovePaymentsIntoOrdering"/> the old <c>payments</c> schema holds only the migration history of the
    /// former Payments module. Dropped without <c>CASCADE</c>: anything else left there stops the migration instead of
    /// being deleted.
    /// </summary>
    public partial class DropPaymentsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS payments.__ef_migrations_history;
                DROP SCHEMA IF EXISTS payments;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
