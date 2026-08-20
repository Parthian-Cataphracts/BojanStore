using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_otp_challenges_Phone",
                table: "otp_challenges");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "otp_challenges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "otp_challenges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmailVerified",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPhoneVerified",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "email_verification_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_verification_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_verification_tokens_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_otp_challenges_CustomerId_Purpose",
                table: "otp_challenges",
                columns: new[] { "CustomerId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_otp_challenges_Phone_Purpose",
                table: "otp_challenges",
                columns: new[] { "Phone", "Purpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_CustomerId",
                table: "email_verification_tokens",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_TokenHash",
                table: "email_verification_tokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_verification_tokens");

            migrationBuilder.DropIndex(
                name: "IX_otp_challenges_CustomerId_Purpose",
                table: "otp_challenges");

            migrationBuilder.DropIndex(
                name: "IX_otp_challenges_Phone_Purpose",
                table: "otp_challenges");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "otp_challenges");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "otp_challenges");

            migrationBuilder.DropColumn(
                name: "IsEmailVerified",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "IsPhoneVerified",
                table: "customers");

            migrationBuilder.CreateIndex(
                name: "IX_otp_challenges_Phone",
                table: "otp_challenges",
                column: "Phone",
                unique: true);
        }
    }
}
