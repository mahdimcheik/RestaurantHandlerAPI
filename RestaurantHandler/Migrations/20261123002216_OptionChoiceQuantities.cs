using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <inheritdoc />
    public partial class OptionChoiceQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreeChoices",
                table: "ProductOptionGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxChoices",
                table: "ProductOptionGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinChoices",
                table: "ProductOptionGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxPerChoice",
                table: "OptionGroups",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductOptionGroups_Choices",
                table: "ProductOptionGroups",
                sql: "(\"MinChoices\" IS NULL AND \"MaxChoices\" IS NULL AND \"FreeChoices\" IS NULL) OR (\"MinChoices\" >= 0 AND \"MaxChoices\" >= 1 AND \"MinChoices\" <= \"MaxChoices\" AND \"FreeChoices\" >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OptionGroups_MaxPerChoice",
                table: "OptionGroups",
                sql: "\"MaxPerChoice\" >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductOptionGroups_Choices",
                table: "ProductOptionGroups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OptionGroups_MaxPerChoice",
                table: "OptionGroups");

            migrationBuilder.DropColumn(
                name: "FreeChoices",
                table: "ProductOptionGroups");

            migrationBuilder.DropColumn(
                name: "MaxChoices",
                table: "ProductOptionGroups");

            migrationBuilder.DropColumn(
                name: "MinChoices",
                table: "ProductOptionGroups");

            migrationBuilder.DropColumn(
                name: "MaxPerChoice",
                table: "OptionGroups");
        }
    }
}
