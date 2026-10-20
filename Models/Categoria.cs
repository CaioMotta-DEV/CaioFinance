namespace CaioFinance.Models;

public class Categoria
{
    public int nCdCategoria { get; set; }
    public string sNmCategoria { get; set; } = "";
    public int nNrOrdem { get; set; }

    public List<Lancamento> Lancamentos { get; set; } = [];
    public List<LancamentoFuturo> LancamentosFuturos { get; set; } = [];
}
