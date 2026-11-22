using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantHandler.Migrations
{
    /// <inheritdoc />
    public partial class AddMailBroadcasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MailBroadcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: false),
                    RecipientCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailBroadcasts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailBroadcasts_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MailBroadcastRecipients",
                columns: table => new
                {
                    BroadcastId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailBroadcastRecipients", x => new { x.BroadcastId, x.UserId });
                    table.ForeignKey(
                        name: "FK_MailBroadcastRecipients_MailBroadcasts_BroadcastId",
                        column: x => x.BroadcastId,
                        principalTable: "MailBroadcasts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MailBroadcastRecipients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MailBroadcastRecipients_UserId",
                table: "MailBroadcastRecipients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MailBroadcasts_CreatedAt",
                table: "MailBroadcasts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MailBroadcasts_SenderId_CreatedAt",
                table: "MailBroadcasts",
                columns: new[] { "SenderId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailBroadcastRecipients");

            migrationBuilder.DropTable(
                name: "MailBroadcasts");

        }
    }
}
