using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOptionGroupFreeChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductOptionGroups_Choices",
                table: "ProductOptionGroups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OptionGroups_ExtraChoicePriceCents",
                table: "OptionGroups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OptionGroups_FreeChoices",
                table: "OptionGroups");

            migrationBuilder.DropColumn(
                name: "FreeChoices",
                table: "ProductOptionGroups");

            migrationBuilder.DropColumn(
                name: "ExtraChoicePriceCents",
                table: "OptionGroups");

            migrationBuilder.DropColumn(
                name: "FreeChoices",
                table: "OptionGroups");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductOptionGroups_Choices",
                table: "ProductOptionGroups",
                sql: "(\"MinChoices\" IS NULL AND \"MaxChoices\" IS NULL) OR (\"MinChoices\" >= 0 AND \"MaxChoices\" >= 1 AND \"MinChoices\" <= \"MaxChoices\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductOptionGroups_Choices",
                table: "ProductOptionGroups");

            migrationBuilder.AddColumn<int>(
                name: "FreeChoices",
                table: "ProductOptionGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExtraChoicePriceCents",
                table: "OptionGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FreeChoices",
                table: "OptionGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductOptionGroups_Choices",
                table: "ProductOptionGroups",
                sql: "(\"MinChoices\" IS NULL AND \"MaxChoices\" IS NULL AND \"FreeChoices\" IS NULL) OR (\"MinChoices\" >= 0 AND \"MaxChoices\" >= 1 AND \"MinChoices\" <= \"MaxChoices\" AND \"FreeChoices\" >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OptionGroups_ExtraChoicePriceCents",
                table: "OptionGroups",
                sql: "\"ExtraChoicePriceCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OptionGroups_FreeChoices",
                table: "OptionGroups",
                sql: "\"FreeChoices\" >= 0");
        }
    }
}
