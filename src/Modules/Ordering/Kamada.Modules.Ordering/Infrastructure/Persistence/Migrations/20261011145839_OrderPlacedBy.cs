using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamada.Modules.Ordering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderPlacedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "placed_by_worker_id",
                schema: "ordering",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_placed_by_worker_id_created_at",
                schema: "ordering",
                table: "orders",
                columns: new[] { "placed_by_worker_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_placed_by_worker_id_created_at",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "placed_by_worker_id",
                schema: "ordering",
                table: "orders");
        }
    }
}
