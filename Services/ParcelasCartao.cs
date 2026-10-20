using CaioFinance.Models;

namespace CaioFinance.Services;

public static class ParcelasCartao
{
    public static List<GastoCartao> MesesSeguintes(GastoCartao atual)
    {
        if (atual.nNrParcela is not int parcela || atual.nNrTotalParcelas is not int total || parcela >= total)
            return [];

        var seguintes = new List<GastoCartao>();
        for (var numero = parcela + 1; numero <= total; numero++)
        {
            var meses = numero - parcela;
            seguintes.Add(new GastoCartao
            {
                nCdCartao = atual.nCdCartao,
                sIdTransacaoPluggy = "m" + Guid.NewGuid().ToString("N"),
                sIdFaturaPluggy = atual.sIdFaturaPluggy ?? "",
                tDtCompra = atual.tDtCompra.AddMonths(meses),
                tDtFatura = atual.tDtFatura.AddMonths(meses),
                sNmDescricao = atual.sNmDescricao,
                sNmApelido = atual.sNmApelido,
                sNmCategoria = atual.sNmCategoria,
                nNrParcela = numero,
                nNrTotalParcelas = total,
                dVlGasto = atual.dVlGasto,
                bFlManual = true,
                tDtCriacao = Calendario.Agora()
            });
        }

        return seguintes;
    }
}
