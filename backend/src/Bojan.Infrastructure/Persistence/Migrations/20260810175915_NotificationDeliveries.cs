using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Makes an SMS or email campaign resumable.
    /// </summary>
    /// <remarks>
    /// The in-app channel could already carry on from where a failed fan-out
    /// stopped, because it writes a row per recipient and can read back what is
    /// already there. SMS and email leave nothing behind, so a campaign that
    /// died on the ten-thousandth recipient started again at the first and
    /// everyone already reached got it twice. That cost a duplicate log line
    /// while the only sender wrote to a log; with a provider behind it, it costs
    /// the shop money per duplicate.
    ///
    /// The unique index on (campaign, customer) is the part that makes it
    /// correct rather than merely likely: two dispatch cycles overlapping on one
    /// campaign would otherwise both read the same already-sent set.
    /// </remarks>
    public partial class NotificationDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_notification_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "notification_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_CampaignId_CustomerId",
                table: "notification_deliveries",
                columns: new[] { "CampaignId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_CustomerId",
                table: "notification_deliveries",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_deliveries");
        }
    }
}
