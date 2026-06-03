using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginCodeOtpAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "email_otps",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "email_otps",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.CreateIndex(
                name: "IX_email_otps_email_purpose_expires_at",
                table: "email_otps",
                columns: new[] { "Email", "Purpose", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_otps_email_purpose_expires_at",
                table: "email_otps");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "email_otps");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                table: "email_otps");
        }
    }
}
