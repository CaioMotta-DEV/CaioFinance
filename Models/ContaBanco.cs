using System.ComponentModel.DataAnnotations.Schema;

namespace CaioFinance.Models;

public class ContaBanco
{
    public int nCdConta { get; set; }
    public string sNmInstituicao { get; set; } = "";
    public string? sNmApelido { get; set; }

    [NotMapped]
    public string NomeExibicao => string.IsNullOrWhiteSpace(sNmApelido) ? sNmInstituicao : sNmApelido.Trim();
    public decimal dVlSaldo { get; set; }
    public SituacaoConta nCdSituacao { get; set; } = SituacaoConta.Conectado;
    public DateTime tDtAtualizacao { get; set; }
    public string? sIdItemPluggy { get; set; }
    public string? sIdContaPluggy { get; set; }
    public int? nCdConectorPluggy { get; set; }

    public List<Lancamento> Lancamentos { get; set; } = [];
    public List<LancamentoFuturo> LancamentosFuturos { get; set; } = [];
}
