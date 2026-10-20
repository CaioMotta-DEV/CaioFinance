using CaioFinance.Data;
using CaioFinance.Models;
using Microsoft.EntityFrameworkCore;

namespace CaioFinance.Services;

public class ServicoContaFixa(CaioFinanceContexto db)
{
    public const int MesesSeguintes = 6;
    public const int MesesMinimos = 1;
    public const int MesesMaximos = 60;

    public async Task<int> MarcarAsync(
        Lancamento lancamento,
        string apelido,
        decimal valorEstimado,
        int meses,
        bool comecarNesteMes,
        CancellationToken cancellationToken = default)
    {
        if (lancamento.dVlLancamento == 0)
            return 0;

        apelido = apelido.Trim();
        if (apelido.Length is 0 or > 160 || valorEstimado <= 0 || !MesesValidos(meses))
            return 0;

        lancamento.nCdSerie ??= await ProximaSerieAsync(cancellationToken);
        lancamento.bFlContaFixa = true;

        var valor = lancamento.dVlLancamento < 0 ? -Math.Abs(valorEstimado) : Math.Abs(valorEstimado);
        var baseMes = new DateOnly(lancamento.tDtOcorrencia.Year, lancamento.tDtOcorrencia.Month, 1);
        var dia = Math.Min(lancamento.tDtOcorrencia.Day, 28);
        return await CriarFuturosAsync(
            lancamento.nCdSerie.Value,
            lancamento.nCdConta,
            lancamento.nCdCategoria,
            apelido,
            valor,
            baseMes,
            dia,
            meses,
            comecarNesteMes,
            cancellationToken);
    }

    public async Task<int> MarcarGastoCartaoAsync(
        GastoCartao gasto,
        int nCdConta,
        int nCdCategoria,
        string apelido,
        decimal valorEstimado,
        int meses,
        bool comecarNesteMes,
        CancellationToken cancellationToken = default)
    {
        apelido = apelido.Trim();
        if (gasto.dVlGasto <= 0 || apelido.Length is 0 or > 160 || valorEstimado <= 0 || !MesesValidos(meses))
            return 0;

        gasto.nCdSerie ??= await ProximaSerieAsync(cancellationToken);
        gasto.bFlContaFixa = true;

        var valor = -Math.Abs(valorEstimado);
        var baseMes = new DateOnly(gasto.tDtFatura.Year, gasto.tDtFatura.Month, 1);
        var dia = Math.Min(gasto.tDtFatura.Day, 28);
        return await CriarFuturosAsync(
            gasto.nCdSerie.Value,
            nCdConta,
            nCdCategoria,
            apelido,
            valor,
            baseMes,
            dia,
            meses,
            comecarNesteMes,
            cancellationToken);
    }

    public async Task<int> MarcarFuturoAsync(
        LancamentoFuturo futuro,
        string apelido,
        decimal valorEstimado,
        int meses,
        bool comecarNesteMes,
        CancellationToken cancellationToken = default)
    {
        if (futuro.dVlLancamento == 0)
            return 0;

        apelido = apelido.Trim();
        if (apelido.Length is 0 or > 160 || valorEstimado <= 0 || !MesesValidos(meses))
            return 0;

        futuro.nCdSerie ??= await ProximaSerieAsync(cancellationToken);
        futuro.nCdTipo = TipoLancamentoFuturo.ContaFixa;
        futuro.sNmLancamento = apelido;
        futuro.dVlLancamento = futuro.dVlLancamento < 0 ? -Math.Abs(valorEstimado) : Math.Abs(valorEstimado);

        var valor = futuro.dVlLancamento;
        var baseMes = new DateOnly(futuro.tDtVencimento.Year, futuro.tDtVencimento.Month, 1);
        var dia = Math.Min(futuro.tDtVencimento.Day, 28);
        return await CriarFuturosAsync(
            futuro.nCdSerie.Value,
            futuro.nCdConta,
            futuro.nCdCategoria,
            apelido,
            valor,
            baseMes,
            dia,
            meses,
            comecarNesteMes,
            cancellationToken);
    }

    public async Task DesmarcarAsync(Lancamento lancamento, CancellationToken cancellationToken = default)
    {
        lancamento.bFlContaFixa = false;
        await RemoverFuturosDaSerieAsync(lancamento.nCdSerie, cancellationToken);
    }

    public async Task DesmarcarGastoCartaoAsync(GastoCartao gasto, CancellationToken cancellationToken = default)
    {
        gasto.bFlContaFixa = false;
        await RemoverFuturosDaSerieAsync(gasto.nCdSerie, cancellationToken);
    }

    public async Task RemoverFuturosDaSerieAsync(int? nCdSerie, CancellationToken cancellationToken = default)
    {
        if (nCdSerie is not int serie)
            return;

        var futuros = await db.LancamentosFuturos
            .Where(item => item.nCdSerie == serie && item.nCdTipo == TipoLancamentoFuturo.ContaFixa)
            .ToListAsync(cancellationToken);

        db.LancamentosFuturos.RemoveRange(futuros);
    }

    public async Task<int> ExcluirSerieFuturaAsync(int nCdLancamentoFuturo, CancellationToken cancellationToken = default)
    {
        var item = await db.LancamentosFuturos
            .FirstOrDefaultAsync(futuro => futuro.nCdLancamentoFuturo == nCdLancamentoFuturo, cancellationToken);
        if (item is null)
            return 0;

        if (item.nCdSerie is not int serie)
        {
            db.LancamentosFuturos.Remove(item);
            return 1;
        }

        var serieItens = await db.LancamentosFuturos
            .Where(futuro => futuro.nCdSerie == serie)
            .ToListAsync(cancellationToken);
        db.LancamentosFuturos.RemoveRange(serieItens);

        var lancamentos = await db.Lancamentos
            .Where(lancamento => lancamento.nCdSerie == serie)
            .ToListAsync(cancellationToken);
        foreach (var lancamento in lancamentos)
            lancamento.bFlContaFixa = false;

        var gastos = await db.GastosCartao
            .Where(gasto => gasto.nCdSerie == serie)
            .ToListAsync(cancellationToken);
        foreach (var gasto in gastos)
            gasto.bFlContaFixa = false;

        return serieItens.Count;
    }

    public async Task<int> ExcluirSerieGastoCartaoAsync(int nCdGasto, CancellationToken cancellationToken = default)
    {
        var gasto = await db.GastosCartao.FirstOrDefaultAsync(item => item.nCdGasto == nCdGasto, cancellationToken);
        if (gasto is null || gasto.bFlOculto)
            return 0;

        var iguais = await db.GastosCartao
            .Where(item => item.nCdCartao == gasto.nCdCartao
                && !item.bFlOculto
                && item.sNmDescricao == gasto.sNmDescricao
                && item.nNrTotalParcelas == gasto.nNrTotalParcelas)
            .ToListAsync(cancellationToken);

        foreach (var serie in iguais.Where(item => item.nCdSerie is not null).Select(item => item.nCdSerie!.Value).Distinct())
            await RemoverFuturosDaSerieAsync(serie, cancellationToken);

        foreach (var item in iguais)
        {
            item.bFlContaFixa = false;
            if (item.bFlManual)
                db.GastosCartao.Remove(item);
            else
                item.bFlOculto = true;
        }

        return iguais.Count;
    }

    public async Task PropagarCategoriaAsync(
        int nCdCategoria,
        string nomeCategoria,
        int? nCdSerie,
        int? nCdCartao = null,
        string? sNmDescricao = null,
        int? nNrTotalParcelas = null,
        CancellationToken cancellationToken = default)
    {
        nomeCategoria = nomeCategoria.Trim();
        if (nomeCategoria.Length == 0)
            return;

        if (nCdSerie is int serie)
        {
            var lancamentos = await db.Lancamentos
                .Where(item => item.nCdSerie == serie)
                .ToListAsync(cancellationToken);
            foreach (var item in lancamentos)
                item.nCdCategoria = nCdCategoria;

            var futuros = await db.LancamentosFuturos
                .Where(item => item.nCdSerie == serie)
                .ToListAsync(cancellationToken);
            foreach (var item in futuros)
                item.nCdCategoria = nCdCategoria;

            var gastosSerie = await db.GastosCartao
                .Where(item => item.nCdSerie == serie)
                .ToListAsync(cancellationToken);
            foreach (var item in gastosSerie)
            {
                item.sNmCategoria = nomeCategoria;
                item.bFlEditado = true;
            }
        }

        if (nCdCartao is int cartao && !string.IsNullOrWhiteSpace(sNmDescricao))
        {
            var gastos = await db.GastosCartao
                .Where(item => item.nCdCartao == cartao
                    && !item.bFlOculto
                    && item.sNmDescricao == sNmDescricao
                    && item.nNrTotalParcelas == nNrTotalParcelas)
                .ToListAsync(cancellationToken);
            foreach (var item in gastos)
            {
                item.sNmCategoria = nomeCategoria;
                item.bFlEditado = true;
            }
        }
    }

    public async Task PropagarNomeCategoriaAsync(string nomeAntigo, string nomeNovo, CancellationToken cancellationToken = default)
    {
        if (string.Equals(nomeAntigo, nomeNovo, StringComparison.Ordinal))
            return;

        var gastos = await db.GastosCartao
            .Where(item => item.sNmCategoria == nomeAntigo)
            .ToListAsync(cancellationToken);
        foreach (var item in gastos)
            item.sNmCategoria = nomeNovo;
    }

    public async Task<int> ProximaSerieAsync(CancellationToken cancellationToken = default)
    {
        var maiorLancamento = await db.Lancamentos.MaxAsync(item => (int?)item.nCdSerie, cancellationToken) ?? 0;
        var maiorFuturo = await db.LancamentosFuturos.MaxAsync(item => (int?)item.nCdSerie, cancellationToken) ?? 0;
        var maiorGasto = await db.GastosCartao.MaxAsync(item => (int?)item.nCdSerie, cancellationToken) ?? 0;
        return Math.Max(maiorLancamento, Math.Max(maiorFuturo, maiorGasto)) + 1;
    }

    public static bool MesesValidos(int meses) => meses is >= MesesMinimos and <= MesesMaximos;

    private async Task<int> CriarFuturosAsync(
        int serie,
        int nCdConta,
        int nCdCategoria,
        string apelido,
        decimal valor,
        DateOnly baseMes,
        int dia,
        int meses,
        bool comecarNesteMes,
        CancellationToken cancellationToken)
    {
        var inicio = comecarNesteMes ? 0 : 1;
        var fim = comecarNesteMes ? meses - 1 : meses;
        var criados = 0;
        var agora = Calendario.Agora();

        for (var deslocamento = inicio; deslocamento <= fim; deslocamento++)
        {
            var mes = baseMes.AddMonths(deslocamento);
            var vencimento = DiaDoMes(mes, dia);
            var jaExiste = await db.LancamentosFuturos.AnyAsync(
                item => item.nCdSerie == serie
                    && item.tDtVencimento.Year == vencimento.Year
                    && item.tDtVencimento.Month == vencimento.Month,
                cancellationToken);

            if (jaExiste)
                continue;

            db.LancamentosFuturos.Add(new LancamentoFuturo
            {
                nCdConta = nCdConta,
                nCdCategoria = nCdCategoria,
                tDtVencimento = vencimento,
                sNmLancamento = apelido,
                dVlLancamento = valor,
                nCdTipo = TipoLancamentoFuturo.ContaFixa,
                nCdSerie = serie,
                tDtCriacao = agora
            });
            criados++;
        }

        return criados;
    }

    private static DateOnly DiaDoMes(DateOnly mes, int dia)
    {
        var limite = DateTime.DaysInMonth(mes.Year, mes.Month);
        return new DateOnly(mes.Year, mes.Month, Math.Clamp(dia, 1, limite));
    }
}
