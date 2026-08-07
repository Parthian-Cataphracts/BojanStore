using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminSecurityStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "admin_users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // One stamp each, as on customers. Every panel session open when
            // this runs stops working, which is the point: those cookies were
            // minted before there was anything to revoke them with.
            migrationBuilder.Sql(
                """UPDATE admin_users SET "SecurityStamp" = gen_random_uuid();""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "admin_users");
        }
    }
}
