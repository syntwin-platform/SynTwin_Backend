using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SyntwinDbContext))]
[Migration("20260611130000_RefactorCompanyRolesOwnerMonitor")]
public sealed class RefactorCompanyRolesOwnerMonitor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE company_members
            SET Role = 'Monitor',
                UpdatedAt = SYSDATETIMEOFFSET()
            WHERE Role IN ('Admin', 'Operator', 'Viewer');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE company_members
            SET Role = 'Viewer',
                UpdatedAt = SYSDATETIMEOFFSET()
            WHERE Role = 'Monitor';
            """);
    }
}
