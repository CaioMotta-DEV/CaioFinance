using CaioFinance.Data;
using CaioFinance.Models;
using CaioFinance.Resources;
using CaioFinance.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CaioFinance.Pages.Categorias;

public class IndexModel(CaioFinanceContexto db, ServicoCategoria servicoCategoria, ServicoContaFixa contasFixas) : PageModel
{
    public int Ano { get; private set; }
    public int Mes { get; private set; }
    public IReadOnlyList<LinhaCategoria> Linhas { get; private set; } = [];
    public int? nCdEdicao { get; private set; }
    public string NomeEdicao { get; private set; } = "";

    public DateOnly MesSelecionado => new(Ano, Mes, 1);
    public DateOnly MesAnterior => MesSelecionado.AddMonths(-1);
    public DateOnly ProximoMes => MesSelecionado.AddMonths(1);
    public string TituloDoMes => Calendario.TituloDoMes(MesSelecionado);

    public async Task OnGetAsync(int? ano, int? mes, int? editar)
    {
        DefinirMes(ano, mes);
        await CarregarAsync(editar);
    }

    public async Task<IActionResult> OnPostIncluirAsync(string? nome, int ano, int mes)
    {
        nome = (nome ?? "").Trim();
        if (nome.Length is 0 or > 40)
        {
            TempData["Erro"] = Textos.NomeCategoriaObrigatorio;
            return RedirectToPage(new { ano, mes });
        }

        if (await db.Categorias.AnyAsync(item => item.sNmCategoria.ToLower() == nome.ToLower()))
        {
            TempData["Erro"] = Textos.CategoriaDuplicada;
            return RedirectToPage(new { ano, mes });
        }

        var ordem = await db.Categorias.MaxAsync(item => (int?)item.nNrOrdem) ?? 0;
        db.Categorias.Add(new Categoria
        {
            sNmCategoria = nome,
            nNrOrdem = ordem + 1
        });
        await db.SaveChangesAsync();

        TempData["Ok"] = Textos.CategoriaIncluida;
        return RedirectToPage(new { ano, mes });
    }

    public async Task<IActionResult> OnPostSalvarAsync(int nCdCategoria, string? nome, int ano, int mes)
    {
        nome = (nome ?? "").Trim();
        if (nome.Length is 0 or > 40)
        {
            TempData["Erro"] = Textos.NomeCategoriaObrigatorio;
            return RedirectToPage(new { ano, mes, editar = nCdCategoria });
        }

        var categoria = await db.Categorias.FirstOrDefaultAsync(item => item.nCdCategoria == nCdCategoria);
        if (categoria is null)
        {
            TempData["Erro"] = Textos.CategoriaNaoEncontrada;
            return RedirectToPage(new { ano, mes });
        }

        var duplicada = await db.Categorias.AnyAsync(item =>
            item.nCdCategoria != nCdCategoria
            && item.sNmCategoria.ToLower() == nome.ToLower());

        if (duplicada)
        {
            TempData["Erro"] = Textos.CategoriaDuplicada;
            return RedirectToPage(new { ano, mes, editar = nCdCategoria });
        }

        var nomeAntigo = categoria.sNmCategoria;
        categoria.sNmCategoria = nome;
        await contasFixas.PropagarNomeCategoriaAsync(nomeAntigo, nome);
        await db.SaveChangesAsync();

        TempData["Ok"] = Textos.CategoriaAtualizada;
        return RedirectToPage(new { ano, mes });
    }

    public async Task<IActionResult> OnPostExcluirAsync(int nCdCategoria, int ano, int mes)
    {
        try
        {
            await servicoCategoria.ExcluirComReatribuicaoAsync(nCdCategoria);
            TempData["Ok"] = Textos.CategoriaExcluida;
        }
        catch (InvalidOperationException ex)
        {
            TempData["Erro"] = ex.Message == "A categoria Outros não pode ser excluída."
                ? Textos.NaoExcluirOutros
                : ex.Message;
        }

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

    private async Task CarregarAsync(int? editar)
    {
        var inicio = MesSelecionado;
        var fim = inicio.AddMonths(1);

        var categorias = await db.Categorias
            .AsNoTracking()
            .OrderBy(item => item.nNrOrdem)
            .ThenBy(item => item.sNmCategoria)
            .ToListAsync();

        var totais = await db.Lancamentos
            .AsNoTracking()
            .Where(item => !item.bFlOculto && item.tDtOcorrencia >= inicio && item.tDtOcorrencia < fim)
            .GroupBy(item => item.nCdCategoria)
            .Select(grupo => new
            {
                nCdCategoria = grupo.Key,
                Gastos = grupo.Where(item => item.dVlLancamento < 0).Sum(item => item.dVlLancamento),
                Entradas = grupo.Where(item => item.dVlLancamento > 0).Sum(item => item.dVlLancamento)
            })
            .ToListAsync();

        var mapa = totais.ToDictionary(item => item.nCdCategoria);

        Linhas = categorias
            .Select(categoria =>
            {
                mapa.TryGetValue(categoria.nCdCategoria, out var total);
                return new LinhaCategoria(
                    categoria.nCdCategoria,
                    categoria.sNmCategoria,
                    Math.Abs(total?.Gastos ?? 0m),
                    total?.Entradas ?? 0m,
                    categoria.sNmCategoria == NomesPadrao.CategoriaOutros);
            })
            .ToList();

        if (editar is int codigo && Linhas.Any(item => item.nCdCategoria == codigo))
        {
            nCdEdicao = codigo;
            NomeEdicao = Linhas.First(item => item.nCdCategoria == codigo).sNmCategoria;
        }
    }

    public sealed record LinhaCategoria(
        int nCdCategoria,
        string sNmCategoria,
        decimal dVlGastos,
        decimal dVlEntradas,
        bool bFlOutros);
}
