using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <inheritdoc />
    public partial class CardsSharedWithStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SharedAt",
                table: "BoardCards",
                type: "timestamp with time zone",
                nullable: true);

            // Les cartes antérieures restaient visibles de l'élève : elles le restent.
            migrationBuilder.Sql("UPDATE \"BoardCards\" SET \"SharedAt\" = \"CreatedAt\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SharedAt",
                table: "BoardCards");
        }
    }
}
