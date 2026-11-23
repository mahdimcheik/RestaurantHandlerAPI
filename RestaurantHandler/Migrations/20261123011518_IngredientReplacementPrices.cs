using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <inheritdoc />
    public partial class IngredientReplacementPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientSubstitutions");

            migrationBuilder.DropTable(
                name: "ProductSupplements");

            migrationBuilder.AddColumn<bool>(
                name: "IsOfferedAsReplacement",
                table: "Ingredients",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceCents",
                table: "Ingredients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ingredients_PriceCents",
                table: "Ingredients",
                sql: "\"PriceCents\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Ingredients_PriceCents",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "IsOfferedAsReplacement",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "PriceCents",
                table: "Ingredients");

            migrationBuilder.CreateTable(
                name: "IngredientSubstitutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubstituteIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtraPriceCents = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientSubstitutions", x => x.Id);
                    table.CheckConstraint("CK_IngredientSubstitutions_DifferentIngredient", "\"SubstituteIngredientId\" <> \"IngredientId\"");
                    table.CheckConstraint("CK_IngredientSubstitutions_ExtraPriceCents", "\"ExtraPriceCents\" >= 0");
                    table.ForeignKey(
                        name: "FK_IngredientSubstitutions_Ingredients_SubstituteIngredientId",
                        column: x => x.SubstituteIngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientSubstitutions_ProductIngredients_ProductId_Ingred~",
                        columns: x => new { x.ProductId, x.IngredientId },
                        principalTable: "ProductIngredients",
                        principalColumns: new[] { "ProductId", "IngredientId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductSupplements",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxQuantity = table.Column<int>(type: "integer", nullable: false),
                    PriceCents = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSupplements", x => new { x.ProductId, x.IngredientId });
                    table.CheckConstraint("CK_ProductSupplements_MaxQuantity", "\"MaxQuantity\" >= 1");
                    table.CheckConstraint("CK_ProductSupplements_PriceCents", "\"PriceCents\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProductSupplements_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductSupplements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSubstitutions_ProductId_IngredientId_SubstituteIn~",
                table: "IngredientSubstitutions",
                columns: new[] { "ProductId", "IngredientId", "SubstituteIngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSubstitutions_SubstituteIngredientId",
                table: "IngredientSubstitutions",
                column: "SubstituteIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSupplements_IngredientId",
                table: "ProductSupplements",
                column: "IngredientId");
        }
    }
}
