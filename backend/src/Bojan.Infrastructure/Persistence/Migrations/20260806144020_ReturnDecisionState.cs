using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReturnDecisionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActorId",
                table: "return_timeline_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromStatus",
                table: "return_timeline_events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "return_timeline_events",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DecidedById",
                table: "return_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RefundAmount",
                table: "return_requests",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RefundedAtUtc",
                table: "return_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Restocked",
                table: "return_requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "return_requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "return_timeline_events");

            migrationBuilder.DropColumn(
                name: "FromStatus",
                table: "return_timeline_events");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "return_timeline_events");

            migrationBuilder.DropColumn(
                name: "DecidedById",
                table: "return_requests");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "return_requests");

            migrationBuilder.DropColumn(
                name: "RefundedAtUtc",
                table: "return_requests");

            migrationBuilder.DropColumn(
                name: "Restocked",
                table: "return_requests");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "return_requests");
        }
    }
}
