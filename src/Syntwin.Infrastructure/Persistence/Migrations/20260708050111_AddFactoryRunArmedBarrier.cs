using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFactoryRunArmedBarrier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArmedAtUtc",
                table: "factory_run_targets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CommandReceivedAtUtc",
                table: "factory_run_targets",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArmedAtUtc",
                table: "factory_run_targets");

            migrationBuilder.DropColumn(
                name: "CommandReceivedAtUtc",
                table: "factory_run_targets");
        }
    }
}
