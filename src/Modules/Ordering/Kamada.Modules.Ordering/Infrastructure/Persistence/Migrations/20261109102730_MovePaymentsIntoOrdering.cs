using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamada.Modules.Ordering.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Payments moved from their own module into Ordering. A database created before the move has
    /// <c>payments.payments</c>: the table (with its data, constraints and indexes) is moved into this schema. A new
    /// database never had it, so the table is created. The empty <c>payments</c> schema is dropped by a later migration.
    /// </summary>
    public partial class MovePaymentsIntoOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('payments.payments') IS NOT NULL THEN
                        ALTER TABLE payments.payments SET SCHEMA ordering;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS ordering.payments (
                    id uuid NOT NULL,
                    order_id uuid NOT NULL,
                    provider character varying(20) NOT NULL,
                    transaction_id character varying(64) NOT NULL,
                    amount numeric(10,2) NOT NULL,
                    status character varying(20) NOT NULL,
                    failure_reason character varying(500),
                    paid_at timestamp with time zone,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone,
                    CONSTRAINT pk_payments PRIMARY KEY (id),
                    CONSTRAINT ck_payments_amount_positive CHECK (amount > 0)
                );
                CREATE INDEX IF NOT EXISTS ix_payments_order_id ON ordering.payments (order_id);
                CREATE UNIQUE INDEX IF NOT EXISTS ix_payments_order_id1 ON ordering.payments (order_id) WHERE status IN ('Pending', 'Completed');
                CREATE UNIQUE INDEX IF NOT EXISTS ix_payments_transaction_id ON ordering.payments (transaction_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payments",
                schema: "ordering");
        }
    }
}
