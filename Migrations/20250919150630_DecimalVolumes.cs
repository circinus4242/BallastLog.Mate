using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BallastLog.Mate.Migrations
{
    /// <inheritdoc />
    public partial class DecimalVolumes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "MaxCapacity",
                table: "Tanks",
                type: "REAL",
                precision: 6,
                scale: 1,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "InitialCapacity",
                table: "Tanks",
                type: "REAL",
                precision: 6,
                scale: 1,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "CurrentCapacity",
                table: "Tanks",
                type: "REAL",
                precision: 6,
                scale: 1,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "TotalAmount",
                table: "Operations",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "VolumeBefore",
                table: "OperationLegs",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "VolumeAfter",
                table: "OperationLegs",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "Delta",
                table: "OperationLegs",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MaxCapacity",
                table: "Tanks",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldPrecision: 6,
                oldScale: 1);

            migrationBuilder.AlterColumn<int>(
                name: "InitialCapacity",
                table: "Tanks",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldPrecision: 6,
                oldScale: 1);

            migrationBuilder.AlterColumn<int>(
                name: "CurrentCapacity",
                table: "Tanks",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldPrecision: 6,
                oldScale: 1);

            migrationBuilder.AlterColumn<int>(
                name: "TotalAmount",
                table: "Operations",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "VolumeBefore",
                table: "OperationLegs",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "VolumeAfter",
                table: "OperationLegs",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "Delta",
                table: "OperationLegs",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");
        }
    }
}
