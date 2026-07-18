using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFactoryRunIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_factory_run_targets_command_id",
                table: "factory_run_targets");

            migrationBuilder.DropIndex(
                name: "IX_factory_run_targets_prepare_command_id",
                table: "factory_run_targets");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "factory_runs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "factory_runs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelCommandId",
                table: "factory_run_targets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_factory_runs_user_client_request",
                table: "factory_runs",
                columns: new[] { "CreatedByUserId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_factory_run_targets_cancel_command_id",
                table: "factory_run_targets",
                column: "CancelCommandId",
                unique: true,
                filter: "[CancelCommandId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_factory_run_targets_command_id",
                table: "factory_run_targets",
                column: "CommandId",
                unique: true,
                filter: "[CommandId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_factory_run_targets_prepare_command_id",
                table: "factory_run_targets",
                column: "PrepareCommandId",
                unique: true,
                filter: "[PrepareCommandId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_factory_run_targets_robot_commands_CancelCommandId",
                table: "factory_run_targets",
                column: "CancelCommandId",
                principalTable: "robot_commands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_factory_run_targets_robot_commands_CancelCommandId",
                table: "factory_run_targets");

            migrationBuilder.DropIndex(
                name: "UX_factory_runs_user_client_request",
                table: "factory_runs");

            migrationBuilder.DropIndex(
                name: "UX_factory_run_targets_cancel_command_id",
                table: "factory_run_targets");

            migrationBuilder.DropIndex(
                name: "UX_factory_run_targets_command_id",
                table: "factory_run_targets");

            migrationBuilder.DropIndex(
                name: "UX_factory_run_targets_prepare_command_id",
                table: "factory_run_targets");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "factory_runs");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "factory_runs");

            migrationBuilder.DropColumn(
                name: "CancelCommandId",
                table: "factory_run_targets");

            migrationBuilder.CreateIndex(
                name: "IX_factory_run_targets_command_id",
                table: "factory_run_targets",
                column: "CommandId");

            migrationBuilder.CreateIndex(
                name: "IX_factory_run_targets_prepare_command_id",
                table: "factory_run_targets",
                column: "PrepareCommandId");
        }
    }
}
