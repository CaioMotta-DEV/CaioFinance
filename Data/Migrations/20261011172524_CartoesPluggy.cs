using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CaioFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class CartoesPluggy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CARTAO",
                columns: table => new
                {
                    nCdCartao = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sIdItemPluggy = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sIdContaPluggy = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sNmInstituicao = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    sNmCartao = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    sNmBandeira = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sNrFinal = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    sDsCor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    tDtAtualizacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CARTAO", x => x.nCdCartao);
                });

            migrationBuilder.CreateTable(
                name: "GASTO_CARTAO",
                columns: table => new
                {
                    nCdGasto = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nCdCartao = table.Column<int>(type: "integer", nullable: false),
                    sIdTransacaoPluggy = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tDtCompra = table.Column<DateOnly>(type: "date", nullable: false),
                    tDtFatura = table.Column<DateOnly>(type: "date", nullable: false),
                    sNmDescricao = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    sNmCategoria = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nNrParcela = table.Column<int>(type: "integer", nullable: true),
                    nNrTotalParcelas = table.Column<int>(type: "integer", nullable: true),
                    dVlGasto = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    tDtCriacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GASTO_CARTAO", x => x.nCdGasto);
                    table.ForeignKey(
                        name: "FK_GASTO_CARTAO_CARTAO_nCdCartao",
                        column: x => x.nCdCartao,
                        principalTable: "CARTAO",
                        principalColumn: "nCdCartao",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CARTAO_sIdContaPluggy",
                table: "CARTAO",
                column: "sIdContaPluggy",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CARTAO_sIdItemPluggy",
                table: "CARTAO",
                column: "sIdItemPluggy");

            migrationBuilder.CreateIndex(
                name: "IX_GASTO_CARTAO_nCdCartao",
                table: "GASTO_CARTAO",
                column: "nCdCartao");

            migrationBuilder.CreateIndex(
                name: "IX_GASTO_CARTAO_sIdTransacaoPluggy",
                table: "GASTO_CARTAO",
                column: "sIdTransacaoPluggy",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GASTO_CARTAO_tDtFatura",
                table: "GASTO_CARTAO",
                column: "tDtFatura");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GASTO_CARTAO");

            migrationBuilder.DropTable(
                name: "CARTAO");
        }
    }
}
