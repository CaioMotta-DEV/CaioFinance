using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CaioFinance.Services.Pluggy;

public sealed record ConectorPluggy(int Id, string Nome, bool Sandbox, bool OpenFinance, string Grupo);

public sealed class ClientePluggy
{
    public const string NomeHttp = "pluggy";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory fabrica;
    private readonly OpcoesPluggy opcoes;
    private readonly IMemoryCache cache;
    private readonly ILogger<ClientePluggy> log;
    private readonly SemaphoreSlim trava = new(1, 1);
    private string? chave;
    private DateTime chaveExpiraEm;

    public ClientePluggy(
        IHttpClientFactory fabrica,
        IOptions<OpcoesPluggy> opcoes,
        IMemoryCache cache,
        ILogger<ClientePluggy> log)
    {
        this.fabrica = fabrica;
        this.opcoes = opcoes.Value;
        this.cache = cache;
        this.log = log;
    }

    public bool Configurado =>
        Guid.TryParse(opcoes.ClientId, out _) && !string.IsNullOrWhiteSpace(opcoes.ClientSecret);

    public bool IncluirSandbox => opcoes.IncluirSandbox;

    public async Task<IReadOnlyList<ConectorPluggy>> ListarBancosAsync(CancellationToken cancellationToken = default)
    {
        var cacheChave = $"pluggy-bancos-{IncluirSandbox}";
        if (cache.TryGetValue(cacheChave, out IReadOnlyList<ConectorPluggy>? emCache) && emCache is not null)
            return emCache;

        var lote = await EnviarAsync<Pagina<ConectorDto>>(
            HttpMethod.Get,
            "connectors?name=MeuPluggy&sandbox=false&pageSize=20",
            null,
            cancellationToken);

        var bancos = lote.Results
            .Where(EhMeuPluggy)
            .GroupBy(item => item.Id)
            .Select(grupo => grupo.First())
            .Select(ParaConector)
            .ToList();

        if (bancos.Count == 0)
            bancos.Add(new ConectorPluggy(200, "MeuPluggy", false, false, "Meu Pluggy"));

        cache.Set(cacheChave, bancos, TimeSpan.FromMinutes(15));
        return bancos;
    }

    public async Task<string> CriarTokenAsync(string? sIdItem = null, CancellationToken cancellationToken = default)
    {
        object corpo = string.IsNullOrWhiteSpace(sIdItem)
            ? new { }
            : new { itemId = sIdItem.Trim() };

        var resposta = await EnviarAsync<TokenResposta>(HttpMethod.Post, "connect_token", corpo, cancellationToken);
        if (string.IsNullOrWhiteSpace(resposta.AccessToken))
            throw new ExcecaoPluggy("A Pluggy não devolveu o token de conexão.");

        return resposta.AccessToken;
    }

    public async Task<ItemPluggy> AguardarItemAsync(string sIdItem, CancellationToken cancellationToken = default)
    {
        for (var tentativa = 0; tentativa < 90; tentativa++)
        {
            var item = await ObterItemAsync(sIdItem, cancellationToken);
            if (Concluido(item.Status))
                return item;

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        throw new ExcecaoPluggy("A Pluggy ainda está sincronizando esta conexão. Use Atualizar daqui a pouco.");
    }

    public async Task AtualizarItemAsync(string sIdItem, CancellationToken cancellationToken = default)
    {
        using var resposta = await EnviarBrutoAsync(HttpMethod.Patch, $"items/{sIdItem}", new { }, cancellationToken);
        if (resposta.IsSuccessStatusCode || resposta.StatusCode is HttpStatusCode.Conflict)
            return;

        var mensagem = await MensagemAsync(resposta, "Não foi possível atualizar o banco.", cancellationToken);
        // O Meu Pluggy atualiza sozinho e recusa um novo sincronismo manual.
        if (NaoPermiteAtualizacaoManual(mensagem))
            return;

        throw new ExcecaoPluggy(mensagem);
    }

    public async Task ExcluirItemAsync(string sIdItem, CancellationToken cancellationToken = default)
    {
        using var resposta = await EnviarBrutoAsync(HttpMethod.Delete, $"items/{sIdItem}", null, cancellationToken);
        if (resposta.IsSuccessStatusCode || resposta.StatusCode is HttpStatusCode.NotFound)
            return;

        log.LogWarning("Pluggy recusou excluir o item {Status}", (int)resposta.StatusCode);
    }

    public async Task<IReadOnlyList<ContaPluggy>> ListarContasAsync(string sIdItem, CancellationToken cancellationToken = default)
    {
        var contas = new List<ContaPluggy>();
        for (var pagina = 1; pagina <= 20; pagina++)
        {
            var caminho = $"accounts?itemId={Uri.EscapeDataString(sIdItem)}&type=BANK&page={pagina}&pageSize=100";
            var lote = await EnviarAsync<Pagina<ContaPluggy>>(HttpMethod.Get, caminho, null, cancellationToken);
            contas.AddRange(lote.Results.Where(conta =>
                string.Equals(conta.Type, "BANK", StringComparison.OrdinalIgnoreCase)));

            if (pagina >= Math.Max(lote.TotalPages, 1) || lote.Results.Count == 0)
                break;
        }

        return contas;
    }

    public async Task<IReadOnlyList<ContaPluggy>> ListarCartoesAsync(string sIdItem, CancellationToken cancellationToken = default)
    {
        var cartoes = new List<ContaPluggy>();
        for (var pagina = 1; pagina <= 20; pagina++)
        {
            var caminho = $"accounts?itemId={Uri.EscapeDataString(sIdItem)}&type=CREDIT&page={pagina}&pageSize=100";
            var lote = await EnviarAsync<Pagina<ContaPluggy>>(HttpMethod.Get, caminho, null, cancellationToken);
            cartoes.AddRange(lote.Results.Where(conta =>
                string.Equals(conta.Subtype, "CREDIT_CARD", StringComparison.OrdinalIgnoreCase)));

            if (pagina >= Math.Max(lote.TotalPages, 1) || lote.Results.Count == 0)
                break;
        }

        return cartoes;
    }

    public async Task<IReadOnlyList<FaturaPluggy>> ListarFaturasAsync(
        string sIdConta,
        CancellationToken cancellationToken = default)
    {
        var faturas = new List<FaturaPluggy>();
        for (var pagina = 1; pagina <= 20; pagina++)
        {
            var caminho = $"bills?accountId={Uri.EscapeDataString(sIdConta)}&page={pagina}&pageSize=100";
            var lote = await EnviarAsync<Pagina<FaturaPluggy>>(HttpMethod.Get, caminho, null, cancellationToken);
            faturas.AddRange(lote.Results.Where(fatura => !string.IsNullOrWhiteSpace(fatura.Id)));

            if (pagina >= Math.Max(lote.TotalPages, 1) || lote.Results.Count == 0)
                break;
        }

        return faturas;
    }

    public async Task<IReadOnlyList<TransacaoPluggy>> ListarTransacoesAsync(
        string sIdConta,
        DateOnly desde,
        DateOnly ate,
        CancellationToken cancellationToken = default,
        DateTime? criadoDesde = null)
    {
        var transacoes = new List<TransacaoPluggy>();
        var caminho =
            $"v2/transactions?accountId={Uri.EscapeDataString(sIdConta)}&dateFrom={desde:yyyy-MM-dd}&dateTo={ate:yyyy-MM-dd}";
        if (criadoDesde is DateTime marco)
        {
            var utc = DateTime.SpecifyKind(marco, DateTimeKind.Local).ToUniversalTime();
            caminho += "&createdAtFrom=" + Uri.EscapeDataString(utc.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        }

        for (var pagina = 0; pagina < 200 && !string.IsNullOrWhiteSpace(caminho); pagina++)
        {
            var lote = await EnviarAsync<PaginaTransacoes>(HttpMethod.Get, caminho, null, cancellationToken);
            transacoes.AddRange(lote.Results);
            caminho = ProximaPagina(lote.Next);
        }

        return transacoes;
    }

    private async Task<ItemPluggy> ObterItemAsync(string sIdItem, CancellationToken cancellationToken) =>
        await EnviarAsync<ItemPluggy>(HttpMethod.Get, $"items/{sIdItem}", null, cancellationToken);

    private static ConectorPluggy ParaConector(ConectorDto conector)
    {
        var meu = EhMeuPluggy(conector);
        var grupo = meu ? "Meu Pluggy" : conector.IsSandbox ? "Teste" : "Open Finance";
        return new ConectorPluggy(conector.Id, conector.Name, conector.IsSandbox, conector.IsOpenFinance, grupo);
    }

    private static bool EhMeuPluggy(ConectorDto conector) =>
        conector.Id == 200
        || conector.Name.Contains("MeuPluggy", StringComparison.OrdinalIgnoreCase)
        || conector.Name.Contains("Meu Pluggy", StringComparison.OrdinalIgnoreCase);

    private static bool NaoPermiteAtualizacaoManual(string mensagem) =>
        mensagem.Contains("cant be updated", StringComparison.OrdinalIgnoreCase)
        || mensagem.Contains("can't be updated", StringComparison.OrdinalIgnoreCase)
        || mensagem.Contains("cannot be updated", StringComparison.OrdinalIgnoreCase);

    private static bool Concluido(string? status) =>
        status is "UPDATED" or "LOGIN_ERROR" or "OUTDATED" or "WAITING_USER_INPUT";

    private static string? ProximaPagina(string? next)
    {
        if (string.IsNullOrWhiteSpace(next))
            return null;

        if (next.StartsWith("http", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(next, UriKind.Absolute, out var uri))
            return uri.PathAndQuery.TrimStart('/');

        if (next.StartsWith('?'))
            return "v2/transactions" + next;

        return next.TrimStart('/');
    }

    private async Task<T> EnviarAsync<T>(HttpMethod metodo, string caminho, object? corpo, CancellationToken cancellationToken)
    {
        using var resposta = await EnviarBrutoAsync(metodo, caminho, corpo, cancellationToken);
        if (!resposta.IsSuccessStatusCode)
        {
            var mensagem = await MensagemAsync(resposta, "A Pluggy recusou a consulta.", cancellationToken);
            log.LogWarning("Pluggy {Metodo} {Caminho} retornou {Status}", metodo, caminho, (int)resposta.StatusCode);
            throw new ExcecaoPluggy(mensagem);
        }

        var conteudo = await resposta.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
        return conteudo ?? throw new ExcecaoPluggy("A Pluggy devolveu uma resposta vazia.");
    }

    private async Task<HttpResponseMessage> EnviarBrutoAsync(
        HttpMethod metodo,
        string caminho,
        object? corpo,
        CancellationToken cancellationToken)
    {
        for (var tentativa = 0; tentativa < 2; tentativa++)
        {
            var http = fabrica.CreateClient(NomeHttp);
            using var requisicao = new HttpRequestMessage(metodo, caminho);
            requisicao.Headers.TryAddWithoutValidation("X-API-KEY", await ObterChaveAsync(tentativa > 0, cancellationToken));
            if (corpo is not null)
                requisicao.Content = JsonContent.Create(corpo, options: Json);

            var resposta = await http.SendAsync(requisicao, cancellationToken);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized && tentativa == 0)
            {
                resposta.Dispose();
                continue;
            }

            return resposta;
        }

        throw new ExcecaoPluggy("Não foi possível autenticar na Pluggy.");
    }

    private async Task<string> ObterChaveAsync(bool forcar, CancellationToken cancellationToken)
    {
        if (!Configurado)
            throw new ExcecaoPluggy("Informe o Client ID e o Client Secret da Pluggy em appsettings.Development.json, na seção Pluggy.");

        await trava.WaitAsync(cancellationToken);
        try
        {
            if (!forcar && !string.IsNullOrWhiteSpace(chave) && DateTime.UtcNow < chaveExpiraEm)
                return chave;

            var http = fabrica.CreateClient(NomeHttp);
            using var resposta = await http.PostAsJsonAsync("auth", new
            {
                clientId = opcoes.ClientId.Trim(),
                clientSecret = opcoes.ClientSecret.Trim()
            }, Json, cancellationToken);

            if (!resposta.IsSuccessStatusCode)
                throw new ExcecaoPluggy(await MensagemAsync(resposta, "Client ID ou Client Secret da Pluggy inválidos.", cancellationToken));

            var token = await resposta.Content.ReadFromJsonAsync<AuthResposta>(Json, cancellationToken);
            if (string.IsNullOrWhiteSpace(token?.ApiKey))
                throw new ExcecaoPluggy("A Pluggy não devolveu a chave de acesso.");

            chave = token.ApiKey;
            chaveExpiraEm = DateTime.UtcNow.AddMinutes(90);
            return chave;
        }
        finally
        {
            trava.Release();
        }
    }

    private static async Task<string> MensagemAsync(HttpResponseMessage resposta, string padrao, CancellationToken cancellationToken)
    {
        try
        {
            var erro = await resposta.Content.ReadFromJsonAsync<ErroApi>(Json, cancellationToken);
            if (!string.IsNullOrWhiteSpace(erro?.Message))
                return erro.Message;
        }
        catch (JsonException)
        {
            // A Pluggy as vezes devolve texto puro.
        }

        return padrao;
    }

    private sealed class AuthResposta
    {
        public string ApiKey { get; set; } = "";
    }

    private sealed class TokenResposta
    {
        public string AccessToken { get; set; } = "";
    }

    private sealed class ErroApi
    {
        public string? Message { get; set; }
    }

    private sealed class Pagina<T>
    {
        public int TotalPages { get; set; }
        public List<T> Results { get; set; } = [];
    }

    private sealed class PaginaTransacoes
    {
        public List<TransacaoPluggy> Results { get; set; } = [];
        public string? Next { get; set; }
    }

    private sealed class ConectorDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsSandbox { get; set; }
        public bool IsOpenFinance { get; set; }
        public List<string> Products { get; set; } = [];
    }
}

public sealed class ItemPluggy
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ExecutionStatus { get; set; }
    public ErroItemPluggy? Error { get; set; }
    public ConectorItemPluggy? Connector { get; set; }
}

public sealed class ErroItemPluggy
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}

public sealed class ConectorItemPluggy
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class ContaPluggy
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? MarketingName { get; set; }
    public string? Number { get; set; }
    public string Type { get; set; } = "";
    public string? Subtype { get; set; }
    public decimal Balance { get; set; }
    public DadosCreditoPluggy? CreditData { get; set; }
}

public sealed class DadosCreditoPluggy
{
    public string? Brand { get; set; }
    public string? Level { get; set; }
    public DateTime? BalanceDueDate { get; set; }
}

public sealed class FaturaPluggy
{
    public string Id { get; set; } = "";
    public DateTime DueDate { get; set; }
    public DateTime? BillClosingDate { get; set; }
}

public sealed class TransacaoPluggy
{
    public string Id { get; set; } = "";
    public string? Description { get; set; }
    public string? DescriptionRaw { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
    public string? Category { get; set; }
    public MetadadosCartaoPluggy? CreditCardMetadata { get; set; }
}

public sealed class MetadadosCartaoPluggy
{
    public int? InstallmentNumber { get; set; }
    public int? TotalInstallments { get; set; }
    public string? BillId { get; set; }
}
