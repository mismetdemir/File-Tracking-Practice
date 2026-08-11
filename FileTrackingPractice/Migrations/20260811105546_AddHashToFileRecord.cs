using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileTrackingPractice.Migrations
{
    /// <inheritdoc />
    public partial class AddHashToFileRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Hash",
                table: "FileRecords",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hash",
                table: "FileRecords");
        }
    }
}
