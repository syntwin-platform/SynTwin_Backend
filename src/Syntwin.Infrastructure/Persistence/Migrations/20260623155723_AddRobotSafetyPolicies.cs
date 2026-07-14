using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRobotSafetyPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "robot_safety_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RobotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RobotModel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PolicyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robot_safety_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_robot_safety_policies_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_robot_safety_policies_robots_RobotId",
                        column: x => x.RobotId,
                        principalTable: "robots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_robot_safety_policies_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_robot_safety_policies_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_robot_safety_policies_company_id",
                table: "robot_safety_policies",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_robot_safety_policies_company_scope_active",
                table: "robot_safety_policies",
                columns: new[] { "CompanyId", "Scope", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_robot_safety_policies_CreatedByUserId",
                table: "robot_safety_policies",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_robot_safety_policies_robot_id",
                table: "robot_safety_policies",
                column: "RobotId");

            migrationBuilder.CreateIndex(
                name: "IX_robot_safety_policies_robot_scope_active",
                table: "robot_safety_policies",
                columns: new[] { "RobotId", "Scope", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_robot_safety_policies_UpdatedByUserId",
                table: "robot_safety_policies",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_robot_safety_policies_company_active",
                table: "robot_safety_policies",
                columns: new[] { "CompanyId", "Scope" },
                unique: true,
                filter: "[RobotId] IS NULL AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_robot_safety_policies_robot_active",
                table: "robot_safety_policies",
                columns: new[] { "RobotId", "Scope" },
                unique: true,
                filter: "[RobotId] IS NOT NULL AND [IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "robot_safety_policies");
        }
    }
}
