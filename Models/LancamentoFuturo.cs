namespace CaioFinance.Models;

public class LancamentoFuturo
{
    public int nCdLancamentoFuturo { get; set; }
    public int nCdConta { get; set; }
    public ContaBanco Conta { get; set; } = null!;
    public int nCdCategoria { get; set; }
    public Categoria Categoria { get; set; } = null!;
    public DateOnly tDtVencimento { get; set; }
    public string sNmLancamento { get; set; } = "";
    public decimal dVlLancamento { get; set; }
    public TipoLancamentoFuturo nCdTipo { get; set; } = TipoLancamentoFuturo.Avulso;
    public int? nCdSerie { get; set; }
    public string? sIdOrigem { get; set; }
    public string? sDsSerie { get; set; }
    public DateTime tDtCriacao { get; set; }
}
