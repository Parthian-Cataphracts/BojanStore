using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameBackupJobArchiveReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FileUrl",
                table: "backup_jobs",
                newName: "ArchiveReference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ArchiveReference",
                table: "backup_jobs",
                newName: "FileUrl");
        }
    }
}
