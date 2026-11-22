using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <summary>
    /// Lot 5 — stockage unifié. Les avatars de l'ancien stockage (clés <c>avatars/…</c> sur R2) ne
    /// sont pas repris : toute valeur qui n'est pas une clé publique du nouveau stockage est
    /// effacée, le membre retrouve l'avatar par défaut. Les factures héritées ne sont pas touchées :
    /// elles sont régénérées à la demande suivante. Aucun changement de modèle.
    /// </summary>
    public partial class Lot5StockageUnifie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET "ImgUrl" = NULL
                WHERE "ImgUrl" IS NOT NULL
                  AND "ImgUrl" NOT LIKE 'public/%';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irréversible : les clés effacées désignaient des fichiers qui ne sont plus servis.
        }
    }
}
