using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaArchive.Migrations
{
    /// <inheritdoc />
    public partial class AddLogCaptureFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PersonalTags",
                table: "UserMediaItems",
                newName: "Discovery");

            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "ConsumptionEntries",
                newName: "Note");

            migrationBuilder.AddColumn<int>(
                name: "EpisodeCount",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PageCount",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RuntimeMinutes",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Show_EpisodeCount",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeToBeatHours",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Context",
                table: "ConsumptionEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaItemTag",
                columns: table => new
                {
                    MediaItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItemTag", x => new { x.MediaItemId, x.TagId });
                    table.ForeignKey(
                        name: "FK_MediaItemTag_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaItemTag_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItemTag_TagId",
                table: "MediaItemTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaItemTag");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropColumn(
                name: "EpisodeCount",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "PageCount",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "RuntimeMinutes",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "Show_EpisodeCount",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "TimeToBeatHours",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "Context",
                table: "ConsumptionEntries");

            migrationBuilder.RenameColumn(
                name: "Discovery",
                table: "UserMediaItems",
                newName: "PersonalTags");

            migrationBuilder.RenameColumn(
                name: "Note",
                table: "ConsumptionEntries",
                newName: "Notes");
        }
    }
}
