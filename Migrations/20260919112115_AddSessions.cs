using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaArchive.Migrations
{
    /// <inheritdoc />
    public partial class AddSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConsumptionEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PausedMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    EntryNoteId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_ConsumptionEntries_ConsumptionEntryId",
                        column: x => x.ConsumptionEntryId,
                        principalTable: "ConsumptionEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sessions_EntryNotes_EntryNoteId",
                        column: x => x.EntryNoteId,
                        principalTable: "EntryNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ConsumptionEntryId",
                table: "Sessions",
                column: "ConsumptionEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_EntryNoteId",
                table: "Sessions",
                column: "EntryNoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sessions");
        }
    }
}
