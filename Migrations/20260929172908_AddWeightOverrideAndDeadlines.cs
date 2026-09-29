using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace LaundryHub2._0.Migrations
{
    /// <inheritdoc />
    public partial class AddWeightOverrideAndDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DetergentMlPerKg",
                table: "LaundryServices",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresDrying",
                table: "LaundryServices",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresWashing",
                table: "LaundryServices",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SoftenerMlPerKg",
                table: "LaundryServices",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "EstimatedWeightMethod",
                table: "LaundryOrders",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                table: "LaundryOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "LaundryOrders",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAt",
                table: "LaundryOrders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RowVersion",
                table: "LaundryOrders",
                type: "datetime(6)",
                rowVersion: true,
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.ComputedColumn);

            migrationBuilder.AddColumn<DateTime>(
                name: "WeightConfirmationDeadline",
                table: "LaundryOrders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WeightConfirmationExtensionDeadline",
                table: "LaundryOrders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WeightConfirmedByCustomerAt",
                table: "LaundryOrders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WeightOverrideApprovedAt",
                table: "LaundryOrders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WeightOverrideAt",
                table: "LaundryOrders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeightOverrideByStaffId",
                table: "LaundryOrders",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeightOverrideReason",
                table: "LaundryOrders",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeightOverrideSupervisorId",
                table: "LaundryOrders",
                type: "longtext",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    AdminName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    TargetUser = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    TargetRole = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    ReporterName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ReporterIsStaff = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ResolutionNote = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Claims_LaundryOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "LaundryOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CustomerNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<string>(type: "varchar(85)", maxLength: 85, nullable: false),
                    AuthorUserId = table.Column<string>(type: "varchar(85)", maxLength: 85, nullable: false),
                    Text = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerNotes_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerNotes_AspNetUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LaundryOrderSequences",
                columns: table => new
                {
                    OrderDate = table.Column<DateTime>(type: "date", nullable: false),
                    NextSeq = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaundryOrderSequences", x => x.OrderDate);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<string>(type: "varchar(85)", maxLength: 85, nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_AspNetUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_LaundryOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "LaundryOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    RecipientUserId = table.Column<string>(type: "varchar(85)", maxLength: 85, nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Body = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    IsRead = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notifications_LaundryOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "LaundryOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LaundryOrders_OrderNumber",
                table: "LaundryOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_OrderId",
                table: "Claims",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotes_AuthorUserId",
                table: "CustomerNotes",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotes_CustomerId",
                table: "CustomerNotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_CustomerId",
                table: "LoyaltyTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_OrderId_Type",
                table: "LoyaltyTransactions",
                columns: new[] { "OrderId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OrderId",
                table: "Notifications",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId",
                table: "Notifications",
                column: "RecipientUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "CustomerNotes");

            migrationBuilder.DropTable(
                name: "LaundryOrderSequences");

            migrationBuilder.DropTable(
                name: "LoyaltyTransactions");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_LaundryOrders_OrderNumber",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "DetergentMlPerKg",
                table: "LaundryServices");

            migrationBuilder.DropColumn(
                name: "RequiresDrying",
                table: "LaundryServices");

            migrationBuilder.DropColumn(
                name: "RequiresWashing",
                table: "LaundryServices");

            migrationBuilder.DropColumn(
                name: "SoftenerMlPerKg",
                table: "LaundryServices");

            migrationBuilder.DropColumn(
                name: "EstimatedWeightMethod",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "WeightConfirmationDeadline",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "WeightConfirmationExtensionDeadline",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "WeightConfirmedByCustomerAt",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "WeightOverrideApprovedAt",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "WeightOverrideAt",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "WeightOverrideByStaffId",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "WeightOverrideReason",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "WeightOverrideSupervisorId",
                table: "LaundryOrders");
        }
    }
}
