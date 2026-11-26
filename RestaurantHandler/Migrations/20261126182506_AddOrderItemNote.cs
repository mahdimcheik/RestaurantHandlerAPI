using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "OrderItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                table: "OrderItems");

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
