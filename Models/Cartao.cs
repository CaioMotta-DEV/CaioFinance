using System.ComponentModel.DataAnnotations.Schema;

namespace CaioFinance.Models;

public class Cartao
{
    public int nCdCartao { get; set; }
    public string sIdItemPluggy { get; set; } = "";
    public string sIdContaPluggy { get; set; } = "";
    public string sNmInstituicao { get; set; } = "";
    public string sNmCartao { get; set; } = "";
    public string? sNmApelido { get; set; }
    public string sNmBandeira { get; set; } = "";
    public string sNrFinal { get; set; } = "";
    public string sDsCor { get; set; } = "#0F766E";
    public int? nNrDiaVencimento { get; set; }
    public bool bFlManual { get; set; }
    public bool bFlOculto { get; set; }
    public DateTime tDtAtualizacao { get; set; }

    [NotMapped]
    public string NomeExibicao => string.IsNullOrWhiteSpace(sNmApelido) ? sNmCartao : sNmApelido.Trim();

    public List<GastoCartao> Gastos { get; set; } = [];
}
