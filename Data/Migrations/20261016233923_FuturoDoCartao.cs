using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaioFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class FuturoDoCartao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sDsSerie",
                table: "LANCAMENTO_FUTURO",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sIdOrigem",
                table: "LANCAMENTO_FUTURO",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LANCAMENTO_FUTURO_sIdOrigem",
                table: "LANCAMENTO_FUTURO",
                column: "sIdOrigem",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LANCAMENTO_FUTURO_sIdOrigem",
                table: "LANCAMENTO_FUTURO");

            migrationBuilder.DropColumn(
                name: "sDsSerie",
                table: "LANCAMENTO_FUTURO");

            migrationBuilder.DropColumn(
                name: "sIdOrigem",
                table: "LANCAMENTO_FUTURO");
        }
    }
}
