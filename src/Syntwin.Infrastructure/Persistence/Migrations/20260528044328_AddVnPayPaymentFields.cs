using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syntwin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVnPayPaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankCode",
                table: "payment_transactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "payment_transactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MerchantTransactionRef",
                table: "payment_transactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PayDate",
                table: "payment_transactions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessedAt",
                table: "payment_transactions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseCode",
                table: "payment_transactions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionStatus",
                table: "payment_transactions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_payment_transactions_merchant_transaction_ref",
                table: "payment_transactions",
                column: "MerchantTransactionRef",
                unique: true,
                filter: "[MerchantTransactionRef] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_payment_transactions_merchant_transaction_ref",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "BankCode",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "MerchantTransactionRef",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "PayDate",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "ResponseCode",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "TransactionStatus",
                table: "payment_transactions");
        }
    }
}
