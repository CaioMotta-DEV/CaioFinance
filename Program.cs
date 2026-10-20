using System.Globalization;
using System.Net.Http.Headers;
using CaioFinance.Data;
using CaioFinance.Services;
using CaioFinance.Services.Pluggy;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var cultura = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = cultura;
CultureInfo.DefaultThreadCurrentUICulture = cultura;

var builder = WebApplication.CreateBuilder(args);

var conexao = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(conexao))
{
    throw new InvalidOperationException(
        "Defina ConnectionStrings:Default com a conexão do Postgres do Supabase. " +
        "Exemplo: Host=db.SEU_PROJETO.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=SUA_SENHA;SSL Mode=Require;Trust Server Certificate=true");
}

builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<CaioFinanceContexto>(opcoes => opcoes.UseNpgsql(conexao));
builder.Services.Configure<OpcoesPluggy>(builder.Configuration.GetSection(OpcoesPluggy.Secao));
builder.Services.AddHttpClient(ClientePluggy.NomeHttp, (provedor, cliente) =>
{
    var opcoes = provedor.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpcoesPluggy>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(opcoes.BaseUrl) ? "https://api.pluggy.ai" : opcoes.BaseUrl.TrimEnd('/');
    cliente.BaseAddress = new Uri(baseUrl + "/");
    cliente.Timeout = TimeSpan.FromSeconds(40);
    cliente.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddSingleton<ClientePluggy>();
builder.Services.AddScoped<ISincronizacaoBanco, SincronizacaoBanco>();
builder.Services.AddScoped<ServicoContaFixa>();
builder.Services.AddScoped<ServicoFuturoCartao>();
builder.Services.AddScoped<ServicoCategoria>();
builder.Services.Configure<RequestLocalizationOptions>(opcoes =>
{
    opcoes.DefaultRequestCulture = new RequestCulture(cultura);
    opcoes.SupportedCultures = [cultura];
    opcoes.SupportedUICultures = [cultura];
});

var app = builder.Build();

if (!string.Equals(Environment.GetEnvironmentVariable("CAIOFINANCE_SEM_BANCO"), "1", StringComparison.Ordinal))
{
    using var escopo = app.Services.CreateScope();
    var db = escopo.ServiceProvider.GetRequiredService<CaioFinanceContexto>();
    await db.Database.MigrateAsync();
    await CargaInicial.CarregarAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.Use(async (contexto, proximo) =>
{
    contexto.Response.OnStarting(() =>
    {
        var cabecalhos = contexto.Response.Headers;
        cabecalhos["X-Content-Type-Options"] = "nosniff";
        cabecalhos["X-Frame-Options"] = "DENY";
        cabecalhos["Referrer-Policy"] = "no-referrer";
        if (contexto.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true)
            cabecalhos.CacheControl = "no-store";
        return Task.CompletedTask;
    });
    await proximo();
});
app.UseRequestLocalization();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.Run();
