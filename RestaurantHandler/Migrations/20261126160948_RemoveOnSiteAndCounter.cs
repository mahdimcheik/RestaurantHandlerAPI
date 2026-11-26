using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOnSiteAndCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR-0013 : plus de comptoir ni de paiement au retrait. Les commandes concernées
            // (essais d'avant l'ouverture) sont supprimées avec leurs lignes, paiements et
            // historique (cascade) ; les statuts retirés de l'enum ne doivent plus exister.
            migrationBuilder.Sql("""
                DELETE FROM "Orders"
                WHERE "Channel" = 'Counter' OR "PaymentChoice" = 'OnSite' OR "CustomerId" IS NULL
                   OR "Id" IN (SELECT "OrderId" FROM "Payments" WHERE "Method" <> 'SumupOnline');
                UPDATE "Orders" SET "Status" = 'Cancelled' WHERE "Status" IN ('AwaitingApproval', 'Refused');
                UPDATE "OrderStatusHistory" SET "Status" = 'Cancelled' WHERE "Status" IN ('AwaitingApproval', 'Refused');
                """);

            migrationBuilder.DropTable(
                name: "NoShowResets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RestaurantSettings_Delays",
                table: "RestaurantSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RestaurantSettings_Slots",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "NoShowCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApprovalTimeoutMinutes",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "NoShowThreshold",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "DeclaredByUserId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerFirstName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentChoice",
                table: "Orders");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "Orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RestaurantSettings_Delays",
                table: "RestaurantSettings",
                sql: "\"AsapDelayMinutes\" >= 0 AND \"MinLeadMinutes\" >= 0 AND \"PickupGraceMinutes\" >= 0 AND \"OnlinePaymentExpiryMinutes\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RestaurantSettings_Slots",
                table: "RestaurantSettings",
                sql: "\"SlotDurationMinutes\" >= 5 AND \"SlotCapacityOrders\" >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RestaurantSettings_Delays",
                table: "RestaurantSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RestaurantSettings_Slots",
                table: "RestaurantSettings");

            migrationBuilder.AddColumn<int>(
                name: "NoShowCount",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalTimeoutMinutes",
                table: "RestaurantSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NoShowThreshold",
                table: "RestaurantSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DeclaredByUserId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "Orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "Orders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerFirstName",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentChoice",
                table: "Orders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "NoShowResets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResetByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousCount = table.Column<int>(type: "integer", nullable: false),
                    ResetAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoShowResets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoShowResets_Users_ResetByUserId",
                        column: x => x.ResetByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoShowResets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "RestaurantSettings",
                keyColumn: "Id",
                keyValue: new Guid("5e771465-0000-4000-8000-000000000001"),
                columns: new[] { "ApprovalTimeoutMinutes", "NoShowThreshold" },
                values: new object[] { 10, 2 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_RestaurantSettings_Delays",
                table: "RestaurantSettings",
                sql: "\"AsapDelayMinutes\" >= 0 AND \"MinLeadMinutes\" >= 0 AND \"ApprovalTimeoutMinutes\" >= 1 AND \"PickupGraceMinutes\" >= 0 AND \"OnlinePaymentExpiryMinutes\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RestaurantSettings_Slots",
                table: "RestaurantSettings",
                sql: "\"SlotDurationMinutes\" >= 5 AND \"SlotCapacityOrders\" >= 1 AND \"NoShowThreshold\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_NoShowResets_ResetByUserId",
                table: "NoShowResets",
                column: "ResetByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoShowResets_UserId_ResetAt",
                table: "NoShowResets",
                columns: new[] { "UserId", "ResetAt" });
        }
    }
}
