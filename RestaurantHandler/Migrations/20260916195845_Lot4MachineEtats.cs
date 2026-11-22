using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <summary>
    /// Lot 4 — machine à états. Unicité du litige par réservation, et reprise des réservations
    /// que le versement, désormais filtré sur <c>PAYABLE</c>, ne verrait plus.
    /// Les identifiants de statut sont recopiés en dur : une migration est figée, elle ne doit
    /// pas suivre une évolution ultérieure de <c>HardCode</c>.
    /// </summary>
    public partial class Lot4MachineEtats : Migration
    {
        // Reservation statuses
        private const string Accepted = "32d854b6-6d4e-445a-9209-31a492970f2d";
        private const string Delivered = "b9a5c1e2-3f47-4d8a-9c1b-7e2f4a6d8c30";
        private const string Payable = "c4d6e8f0-1a2b-4c3d-8e5f-6a7b8c9d0e11";
        private const string Done = "6d281aec-d093-4071-8bf4-c8363361b5b4";

        // Wallet line type PAYMENT, transaction status PENDING
        private const string WalletPayment = "7305a3d5-bfa9-41ce-be10-174c406cb842";
        private const string TransactionPending = "ead0ecc1-a58d-4436-a87f-89c2dbc665a8";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Un seul litige vivant par réservation : les éventuels doublons (tous sauf le plus
            // ancien) sont archivés avant de poser l'index.
            migrationBuilder.Sql(
                """
                UPDATE "Disputes" d
                SET "ArchivedAt" = now(), "UpdatedAt" = now()
                WHERE d."ArchivedAt" IS NULL
                  AND EXISTS (
                    SELECT 1 FROM "Disputes" o
                    WHERE o."ReservationId" = d."ReservationId"
                      AND o."ArchivedAt" IS NULL
                      AND (o."CreatedAt", o."Id") < (d."CreatedAt", d."Id")
                  );
                """
            );

            migrationBuilder.DropIndex(name: "IX_Disputes_ReservationId", table: "Disputes");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ReservationId",
                table: "Disputes",
                column: "ReservationId",
                unique: true,
                filter: "\"ArchivedAt\" IS NULL"
            );

            // Réservations à rendre versables :
            //  1. DONE avec un crédit PAYMENT non versé — litiges tranchés pour le professeur
            //     avant le lot 4, que l'ancien versement payait malgré le statut ;
            //  2. ACCEPTED / DELIVERED dont un versement attend l'acquittement Stripe — sans
            //     quoi le rejeu tenterait ACCEPTED → DONE, que la garde refuse en flux payant.
            // Chaque changement est journalisé dans l'historique (acteur système).
            migrationBuilder.Sql(
                $"""
                CREATE TEMP TABLE lot4_payable AS
                SELECT r."Id", r."StatusId" AS from_status
                FROM "Reservations" r
                WHERE r."ArchivedAt" IS NULL
                  AND (
                    (r."StatusId" = '{Done}' AND EXISTS (
                        SELECT 1 FROM "TeacherWalletTransactions" t
                        WHERE t."ReservationId" = r."Id"
                          AND t."TypeId" = '{WalletPayment}'
                          AND t."PayoutId" IS NULL
                          AND t."ArchivedAt" IS NULL))
                    OR
                    (r."StatusId" IN ('{Accepted}', '{Delivered}') AND EXISTS (
                        SELECT 1 FROM "TeacherPayouts" p
                        WHERE p."ReservationId" = r."Id"
                          AND p."StatusId" = '{TransactionPending}'
                          AND p."ArchivedAt" IS NULL))
                  );

                INSERT INTO "ReservationStatusHistories"
                    ("Id", "ReservationId", "FromStatusId", "ToStatusId", "ChangedById", "Reason", "CreatedAt")
                SELECT gen_random_uuid(), x."Id", x.from_status, '{Payable}', NULL, 'Reprise lot 4', now()
                FROM lot4_payable x;

                UPDATE "Reservations" r
                SET "StatusId" = '{Payable}', "UpdatedAt" = now()
                FROM lot4_payable x
                WHERE r."Id" = x."Id";

                DROP TABLE lot4_payable;
                """
            );

            // Le code de présence n'existe que pour un cours payé (ADR-0012).
            migrationBuilder.Sql(
                """
                UPDATE "Reservations" r
                SET "ValidationCode" = NULL
                FROM "Orders" o
                WHERE r."OrderId" = o."Id"
                  AND o."PaymentRequired" = FALSE
                  AND r."ValidationCode" IS NOT NULL;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La reprise des données n'est pas annulée : PAYABLE reste un état valide, et
            // rebasculer en DONE réintroduirait la transition interdite.
            migrationBuilder.DropIndex(name: "IX_Disputes_ReservationId", table: "Disputes");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ReservationId",
                table: "Disputes",
                column: "ReservationId"
            );
        }
    }
}
