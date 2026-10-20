using System.Globalization;
using CaioFinance.Data;
using CaioFinance.Models;
using CaioFinance.Resources;
using CaioFinance.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CaioFinance.Pages.LancamentosFuturos;

public class IndexModel(CaioFinanceContexto db, ServicoContaFixa contasFixas, ServicoFuturoCartao futurosCartao) : PageModel
{
    public int Ano { get; private set; }
    public int Mes { get; private set; }
    public decimal TotalReceitas { get; private set; }
    public decimal TotalDespesas { get; private set; }
    public decimal GastoReal { get; private set; }
    public IReadOnlyList<LancamentoFuturo> Receitas { get; private set; } = [];
    public IReadOnlyList<LancamentoFuturo> Despesas { get; private set; } = [];
    public IReadOnlyList<Categoria> Categorias { get; private set; } = [];
    public IReadOnlyList<ContaBanco> Contas { get; private set; } = [];

    public DateOnly MesSelecionado => new(Ano, Mes, 1);
    public DateOnly MesAnterior => MesSelecionado.AddMonths(-1);
    public DateOnly ProximoMes => MesSelecionado.AddMonths(1);
    public string TituloDoMes => Calendario.TituloDoMes(MesSelecionado);

    public DateOnly DataSugerida
    {
        get
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            return hoje.Year == Ano && hoje.Month == Mes ? hoje : MesSelecionado;
        }
    }

    public async Task OnGetAsync(int? ano, int? mes)
    {
        DefinirMes(ano, mes);
        await CarregarAsync();
    }

    public async Task<IActionResult> OnPostAtualizarAsync(int ano, int mes)
    {
        var incluidos = await futurosCartao.SincronizarAsync();
        TempData["Ok"] = incluidos == 0
            ? "Nenhum gasto de cartão novo para os próximos meses."
            : $"{incluidos} gasto(s) de cartão colocados no mês da fatura.";
        return RedirectToPage(new { ano, mes });
    }

    public async Task<IActionResult> OnPostIncluirAsync(
        string? data,
        string? nome,
        string? tipo,
        string? valor,
        int nCdConta,
        int nCdCategoria,
        int meses,
        int ano,
        int mes)
    {
        if (!DateOnly.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var vencimento)
            || vencimento.Year is < 2000 or > 2100)
        {
            TempData["Erro"] = Textos.DataInvalida;
            return RedirectToPage(new { ano, mes });
        }

        var mesAtual = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (vencimento < mesAtual)
        {
            TempData["Erro"] = Textos.FuturoAntesDoMesAtual;
            return RedirectToPage(new { ano, mes });
        }

        nome = (nome ?? "").Trim();
        if (nome.Length is 0 or > 160)
        {
            TempData["Erro"] = Textos.NomeObrigatorio;
            return RedirectToPage(new { ano, mes });
        }

        if (!LeitorValor.TentarLer(valor, out var quantia) || quantia == 0 || Math.Abs(quantia) > 1_000_000m)
        {
            TempData["Erro"] = Textos.ValorInvalido;
            return RedirectToPage(new { ano, mes });
        }

        if (tipo is not ("gasto" or "entrada"))
        {
            TempData["Erro"] = Textos.TipoInvalido;
            return RedirectToPage(new { ano, mes });
        }

        if (meses is < 1 or > 60)
        {
            TempData["Erro"] = Textos.RepeticaoInvalida;
            return RedirectToPage(new { ano, mes });
        }

        if (!await db.ContasBanco.AnyAsync(conta => conta.nCdConta == nCdConta)
            || !await db.Categorias.AnyAsync(categoria => categoria.nCdCategoria == nCdCategoria))
        {
            TempData["Erro"] = Textos.ContaCategoriaObrigatorias;
            return RedirectToPage(new { ano, mes });
        }

        var comSinal = tipo == "gasto" ? -Math.Abs(quantia) : Math.Abs(quantia);
        var repetir = meses > 1;
        int? serie = repetir ? await contasFixas.ProximaSerieAsync() : null;
        var agora = Calendario.Agora();

        for (var deslocamento = 0; deslocamento < meses; deslocamento++)
        {
            db.LancamentosFuturos.Add(new LancamentoFuturo
            {
                nCdConta = nCdConta,
                nCdCategoria = nCdCategoria,
                tDtVencimento = vencimento.AddMonths(deslocamento),
                sNmLancamento = nome,
                dVlLancamento = comSinal,
                nCdTipo = repetir ? TipoLancamentoFuturo.ContaFixa : TipoLancamentoFuturo.Avulso,
                nCdSerie = serie,
                tDtCriacao = agora
            });
        }

        await db.SaveChangesAsync();
        TempData["Ok"] = repetir ? $"Lançamento repetido por {meses} meses." : Textos.FuturoIncluido;
        return RedirectToPage(new { ano = vencimento.Year, mes = vencimento.Month });
    }

    public async Task<IActionResult> OnPostSalvarAsync(
        int nCdLancamentoFuturo,
        string? data,
        string? nome,
        string? tipo,
        string? valor,
        int nCdConta,
        int nCdCategoria,
        int ano,
        int mes)
    {
        if (!DateOnly.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var vencimento)
            || vencimento.Year is < 2000 or > 2100)
        {
            TempData["Erro"] = Textos.DataInvalida;
            return RedirectToPage(new { ano, mes });
        }

        nome = (nome ?? "").Trim();
        if (nome.Length is 0 or > 160)
        {
            TempData["Erro"] = Textos.NomeObrigatorio;
            return RedirectToPage(new { ano, mes });
        }

        if (!LeitorValor.TentarLer(valor, out var quantia) || quantia <= 0 || quantia > 1_000_000m)
        {
            TempData["Erro"] = Textos.ValorInvalido;
            return RedirectToPage(new { ano, mes });
        }

        if (tipo is not ("gasto" or "entrada"))
        {
            TempData["Erro"] = Textos.TipoInvalido;
            return RedirectToPage(new { ano, mes });
        }

        if (!await db.ContasBanco.AnyAsync(conta => conta.nCdConta == nCdConta)
            || !await db.Categorias.AnyAsync(categoria => categoria.nCdCategoria == nCdCategoria))
        {
            TempData["Erro"] = Textos.ContaCategoriaObrigatorias;
            return RedirectToPage(new { ano, mes });
        }

        var item = await db.LancamentosFuturos.FirstOrDefaultAsync(futuro => futuro.nCdLancamentoFuturo == nCdLancamentoFuturo);
        if (item is null)
        {
            TempData["Erro"] = Textos.FuturoNaoEncontrado;
            return RedirectToPage(new { ano, mes });
        }

        var mudouCategoria = item.nCdCategoria != nCdCategoria;
        item.tDtVencimento = vencimento;
        item.sNmLancamento = nome;
        item.dVlLancamento = tipo == "gasto" ? -Math.Abs(quantia) : Math.Abs(quantia);
        item.nCdConta = nCdConta;
        item.nCdCategoria = nCdCategoria;

        if (mudouCategoria)
        {
            var nomeCategoria = await db.Categorias
                .AsNoTracking()
                .Where(categoria => categoria.nCdCategoria == nCdCategoria)
                .Select(categoria => categoria.sNmCategoria)
                .FirstAsync();
            await contasFixas.PropagarCategoriaAsync(nCdCategoria, nomeCategoria, item.nCdSerie);
        }

        await db.SaveChangesAsync();
        TempData["Ok"] = "Lançamento futuro atualizado.";
        return RedirectToPage(new { ano = vencimento.Year, mes = vencimento.Month });
    }

    public async Task<IActionResult> OnPostMarcarContaFixaAsync(
        int nCdLancamentoFuturo,
        string? apelido,
        string? valorEstimado,
        int meses,
        string? inicio,
        int ano,
        int mes)
    {
        apelido = (apelido ?? "").Trim();
        if (apelido.Length is 0 or > 160)
        {
            TempData["Erro"] = Textos.ApelidoObrigatorio;
            return RedirectToPage(new { ano, mes });
        }

        if (!LeitorValor.TentarLer(valorEstimado, out var quantia) || quantia <= 0 || quantia > 1_000_000m)
        {
            TempData["Erro"] = Textos.ValorInvalido;
            return RedirectToPage(new { ano, mes });
        }

        if (!ServicoContaFixa.MesesValidos(meses))
        {
            TempData["Erro"] = Textos.MesesContaFixaInvalidos;
            return RedirectToPage(new { ano, mes });
        }

        var item = await db.LancamentosFuturos
            .FirstOrDefaultAsync(futuro => futuro.nCdLancamentoFuturo == nCdLancamentoFuturo);
        if (item is null || item.dVlLancamento == 0)
        {
            TempData["Erro"] = Textos.FuturoNaoEncontrado;
            return RedirectToPage(new { ano, mes });
        }

        if (item.nCdTipo == TipoLancamentoFuturo.ContaFixa && item.nCdSerie is not null)
        {
            TempData["Erro"] = Textos.JaEhContaFixa;
            return RedirectToPage(new { ano, mes });
        }

        var comecarNesteMes = !string.Equals(inicio, "proximo", StringComparison.OrdinalIgnoreCase);
        var criados = await contasFixas.MarcarFuturoAsync(item, apelido, quantia, meses, comecarNesteMes);
        await db.SaveChangesAsync();

        TempData["Ok"] = criados == 0 ? Textos.ContaFixaJaPrevista : Textos.ContaFixaMarcada;
        return RedirectToPage(new { ano, mes });
    }

    public async Task<IActionResult> OnPostRemoverAsync(int nCdLancamentoFuturo, int ano, int mes, string? escopo)
    {
        var item = await db.LancamentosFuturos
            .FirstOrDefaultAsync(futuro => futuro.nCdLancamentoFuturo == nCdLancamentoFuturo);

        if (item is null)
        {
            TempData["Erro"] = Textos.FuturoNaoEncontrado;
            return RedirectToPage(new { ano, mes });
        }

        if (item.nCdTipo == TipoLancamentoFuturo.Cartao)
        {
            var tudo = string.Equals(escopo, "tudo", StringComparison.OrdinalIgnoreCase);
            var removidos = await futurosCartao.RemoverAsync(nCdLancamentoFuturo, tudo);
            TempData["Ok"] = removidos <= 1 ? Textos.FuturoRemovido : Textos.SerieRemovida;
            return RedirectToPage(new { ano, mes });
        }

        if (string.Equals(escopo, "tudo", StringComparison.OrdinalIgnoreCase))
        {
            var removidos = await contasFixas.ExcluirSerieFuturaAsync(nCdLancamentoFuturo);
            await db.SaveChangesAsync();
            TempData["Ok"] = removidos <= 1 ? Textos.FuturoRemovido : Textos.SerieRemovida;
            return RedirectToPage(new { ano, mes });
        }

        var eraContaFixa = item.nCdTipo == TipoLancamentoFuturo.ContaFixa;
        db.LancamentosFuturos.Remove(item);
        await db.SaveChangesAsync();
        TempData["Ok"] = eraContaFixa ? Textos.OcorrenciaRemovida : Textos.FuturoRemovido;
        return RedirectToPage(new { ano, mes });
    }

    private void DefinirMes(int? ano, int? mes)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        if (ano is >= 2000 and <= 2100 && mes is >= 1 and <= 12)
        {
            Ano = ano.Value;
            Mes = mes.Value;
            return;
        }

        Ano = hoje.Year;
        Mes = hoje.Month;
    }

    private async Task CarregarAsync()
    {
        var inicio = MesSelecionado;
        var fim = inicio.AddMonths(1);

        Contas = await db.ContasBanco
            .AsNoTracking()
            .OrderBy(conta => conta.sNmInstituicao)
            .ToListAsync();

        Categorias = await db.Categorias
            .AsNoTracking()
            .OrderBy(categoria => categoria.nNrOrdem)
            .ToListAsync();

        var itens = await db.LancamentosFuturos
            .AsNoTracking()
            .Include(item => item.Conta)
            .Include(item => item.Categoria)
            .Where(item => item.nCdTipo != TipoLancamentoFuturo.Ignorado)
            .Where(item => item.tDtVencimento >= inicio && item.tDtVencimento < fim)
            .OrderByDescending(item => item.tDtVencimento)
            .ThenBy(item => item.sNmLancamento)
            .ToListAsync();

        Receitas = Ordenar(itens.Where(item => item.dVlLancamento > 0));
        Despesas = Ordenar(itens.Where(item => item.dVlLancamento < 0));
        TotalReceitas = Receitas.Sum(item => item.dVlLancamento);
        TotalDespesas = Math.Abs(Despesas.Sum(item => item.dVlLancamento));
        GastoReal = TotalDespesas - TotalReceitas;
    }

    private static List<LancamentoFuturo> Ordenar(IEnumerable<LancamentoFuturo> origem) =>
        origem
            .OrderBy(item => item.nCdTipo == TipoLancamentoFuturo.ContaFixa ? 0 : 1)
            .ThenBy(item => item.sNmLancamento, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.tDtVencimento)
            .ToList();
}
