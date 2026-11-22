using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <inheritdoc />
    public partial class RenameProductToCursus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "Products", newName: "Cursuses");
            migrationBuilder.RenameTable(name: "CategoryCursusProduct", newName: "CategoryCursusCursus");
            migrationBuilder.RenameColumn(name: "ProductId", table: "Reservations", newName: "CursusId");
            migrationBuilder.RenameColumn(name: "ProductId", table: "BoardCards", newName: "CursusId");
            migrationBuilder.RenameColumn(name: "ProductsId", table: "CategoryCursusCursus", newName: "CursusesId");
            migrationBuilder.RenameIndex(name: "IX_Reservations_ProductId", table: "Reservations", newName: "IX_Reservations_CursusId");
            migrationBuilder.RenameIndex(name: "IX_BoardCards_ProductId", table: "BoardCards", newName: "IX_BoardCards_CursusId");
            migrationBuilder.RenameIndex(name: "IX_Products_LevelId", table: "Cursuses", newName: "IX_Cursuses_LevelId");
            migrationBuilder.RenameIndex(name: "IX_Products_TeacherId", table: "Cursuses", newName: "IX_Cursuses_TeacherId");
            migrationBuilder.RenameIndex(name: "IX_CategoryCursusProduct_ProductsId", table: "CategoryCursusCursus", newName: "IX_CategoryCursusCursus_CursusesId");
            migrationBuilder.Sql("ALTER TABLE \"Cursuses\" RENAME CONSTRAINT \"PK_Products\" TO \"PK_Cursuses\";");
            migrationBuilder.Sql("ALTER TABLE \"Cursuses\" RENAME CONSTRAINT \"FK_Products_LevelCursuses_LevelId\" TO \"FK_Cursuses_LevelCursuses_LevelId\";");
            migrationBuilder.Sql("ALTER TABLE \"Cursuses\" RENAME CONSTRAINT \"FK_Products_Teachers_TeacherId\" TO \"FK_Cursuses_Teachers_TeacherId\";");
            migrationBuilder.Sql("ALTER TABLE \"CategoryCursusCursus\" RENAME CONSTRAINT \"PK_CategoryCursusProduct\" TO \"PK_CategoryCursusCursus\";");
            migrationBuilder.Sql("ALTER TABLE \"CategoryCursusCursus\" RENAME CONSTRAINT \"FK_CategoryCursusProduct_CategoryCursuses_CategoriesId\" TO \"FK_CategoryCursusCursus_CategoryCursuses_CategoriesId\";");
            migrationBuilder.Sql("ALTER TABLE \"CategoryCursusCursus\" RENAME CONSTRAINT \"FK_CategoryCursusProduct_Products_ProductsId\" TO \"FK_CategoryCursusCursus_Cursuses_CursusesId\";");
            migrationBuilder.Sql("ALTER TABLE \"Reservations\" RENAME CONSTRAINT \"FK_Reservations_Products_ProductId\" TO \"FK_Reservations_Cursuses_CursusId\";");
            migrationBuilder.Sql("ALTER TABLE \"BoardCards\" RENAME CONSTRAINT \"FK_BoardCards_Products_ProductId\" TO \"FK_BoardCards_Cursuses_CursusId\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Cursuses\" RENAME CONSTRAINT \"PK_Cursuses\" TO \"PK_Products\";");
            migrationBuilder.Sql("ALTER TABLE \"Cursuses\" RENAME CONSTRAINT \"FK_Cursuses_LevelCursuses_LevelId\" TO \"FK_Products_LevelCursuses_LevelId\";");
            migrationBuilder.Sql("ALTER TABLE \"Cursuses\" RENAME CONSTRAINT \"FK_Cursuses_Teachers_TeacherId\" TO \"FK_Products_Teachers_TeacherId\";");
            migrationBuilder.Sql("ALTER TABLE \"CategoryCursusCursus\" RENAME CONSTRAINT \"PK_CategoryCursusCursus\" TO \"PK_CategoryCursusProduct\";");
            migrationBuilder.Sql("ALTER TABLE \"CategoryCursusCursus\" RENAME CONSTRAINT \"FK_CategoryCursusCursus_CategoryCursuses_CategoriesId\" TO \"FK_CategoryCursusProduct_CategoryCursuses_CategoriesId\";");
            migrationBuilder.Sql("ALTER TABLE \"CategoryCursusCursus\" RENAME CONSTRAINT \"FK_CategoryCursusCursus_Cursuses_CursusesId\" TO \"FK_CategoryCursusProduct_Products_ProductsId\";");
            migrationBuilder.Sql("ALTER TABLE \"Reservations\" RENAME CONSTRAINT \"FK_Reservations_Cursuses_CursusId\" TO \"FK_Reservations_Products_ProductId\";");
            migrationBuilder.Sql("ALTER TABLE \"BoardCards\" RENAME CONSTRAINT \"FK_BoardCards_Cursuses_CursusId\" TO \"FK_BoardCards_Products_ProductId\";");
            migrationBuilder.RenameIndex(name: "IX_Reservations_CursusId", table: "Reservations", newName: "IX_Reservations_ProductId");
            migrationBuilder.RenameIndex(name: "IX_BoardCards_CursusId", table: "BoardCards", newName: "IX_BoardCards_ProductId");
            migrationBuilder.RenameIndex(name: "IX_Cursuses_LevelId", table: "Cursuses", newName: "IX_Products_LevelId");
            migrationBuilder.RenameIndex(name: "IX_Cursuses_TeacherId", table: "Cursuses", newName: "IX_Products_TeacherId");
            migrationBuilder.RenameIndex(name: "IX_CategoryCursusCursus_CursusesId", table: "CategoryCursusCursus", newName: "IX_CategoryCursusProduct_ProductsId");
            migrationBuilder.RenameColumn(name: "CursusId", table: "Reservations", newName: "ProductId");
            migrationBuilder.RenameColumn(name: "CursusId", table: "BoardCards", newName: "ProductId");
            migrationBuilder.RenameColumn(name: "CursusesId", table: "CategoryCursusCursus", newName: "ProductsId");
            migrationBuilder.RenameTable(name: "Cursuses", newName: "Products");
            migrationBuilder.RenameTable(name: "CategoryCursusCursus", newName: "CategoryCursusProduct");
        }
    }
}
