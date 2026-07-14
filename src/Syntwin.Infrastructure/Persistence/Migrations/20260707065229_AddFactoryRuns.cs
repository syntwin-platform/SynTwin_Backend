using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFactoryRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "factory_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProgramName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LuaFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    LuaContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LuaContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetCount = table.Column<int>(type: "int", nullable: false),
                    ScheduledStartAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PreparedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factory_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_factory_runs_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_factory_runs_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "factory_run_targets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RobotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RuntimeSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReadinessError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PrepareStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PreparedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReadyAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factory_run_targets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_factory_run_targets_factory_runs_FactoryRunId",
                        column: x => x.FactoryRunId,
                        principalTable: "factory_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_factory_run_targets_robot_commands_CommandId",
                        column: x => x.CommandId,
                        principalTable: "robot_commands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_factory_run_targets_robot_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "robot_programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_factory_run_targets_robots_RobotId",
                        column: x => x.RobotId,
                        principalTable: "robots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_factory_run_targets_command_id",
                table: "factory_run_targets",
                column: "CommandId");

            migrationBuilder.CreateIndex(
                name: "IX_factory_run_targets_factory_run_id",
                table: "factory_run_targets",
                column: "FactoryRunId");

            migrationBuilder.CreateIndex(
                name: "IX_factory_run_targets_program_id",
                table: "factory_run_targets",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_factory_run_targets_robot_id",
                table: "factory_run_targets",
                column: "RobotId");

            migrationBuilder.CreateIndex(
                name: "UX_factory_run_targets_run_robot",
                table: "factory_run_targets",
                columns: new[] { "FactoryRunId", "RobotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_factory_runs_company_id",
                table: "factory_runs",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_factory_runs_company_status_created_at",
                table: "factory_runs",
                columns: new[] { "CompanyId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_factory_runs_created_by_user_id",
                table: "factory_runs",
                column: "CreatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "factory_run_targets");

            migrationBuilder.DropTable(
                name: "factory_runs");
        }
    }
}
