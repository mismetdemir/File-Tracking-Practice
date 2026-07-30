using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileTrackingPractice.Migrations
{
    /// <inheritdoc />
    public partial class AddPathToFileRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "FileRecords",
                type: "TEXT",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_FileRecords_Path",
                table: "FileRecords",
                column: "Path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileRecords_Path",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "FileRecords");
        }
    }
}
