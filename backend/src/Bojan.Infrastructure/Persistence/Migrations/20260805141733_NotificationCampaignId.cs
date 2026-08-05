using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationCampaignId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "customer_notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_notifications_CustomerId_CampaignId",
                table: "customer_notifications",
                columns: new[] { "CustomerId", "CampaignId" },
                unique: true,
                filter: "\"CampaignId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_customer_notifications_CustomerId_CampaignId",
                table: "customer_notifications");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "customer_notifications");
        }
    }
}
