using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFactoryRunActualStartMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActualStartSkewMs",
                table: "factory_runs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActualStartedAtUtc",
                table: "factory_run_targets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartLateByMs",
                table: "factory_run_targets",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualStartSkewMs",
                table: "factory_runs");

            migrationBuilder.DropColumn(
                name: "ActualStartedAtUtc",
                table: "factory_run_targets");

            migrationBuilder.DropColumn(
                name: "StartLateByMs",
                table: "factory_run_targets");
        }
    }
}
