using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamada.Modules.Ordering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentsOrderForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ix_payments_order_id1",
                schema: "ordering",
                table: "payments",
                newName: "ux_payments_one_active_per_order");

            migrationBuilder.AddForeignKey(
                name: "fk_payments_orders_order_id",
                schema: "ordering",
                table: "payments",
                column: "order_id",
                principalSchema: "ordering",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payments_orders_order_id",
                schema: "ordering",
                table: "payments");

            migrationBuilder.RenameIndex(
                name: "ux_payments_one_active_per_order",
                schema: "ordering",
                table: "payments",
                newName: "ix_payments_order_id1");
        }
    }
}
