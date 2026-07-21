using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaArchive.Migrations
{
    /// <inheritdoc />
    public partial class AddGenreHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentGenreId",
                table: "Genres",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Genres_ParentGenreId",
                table: "Genres",
                column: "ParentGenreId");

            migrationBuilder.AddForeignKey(
                name: "FK_Genres_Genres_ParentGenreId",
                table: "Genres",
                column: "ParentGenreId",
                principalTable: "Genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Genres_Genres_ParentGenreId",
                table: "Genres");

            migrationBuilder.DropIndex(
                name: "IX_Genres_ParentGenreId",
                table: "Genres");

            migrationBuilder.DropColumn(
                name: "ParentGenreId",
                table: "Genres");
        }
    }
}
