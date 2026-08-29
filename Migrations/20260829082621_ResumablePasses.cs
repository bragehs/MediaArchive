using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaArchive.Migrations
{
    /// <inheritdoc />
    public partial class ResumablePasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResumesEntryId",
                table: "ConsumptionEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartingEffort",
                table: "ConsumptionEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionEntries_ResumesEntryId",
                table: "ConsumptionEntries",
                column: "ResumesEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsumptionEntries_ConsumptionEntries_ResumesEntryId",
                table: "ConsumptionEntries",
                column: "ResumesEntryId",
                principalTable: "ConsumptionEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConsumptionEntries_ConsumptionEntries_ResumesEntryId",
                table: "ConsumptionEntries");

            migrationBuilder.DropIndex(
                name: "IX_ConsumptionEntries_ResumesEntryId",
                table: "ConsumptionEntries");

            migrationBuilder.DropColumn(
                name: "ResumesEntryId",
                table: "ConsumptionEntries");

            migrationBuilder.DropColumn(
                name: "StartingEffort",
                table: "ConsumptionEntries");
        }
    }
}
