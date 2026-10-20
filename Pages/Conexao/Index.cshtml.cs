using System.Text.Json;
using CaioFinance.Data;
using CaioFinance.Models;
using CaioFinance.Resources;
using CaioFinance.Services;
using CaioFinance.Services.Pluggy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CaioFinance.Pages.Conexao;

public class IndexModel(CaioFinanceContexto db, ISincronizacaoBanco sincronizacao, ClientePluggy pluggy) : PageModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<ContaBanco> Contas { get; private set; } = [];
    public string BancosJson { get; private set; } = "[]";
    public bool PluggyConfigurada { get; private set; }
    public string? AvisoPluggy { get; private set; }

    public async Task OnGetAsync() => await CarregarAsync();

    public async Task<IActionResult> OnPostTokenAsync(string? sIdItem)
    {
        if (!pluggy.Configurado)
            return JsonErro(Textos.PluggySemCredencial);

        try
        {
            var token = await pluggy.CriarTokenAsync(sIdItem);
            return new JsonResult(new
            {
                ok = true,
                accessToken = token,
                incluirSandbox = pluggy.IncluirSandbox
            });
        }
        catch (ExcecaoPluggy ex)
        {
            return JsonErro(ex.Message);
        }
    }

    public async Task<IActionResult> OnPostConcluirAsync(string? sIdItem)
    {
        try
        {
            var resultado = await sincronizacao.ImportarItemAsync(sIdItem ?? "");
            return new JsonResult(new { ok = true, mensagem = resultado.Mensagem });
        }
        catch (Exception ex) when (ex is ExcecaoPluggy or InvalidOperationException)
        {
            return JsonErro(ex.Message);
        }
    }

    public async Task<IActionResult> OnPostExcluirAsync(int nCdConta)
    {
        try
        {
            await sincronizacao.ExcluirConexaoAsync(nCdConta);
            TempData["Ok"] = "Conexão excluída.";
        }
        catch (Exception ex) when (ex is ExcecaoPluggy or InvalidOperationException)
        {
            TempData["Erro"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostApelidoAsync(int nCdConta, string? apelido)
    {
        apelido = (apelido ?? "").Trim();
        if (apelido.Length > 80)
        {
            TempData["Erro"] = Textos.ApelidoObrigatorio;
            return RedirectToPage();
        }

        var conta = await db.ContasBanco.FirstOrDefaultAsync(item => item.nCdConta == nCdConta);
        if (conta is null)
        {
            TempData["Erro"] = Textos.BancoNaoEncontrado;
            return RedirectToPage();
        }

        conta.sNmApelido = apelido.Length == 0 ? null : apelido;
        await db.SaveChangesAsync();
        TempData["Ok"] = "Apelido atualizado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAtualizarTudoAsync()
    {
        try
        {
            var resultado = await sincronizacao.AtualizarTudoAsync();
            TempData["Ok"] = resultado.Mensagem;
        }
        catch (Exception ex) when (ex is ExcecaoPluggy or InvalidOperationException)
        {
            TempData["Erro"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAtualizarAsync(int nCdConta)
    {
        try
        {
            var resultado = await sincronizacao.AtualizarContaAsync(nCdConta);
            return Responder(true, resultado.Mensagem);
        }
        catch (Exception ex) when (ex is ExcecaoPluggy or InvalidOperationException)
        {
            return Responder(false, ex.Message);
        }
    }

    private async Task CarregarAsync()
    {
        Contas = await db.ContasBanco
            .AsNoTracking()
            .OrderBy(conta => conta.sNmInstituicao)
            .ToListAsync();

        PluggyConfigurada = pluggy.Configurado;
        if (!PluggyConfigurada)
        {
            AvisoPluggy = Textos.PluggySemCredencial;
            return;
        }

        try
        {
            var bancos = await pluggy.ListarBancosAsync();
            BancosJson = JsonSerializer.Serialize(bancos, Json);
            if (bancos.Count == 0)
                AvisoPluggy = "A Pluggy não devolveu bancos disponíveis para esta aplicação.";
        }
        catch (ExcecaoPluggy ex)
        {
            AvisoPluggy = ex.Message;
        }
    }

    private IActionResult Responder(bool ok, string mensagem)
    {
        if (QuerJson)
            return ok
                ? new JsonResult(new { ok = true, mensagem })
                : JsonErro(mensagem);

        TempData[ok ? "Ok" : "Erro"] = mensagem;
        return RedirectToPage();
    }

    private JsonResult JsonErro(string mensagem) =>
        new(new { ok = false, mensagem }) { StatusCode = 400 };

    private bool QuerJson =>
        Request.Headers.Accept.Any(valor => valor?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    public static string Situacao(SituacaoConta situacao) =>
        situacao == SituacaoConta.Conectado ? Textos.Conectado : situacao.ToString();

    public static string Atualizacao(DateTime data) => data.ToString("dd/MM/yyyy HH:mm");
}
