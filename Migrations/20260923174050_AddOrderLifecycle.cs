using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace LaundryHub2._0.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LaundryOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OrderNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    CustomerId = table.Column<string>(type: "varchar(85)", maxLength: 85, nullable: false),
                    PreferredPickupDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PreferredPickupTime = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    PickupLocation = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    ContactNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    SpecialInstructions = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PickupRiderId = table.Column<string>(type: "varchar(85)", maxLength: 85, nullable: true),
                    RiderAssignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PickupPhotoPath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    PickedUpAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    WeightPhotoPath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    WeightConfirmedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    WashingStartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DryingStartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsPaymentConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PayMongoPaymentId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    PayMongoCheckoutUrl = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    PaymentConfirmedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReadyForDeliveryNotifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AccruedPenaltyAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    IsAbandoned = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AbandonedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeliveryRiderId = table.Column<string>(type: "varchar(85)", maxLength: 85, nullable: true),
                    DeliveryAssignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeliveryPhotoPath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TermsAcceptedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TermsVersion = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaundryOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaundryOrders_AspNetUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaundryOrders_AspNetUsers_DeliveryRiderId",
                        column: x => x.DeliveryRiderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaundryOrders_AspNetUsers_PickupRiderId",
                        column: x => x.PickupRiderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LaundryServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    PricePerKg = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaundryServices", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LaundryOrderServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    PricePerKgSnapshot = table.Column<decimal>(type: "decimal(8,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaundryOrderServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaundryOrderServices_LaundryOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "LaundryOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LaundryOrderServices_LaundryServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "LaundryServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LaundryOrders_CustomerId",
                table: "LaundryOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_LaundryOrders_DeliveryRiderId",
                table: "LaundryOrders",
                column: "DeliveryRiderId");

            migrationBuilder.CreateIndex(
                name: "IX_LaundryOrders_PickupRiderId",
                table: "LaundryOrders",
                column: "PickupRiderId");

            migrationBuilder.CreateIndex(
                name: "IX_LaundryOrderServices_OrderId",
                table: "LaundryOrderServices",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LaundryOrderServices_ServiceId",
                table: "LaundryOrderServices",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LaundryOrderServices");

            migrationBuilder.DropTable(
                name: "LaundryOrders");

            migrationBuilder.DropTable(
                name: "LaundryServices");
        }
    }
}
