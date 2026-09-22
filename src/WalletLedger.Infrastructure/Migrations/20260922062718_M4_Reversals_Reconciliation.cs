using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WalletLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M4_Reversals_Reconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ReversalOfTransactionId",
                table: "Transactions",
                column: "ReversalOfTransactionId",
                unique: true,
                filter: "\"ReversalOfTransactionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_ReversalOfTransactionId",
                table: "Transactions");
        }
    }
}
