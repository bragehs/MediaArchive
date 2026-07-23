using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaArchive.Migrations
{
    /// <inheritdoc />
    public partial class CreditsReleaseDateEpisodeRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Author",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "Developer",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "Director",
                table: "MediaItems");

            // Scaffolded as renames (Studio -> ReleaseDate, ReleaseYear -> EpisodeRuntime).
            // Those columns hold unrelated data, so they're dropped and re-added instead.
            migrationBuilder.DropColumn(
                name: "Studio",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "ReleaseYear",
                table: "MediaItems");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReleaseDate",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EpisodeRuntime",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaItemCredit",
                columns: table => new
                {
                    MediaItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItemCredit", x => new { x.MediaItemId, x.PersonId, x.Role });
                    table.ForeignKey(
                        name: "FK_MediaItemCredit_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaItemCredit_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItemCredit_PersonId",
                table: "MediaItemCredit",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_People_Name",
                table: "People",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaItemCredit");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "EpisodeRuntime",
                table: "MediaItems");

            migrationBuilder.AddColumn<int>(
                name: "ReleaseYear",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Studio",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Author",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Developer",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Director",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);
        }
    }
}
