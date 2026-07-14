using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRobotModelsAndSceneBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RobotModelId",
                table: "robots",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "robot_models",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Vendor = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ModelCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Dof = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UrdfPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MeshRootPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DefaultTcpFrame = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    JointNamesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JointLimitsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robot_models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "robot_scene_bindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RobotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SceneType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "FairinoStudio"),
                    BaseX = table.Column<double>(type: "float", nullable: false),
                    BaseY = table.Column<double>(type: "float", nullable: false),
                    BaseZ = table.Column<double>(type: "float", nullable: false),
                    BaseYaw = table.Column<double>(type: "float", nullable: false),
                    UrdfPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PrimPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RosNamespace = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GraphPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robot_scene_bindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_robot_scene_bindings_robots_RobotId",
                        column: x => x.RobotId,
                        principalTable: "robots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "robot_models",
                columns: new[] { "Id", "CreatedAt", "DefaultTcpFrame", "Description", "DisplayName", "Dof", "IsActive", "JointLimitsJson", "JointNamesJson", "MeshRootPath", "ModelCode", "UpdatedAt", "UrdfPath", "Vendor" },
                values: new object[] { new Guid("8f1e8d7a-5b7a-4a0f-8b2e-4e5d9f0f0005"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "tool_link", "Fairino FR5 6-DOF collaborative robot model for simulator onboarding.", "Fairino FR5", 6, true, "[{\"joint\":1,\"minDeg\":-175,\"maxDeg\":175},{\"joint\":2,\"minDeg\":-265,\"maxDeg\":85},{\"joint\":3,\"minDeg\":-160,\"maxDeg\":160},{\"joint\":4,\"minDeg\":-265,\"maxDeg\":265},{\"joint\":5,\"minDeg\":-175,\"maxDeg\":175},{\"joint\":6,\"minDeg\":-175,\"maxDeg\":175}]", "[\"j1\",\"j2\",\"j3\",\"j4\",\"j5\",\"j6\"]", "/fairino_description/meshes/fairino5_v6", "FR5", null, "/fairino_description/urdf/fairino5_v6.urdf", "Fairino" });

            migrationBuilder.CreateIndex(
                name: "IX_robots_robot_model_id",
                table: "robots",
                column: "RobotModelId");

            migrationBuilder.CreateIndex(
                name: "IX_robot_models_is_active",
                table: "robot_models",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "UX_robot_models_vendor_model_code",
                table: "robot_models",
                columns: new[] { "Vendor", "ModelCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_robot_scene_bindings_scene_robot",
                table: "robot_scene_bindings",
                columns: new[] { "SceneType", "RobotId" });

            migrationBuilder.CreateIndex(
                name: "UX_robot_scene_bindings_robot_id",
                table: "robot_scene_bindings",
                column: "RobotId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_robots_robot_models_RobotModelId",
                table: "robots",
                column: "RobotModelId",
                principalTable: "robot_models",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_robots_robot_models_RobotModelId",
                table: "robots");

            migrationBuilder.DropTable(
                name: "robot_models");

            migrationBuilder.DropTable(
                name: "robot_scene_bindings");

            migrationBuilder.DropIndex(
                name: "IX_robots_robot_model_id",
                table: "robots");

            migrationBuilder.DropColumn(
                name: "RobotModelId",
                table: "robots");
        }
    }
}
