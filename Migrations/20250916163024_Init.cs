using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BallastLog.Mate.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartLocal = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StopLocal = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TzOffset = table.Column<string>(type: "TEXT", maxLength: 6, nullable: false),
                    LocationStart = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LocationStop = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    BwtsUsed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Custom1 = table.Column<string>(type: "TEXT", nullable: true),
                    Custom2 = table.Column<string>(type: "TEXT", nullable: true),
                    Custom3 = table.Column<string>(type: "TEXT", nullable: true),
                    Custom4 = table.Column<string>(type: "TEXT", nullable: true),
                    Custom5 = table.Column<string>(type: "TEXT", nullable: true),
                    TotalAmount = table.Column<int>(type: "INTEGER", nullable: false),
                    FlowRate = table.Column<double>(type: "REAL", nullable: false),
                    RecordedToLogBook = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecordedToFm123 = table.Column<bool>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShipProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShipName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ShipClass = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MaxFlowRate = table.Column<int>(type: "INTEGER", nullable: false),
                    Custom1Label = table.Column<string>(type: "TEXT", nullable: true),
                    Custom2Label = table.Column<string>(type: "TEXT", nullable: true),
                    Custom3Label = table.Column<string>(type: "TEXT", nullable: true),
                    Custom4Label = table.Column<string>(type: "TEXT", nullable: true),
                    Custom5Label = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tanks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MaxCapacity = table.Column<int>(type: "INTEGER", nullable: false),
                    InitialCapacity = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentCapacity = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tanks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationLegs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TankId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSea = table.Column<bool>(type: "INTEGER", nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    Delta = table.Column<int>(type: "INTEGER", nullable: false),
                    VolumeBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    VolumeAfter = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationLegs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationLegs_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperationLegs_Tanks_TankId",
                        column: x => x.TankId,
                        principalTable: "Tanks",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "ShipProfiles",
                columns: new[] { "Id", "Custom1Label", "Custom2Label", "Custom3Label", "Custom4Label", "Custom5Label", "MaxFlowRate", "ShipClass", "ShipName" },
                values: new object[] { 1, null, null, null, null, null, 0, null, "" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLegs_OperationId",
                table: "OperationLegs",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLegs_TankId",
                table: "OperationLegs",
                column: "TankId");

            migrationBuilder.CreateIndex(
                name: "IX_Tanks_Code",
                table: "Tanks",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationLegs");

            migrationBuilder.DropTable(
                name: "ShipProfiles");

            migrationBuilder.DropTable(
                name: "Operations");

            migrationBuilder.DropTable(
                name: "Tanks");
        }
    }
}
