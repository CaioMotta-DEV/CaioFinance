using System.Globalization;
using System.Text.Json;
using CaioFinance.Data;
using CaioFinance.Models;
using CaioFinance.Resources;
using CaioFinance.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CaioFinance.Pages.Dashboard;

public class IndexModel(CaioFinanceContexto db, ServicoContaFixa contasFixas) : PageModel
{
    public int Ano { get; private set; }
    public int Mes { get; private set; }
    public decimal SaldoTotal { get; private set; }
    public decimal GastosDoMes { get; private set; }
    public decimal EntradasDoMes { get; private set; }
    public IReadOnlyList<Lancamento> Extrato { get; private set; } = [];
    public IReadOnlyList<Categoria> Categorias { get; private set; } = [];
    public IReadOnlyList<ContaBanco> Contas { get; private set; } = [];
    public string GraficoJson { get; private set; } = "{}";

    public DateOnly MesSelecionado => new(Ano, Mes, 1);
    public DateOnly MesAnterior => MesSelecionado.AddMonths(-1);
    public DateOnly ProximoMes => MesSelecionado.AddMonths(1);
    public string TituloDoMes => Calendario.TituloDoMes(MesSelecionado);

    public async Task OnGetAsync(int? ano, int? mes)
    {
        DefinirMes(ano, mes);
        await CarregarAsync();
    }

    public async Task<IActionResult> OnPostSalvarAsync(
        int nCdLancamento,
        string? data,
        string? sNmExibicao,
        string? tipo,
        string? valor,
        int nCdCategoria,
        int nCdConta,
        int ano,
        int mes)
    {
        if (!DateOnly.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ocorrencia)
            || ocorrencia.Year is < 2000 or > 2100)
        {
            TempData["Erro"] = Textos.DataInvalida;
            return RedirectToPage(new { ano, mes });
        }

        sNmExibicao = (sNmExibicao ?? "").Trim();
        if (sNmExibicao.Length is 0 or > 160)
        {
            TempData["Erro"] = Textos.NomeExibicaoObrigatorio;
            return RedirectToPage(new { ano, mes });
        }

        if (tipo is not ("gasto" or "entrada"))
        {
            TempData["Erro"] = Textos.TipoInvalido;
            return RedirectToPage(new { ano, mes });
        }

        if (!LeitorValor.TentarLer(valor, out var quantia) || quantia <= 0 || quantia > 1_000_000m)
        {
            TempData["Erro"] = Textos.ValorInvalido;
            return RedirectToPage(new { ano, mes });
        }

        var lancamento = await db.Lancamentos.FirstOrDefaultAsync(item => item.nCdLancamento == nCdLancamento);
        if (lancamento is null || lancamento.bFlOculto)
        {
            TempData["Erro"] = Textos.LancamentoNaoEncontrado;
            return RedirectToPage(new { ano, mes });
        }

        if (!await db.Categorias.AnyAsync(categoria => categoria.nCdCategoria == nCdCategoria)
            || !await db.ContasBanco.AnyAsync(conta => conta.nCdConta == nCdConta))
        {
            TempData["Erro"] = Textos.ContaCategoriaObrigatorias;
            return RedirectToPage(new { ano, mes });
        }

        var comSinal = tipo == "gasto" ? -Math.Abs(quantia) : Math.Abs(quantia);
        var mudouDadoDoBanco = lancamento.tDtOcorrencia != ocorrencia
            || lancamento.dVlLancamento != comSinal
            || lancamento.nCdConta != nCdConta;
        var mudouCategoria = lancamento.nCdCategoria != nCdCategoria;

        lancamento.tDtOcorrencia = ocorrencia;
        lancamento.sNmExibicao = sNmExibicao;
        lancamento.dVlLancamento = comSinal;
        lancamento.nCdCategoria = nCdCategoria;
        lancamento.nCdConta = nCdConta;
        if (mudouDadoDoBanco)
            lancamento.nCdOrigem = OrigemLancamento.Manual;

        if (mudouCategoria)
        {
            var nomeCategoria = await db.Categorias
                .AsNoTracking()
                .Where(item => item.nCdCategoria == nCdCategoria)
                .Select(item => item.sNmCategoria)
                .FirstAsync();
            await contasFixas.PropagarCategoriaAsync(nCdCategoria, nomeCategoria, lancamento.nCdSerie);
        }

        await db.SaveChangesAsync();

        TempData["Ok"] = Textos.LancamentoAtualizado;
        return RedirectToPage(new { ano = ocorrencia.Year, mes = ocorrencia.Month });
    }

    public async Task<IActionResult> OnPostExcluirAsync(int nCdLancamento, int ano, int mes)
    {
        var lancamento = await db.Lancamentos.FirstOrDefaultAsync(item => item.nCdLancamento == nCdLancamento);
        if (lancamento is null || lancamento.bFlOculto)
        {
            TempData["Erro"] = Textos.LancamentoNaoEncontrado;
            return RedirectToPage(new { ano, mes });
        }

        if (lancamento.bFlContaFixa)
            await contasFixas.DesmarcarAsync(lancamento);

        if (string.IsNullOrWhiteSpace(lancamento.sIdTransacaoPluggy))
            db.Lancamentos.Remove(lancamento);
        else
            lancamento.bFlOculto = true;

        await db.SaveChangesAsync();
        TempData["Ok"] = Textos.LancamentoExcluido;
        return RedirectToPage(new { ano, mes });
    }

    public async Task<IActionResult> OnPostMarcarContaFixaAsync(
        int nCdLancamento,
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

        var lancamento = await db.Lancamentos.FirstOrDefaultAsync(item => item.nCdLancamento == nCdLancamento);
        if (lancamento is null || lancamento.bFlOculto || lancamento.dVlLancamento == 0)
        {
            TempData["Erro"] = Textos.LancamentoNaoEncontrado;
            return RedirectToPage(new { ano, mes });
        }

        if (lancamento.bFlContaFixa)
        {
            TempData["Erro"] = Textos.JaEhContaFixa;
            return RedirectToPage(new { ano, mes });
        }

        var comecarNesteMes = string.Equals(inicio, "este", StringComparison.OrdinalIgnoreCase);
        var criados = await contasFixas.MarcarAsync(lancamento, apelido, quantia, meses, comecarNesteMes);
        await db.SaveChangesAsync();

        TempData["Ok"] = criados == 0 ? Textos.ContaFixaJaPrevista : Textos.ContaFixaMarcada;
        return RedirectToPage(new { ano, mes });
    }

    public async Task<IActionResult> OnPostDesmarcarContaFixaAsync(int nCdLancamento, int ano, int mes)
    {
        var lancamento = await db.Lancamentos.FirstOrDefaultAsync(item => item.nCdLancamento == nCdLancamento);
        if (lancamento is null || lancamento.bFlOculto)
        {
            TempData["Erro"] = Textos.LancamentoNaoEncontrado;
            return RedirectToPage(new { ano, mes });
        }

        if (!lancamento.bFlContaFixa)
        {
            TempData["Erro"] = Textos.NaoEhContaFixa;
            return RedirectToPage(new { ano, mes });
        }

        await contasFixas.DesmarcarAsync(lancamento);
        await db.SaveChangesAsync();

        TempData["Ok"] = Textos.ContaFixaDesmarcada;
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

        Categorias = await db.Categorias
            .AsNoTracking()
            .OrderBy(categoria => categoria.nNrOrdem)
            .ToListAsync();

        var contas = await db.ContasBanco
            .AsNoTracking()
            .OrderBy(conta => conta.sNmInstituicao)
            .ToListAsync();

        var usarPluggy = contas.Any(conta => !string.IsNullOrWhiteSpace(conta.sIdItemPluggy));
        if (usarPluggy)
            contas = contas.Where(conta => !string.IsNullOrWhiteSpace(conta.sIdItemPluggy)).ToList();

        Contas = contas;

        SaldoTotal = contas
            .Where(conta => conta.nCdSituacao == SituacaoConta.Conectado)
            .Sum(conta => conta.dVlSaldo);

        var consultaMes = db.Lancamentos
            .AsNoTracking()
            .Include(item => item.Categoria)
            .Include(item => item.Conta)
            .Where(item => !item.bFlOculto && item.tDtOcorrencia >= inicio && item.tDtOcorrencia < fim);

        if (usarPluggy)
            consultaMes = consultaMes.Where(item => item.Conta.sIdItemPluggy != null);

        var doMes = await consultaMes.ToListAsync();

        Extrato = doMes
            .OrderByDescending(item => item.tDtOcorrencia)
            .ThenByDescending(item => item.tDtCriacao)
            .ToList();

        GastosDoMes = doMes.Where(item => item.dVlLancamento < 0).Sum(item => item.dVlLancamento);
        EntradasDoMes = doMes.Where(item => item.dVlLancamento > 0).Sum(item => item.dVlLancamento);

        var fatias = doMes
            .Where(item => item.dVlLancamento < 0)
            .GroupBy(item => item.Categoria.sNmCategoria)
            .Select(grupo => new FatiaCategoria(grupo.Key, Math.Abs(grupo.Sum(item => item.dVlLancamento))))
            .Where(fatia => fatia.Valor > 0)
            .OrderByDescending(fatia => fatia.Valor)
            .ToList();

        await MontarGraficoAsync(fatias);
    }

    private async Task MontarGraficoAsync(IReadOnlyList<FatiaCategoria> fatias)
    {
        // Ultimos 12 meses ate o mes selecionado (inclusive).
        var fimExclusivo = MesSelecionado.AddMonths(1);
        var inicioSerie = MesSelecionado.AddMonths(-11);

        var usarPluggy = await db.ContasBanco.AnyAsync(conta => conta.sIdItemPluggy != null);
        var consulta = db.Lancamentos
            .AsNoTracking()
            .Where(item => !item.bFlOculto && item.tDtOcorrencia >= inicioSerie && item.tDtOcorrencia < fimExclusivo);

        if (usarPluggy)
            consulta = consulta.Where(item => item.Conta.sIdItemPluggy != null);

        var lancamentos = await consulta
            .Select(item => new { item.tDtOcorrencia, item.dVlLancamento })
            .ToListAsync();

        var rotulos = new List<string>();
        var gastos = new List<decimal>();
        var entradas = new List<decimal>();

        for (var deslocamento = 0; deslocamento < 12; deslocamento++)
        {
            var mes = inicioSerie.AddMonths(deslocamento);
            var proximo = mes.AddMonths(1);
            var doPeriodo = lancamentos
                .Where(item => item.tDtOcorrencia >= mes && item.tDtOcorrencia < proximo)
                .ToList();

            var gasto = Math.Abs(doPeriodo.Where(item => item.dVlLancamento < 0).Sum(item => item.dVlLancamento));
            var entrada = doPeriodo.Where(item => item.dVlLancamento > 0).Sum(item => item.dVlLancamento);

            rotulos.Add(mes.ToString("MMM/yy"));
            gastos.Add(gasto);
            entradas.Add(entrada);
        }

        GraficoJson = JsonSerializer.Serialize(new
        {
            labels = rotulos,
            gastos,
            entradas,
            categorias = fatias.Select(fatia => new { nome = fatia.Nome, valor = fatia.Valor })
        });
    }

    private sealed record FatiaCategoria(string Nome, decimal Valor);
}
