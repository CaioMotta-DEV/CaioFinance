using System.Security.Cryptography;
using System.Text;
using CaioFinance.Data;
using CaioFinance.Models;
using Microsoft.EntityFrameworkCore;

namespace CaioFinance.Services;

public class ServicoFuturoCartao(CaioFinanceContexto db)
{
    public async Task LimparAsync(CancellationToken cancellationToken = default)
    {
        var atuais = await db.LancamentosFuturos
            .Where(item => item.nCdTipo == TipoLancamentoFuturo.Cartao || item.nCdTipo == TipoLancamentoFuturo.Ignorado)
            .ToListAsync(cancellationToken);
        db.LancamentosFuturos.RemoveRange(atuais);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> SincronizarAsync(CancellationToken cancellationToken = default)
    {
        var inicio = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var contas = await db.ContasBanco.AsNoTracking().ToListAsync(cancellationToken);
        var categorias = await db.Categorias.AsNoTracking().ToListAsync(cancellationToken);
        var outros = categorias.FirstOrDefault(item => item.sNmCategoria == NomesPadrao.CategoriaOutros)
            ?? throw new InvalidOperationException("A categoria Outros não foi encontrada.");
        var categoriaPorNome = categorias.ToDictionary(
            item => item.sNmCategoria.Trim().ToLowerInvariant(),
            item => item.nCdCategoria);

        var gastos = await db.GastosCartao
            .AsNoTracking()
            .Include(gasto => gasto.Cartao)
            .Where(gasto => !gasto.bFlOculto && !gasto.Cartao.bFlOculto && gasto.tDtFatura >= inicio)
            .ToListAsync(cancellationToken);

        var futurosFixos = await db.LancamentosFuturos
            .AsNoTracking()
            .Where(item => item.nCdTipo == TipoLancamentoFuturo.ContaFixa)
            .Select(item => item.sNmLancamento)
            .ToListAsync(cancellationToken);

        var nomesFixos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var gasto in await db.GastosCartao.AsNoTracking()
            .Where(gasto => gasto.bFlContaFixa && !gasto.bFlOculto)
            .Select(gasto => new { gasto.sNmDescricao, gasto.sNmApelido })
            .ToListAsync(cancellationToken))
        {
            Adicionar(nomesFixos, gasto.sNmDescricao);
            Adicionar(nomesFixos, gasto.sNmApelido);
        }

        foreach (var nome in futurosFixos)
            Adicionar(nomesFixos, nome);

        var desejados = new Dictionary<string, Previsto>(StringComparer.Ordinal);
        foreach (var gasto in gastos)
        {
            if (JaEhContaFixa(nomesFixos, gasto))
                continue;

            if (ContaDoCartao(gasto.Cartao, contas) is not int nCdConta)
                continue;

            var origem = "c:" + gasto.sIdTransacaoPluggy;
            desejados[origem] = new Previsto(
                origem,
                Serie(gasto),
                nCdConta,
                categoriaPorNome.TryGetValue(gasto.sNmCategoria.Trim().ToLowerInvariant(), out var categoria)
                    ? categoria
                    : outros.nCdCategoria,
                gasto.tDtFatura,
                Nome(gasto),
                -Math.Abs(gasto.dVlGasto));
        }

        var atuais = await db.LancamentosFuturos
            .Where(item => item.nCdTipo == TipoLancamentoFuturo.Cartao || item.nCdTipo == TipoLancamentoFuturo.Ignorado)
            .ToListAsync(cancellationToken);
        db.LancamentosFuturos.RemoveRange(atuais);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var previsto in desejados.Values)
        {
            db.LancamentosFuturos.Add(new LancamentoFuturo
            {
                nCdConta = previsto.Conta,
                nCdCategoria = previsto.Categoria,
                tDtVencimento = previsto.Vencimento,
                sNmLancamento = previsto.Nome,
                dVlLancamento = previsto.Valor,
                nCdTipo = TipoLancamentoFuturo.Cartao,
                sIdOrigem = previsto.Origem,
                sDsSerie = previsto.Serie,
                tDtCriacao = Calendario.Agora()
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return desejados.Count;
    }

    public async Task<int> RemoverAsync(int nCdLancamentoFuturo, bool serieInteira, CancellationToken cancellationToken = default)
    {
        var item = await db.LancamentosFuturos
            .FirstOrDefaultAsync(futuro => futuro.nCdLancamentoFuturo == nCdLancamentoFuturo, cancellationToken);
        if (item is null || item.nCdTipo != TipoLancamentoFuturo.Cartao)
            return 0;

        if (!serieInteira || string.IsNullOrWhiteSpace(item.sDsSerie))
        {
            db.LancamentosFuturos.Remove(item);
            await db.SaveChangesAsync(cancellationToken);
            return 1;
        }

        var linhas = await db.LancamentosFuturos
            .Where(futuro => futuro.nCdTipo == TipoLancamentoFuturo.Cartao && futuro.sDsSerie == item.sDsSerie)
            .ToListAsync(cancellationToken);
        db.LancamentosFuturos.RemoveRange(linhas);
        await db.SaveChangesAsync(cancellationToken);
        return linhas.Count;
    }

    private static bool JaEhContaFixa(HashSet<string> nomesFixos, GastoCartao gasto) =>
        gasto.bFlContaFixa
        || nomesFixos.Contains(Normalizar(gasto.sNmDescricao))
        || (!string.IsNullOrWhiteSpace(gasto.sNmApelido) && nomesFixos.Contains(Normalizar(gasto.sNmApelido)));

    private static void Adicionar(HashSet<string> nomes, string? texto)
    {
        if (!string.IsNullOrWhiteSpace(texto))
            nomes.Add(Normalizar(texto));
    }

    private static string Normalizar(string nome) => nome.Trim().ToUpperInvariant();

    private static string Nome(GastoCartao gasto)
    {
        var nome = gasto.NomeExibicao.Trim();
        if (gasto.nNrParcela is int parcela && gasto.nNrTotalParcelas is int total && total > 1)
            nome = $"{nome} {parcela}/{total}";

        return nome.Length <= 160 ? nome : nome[..160];
    }

    private static string Serie(GastoCartao gasto)
    {
        var total = gasto.nNrTotalParcelas is >= 2 ? gasto.nNrTotalParcelas.Value : 0;
        var parcela = gasto.nNrParcela is >= 1 ? gasto.nNrParcela.Value : 1;
        var chave = total > 1
            ? $"{gasto.nCdCartao}|{Normalizar(gasto.sNmDescricao)}|{total}|{new DateOnly(gasto.tDtFatura.Year, gasto.tDtFatura.Month, 1).AddMonths(1 - parcela):yyyyMM}"
            : $"{gasto.nCdCartao}|{gasto.sIdTransacaoPluggy}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chave)))[..32];
    }

    private static int? ContaDoCartao(Cartao cartao, IReadOnlyList<ContaBanco> contas)
    {
        var instituicao = cartao.sNmInstituicao.Trim();
        var ligada = contas.FirstOrDefault(conta =>
            !string.IsNullOrWhiteSpace(conta.sIdItemPluggy)
            && (conta.sNmInstituicao.Contains(instituicao, StringComparison.OrdinalIgnoreCase)
                || instituicao.Contains(conta.NomeExibicao, StringComparison.OrdinalIgnoreCase)
                || conta.NomeExibicao.Contains(instituicao, StringComparison.OrdinalIgnoreCase)));

        return (ligada
            ?? contas.FirstOrDefault(conta => !string.IsNullOrWhiteSpace(conta.sIdItemPluggy))
            ?? contas.FirstOrDefault())?.nCdConta;
    }

    private sealed record Previsto(
        string Origem,
        string Serie,
        int Conta,
        int Categoria,
        DateOnly Vencimento,
        string Nome,
        decimal Valor);
}
