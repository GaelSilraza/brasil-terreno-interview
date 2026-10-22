using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class StaleProcessingTransfersIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_transfers_processing_started_at_stale",
                schema: "transfers",
                table: "transfers",
                column: "processing_started_at",
                filter: "status = 'Processing'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transfers_processing_started_at_stale",
                schema: "transfers",
                table: "transfers");
        }
    }
}
