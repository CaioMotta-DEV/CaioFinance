namespace CaioFinance.Models;

public class Lancamento
{
    public int nCdLancamento { get; set; }
    public int nCdConta { get; set; }
    public ContaBanco Conta { get; set; } = null!;
    public int nCdCategoria { get; set; }
    public Categoria Categoria { get; set; } = null!;
    public DateOnly tDtOcorrencia { get; set; }
    public string sNmOriginal { get; set; } = "";
    public string sNmExibicao { get; set; } = "";
    public decimal dVlLancamento { get; set; }
    public bool bFlContaFixa { get; set; }
    public bool bFlOculto { get; set; }
    public int? nCdSerie { get; set; }
    public OrigemLancamento nCdOrigem { get; set; } = OrigemLancamento.Banco;
    public string? sIdTransacaoPluggy { get; set; }
    public DateTime tDtCriacao { get; set; }
}
