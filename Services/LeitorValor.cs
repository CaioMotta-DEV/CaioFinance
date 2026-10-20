using System.Globalization;

namespace CaioFinance.Services;

public static class LeitorValor
{
    public static bool TentarLer(string? texto, out decimal valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        var limpo = texto.Trim()
            .Replace("R$", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "");

        if (limpo.Length == 0)
            return false;

        var ultimaVirgula = limpo.LastIndexOf(',');
        var ultimoPonto = limpo.LastIndexOf('.');

        if (ultimaVirgula >= 0 && ultimoPonto >= 0)
        {
            limpo = ultimaVirgula > ultimoPonto
                ? limpo.Replace(".", "").Replace(',', '.')
                : limpo.Replace(",", "");
        }
        else if (ultimaVirgula >= 0)
        {
            limpo = limpo.Replace(',', '.');
        }
        else if (ultimoPonto >= 0)
        {
            var casas = limpo.Length - ultimoPonto - 1;
            if (casas == 3 && limpo.Count(caractere => caractere == '.') == 1)
                limpo = limpo.Replace(".", "");
        }

        return decimal.TryParse(limpo, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
    }
}
