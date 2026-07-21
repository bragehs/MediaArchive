using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaArchive.Migrations
{
    /// <inheritdoc />
    public partial class DropUserMediaItemNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "UserMediaItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "UserMediaItems",
                type: "TEXT",
                nullable: true);
        }
    }
}
