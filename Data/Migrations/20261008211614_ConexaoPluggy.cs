using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaioFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConexaoPluggy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sIdTransacaoPluggy",
                table: "LANCAMENTO",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "nCdConectorPluggy",
                table: "CONTA_BANCO",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sIdContaPluggy",
                table: "CONTA_BANCO",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sIdItemPluggy",
                table: "CONTA_BANCO",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LANCAMENTO_sIdTransacaoPluggy",
                table: "LANCAMENTO",
                column: "sIdTransacaoPluggy",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CONTA_BANCO_sIdContaPluggy",
                table: "CONTA_BANCO",
                column: "sIdContaPluggy",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CONTA_BANCO_sIdItemPluggy",
                table: "CONTA_BANCO",
                column: "sIdItemPluggy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LANCAMENTO_sIdTransacaoPluggy",
                table: "LANCAMENTO");

            migrationBuilder.DropIndex(
                name: "IX_CONTA_BANCO_sIdContaPluggy",
                table: "CONTA_BANCO");

            migrationBuilder.DropIndex(
                name: "IX_CONTA_BANCO_sIdItemPluggy",
                table: "CONTA_BANCO");

            migrationBuilder.DropColumn(
                name: "sIdTransacaoPluggy",
                table: "LANCAMENTO");

            migrationBuilder.DropColumn(
                name: "nCdConectorPluggy",
                table: "CONTA_BANCO");

            migrationBuilder.DropColumn(
                name: "sIdContaPluggy",
                table: "CONTA_BANCO");

            migrationBuilder.DropColumn(
                name: "sIdItemPluggy",
                table: "CONTA_BANCO");
        }
    }
}
