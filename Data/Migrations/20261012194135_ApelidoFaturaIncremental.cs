using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaioFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApelidoFaturaIncremental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "bFlEditado",
                table: "GASTO_CARTAO",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "bFlManual",
                table: "GASTO_CARTAO",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "bFlOculto",
                table: "GASTO_CARTAO",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "sIdFaturaPluggy",
                table: "GASTO_CARTAO",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sNmApelido",
                table: "GASTO_CARTAO",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sNmApelido",
                table: "CONTA_BANCO",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "bFlManual",
                table: "CARTAO",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "bFlOculto",
                table: "CARTAO",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "nNrDiaVencimento",
                table: "CARTAO",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sNmApelido",
                table: "CARTAO",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bFlEditado",
                table: "GASTO_CARTAO");

            migrationBuilder.DropColumn(
                name: "bFlManual",
                table: "GASTO_CARTAO");

            migrationBuilder.DropColumn(
                name: "bFlOculto",
                table: "GASTO_CARTAO");

            migrationBuilder.DropColumn(
                name: "sIdFaturaPluggy",
                table: "GASTO_CARTAO");

            migrationBuilder.DropColumn(
                name: "sNmApelido",
                table: "GASTO_CARTAO");

            migrationBuilder.DropColumn(
                name: "sNmApelido",
                table: "CONTA_BANCO");

            migrationBuilder.DropColumn(
                name: "bFlManual",
                table: "CARTAO");

            migrationBuilder.DropColumn(
                name: "bFlOculto",
                table: "CARTAO");

            migrationBuilder.DropColumn(
                name: "nNrDiaVencimento",
                table: "CARTAO");

            migrationBuilder.DropColumn(
                name: "sNmApelido",
                table: "CARTAO");
        }
    }
}
