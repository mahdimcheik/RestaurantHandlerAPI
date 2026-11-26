using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessInvoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InvoiceRequested",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BusinessProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Siren = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    Siret = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VatNumber = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    AddressLine1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AddressLine2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_BusinessProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BuyerSiren = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    BuyerSiret = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    BuyerLegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BuyerVatNumber = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    BuyerAddressLine1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BuyerAddressLine2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BuyerPostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BuyerCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BuyerCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    SellerLegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SellerSiret = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    SellerVatNumber = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    SellerAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceRequests_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfiles_Siren",
                table: "BusinessProfiles",
                column: "Siren");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRequests_OrderId",
                table: "InvoiceRequests",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRequests_Status_CreatedAt",
                table: "InvoiceRequests",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessProfiles");

            migrationBuilder.DropTable(
                name: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "InvoiceRequested",
                table: "Orders");

            migrationBuilder.UpdateData(
                table: "StatusAccount",
                keyColumn: "Id",
                keyValue: new Guid("12e65875-bec4-422a-aad4-c28de0e06fff"),
                column: "Translations",
                value: new Dictionary<string, string> { ["fr"] = "Actif", ["en"] = "Active" });

            migrationBuilder.UpdateData(
                table: "StatusAccount",
                keyColumn: "Id",
                keyValue: new Guid("acc9ddb6-824b-42cb-8276-5d91b6af2003"),
                column: "Translations",
                value: new Dictionary<string, string> { ["fr"] = "En attente", ["en"] = "Pending" });

            migrationBuilder.UpdateData(
                table: "StatusAccount",
                keyColumn: "Id",
                keyValue: new Guid("fd2621ef-85f0-46a6-acdd-c77fb111630f"),
                column: "Translations",
                value: new Dictionary<string, string> { ["fr"] = "Banni", ["en"] = "Banned" });
        }
    }
}
