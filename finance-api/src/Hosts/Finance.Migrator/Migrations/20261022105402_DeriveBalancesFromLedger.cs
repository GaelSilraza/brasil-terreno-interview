using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class DeriveBalancesFromLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "transfer_id",
                table: "account_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "counterpart_account_id",
                table: "account_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.Sql("""
                INSERT INTO account_entries (id, account_id, transfer_id, counterpart_account_id, entry_type, amount, balance_after, created_at)
                SELECT
                    uuidv7(opening.created_at - clock_timestamp()),
                    opening.account_id,
                    NULL,
                    NULL,
                    CASE WHEN opening.opening_balance < 0 THEN 'Debit' ELSE 'Credit' END,
                    abs(opening.opening_balance),
                    opening.opening_balance,
                    opening.created_at
                FROM (
                    SELECT
                        accounts.id AS account_id,
                        accounts.created_at,
                        accounts.balance - COALESCE((
                            SELECT sum(CASE WHEN account_entries.entry_type = 'Credit' THEN account_entries.amount ELSE -account_entries.amount END)
                            FROM account_entries
                            WHERE account_entries.account_id = accounts.id), 0) AS opening_balance
                    FROM accounts
                ) AS opening
                WHERE opening.opening_balance <> 0;
                """);

            migrationBuilder.DropColumn(
                name: "balance",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "ix_account_entries_account_id_id",
                table: "account_entries");

            migrationBuilder.CreateIndex(
                name: "ix_account_entries_account_id_id",
                table: "account_entries",
                columns: new[] { "account_id", "id" })
                .Annotation("Npgsql:IndexInclude", new[] { "entry_type", "amount" });

            migrationBuilder.CreateIndex(
                name: "ux_account_entries_opening",
                table: "account_entries",
                column: "account_id",
                unique: true,
                filter: "transfer_id IS NULL");

            migrationBuilder.Sql("""
                CREATE VIEW account_balances AS
                SELECT
                    accounts.id AS account_id,
                    COALESCE((
                        SELECT sum(CASE WHEN account_entries.entry_type = 'Credit' THEN account_entries.amount ELSE -account_entries.amount END)
                        FROM account_entries
                        WHERE account_entries.account_id = accounts.id), 0)::numeric(18,2) AS balance
                FROM accounts;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "balance",
                table: "accounts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE accounts
                SET balance = account_balances.balance
                FROM account_balances
                WHERE account_balances.account_id = accounts.id;
                """);

            migrationBuilder.Sql("DROP VIEW account_balances;");

            migrationBuilder.Sql("DELETE FROM account_entries WHERE transfer_id IS NULL;");

            migrationBuilder.DropIndex(
                name: "ux_account_entries_opening",
                table: "account_entries");

            migrationBuilder.DropIndex(
                name: "ix_account_entries_account_id_id",
                table: "account_entries");

            migrationBuilder.CreateIndex(
                name: "ix_account_entries_account_id_id",
                table: "account_entries",
                columns: new[] { "account_id", "id" });

            migrationBuilder.AlterColumn<Guid>(
                name: "transfer_id",
                table: "account_entries",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "counterpart_account_id",
                table: "account_entries",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
