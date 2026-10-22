using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class MoveTablesToPublicSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "transfers",
                schema: "transfers",
                newName: "transfers",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "transfer_limit_policies",
                schema: "transfers",
                newName: "transfer_limit_policies",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "persons",
                schema: "accounts",
                newName: "persons",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "outbox_messages",
                schema: "messaging",
                newName: "outbox_messages",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "accounts",
                schema: "accounts",
                newName: "accounts",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "account_entries",
                schema: "accounts",
                newName: "account_entries",
                newSchema: "public");

            migrationBuilder.DropSchema(
                name: "accounts");

            migrationBuilder.DropSchema(
                name: "messaging");

            migrationBuilder.DropSchema(
                name: "transfers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "accounts");

            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.EnsureSchema(
                name: "transfers");

            migrationBuilder.RenameTable(
                name: "transfers",
                newName: "transfers",
                newSchema: "transfers");

            migrationBuilder.RenameTable(
                name: "transfer_limit_policies",
                newName: "transfer_limit_policies",
                newSchema: "transfers");

            migrationBuilder.RenameTable(
                name: "persons",
                newName: "persons",
                newSchema: "accounts");

            migrationBuilder.RenameTable(
                name: "outbox_messages",
                newName: "outbox_messages",
                newSchema: "messaging");

            migrationBuilder.RenameTable(
                name: "accounts",
                newName: "accounts",
                newSchema: "accounts");

            migrationBuilder.RenameTable(
                name: "account_entries",
                newName: "account_entries",
                newSchema: "accounts");
        }
    }
}
