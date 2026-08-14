using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OtpChallengeUniquePhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_otp_challenges_Phone",
                table: "otp_challenges");

            /*
                The duplicates this index exists to prevent are already in the
                table on any shop that has run for a while — that is the whole
                fault: two sign-in requests for one number racing each other
                left two rows, and nothing ever cleaned them up. Creating the
                index over them would fail, so the older ones go first.

                Newest kept, which is what the domain says happens and what
                FindActiveAsync was picking anyway: a new request supersedes the
                pending one.
            */
            migrationBuilder.Sql("""
                DELETE FROM otp_challenges a
                USING otp_challenges b
                WHERE a."Phone" = b."Phone"
                  AND (a."ExpiresAtUtc" < b."ExpiresAtUtc"
                       OR (a."ExpiresAtUtc" = b."ExpiresAtUtc" AND a."Id" < b."Id"));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_otp_challenges_Phone",
                table: "otp_challenges",
                column: "Phone",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_otp_challenges_Phone",
                table: "otp_challenges");

            migrationBuilder.CreateIndex(
                name: "IX_otp_challenges_Phone",
                table: "otp_challenges",
                column: "Phone");
        }
    }
}
