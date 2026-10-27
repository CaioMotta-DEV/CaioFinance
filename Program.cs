using System.Globalization;
using System.Net.Http.Headers;
using CaioFinance.Data;
using CaioFinance.Services;
using CaioFinance.Services.Acesso;
using CaioFinance.Services.Pluggy;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
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

builder.Services.AddRazorPages(paginas =>
{
    paginas.Conventions.AuthorizeFolder("/");
    paginas.Conventions.AllowAnonymousToPage("/Login/Index");
    paginas.Conventions.AllowAnonymousToPage("/Error");
});
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(cookies =>
    {
        cookies.LoginPath = "/login";
        cookies.AccessDeniedPath = "/login";
        cookies.ExpireTimeSpan = TimeSpan.FromDays(30);
        cookies.SlidingExpiration = true;
        cookies.Cookie.Name = "CaioFinance.Sessao";
        cookies.Cookie.HttpOnly = true;
        cookies.Cookie.SameSite = SameSiteMode.Lax;
        // Em localhost (desenvolvimento) o acesso é http, então o cookie não pode exigir https.
        // Hospedado (produção) o cookie só trafega por https.
        cookies.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });
builder.Services.AddMemoryCache();
builder.Services.Configure<OpcoesAcesso>(builder.Configuration.GetSection(OpcoesAcesso.Secao));
builder.Services.AddSingleton<ServicoAcesso>();
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

// Hospedado atrás de um proxy (Render, Cloudflare, etc.), lê o esquema e o IP originais
// dos cabeçalhos X-Forwarded-*, para o HTTPS e o bloqueio por IP funcionarem.
builder.Services.Configure<ForwardedHeadersOptions>(opcoes =>
{
    opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opcoes.KnownNetworks.Clear();
    opcoes.KnownProxies.Clear();
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
{
    app.UseForwardedHeaders();
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

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
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.Run();
