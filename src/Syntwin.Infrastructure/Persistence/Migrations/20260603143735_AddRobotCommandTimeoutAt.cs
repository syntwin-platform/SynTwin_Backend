using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRobotCommandTimeoutAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TimeoutAt",
                table: "robot_commands",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_robot_commands_status_timeout_at",
                table: "robot_commands",
                columns: new[] { "Status", "TimeoutAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_robot_commands_status_timeout_at",
                table: "robot_commands");

            migrationBuilder.DropColumn(
                name: "TimeoutAt",
                table: "robot_commands");
        }
    }
}
