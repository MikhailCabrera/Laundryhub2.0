using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaundryHub2._0.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPickupCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PickupLatitude",
                table: "LaundryOrders",
                type: "decimal(10,8)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PickupLongitude",
                table: "LaundryOrders",
                type: "decimal(11,8)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PickupLatitude",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "PickupLongitude",
                table: "LaundryOrders");
        }
    }
}
