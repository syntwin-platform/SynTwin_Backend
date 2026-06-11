using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRobotCompanyOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "robots",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                ;WITH OwnerCompanies AS
                (
                    SELECT
                        cm.UserId,
                        cm.CompanyId,
                        COUNT(*) OVER (PARTITION BY cm.UserId) AS OwnerCount
                    FROM company_members cm
                    INNER JOIN companies c ON c.Id = cm.CompanyId
                    WHERE cm.IsActive = 1
                      AND cm.Role = 'Owner'
                      AND c.Status = 'Active'
                )
                UPDATE r
                SET r.CompanyId = oc.CompanyId
                FROM robots r
                INNER JOIN OwnerCompanies oc ON oc.UserId = r.UserId
                WHERE r.CompanyId IS NULL
                  AND oc.OwnerCount = 1;

                UPDATE r
                SET r.CompanyId = c.Id
                FROM robots r
                INNER JOIN companies c
                    ON c.CreatedByUserId = r.UserId
                   AND c.Slug = 'legacy-robots-' +
                       LOWER(REPLACE(CONVERT(varchar(36), r.UserId), '-', ''))
                INNER JOIN company_members cm
                    ON cm.CompanyId = c.Id
                   AND cm.UserId = r.UserId
                   AND cm.IsActive = 1
                   AND cm.Role = 'Owner'
                WHERE r.CompanyId IS NULL;

                DECLARE @LegacyCompanies TABLE
                (
                    UserId uniqueidentifier NOT NULL PRIMARY KEY,
                    CompanyId uniqueidentifier NOT NULL
                );

                INSERT INTO @LegacyCompanies (UserId, CompanyId)
                SELECT DISTINCT r.UserId, NEWID()
                FROM robots r
                WHERE r.CompanyId IS NULL;

                INSERT INTO companies
                (
                    Id,
                    Name,
                    Slug,
                    Timezone,
                    Status,
                    CreatedByUserId,
                    CreatedAt
                )
                SELECT
                    legacy.CompanyId,
                    'Legacy Robots',
                    'legacy-robots-' +
                        LOWER(REPLACE(CONVERT(varchar(36), legacy.UserId), '-', '')),
                    'Asia/Ho_Chi_Minh',
                    'Active',
                    legacy.UserId,
                    SYSUTCDATETIME()
                FROM @LegacyCompanies legacy;

                INSERT INTO company_members
                (
                    CompanyId,
                    UserId,
                    Role,
                    IsActive,
                    JoinedAt
                )
                SELECT
                    legacy.CompanyId,
                    legacy.UserId,
                    'Owner',
                    1,
                    SYSUTCDATETIME()
                FROM @LegacyCompanies legacy;

                UPDATE r
                SET r.CompanyId = legacy.CompanyId
                FROM robots r
                INNER JOIN @LegacyCompanies legacy ON legacy.UserId = r.UserId
                WHERE r.CompanyId IS NULL;

                IF EXISTS (SELECT 1 FROM robots WHERE CompanyId IS NULL)
                BEGIN
                    THROW 51000, 'Unable to assign every existing robot to a company.', 1;
                END;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "robots",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_robots_company_id",
                table: "robots",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_robots_companies_CompanyId",
                table: "robots",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_robots_companies_CompanyId",
                table: "robots");

            migrationBuilder.DropIndex(
                name: "IX_robots_company_id",
                table: "robots");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "robots");
        }
    }
}
