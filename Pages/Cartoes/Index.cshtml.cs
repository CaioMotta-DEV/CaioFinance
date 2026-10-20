using System.Globalization;
using CaioFinance.Data;
using CaioFinance.Models;
using CaioFinance.Resources;
using CaioFinance.Services;
using CaioFinance.Services.Pluggy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CaioFinance.Pages.Cartoes;

public class IndexModel(CaioFinanceContexto db, ISincronizacaoBanco sincronizacao, ServicoContaFixa contasFixas) : PageModel
{

    public int Ano { get; private set; }
    public int Mes { get; private set; }
    public int? CartaoAberto { get; private set; }
    public IReadOnlyList<CartaoDoMes> Cartoes { get; private set; } = [];
    public IReadOnlyList<GastoCartao> Gastos { get; private set; } = [];
    public IReadOnlyList<Categoria> Categorias { get; private set; } = [];

    public DateOnly MesSelecionado => new(Ano, Mes, 1);
    public DateOnly MesAnterior => MesSelecionado.AddMonths(-1);
    public DateOnly ProximoMes => MesSelecionado.AddMonths(1);
    public string TituloDoMes => Calendario.TituloDoMes(MesSelecionado);
    public decimal TotalDoCartao => Gastos.Sum(gasto => gasto.dVlGasto);

    public async Task OnGetAsync(int? ano, int? mes, int? nCdCartao)
    {
        DefinirMes(ano, mes);
        if (!await db.Cartoes.AnyAsync(cartao => !cartao.bFlOculto))
        {
            try
            {
                await sincronizacao.ImportarCartoesConectadosAsync();
            }
            catch (ExcecaoPluggy ex)
            {
                TempData["Erro"] = ex.Message;
            }
        }

        await CarregarAsync(nCdCartao);
    }

    public async Task<IActionResult> OnPostAtualizarAsync(int ano, int mes, int? nCdCartao)
    {
        try
        {
            TempData["Ok"] = await sincronizacao.ImportarCartoesConectadosAsync();
        }
        catch (ExcecaoPluggy ex)
        {
            TempData["Erro"] = ex.Message;
        }

        return RedirectToPage(new { ano, mes, nCdCartao });
    }

    public async Task<IActionResult> OnPostIncluirCartaoAsync(
        string? instituicao,
        string? nome,
        string? bandeira,
        string? final,
        string? cor,
        int? diaVencimento,
        int ano,
        int mes)
    {
        instituicao = (instituicao ?? "").Trim();
        nome = (nome ?? "").Trim();
        bandeira = (bandeira ?? "").Trim();
        final = new string((final ?? "").Where(char.IsDigit).ToArray());
        if (instituicao.Length is 0 or > 80 || nome.Length is 0 or > 80)
        {
            TempData["Erro"] = Textos.NomeObrigatorio;
            return RedirectToPage(new { ano, mes });
        }

        if (bandeira.Length is 0 or > 40)
            bandeira = "Cartão";
        if (final.Length > 4)
            final = final[^4..];
        if (diaVencimento is < 1 or > 31)
        {
            TempData["Erro"] = Textos.DataInvalida;
            return RedirectToPage(new { ano, mes });
        }

        var cartao = new Cartao
        {
            sIdItemPluggy = "manual",
            sIdContaPluggy = Guid.NewGuid().ToString("N"),
            sNmInstituicao = instituicao,
            sNmCartao = nome,
            sNmBandeira = bandeira,
            sNrFinal = final,
            sDsCor = CorValida(cor),
            nNrDiaVencimento = diaVencimento,
            bFlManual = true,
            tDtAtualizacao = Calendario.Agora()
        };
        db.Cartoes.Add(cartao);
        await db.SaveChangesAsync();
        TempData["Ok"] = "Cartão incluído.";
        return RedirectToPage(new { ano, mes, nCdCartao = cartao.nCdCartao });
    }

    public async Task<IActionResult> OnPostSalvarCartaoAsync(
        int nCdCartao,
        string? apelido,
        int? diaVencimento,
        int ano,
        int mes)
    {
        var cartao = await db.Cartoes.FirstOrDefaultAsync(item => item.nCdCartao == nCdCartao && !item.bFlOculto);
        if (cartao is null)
        {
            TempData["Erro"] = "Cartão não encontrado.";
            return RedirectToPage(new { ano, mes });
        }

        apelido = (apelido ?? "").Trim();
        if (apelido.Length > 80)
        {
            TempData["Erro"] = Textos.ApelidoObrigatorio;
            return RedirectToPage(new { ano, mes, nCdCartao });
        }

        if (diaVencimento is < 1 or > 31)
        {
            TempData["Erro"] = Textos.DataInvalida;
            return RedirectToPage(new { ano, mes, nCdCartao });
        }

        cartao.sNmApelido = apelido.Length == 0 ? null : apelido;
        if (diaVencimento is int dia)
            cartao.nNrDiaVencimento = dia;
        await db.SaveChangesAsync();
        TempData["Ok"] = "Cartão atualizado.";
        return RedirectToPage(new { ano, mes, nCdCartao });
    }

    public async Task<IActionResult> OnPostExcluirCartaoAsync(int nCdCartao, int ano, int mes)
    {
        var cartao = await db.Cartoes.FirstOrDefaultAsync(item => item.nCdCartao == nCdCartao);
        if (cartao is null || cartao.bFlOculto)
        {
            TempData["Erro"] = "Cartão não encontrado.";
            return RedirectToPage(new { ano, mes });
        }

        var gastosDoCartao = await db.GastosCartao.Where(gasto => gasto.nCdCartao == cartao.nCdCartao).ToListAsync();
        foreach (var gastoFixo in gastosDoCartao.Where(gasto => gasto.bFlContaFixa))
            await contasFixas.DesmarcarGastoCartaoAsync(gastoFixo);

        if (cartao.bFlManual)
        {
            db.GastosCartao.RemoveRange(gastosDoCartao);
            db.Cartoes.Remove(cartao);
        }
        else
        {
            cartao.bFlOculto = true;
        }

        await db.SaveChangesAsync();
        TempData["Ok"] = "Cartão excluído.";
        return RedirectToPage(new { ano, mes });
    }

    public async Task<IActionResult> OnPostIncluirGastoAsync(
        int nCdCartao,
        string? dataCompra,
        string? dataFatura,
        string? descricao,
        string? categoria,
        string? valor,
        int? parcela,
        int? totalParcelas,
        int ano,
        int mes)
    {
        var cartao = await db.Cartoes.FirstOrDefaultAsync(item => item.nCdCartao == nCdCartao && !item.bFlOculto);
        if (cartao is null)
        {
            TempData["Erro"] = "Cartão não encontrado.";
            return RedirectToPage(new { ano, mes });
        }

        if (!TentarLerGasto(dataCompra, dataFatura, descricao, categoria, valor, parcela, totalParcelas, out var gasto, out var erro))
        {
            TempData["Erro"] = erro;
            return RedirectToPage(new { ano, mes, nCdCartao });
        }

        gasto.nCdCartao = cartao.nCdCartao;
        gasto.sIdTransacaoPluggy = "m" + Guid.NewGuid().ToString("N");
        gasto.sIdFaturaPluggy = "";
        gasto.bFlManual = true;
        gasto.tDtCriacao = Calendario.Agora();
        var seguintes = ParcelasCartao.MesesSeguintes(gasto);
        db.GastosCartao.Add(gasto);
        db.GastosCartao.AddRange(seguintes);
        await db.SaveChangesAsync();
        TempData["Ok"] = seguintes.Count == 0
            ? "Gasto incluído na fatura."
            : $"Gasto incluído da parcela {gasto.nNrParcela} até {gasto.nNrTotalParcelas}, uma em cada mês.";
        return RedirectToPage(new { ano = gasto.tDtFatura.Year, mes = gasto.tDtFatura.Month, nCdCartao });
    }

    public async Task<IActionResult> OnPostSalvarGastoAsync(
        int nCdGasto,
        string? dataCompra,
        string? dataFatura,
        string? apelido,
        string? categoria,
        string? valor,
        int? parcela,
        int? totalParcelas,
        string? escopo,
        int ano,
        int mes,
        int? nCdCartao)
    {
        var gasto = await db.GastosCartao.FirstOrDefaultAsync(item => item.nCdGasto == nCdGasto && !item.bFlOculto);
        if (gasto is null)
        {
            TempData["Erro"] = "Gasto não encontrado.";
            return RedirectToPage(new { ano, mes, nCdCartao });
        }

        if (!TentarLerGasto(dataCompra, dataFatura, gasto.sNmDescricao, categoria, valor, parcela, totalParcelas, out var lido, out var erro))
        {
            TempData["Erro"] = erro;
            return RedirectToPage(new { ano, mes, nCdCartao = gasto.nCdCartao });
        }

        apelido = (apelido ?? "").Trim();
        if (apelido.Length > 160)
        {
            TempData["Erro"] = Textos.ApelidoObrigatorio;
            return RedirectToPage(new { ano, mes, nCdCartao = gasto.nCdCartao });
        }

        var apelidoSalvo = apelido.Length == 0 ? null : apelido;
        var mudouCategoria = !string.Equals(gasto.sNmCategoria, lido.sNmCategoria, StringComparison.OrdinalIgnoreCase);

        if (string.Equals(escopo, "todos", StringComparison.OrdinalIgnoreCase))
        {
            var iguais = await db.GastosCartao
                .Where(item => item.nCdCartao == gasto.nCdCartao
                    && !item.bFlOculto
                    && item.sNmDescricao == gasto.sNmDescricao
                    && item.nNrTotalParcelas == gasto.nNrTotalParcelas)
                .ToListAsync();

            foreach (var item in iguais)
            {
                item.tDtCompra = lido.tDtCompra;
                item.sNmApelido = apelidoSalvo;
                item.sNmCategoria = lido.sNmCategoria;
                item.dVlGasto = lido.dVlGasto;
                item.bFlEditado = true;
            }

            if (mudouCategoria)
            {
                var nCdCategoria = await CategoriaDoGastoAsync(lido.sNmCategoria);
                await contasFixas.PropagarCategoriaAsync(
                    nCdCategoria,
                    lido.sNmCategoria,
                    gasto.nCdSerie,
                    gasto.nCdCartao,
                    gasto.sNmDescricao,
                    gasto.nNrTotalParcelas);
            }

            await db.SaveChangesAsync();
            TempData["Ok"] = $"Apelido, categoria, data da compra e valor aplicados em {iguais.Count} fatura(s).";
            return RedirectToPage(new { ano, mes, nCdCartao = gasto.nCdCartao });
        }

        gasto.tDtCompra = lido.tDtCompra;
        gasto.tDtFatura = lido.tDtFatura;
        gasto.sNmApelido = apelidoSalvo;
        gasto.sNmCategoria = lido.sNmCategoria;
        gasto.dVlGasto = lido.dVlGasto;
        gasto.nNrParcela = lido.nNrParcela;
        gasto.nNrTotalParcelas = lido.nNrTotalParcelas;
        gasto.bFlEditado = true;

        if (mudouCategoria)
        {
            var nCdCategoria = await CategoriaDoGastoAsync(lido.sNmCategoria);
            await contasFixas.PropagarCategoriaAsync(
                nCdCategoria,
                lido.sNmCategoria,
                gasto.nCdSerie,
                gasto.nCdCartao,
                gasto.sNmDescricao,
                gasto.nNrTotalParcelas);
        }

        await db.SaveChangesAsync();
        TempData["Ok"] = "Gasto atualizado.";
        return RedirectToPage(new { ano = gasto.tDtFatura.Year, mes = gasto.tDtFatura.Month, nCdCartao = gasto.nCdCartao });
    }

    public async Task<IActionResult> OnPostMarcarContaFixaAsync(
        int nCdGasto,
        string? apelido,
        string? valorEstimado,
        int meses,
        string? inicio,
        int ano,
        int mes,
        int? nCdCartao)
    {
        apelido = (apelido ?? "").Trim();
        if (apelido.Length is 0 or > 160)
        {
            TempData["Erro"] = Textos.ApelidoObrigatorio;
            return RedirectToPage(new { ano, mes, nCdCartao });
        }

        if (!LeitorValor.TentarLer(valorEstimado, out var quantia) || quantia <= 0 || quantia > 1_000_000m)
        {
            TempData["Erro"] = Textos.ValorInvalido;
            return RedirectToPage(new { ano, mes, nCdCartao });
        }

        if (!ServicoContaFixa.MesesValidos(meses))
        {
            TempData["Erro"] = Textos.MesesContaFixaInvalidos;
            return RedirectToPage(new { ano, mes, nCdCartao });
        }

        var gasto = await db.GastosCartao
            .Include(item => item.Cartao)
            .FirstOrDefaultAsync(item => item.nCdGasto == nCdGasto && !item.bFlOculto);
        if (gasto is null || gasto.dVlGasto <= 0)
        {
            TempData["Erro"] = "Gasto não encontrado.";
            return RedirectToPage(new { ano, mes, nCdCartao });
        }

        if (gasto.bFlContaFixa)
        {
            TempData["Erro"] = Textos.JaEhContaFixa;
            return RedirectToPage(new { ano, mes, nCdCartao = gasto.nCdCartao });
        }

        var nCdConta = await ContaDoCartaoAsync(gasto.Cartao);
        if (nCdConta is null)
        {
            TempData["Erro"] = Textos.BancoNaoEncontrado;
            return RedirectToPage(new { ano, mes, nCdCartao = gasto.nCdCartao });
        }

        var comecarNesteMes = !string.Equals(inicio, "proximo", StringComparison.OrdinalIgnoreCase);
        var nCdCategoria = await CategoriaDoGastoAsync(gasto.sNmCategoria);
        var criados = await contasFixas.MarcarGastoCartaoAsync(
            gasto,
            nCdConta.Value,
            nCdCategoria,
            apelido,
            quantia,
            meses,
            comecarNesteMes);
        await db.SaveChangesAsync();

        TempData["Ok"] = criados == 0 ? Textos.ContaFixaJaPrevista : Textos.ContaFixaMarcada;
        return RedirectToPage(new { ano, mes, nCdCartao = gasto.nCdCartao });
    }

    public async Task<IActionResult> OnPostDesmarcarContaFixaAsync(int nCdGasto, int ano, int mes, int? nCdCartao)
    {
        var gasto = await db.GastosCartao.FirstOrDefaultAsync(item => item.nCdGasto == nCdGasto && !item.bFlOculto);
        if (gasto is null)
        {
            TempData["Erro"] = "Gasto não encontrado.";
            return RedirectToPage(new { ano, mes, nCdCartao });
        }

        if (!gasto.bFlContaFixa)
        {
            TempData["Erro"] = Textos.NaoEhContaFixa;
            return RedirectToPage(new { ano, mes, nCdCartao = gasto.nCdCartao });
        }

        await contasFixas.DesmarcarGastoCartaoAsync(gasto);
        await db.SaveChangesAsync();
        TempData["Ok"] = Textos.ContaFixaDesmarcada;
        return RedirectToPage(new { ano, mes, nCdCartao = gasto.nCdCartao });
    }

    public async Task<IActionResult> OnPostExcluirGastoAsync(int nCdGasto, int ano, int mes, int? nCdCartao, string? escopo)
    {
        var gasto = await db.GastosCartao.FirstOrDefaultAsync(item => item.nCdGasto == nCdGasto);
        if (gasto is null || gasto.bFlOculto)
        {
            TempData["Erro"] = "Gasto não encontrado.";
            return RedirectToPage(new { ano, mes, nCdCartao });
        }

        var cartaoId = gasto.nCdCartao;
        if (string.Equals(escopo, "tudo", StringComparison.OrdinalIgnoreCase))
        {
            var removidos = await contasFixas.ExcluirSerieGastoCartaoAsync(nCdGasto);
            await db.SaveChangesAsync();
            TempData["Ok"] = removidos <= 1 ? "Gasto excluído." : Textos.SerieRemovida;
            return RedirectToPage(new { ano, mes, nCdCartao = cartaoId });
        }

        if (gasto.bFlContaFixa)
            await contasFixas.DesmarcarGastoCartaoAsync(gasto);

        if (gasto.bFlManual)
            db.GastosCartao.Remove(gasto);
        else
            gasto.bFlOculto = true;

        await db.SaveChangesAsync();
        TempData["Ok"] = "Gasto excluído.";
        return RedirectToPage(new { ano, mes, nCdCartao = cartaoId });
    }

    private async Task<int?> ContaDoCartaoAsync(Cartao cartao)
    {
        var contas = await db.ContasBanco.AsNoTracking().ToListAsync();
        var instituicao = cartao.sNmInstituicao.Trim();
        var ligada = contas.FirstOrDefault(conta =>
            !string.IsNullOrWhiteSpace(conta.sIdItemPluggy)
            && (conta.sNmInstituicao.Contains(instituicao, StringComparison.OrdinalIgnoreCase)
                || instituicao.Contains(conta.NomeExibicao, StringComparison.OrdinalIgnoreCase)
                || conta.NomeExibicao.Contains(instituicao, StringComparison.OrdinalIgnoreCase)));

        return (ligada ?? contas.FirstOrDefault(conta => !string.IsNullOrWhiteSpace(conta.sIdItemPluggy)) ?? contas.FirstOrDefault())
            ?.nCdConta;
    }

    private async Task<int> CategoriaDoGastoAsync(string nome)
    {
        var categoria = await db.Categorias
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.sNmCategoria.ToLower() == nome.ToLower());
        if (categoria is not null)
            return categoria.nCdCategoria;

        var outros = await db.Categorias
            .AsNoTracking()
            .FirstAsync(item => item.sNmCategoria == NomesPadrao.CategoriaOutros);
        return outros.nCdCategoria;
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

    private async Task CarregarAsync(int? nCdCartao)
    {
        var inicio = MesSelecionado;
        var fim = inicio.AddMonths(1);
        Categorias = await db.Categorias.AsNoTracking().OrderBy(categoria => categoria.nNrOrdem).ToListAsync();
        var cartoes = await db.Cartoes
            .AsNoTracking()
            .Where(cartao => !cartao.bFlOculto)
            .OrderBy(cartao => cartao.sNmInstituicao)
            .ThenBy(cartao => cartao.sNmCartao)
            .ToListAsync();

        var gastosDoMes = await db.GastosCartao
            .AsNoTracking()
            .Where(gasto => !gasto.bFlOculto && gasto.tDtFatura >= inicio && gasto.tDtFatura < fim)
            .ToListAsync();

        Cartoes = cartoes
            .Select(cartao => new CartaoDoMes(
                cartao,
                gastosDoMes.Where(gasto => gasto.nCdCartao == cartao.nCdCartao).Sum(gasto => gasto.dVlGasto)))
            .ToList();

        if (nCdCartao is int codigo && cartoes.Any(cartao => cartao.nCdCartao == codigo))
        {
            CartaoAberto = codigo;
            Gastos = gastosDoMes
                .Where(gasto => gasto.nCdCartao == codigo)
                .OrderByDescending(gasto => gasto.tDtCompra)
                .ThenByDescending(gasto => gasto.tDtCriacao)
                .ToList();
        }
    }

    private bool TentarLerGasto(
        string? dataCompra,
        string? dataFatura,
        string? descricao,
        string? categoria,
        string? valor,
        int? parcela,
        int? totalParcelas,
        out GastoCartao gasto,
        out string erro)
    {
        gasto = new GastoCartao();
        erro = "";
        if (!DateOnly.TryParseExact(dataCompra, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var compra)
            || !DateOnly.TryParseExact(dataFatura, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fatura)
            || compra.Year is < 2000 or > 2100
            || fatura.Year is < 2000 or > 2100)
        {
            erro = Textos.DataInvalida;
            return false;
        }

        descricao = (descricao ?? "").Trim();
        categoria = (categoria ?? "").Trim();
        if (descricao.Length is 0 or > 160 || categoria.Length is 0 or > 40)
        {
            erro = Textos.NomeObrigatorio;
            return false;
        }

        if (!LeitorValor.TentarLer(valor, out var quantia) || quantia <= 0 || quantia > 1_000_000m)
        {
            erro = Textos.ValorInvalido;
            return false;
        }

        int? numero = null;
        int? total = null;
        if (totalParcelas is > 1)
        {
            if (parcela is null or < 1 || parcela > totalParcelas || totalParcelas > 48)
            {
                erro = "Informe a parcela e o total de parcelas.";
                return false;
            }

            numero = parcela;
            total = totalParcelas;
        }

        gasto.tDtCompra = compra;
        gasto.tDtFatura = fatura;
        gasto.sNmDescricao = descricao;
        gasto.sNmCategoria = categoria;
        gasto.dVlGasto = quantia;
        gasto.nNrParcela = numero;
        gasto.nNrTotalParcelas = total;
        return true;
    }

    private static string CorValida(string? cor)
    {
        var cores = new[] { "#820AD1", "#EC7000", "#242424", "#009EE3", "#EA4E0F", "#1A1F71", "#0F766E" };
        return cores.Contains(cor) ? cor! : "#0F766E";
    }

    public static string Parcela(GastoCartao gasto) =>
        gasto.nNrParcela is int numero && gasto.nNrTotalParcelas is int total
            ? $"{numero}/{total}"
            : "À vista";
}

public sealed record CartaoDoMes(Cartao Cartao, decimal Total);
