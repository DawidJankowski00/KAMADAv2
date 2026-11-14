using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamada.Modules.Delivery.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Drops the foreign-key index on <c>delivery_run_id</c>: the DEFERRABLE unique constraint
    /// <c>ux_delivery_run_orders_run_seq</c> (created in <c>InitialDelivery</c>) already has an index starting with that
    /// column. The model now declares that index, so EF stops asking for its own; it is not created here because the
    /// constraint already owns it.
    /// </summary>
    public partial class DropRedundantRunIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_delivery_run_orders_delivery_run_id",
                schema: "delivery",
                table: "delivery_run_orders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_delivery_run_orders_delivery_run_id",
                schema: "delivery",
                table: "delivery_run_orders",
                column: "delivery_run_id");
        }
    }
}
