using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaArchive.Migrations
{
    /// <inheritdoc />
    public partial class AddPassOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Outcome",
                table: "ConsumptionEntries",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "ConsumptionEntries");
        }
    }
}
