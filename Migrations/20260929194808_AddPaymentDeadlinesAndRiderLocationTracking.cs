using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaundryHub2._0.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDeadlinesAndRiderLocationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AwaitingPaymentAt",
                table: "LaundryOrders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GracePeriodEndAt",
                table: "LaundryOrders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDeadlineAt",
                table: "LaundryOrders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentLatitude",
                table: "AspNetUsers",
                type: "decimal(10,8)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentLongitude",
                table: "AspNetUsers",
                type: "decimal(11,8)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLocationUpdatedAt",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwaitingPaymentAt",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "GracePeriodEndAt",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "PaymentDeadlineAt",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "CurrentLatitude",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CurrentLongitude",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLocationUpdatedAt",
                table: "AspNetUsers");
        }
    }
}
