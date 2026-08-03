using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerSecurityStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "customers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // One stamp each, rather than the all-zero default every existing
            // row would otherwise share. The stamp is only ever compared against
            // the account it belongs to, so a shared value is not by itself a
            // way in — but a column whose rows are all identical invites being
            // read as meaningless, and the first rotation would be the only
            // thing that ever made it distinct.
            migrationBuilder.Sql(
                """UPDATE customers SET "SecurityStamp" = gen_random_uuid();""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "customers");
        }
    }
}
