using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRobotRuntimeSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "robot_runtime_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RobotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DetectedOfflineAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationSeconds = table.Column<long>(type: "bigint", nullable: true),
                    EndReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robot_runtime_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_robot_runtime_sessions_robots_RobotId",
                        column: x => x.RobotId,
                        principalTable: "robots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_robot_runtime_sessions_robot_ended_at",
                table: "robot_runtime_sessions",
                columns: new[] { "RobotId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_robot_runtime_sessions_robot_ended_at_reason",
                table: "robot_runtime_sessions",
                columns: new[] { "RobotId", "EndedAt", "EndReason" });

            migrationBuilder.CreateIndex(
                name: "IX_robot_runtime_sessions_robot_started_at",
                table: "robot_runtime_sessions",
                columns: new[] { "RobotId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_robot_runtime_sessions_robot_open",
                table: "robot_runtime_sessions",
                column: "RobotId",
                unique: true,
                filter: "[EndedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "robot_runtime_sessions");
        }
    }
}
