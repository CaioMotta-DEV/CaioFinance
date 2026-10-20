using CaioFinance.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaioFinance.Data.Migrations
{
    /// <summary>
    /// Fecha o acesso da API publica do Supabase. O site continua pelo papel postgres,
    /// que ignora RLS. anon, authenticated e service_role deixam de ler e alterar as tabelas.
    /// </summary>
    [DbContext(typeof(CaioFinanceContexto))]
    [Migration("20261020003000_TravarApiSupabase")]
    public class TravarApiSupabase : Migration
    {
        private static readonly string[] Tabelas =
        [
            "CONTA_BANCO",
            "CATEGORIA",
            "LANCAMENTO",
            "LANCAMENTO_FUTURO",
            "CARTAO",
            "GASTO_CARTAO",
            "__EFMigrationsHistory"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var tabela in Tabelas)
            {
                migrationBuilder.Sql($"""ALTER TABLE "{tabela}" ENABLE ROW LEVEL SECURITY;""");
                migrationBuilder.Sql($"""REVOKE ALL ON TABLE "{tabela}" FROM PUBLIC, anon, authenticated, service_role;""");
            }

            migrationBuilder.Sql("""REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM PUBLIC, anon, authenticated, service_role;""");
            migrationBuilder.Sql("""
                ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public
                REVOKE ALL ON TABLES FROM PUBLIC, anon, authenticated, service_role;
                """);
            migrationBuilder.Sql("""
                ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public
                REVOKE ALL ON SEQUENCES FROM PUBLIC, anon, authenticated, service_role;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tabela in Tabelas)
            {
                migrationBuilder.Sql($"""ALTER TABLE "{tabela}" DISABLE ROW LEVEL SECURITY;""");
                migrationBuilder.Sql($"""GRANT ALL ON TABLE "{tabela}" TO anon, authenticated, service_role;""");
            }
        }
    }
}
