using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaArchive.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ExternalRating",
                table: "MediaItems",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalRatingCount",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalRating",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "ExternalRatingCount",
                table: "MediaItems");
        }
    }
}
