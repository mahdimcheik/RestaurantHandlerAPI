using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <inheritdoc />
    public partial class FormationDescriptionRichText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Formations",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            // Texte brut existant : échappé, une ligne par paragraphe, lignes vides retirées.
            migrationBuilder.Sql(
                """
                UPDATE "Formations"
                SET "Description" = replace(
                    '<p>' || replace(
                        replace(replace(replace(replace("Description", E'\r', ''), '&', '&amp;'), '<', '&lt;'), '>', '&gt;'),
                        E'\n', '</p><p>'
                    ) || '</p>',
                    '<p></p>', ''
                )
                WHERE "Description" NOT LIKE '<p>%';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Formations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10000)",
                oldMaxLength: 10000);
        }
    }
}
