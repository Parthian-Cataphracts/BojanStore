using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LiveChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_chat_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    FromSupport = table.Column<bool>(type: "boolean", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Read = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_chat_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_chat_messages_VisitorId_FromSupport_Read",
                table: "live_chat_messages",
                columns: new[] { "VisitorId", "FromSupport", "Read" });

            migrationBuilder.CreateIndex(
                name: "IX_live_chat_messages_VisitorId_SentAtUtc",
                table: "live_chat_messages",
                columns: new[] { "VisitorId", "SentAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_chat_messages");
        }
    }
}
