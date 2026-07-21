using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaArchive.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryNotesDropAnime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Show_EpisodeCount",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "Show_Studio",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "ConsumptionEntries");

            migrationBuilder.CreateTable(
                name: "EntryNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConsumptionEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    EffortAtTime = table.Column<int>(type: "INTEGER", nullable: true),
                    Text = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryNotes_ConsumptionEntries_ConsumptionEntryId",
                        column: x => x.ConsumptionEntryId,
                        principalTable: "ConsumptionEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryNotes_ConsumptionEntryId",
                table: "EntryNotes",
                column: "ConsumptionEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntryNotes");

            migrationBuilder.AddColumn<int>(
                name: "Show_EpisodeCount",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Show_Studio",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "ConsumptionEntries",
                type: "TEXT",
                nullable: true);
        }
    }
}
