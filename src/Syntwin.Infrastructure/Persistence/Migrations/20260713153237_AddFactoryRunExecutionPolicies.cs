using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFactoryRunExecutionPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoordinationMode",
                table: "factory_runs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Synchronized");

            migrationBuilder.AddColumn<string>(
                name: "FailurePolicy",
                table: "factory_runs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "IsolateTarget");

            migrationBuilder.AddColumn<string>(
                name: "TerminationReason",
                table: "factory_run_targets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoordinationMode",
                table: "factory_runs");

            migrationBuilder.DropColumn(
                name: "FailurePolicy",
                table: "factory_runs");

            migrationBuilder.DropColumn(
                name: "TerminationReason",
                table: "factory_run_targets");
        }
    }
}
