using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CaioFinance.Data;

public class CaioFinanceContextoFactory : IDesignTimeDbContextFactory<CaioFinanceContexto>
{
    public CaioFinanceContexto CreateDbContext(string[] args)
    {
        var configuracao = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var conexao = configuracao.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(conexao))
        {
            throw new InvalidOperationException(
                "Defina ConnectionStrings:Default com a conexão do Postgres do Supabase.");
        }

        var opcoes = new DbContextOptionsBuilder<CaioFinanceContexto>()
            .UseNpgsql(conexao)
            .Options;

        return new CaioFinanceContexto(opcoes);
    }
}
