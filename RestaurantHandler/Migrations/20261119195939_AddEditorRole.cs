using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "ArchivedAt", "Color", "ConcurrencyStamp", "CreatedAt", "Name", "NormalizedName", "UpdatedAt" },
                values: new object[] { new Guid("c3f1e2a4-5b6d-4e7f-8a9b-0c1d2e3f4a5b"), null, "#f59e0b", "EDITOR-STAMP-2026", new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Utc), "Editor", "EDITOR", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("c3f1e2a4-5b6d-4e7f-8a9b-0c1d2e3f4a5b"));
        }
    }
}
