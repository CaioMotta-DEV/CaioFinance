namespace CaioFinance.Services.Pluggy;

public class OpcoesPluggy
{
    public const string Secao = "Pluggy";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.pluggy.ai";
    public bool IncluirSandbox { get; set; }
}
