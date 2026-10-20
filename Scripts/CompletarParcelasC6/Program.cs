using Npgsql;

var raiz = new DirectoryInfo(AppContext.BaseDirectory);
while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "CaioFinance.csproj")))
    raiz = raiz.Parent;

if (raiz is null)
    throw new InvalidOperationException("Não encontrei o projeto CaioFinance.");

var json = File.ReadAllText(Path.Combine(raiz.FullName, "appsettings.Development.json"));
var marcador = "\"Default\": \"";
var inicio = json.IndexOf(marcador, StringComparison.Ordinal);
if (inicio < 0)
    throw new InvalidOperationException("ConnectionStrings:Default não encontrada.");
inicio += marcador.Length;
var fim = json.IndexOf('"', inicio);
var conexao = json[inicio..fim];

await using var conn = new NpgsqlConnection(conexao);
await conn.OpenAsync();

var atuais = new List<Gasto>();
await using (var cmd = new NpgsqlCommand("""
    SELECT g."nCdCartao", g."sNmDescricao", g."sNmApelido", g."sNmCategoria",
           g."nNrParcela", g."nNrTotalParcelas", g."tDtCompra", g."tDtFatura", g."dVlGasto", g."sIdFaturaPluggy"
    FROM "GASTO_CARTAO" g
    JOIN "CARTAO" c ON c."nCdCartao" = g."nCdCartao"
    WHERE NOT g."bFlOculto"
      AND g."bFlManual"
      AND c."sNmInstituicao" ILIKE '%c6%'
      AND g."nNrParcela" IS NOT NULL
      AND g."nNrTotalParcelas" > g."nNrParcela"
    """, conn))
await using (var leitor = await cmd.ExecuteReaderAsync())
{
    while (await leitor.ReadAsync())
    {
        atuais.Add(new Gasto(
            leitor.GetInt32(0),
            leitor.GetString(1),
            leitor.IsDBNull(2) ? null : leitor.GetString(2),
            leitor.GetString(3),
            leitor.GetInt32(4),
            leitor.GetInt32(5),
            DateOnly.FromDateTime(leitor.GetDateTime(6)),
            DateOnly.FromDateTime(leitor.GetDateTime(7)),
            leitor.GetDecimal(8),
            leitor.IsDBNull(9) ? "" : leitor.GetString(9)));
    }
}

var chaves = new HashSet<string>(StringComparer.Ordinal);
await using (var cmd = new NpgsqlCommand("""
    SELECT "nCdCartao", "sNmDescricao", "nNrParcela", "nNrTotalParcelas"
    FROM "GASTO_CARTAO"
    WHERE NOT "bFlOculto" AND "nNrParcela" IS NOT NULL
    """, conn))
await using (var leitor = await cmd.ExecuteReaderAsync())
{
    while (await leitor.ReadAsync())
        chaves.Add(Chave(leitor.GetInt32(0), leitor.GetString(1), leitor.GetInt32(2), leitor.GetInt32(3)));
}

var criados = 0;
foreach (var gasto in atuais)
{
    for (var numero = gasto.Parcela + 1; numero <= gasto.Total; numero++)
    {
        var chave = Chave(gasto.Cartao, gasto.Descricao, numero, gasto.Total);
        if (!chaves.Add(chave))
            continue;

        var meses = numero - gasto.Parcela;
        var fatura = gasto.Fatura.AddMonths(meses);
        var compra = gasto.Compra.AddMonths(meses);
        await using var inserir = new NpgsqlCommand("""
            INSERT INTO "GASTO_CARTAO"
                ("nCdCartao", "sIdTransacaoPluggy", "sIdFaturaPluggy", "tDtCompra", "tDtFatura",
                 "sNmDescricao", "sNmApelido", "sNmCategoria", "nNrParcela", "nNrTotalParcelas",
                 "dVlGasto", "bFlManual", "bFlEditado", "bFlOculto", "bFlContaFixa", "tDtCriacao")
            VALUES
                (@cartao, @id, @faturaId, @compra, @fatura, @descricao, @apelido, @categoria,
                 @parcela, @total, @valor, true, false, false, false, @criacao)
            """, conn);
        inserir.Parameters.AddWithValue("cartao", gasto.Cartao);
        inserir.Parameters.AddWithValue("id", "m" + Guid.NewGuid().ToString("N"));
        inserir.Parameters.AddWithValue("faturaId", gasto.FaturaId);
        inserir.Parameters.AddWithValue("compra", compra.ToDateTime(TimeOnly.MinValue));
        inserir.Parameters.AddWithValue("fatura", fatura.ToDateTime(TimeOnly.MinValue));
        inserir.Parameters.AddWithValue("descricao", gasto.Descricao);
        inserir.Parameters.AddWithValue("apelido", (object?)gasto.Apelido ?? DBNull.Value);
        inserir.Parameters.AddWithValue("categoria", gasto.Categoria);
        inserir.Parameters.AddWithValue("parcela", numero);
        inserir.Parameters.AddWithValue("total", gasto.Total);
        inserir.Parameters.AddWithValue("valor", gasto.Valor);
        inserir.Parameters.AddWithValue("criacao", DateTime.Now);
        await inserir.ExecuteNonQueryAsync();
        criados++;
        Console.WriteLine($"{gasto.Descricao} {numero}/{gasto.Total} em {fatura:dd/MM/yyyy}");
    }
}

Console.WriteLine(criados == 0 ? "Nenhuma parcela nova." : $"{criados} parcela(s) incluída(s).");

static string Chave(int cartao, string descricao, int parcela, int total) =>
    $"{cartao}|{descricao.Trim().ToUpperInvariant()}|{parcela}|{total}";

sealed record Gasto(
    int Cartao,
    string Descricao,
    string? Apelido,
    string Categoria,
    int Parcela,
    int Total,
    DateOnly Compra,
    DateOnly Fatura,
    decimal Valor,
    string FaturaId);
