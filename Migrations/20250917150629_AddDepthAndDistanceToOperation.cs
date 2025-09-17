using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BallastLog.Mate.Migrations
{
    /// <inheritdoc />
    public partial class AddDepthAndDistanceToOperation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RecordedToFm123",
                table: "Operations",
                newName: "RecordedToFm232");

            migrationBuilder.AddColumn<int>(
                name: "DistanceNearestLand",
                table: "Operations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinDepth",
                table: "Operations",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DistanceNearestLand",
                table: "Operations");

            migrationBuilder.DropColumn(
                name: "MinDepth",
                table: "Operations");

            migrationBuilder.RenameColumn(
                name: "RecordedToFm232",
                table: "Operations",
                newName: "RecordedToFm123");
        }
    }
}
