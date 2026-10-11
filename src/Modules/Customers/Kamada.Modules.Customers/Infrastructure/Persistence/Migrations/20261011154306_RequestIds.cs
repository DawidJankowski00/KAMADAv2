using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamada.Modules.Customers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequestIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "request_id",
                schema: "customers",
                table: "clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "request_id",
                schema: "customers",
                table: "addresses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_clients_request_id",
                schema: "customers",
                table: "clients",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_addresses_request_id",
                schema: "customers",
                table: "addresses",
                column: "request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clients_request_id",
                schema: "customers",
                table: "clients");

            migrationBuilder.DropIndex(
                name: "ix_addresses_request_id",
                schema: "customers",
                table: "addresses");

            migrationBuilder.DropColumn(
                name: "request_id",
                schema: "customers",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "request_id",
                schema: "customers",
                table: "addresses");
        }
    }
}
