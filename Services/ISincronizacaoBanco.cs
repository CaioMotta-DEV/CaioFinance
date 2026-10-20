namespace CaioFinance.Services;

public interface ISincronizacaoBanco
{
    Task<ResultadoSincronizacao> AtualizarContaAsync(int nCdConta, CancellationToken cancellationToken = default);
    Task<ResultadoSincronizacao> ImportarItemAsync(string sIdItem, CancellationToken cancellationToken = default);
    Task ExcluirConexaoAsync(int nCdConta, CancellationToken cancellationToken = default);
    Task<string> ImportarCartoesConectadosAsync(CancellationToken cancellationToken = default);
    Task<ResultadoSincronizacao> AtualizarTudoAsync(CancellationToken cancellationToken = default);
}

public sealed record ResultadoSincronizacao(int Contas, int LancamentosNovos, string Mensagem);
