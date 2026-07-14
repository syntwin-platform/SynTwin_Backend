using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFactoryRunSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrepareCommandId",
                table: "factory_run_targets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_factory_run_targets_prepare_command_id",
                table: "factory_run_targets",
                column: "PrepareCommandId");

            migrationBuilder.AddForeignKey(
                name: "FK_factory_run_targets_robot_commands_PrepareCommandId",
                table: "factory_run_targets",
                column: "PrepareCommandId",
                principalTable: "robot_commands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_factory_run_targets_robot_commands_PrepareCommandId",
                table: "factory_run_targets");

            migrationBuilder.DropIndex(
                name: "IX_factory_run_targets_prepare_command_id",
                table: "factory_run_targets");

            migrationBuilder.DropColumn(
                name: "PrepareCommandId",
                table: "factory_run_targets");
        }
    }
}
