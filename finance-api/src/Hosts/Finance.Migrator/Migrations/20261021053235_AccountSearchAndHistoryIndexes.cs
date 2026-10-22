using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class AccountSearchAndHistoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transfers_destination_account_id",
                schema: "transfers",
                table: "transfers");

            migrationBuilder.DropIndex(
                name: "ux_persons_document_number",
                schema: "accounts",
                table: "persons");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "ix_transfers_destination_account_id_id",
                schema: "transfers",
                table: "transfers",
                columns: new[] { "destination_account_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_transfers_source_account_id_id",
                schema: "transfers",
                table: "transfers",
                columns: new[] { "source_account_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_persons_full_name_id",
                schema: "accounts",
                table: "persons",
                columns: new[] { "full_name", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_persons_full_name_trgm",
                schema: "accounts",
                table: "persons",
                column: "full_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ux_persons_document_number",
                schema: "accounts",
                table: "persons",
                column: "document_number",
                unique: true)
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transfers_destination_account_id_id",
                schema: "transfers",
                table: "transfers");

            migrationBuilder.DropIndex(
                name: "ix_transfers_source_account_id_id",
                schema: "transfers",
                table: "transfers");

            migrationBuilder.DropIndex(
                name: "ix_persons_full_name_id",
                schema: "accounts",
                table: "persons");

            migrationBuilder.DropIndex(
                name: "ix_persons_full_name_trgm",
                schema: "accounts",
                table: "persons");

            migrationBuilder.DropIndex(
                name: "ux_persons_document_number",
                schema: "accounts",
                table: "persons");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "ix_transfers_destination_account_id",
                schema: "transfers",
                table: "transfers",
                column: "destination_account_id");

            migrationBuilder.CreateIndex(
                name: "ux_persons_document_number",
                schema: "accounts",
                table: "persons",
                column: "document_number",
                unique: true);
        }
    }
}
