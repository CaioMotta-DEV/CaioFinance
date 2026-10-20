using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CaioFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CATEGORIA",
                columns: table => new
                {
                    nCdCategoria = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sNmCategoria = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nNrOrdem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CATEGORIA", x => x.nCdCategoria);
                });

            migrationBuilder.CreateTable(
                name: "CONTA_BANCO",
                columns: table => new
                {
                    nCdConta = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sNmInstituicao = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    dVlSaldo = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    nCdSituacao = table.Column<int>(type: "integer", nullable: false),
                    tDtAtualizacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTA_BANCO", x => x.nCdConta);
                });

            migrationBuilder.CreateTable(
                name: "PERFIL",
                columns: table => new
                {
                    nCdPerfil = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sNmPessoa = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sDsEmail = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PERFIL", x => x.nCdPerfil);
                });

            migrationBuilder.CreateTable(
                name: "LANCAMENTO",
                columns: table => new
                {
                    nCdLancamento = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nCdConta = table.Column<int>(type: "integer", nullable: false),
                    nCdCategoria = table.Column<int>(type: "integer", nullable: false),
                    tDtOcorrencia = table.Column<DateOnly>(type: "date", nullable: false),
                    sNmOriginal = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    sNmExibicao = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    dVlLancamento = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    bFlContaFixa = table.Column<bool>(type: "boolean", nullable: false),
                    nCdSerie = table.Column<int>(type: "integer", nullable: true),
                    nCdOrigem = table.Column<int>(type: "integer", nullable: false),
                    tDtCriacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LANCAMENTO", x => x.nCdLancamento);
                    table.ForeignKey(
                        name: "FK_LANCAMENTO_CATEGORIA_nCdCategoria",
                        column: x => x.nCdCategoria,
                        principalTable: "CATEGORIA",
                        principalColumn: "nCdCategoria",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LANCAMENTO_CONTA_BANCO_nCdConta",
                        column: x => x.nCdConta,
                        principalTable: "CONTA_BANCO",
                        principalColumn: "nCdConta",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LANCAMENTO_FUTURO",
                columns: table => new
                {
                    nCdLancamentoFuturo = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nCdConta = table.Column<int>(type: "integer", nullable: false),
                    nCdCategoria = table.Column<int>(type: "integer", nullable: false),
                    tDtVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    sNmLancamento = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    dVlLancamento = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    nCdTipo = table.Column<int>(type: "integer", nullable: false),
                    nCdSerie = table.Column<int>(type: "integer", nullable: true),
                    tDtCriacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LANCAMENTO_FUTURO", x => x.nCdLancamentoFuturo);
                    table.ForeignKey(
                        name: "FK_LANCAMENTO_FUTURO_CATEGORIA_nCdCategoria",
                        column: x => x.nCdCategoria,
                        principalTable: "CATEGORIA",
                        principalColumn: "nCdCategoria",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LANCAMENTO_FUTURO_CONTA_BANCO_nCdConta",
                        column: x => x.nCdConta,
                        principalTable: "CONTA_BANCO",
                        principalColumn: "nCdConta",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LANCAMENTO_nCdCategoria",
                table: "LANCAMENTO",
                column: "nCdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_LANCAMENTO_nCdConta",
                table: "LANCAMENTO",
                column: "nCdConta");

            migrationBuilder.CreateIndex(
                name: "IX_LANCAMENTO_nCdSerie",
                table: "LANCAMENTO",
                column: "nCdSerie");

            migrationBuilder.CreateIndex(
                name: "IX_LANCAMENTO_tDtOcorrencia",
                table: "LANCAMENTO",
                column: "tDtOcorrencia");

            migrationBuilder.CreateIndex(
                name: "IX_LANCAMENTO_FUTURO_nCdCategoria",
                table: "LANCAMENTO_FUTURO",
                column: "nCdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_LANCAMENTO_FUTURO_nCdConta",
                table: "LANCAMENTO_FUTURO",
                column: "nCdConta");

            migrationBuilder.CreateIndex(
                name: "IX_LANCAMENTO_FUTURO_nCdSerie_tDtVencimento",
                table: "LANCAMENTO_FUTURO",
                columns: new[] { "nCdSerie", "tDtVencimento" });

            migrationBuilder.CreateIndex(
                name: "IX_LANCAMENTO_FUTURO_tDtVencimento",
                table: "LANCAMENTO_FUTURO",
                column: "tDtVencimento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LANCAMENTO");

            migrationBuilder.DropTable(
                name: "LANCAMENTO_FUTURO");

            migrationBuilder.DropTable(
                name: "PERFIL");

            migrationBuilder.DropTable(
                name: "CATEGORIA");

            migrationBuilder.DropTable(
                name: "CONTA_BANCO");
        }
    }
}
