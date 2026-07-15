using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFactoryRunPrograms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FactoryRunProgramId",
                table: "factory_run_targets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "factory_run_programs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactoryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProgramName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LuaFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    LuaContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LuaContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SyncPlanHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factory_run_programs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_factory_run_programs_factory_runs_FactoryRunId",
                        column: x => x.FactoryRunId,
                        principalTable: "factory_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_factory_run_targets_factory_run_program_id",
                table: "factory_run_targets",
                column: "FactoryRunProgramId");

            migrationBuilder.CreateIndex(
                name: "UX_factory_run_programs_run_content_hash",
                table: "factory_run_programs",
                columns: new[] { "FactoryRunId", "LuaContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_factory_run_programs_run_key",
                table: "factory_run_programs",
                columns: new[] { "FactoryRunId", "ProgramKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_factory_run_targets_factory_run_programs_FactoryRunProgramId",
                table: "factory_run_targets",
                column: "FactoryRunProgramId",
                principalTable: "factory_run_programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_factory_run_targets_factory_run_programs_FactoryRunProgramId",
                table: "factory_run_targets");

            migrationBuilder.DropTable(
                name: "factory_run_programs");

            migrationBuilder.DropIndex(
                name: "IX_factory_run_targets_factory_run_program_id",
                table: "factory_run_targets");

            migrationBuilder.DropColumn(
                name: "FactoryRunProgramId",
                table: "factory_run_targets");
        }
    }
}
