using System.ComponentModel.DataAnnotations.Schema;

namespace CaioFinance.Models;

public class GastoCartao
{
    public int nCdGasto { get; set; }
    public int nCdCartao { get; set; }
    public Cartao Cartao { get; set; } = null!;
    public string sIdTransacaoPluggy { get; set; } = "";
    public string? sIdFaturaPluggy { get; set; }
    public DateOnly tDtCompra { get; set; }
    public DateOnly tDtFatura { get; set; }
    public string sNmDescricao { get; set; } = "";
    public string? sNmApelido { get; set; }
    public string sNmCategoria { get; set; } = "";
    public int? nNrParcela { get; set; }
    public int? nNrTotalParcelas { get; set; }
    public decimal dVlGasto { get; set; }
    public bool bFlManual { get; set; }
    public bool bFlEditado { get; set; }
    public bool bFlOculto { get; set; }
    public bool bFlContaFixa { get; set; }
    public int? nCdSerie { get; set; }
    public DateTime tDtCriacao { get; set; }

    [NotMapped]
    public string NomeExibicao => string.IsNullOrWhiteSpace(sNmApelido) ? sNmDescricao : sNmApelido.Trim();
}
