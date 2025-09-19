using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BallastLog.Mate.Migrations
{
    /// <inheritdoc />
    public partial class AddTankTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TankTypeId",
                table: "Tanks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TankTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ColorHex = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TankTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tanks_TankTypeId",
                table: "Tanks",
                column: "TankTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tanks_TankTypes_TankTypeId",
                table: "Tanks",
                column: "TankTypeId",
                principalTable: "TankTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tanks_TankTypes_TankTypeId",
                table: "Tanks");

            migrationBuilder.DropTable(
                name: "TankTypes");

            migrationBuilder.DropIndex(
                name: "IX_Tanks_TankTypeId",
                table: "Tanks");

            migrationBuilder.DropColumn(
                name: "TankTypeId",
                table: "Tanks");
        }
    }
}
