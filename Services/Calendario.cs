namespace CaioFinance.Services;

public static class Calendario
{
    public static DateTime Agora() => DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

    public static string TituloDoMes(DateOnly mes)
    {
        var texto = mes.ToString("MMMM 'de' yyyy");
        if (string.IsNullOrEmpty(texto))
            return texto;

        return char.ToUpper(texto[0]) + texto[1..];
    }
}
