using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddressCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "addresses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Existing rows would all carry year one, which is a tie rather
            // than an order — and this column exists to break ties. The
            // customer's current default is dated earliest so it stays first in
            // line, and the rest fall in behind it; the exact spacing does not
            // matter, only that no two of one customer's rows share a value.
            migrationBuilder.Sql(
                """
                UPDATE addresses AS a
                SET "CreatedAtUtc" = now() - make_interval(secs => ranked.age)
                FROM (
                    SELECT
                        "Id",
                        -- Ordering with the default last gives it the largest
                        -- rank, and the largest rank is subtracted the furthest
                        -- back — so it reads as the oldest and keeps the flag.
                        ROW_NUMBER() OVER (
                            PARTITION BY "CustomerId"
                            ORDER BY "IsDefault", "Id"
                        ) AS age
                    FROM addresses
                ) AS ranked
                WHERE a."Id" = ranked."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "addresses");
        }
    }
}
