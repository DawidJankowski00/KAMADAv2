using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamada.Modules.Delivery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "delivery");

            migrationBuilder.CreateTable(
                name: "route_locations",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    building_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    country = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "delivery_runs",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    planned_date = table.Column<DateOnly>(type: "date", nullable: false),
                    courier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_plate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    end_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    optimized_duration_seconds = table.Column<double>(type: "double precision", nullable: true),
                    optimized_distance_meters = table.Column<double>(type: "double precision", nullable: true),
                    optimized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    optimization_algorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    route_is_optimal = table.Column<bool>(type: "boolean", nullable: false),
                    route_is_approximate = table.Column<bool>(type: "boolean", nullable: false),
                    matrix_provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    google_maps_urls = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_runs_route_locations_end_location_id",
                        column: x => x.end_location_id,
                        principalSchema: "delivery",
                        principalTable: "route_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_runs_route_locations_start_location_id",
                        column: x => x.start_location_id,
                        principalSchema: "delivery",
                        principalTable: "route_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_run_orders",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_sequence = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    estimated_arrival_offset_seconds = table.Column<double>(type: "double precision", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_run_orders", x => x.id);
                    table.CheckConstraint("ck_delivery_run_orders_sequence_positive", "delivery_sequence > 0");
                    table.ForeignKey(
                        name: "fk_delivery_run_orders_delivery_runs_delivery_run_id",
                        column: x => x.delivery_run_id,
                        principalSchema: "delivery",
                        principalTable: "delivery_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_run_orders_delivery_run_id",
                schema: "delivery",
                table: "delivery_run_orders",
                column: "delivery_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_run_orders_order_id",
                schema: "delivery",
                table: "delivery_run_orders",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_runs_courier_id_planned_date",
                schema: "delivery",
                table: "delivery_runs",
                columns: new[] { "courier_id", "planned_date" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_runs_end_location_id",
                schema: "delivery",
                table: "delivery_runs",
                column: "end_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_runs_start_location_id",
                schema: "delivery",
                table: "delivery_runs",
                column: "start_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_runs_status_planned_date",
                schema: "delivery",
                table: "delivery_runs",
                columns: new[] { "status", "planned_date" });

            migrationBuilder.CreateIndex(
                name: "ix_route_locations_name",
                schema: "delivery",
                table: "route_locations",
                column: "name",
                unique: true);

            // Not expressible in the EF model: checked at commit, so stops can swap positions in one save.
            migrationBuilder.Sql(
                "ALTER TABLE delivery.delivery_run_orders ADD CONSTRAINT ux_delivery_run_orders_run_sequence " +
                "UNIQUE (delivery_run_id, delivery_sequence) DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_run_orders",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "delivery_runs",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "route_locations",
                schema: "delivery");
        }
    }
}
