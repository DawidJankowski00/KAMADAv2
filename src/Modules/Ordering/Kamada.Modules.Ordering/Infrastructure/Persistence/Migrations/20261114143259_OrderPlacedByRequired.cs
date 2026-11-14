using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamada.Modules.Ordering.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Every order records who placed it. Only orders placed before that was recorded (stage 4) lack the value; there is
    /// no production data, so such a database is reset instead of guessing a worker (and Ordering cannot read Workforce
    /// tables). The column gets no default: a missing worker must fail, not become an empty id.
    /// </summary>
    public partial class OrderPlacedByRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM ordering.orders WHERE placed_by_worker_id IS NULL) THEN
                        RAISE EXCEPTION 'ordering.orders has orders without placed_by_worker_id (placed before it was recorded). Reset the database: docker compose down -v';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "placed_by_worker_id",
                schema: "ordering",
                table: "orders",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "placed_by_worker_id",
                schema: "ordering",
                table: "orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
