namespace CaioFinance.Services;

public static class MapaCategoriaCartao
{
    private static readonly Dictionary<string, string> Traducoes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Groceries"] = "Alimentação",
        ["Food and drinks"] = "Alimentação",
        ["Eating out"] = "Alimentação",
        ["Food delivery"] = "Alimentação",
        ["Automotive"] = "Transporte",
        ["Gas stations"] = "Transporte",
        ["Public transportation"] = "Transporte",
        ["Taxi and ride-hailing"] = "Transporte",
        ["Telecommunications"] = "Assinaturas",
        ["Digital services"] = "Assinaturas",
        ["Services"] = "Assinaturas",
        ["Subscriptions"] = "Assinaturas",
        ["Housing"] = "Moradia",
        ["Rent"] = "Moradia",
        ["Pharmacy"] = "Saúde",
        ["Hospital clinics and labs"] = "Saúde",
        ["Health"] = "Saúde",
        ["Pet supplies and vet"] = "Saúde",
        ["Entertainment"] = "Lazer",
        ["Sports goods"] = "Lazer",
        ["Shopping"] = "Lazer",
        ["Clothing"] = "Lazer",
        ["Electronics"] = "Lazer",
        ["Online shopping"] = "Lazer",
        ["Kids and toys"] = "Lazer",
        ["Houseware"] = "Outros",
        ["Office supplies"] = "Outros"
    };

    public static string Traduzir(string? categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria))
            return "Outros";

        return Traducoes.TryGetValue(categoria.Trim(), out var nome) ? nome : "Outros";
    }
}
