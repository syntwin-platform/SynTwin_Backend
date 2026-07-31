using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSubscriptionPlansToFactoryPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "subscription_plans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[]
                {
                    "AuditRetentionDays",
                    "CanSendCommand",
                    "CanView3D",
                    "Code",
                    "MaxRobots",
                    "MonthlyPrice",
                    "Name"
                },
                values: new object[]
                {
                    30,
                    false,
                    true,
                    "Starter",
                    5,
                    5000000m,
                    "Starter"
                });

            migrationBuilder.UpdateData(
                table: "subscription_plans",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[]
                {
                    "AuditRetentionDays",
                    "CanSendCommand",
                    "CanView3D",
                    "Code",
                    "MaxRobots",
                    "MonthlyPrice",
                    "Name"
                },
                values: new object[]
                {
                    365,
                    true,
                    true,
                    "Business",
                    20,
                    15000000m,
                    "Business (SME Target)"
                });

            migrationBuilder.InsertData(
                table: "subscription_plans",
                columns: new[]
                {
                    "Id",
                    "AuditRetentionDays",
                    "CanSendCommand",
                    "CanView3D",
                    "Code",
                    "CreatedAt",
                    "IsActive",
                    "MaxRobots",
                    "MonthlyPrice",
                    "Name"
                },
                values: new object[]
                {
                    4,
                    3650,
                    true,
                    true,
                    "Enterprise",
                    new DateTimeOffset(
                        new DateTime(
                            2026,
                            1,
                            1,
                            0,
                            0,
                            0,
                            0,
                            DateTimeKind.Unspecified),
                        new TimeSpan(0, 0, 0, 0, 0)),
                    true,
                    9999,
                    50000000m,
                    "Enterprise"
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "subscription_plans",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.UpdateData(
                table: "subscription_plans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[]
                {
                    "AuditRetentionDays",
                    "CanSendCommand",
                    "CanView3D",
                    "Code",
                    "MaxRobots",
                    "MonthlyPrice",
                    "Name"
                },
                values: new object[]
                {
                    30,
                    false,
                    true,
                    "Basic",
                    3,
                    99000m,
                    "Basic"
                });

            migrationBuilder.UpdateData(
                table: "subscription_plans",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[]
                {
                    "AuditRetentionDays",
                    "CanSendCommand",
                    "CanView3D",
                    "Code",
                    "MaxRobots",
                    "MonthlyPrice",
                    "Name"
                },
                values: new object[]
                {
                    365,
                    true,
                    true,
                    "Premium",
                    30,
                    299000m,
                    "Premium"
                });
        }
    }
}
