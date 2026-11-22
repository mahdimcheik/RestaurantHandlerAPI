using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <inheritdoc />
    public partial class ProductDescriptionRichText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Texte brut existant : échappé, une ligne par paragraphe, lignes vides retirées.
            migrationBuilder.Sql(
                """
                UPDATE "Products"
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
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
