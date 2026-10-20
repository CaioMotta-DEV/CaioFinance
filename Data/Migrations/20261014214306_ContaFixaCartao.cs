using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaioFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContaFixaCartao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "bFlContaFixa",
                table: "GASTO_CARTAO",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "nCdSerie",
                table: "GASTO_CARTAO",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GASTO_CARTAO_nCdSerie",
                table: "GASTO_CARTAO",
                column: "nCdSerie");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GASTO_CARTAO_nCdSerie",
                table: "GASTO_CARTAO");

            migrationBuilder.DropColumn(
                name: "bFlContaFixa",
                table: "GASTO_CARTAO");

            migrationBuilder.DropColumn(
                name: "nCdSerie",
                table: "GASTO_CARTAO");
        }
    }
}
