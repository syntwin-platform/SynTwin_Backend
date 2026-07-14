using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFactoryRunSharedTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StepDurationsJson",
                table: "factory_runs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimatedStepDurationsJson",
                table: "factory_run_targets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StepDurationsJson",
                table: "factory_runs");

            migrationBuilder.DropColumn(
                name: "EstimatedStepDurationsJson",
                table: "factory_run_targets");
        }
    }
}
