using CaioFinance.Models;
using CaioFinance.Services;
using Microsoft.EntityFrameworkCore;

namespace CaioFinance.Data;

public static class CargaInicial
{
    public static async Task CarregarAsync(CaioFinanceContexto db, CancellationToken cancellationToken = default)
    {
        // Se ja existem categorias, a base ja foi preparada.
        if (await db.Categorias.AnyAsync(cancellationToken))
            return;

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var mesAtual = new DateOnly(hoje.Year, hoje.Month, 1);
        var mesAnterior = mesAtual.AddMonths(-1);
        var agora = Calendario.Agora();

        var nubank = await db.ContasBanco
            .FirstOrDefaultAsync(conta => conta.sNmInstituicao == "Nubank", cancellationToken);
        var itau = await db.ContasBanco
            .FirstOrDefaultAsync(conta => conta.sNmInstituicao == "Itaú", cancellationToken);

        if (nubank is null)
        {
            nubank = new ContaBanco
            {
                sNmInstituicao = "Nubank",
                dVlSaldo = 3842.17m,
                nCdSituacao = SituacaoConta.Conectado,
                tDtAtualizacao = agora.AddHours(-6)
            };
            db.ContasBanco.Add(nubank);
        }

        if (itau is null)
        {
            itau = new ContaBanco
            {
                sNmInstituicao = "Itaú",
                dVlSaldo = 8560.40m,
                nCdSituacao = SituacaoConta.Conectado,
                tDtAtualizacao = agora.AddHours(-30)
            };
            db.ContasBanco.Add(itau);
        }

        var nomes = new[]
        {
            "Moradia",
            "Alimentação",
            "Transporte",
            "Assinaturas",
            "Saúde",
            "Lazer",
            "Outros"
        };

        var categorias = nomes
            .Select((nome, indice) => new Categoria
            {
                sNmCategoria = nome,
                nNrOrdem = indice + 1
            })
            .ToDictionary(categoria => categoria.sNmCategoria);

        db.Categorias.AddRange(categorias.Values);
        await db.SaveChangesAsync(cancellationToken);

        if (await db.Lancamentos.AnyAsync(cancellationToken))
            return;

        const int serieAluguel = 1;
        const int serieNetflix = 2;
        const int serieSpotify = 3;
        const int serieSmartFit = 4;

        var lancamentos = new List<Lancamento>();
        var criacao = agora.AddDays(-40);

        void Adicionar(
            DateOnly mes,
            int dia,
            string exibicao,
            string original,
            string categoria,
            ContaBanco conta,
            decimal valor,
            bool contaFixa = false,
            int? serie = null)
        {
            lancamentos.Add(new Lancamento
            {
                nCdConta = conta.nCdConta,
                nCdCategoria = categorias[categoria].nCdCategoria,
                tDtOcorrencia = Dia(mes, dia),
                sNmOriginal = original,
                sNmExibicao = exibicao,
                dVlLancamento = valor,
                bFlContaFixa = contaFixa,
                nCdSerie = serie,
                nCdOrigem = OrigemLancamento.Banco,
                tDtCriacao = criacao.AddMinutes(lancamentos.Count)
            });
        }

        Adicionar(mesAnterior, 1, "Aluguel", "PIX ALUGUEL IMOVEIS", "Moradia", itau, -1850.00m, true, serieAluguel);
        Adicionar(mesAnterior, 2, "Netflix", "NETFLIX.COM", "Assinaturas", nubank, -39.90m, true, serieNetflix);
        Adicionar(mesAnterior, 4, "Hortifruti", "HORTIFRUTI *MERCADO", "Alimentação", itau, -86.20m);
        Adicionar(mesAnterior, 6, "99", "99APP *CORRIDA", "Transporte", nubank, -27.00m);
        Adicionar(mesAnterior, 7, "Spotify", "SPOTIFY AB", "Assinaturas", nubank, -21.90m, true, serieSpotify);
        Adicionar(mesAnterior, 9, "Consulta clínica", "CLINICA SAO LUCAS", "Saúde", itau, -250.00m);
        Adicionar(mesAnterior, 11, "Carrefour", "CARREFOUR LOJA 14", "Alimentação", itau, -356.40m);
        Adicionar(mesAnterior, 15, "Salário", "TED SALARIO", "Outros", itau, 6850.00m);
        Adicionar(mesAnterior, 16, "Livraria", "LIVRARIA CULTURA", "Lazer", nubank, -64.00m);
        Adicionar(mesAnterior, 20, "Posto Shell", "POSTO SHELL", "Transporte", itau, -198.50m);
        Adicionar(mesAnterior, 22, "Smart Fit", "SMARTFIT MENSAL", "Saúde", nubank, -119.90m, true, serieSmartFit);
        Adicionar(mesAnterior, 25, "Restaurante", "RESTAURANTE MOCOTO", "Alimentação", nubank, -143.00m);

        Adicionar(mesAtual, 1, "Aluguel", "PIX ALUGUEL IMOVEIS", "Moradia", itau, -1850.00m, true, serieAluguel);
        Adicionar(mesAtual, 2, "Netflix", "NETFLIX.COM", "Assinaturas", nubank, -39.90m, true, serieNetflix);
        Adicionar(mesAtual, 2, "Padaria Estrela", "PADARIA ESTRELA LJ 02", "Alimentação", nubank, -18.50m);
        Adicionar(mesAtual, 3, "Uber", "UBER *TRIP", "Transporte", nubank, -32.40m);
        Adicionar(mesAtual, 5, "Salário", "TED SALARIO", "Outros", itau, 6850.00m);
        Adicionar(mesAtual, 7, "Spotify", "SPOTIFY AB", "Assinaturas", nubank, -21.90m, true, serieSpotify);
        Adicionar(mesAtual, 8, "Drogaria São Paulo", "DROGARIA SAO PAULO", "Saúde", itau, -76.30m);
        Adicionar(mesAtual, 10, "Pão de Açúcar", "PAO DE ACUCAR", "Alimentação", itau, -428.76m);
        Adicionar(mesAtual, 12, "Cinema", "KINOPLEX", "Lazer", nubank, -56.00m);
        Adicionar(mesAtual, 14, "Posto Ipiranga", "POSTO IPIRANGA", "Transporte", itau, -215.00m);
        Adicionar(mesAtual, 18, "iFood", "IFOOD *IFOOD", "Alimentação", nubank, -72.80m);
        Adicionar(mesAtual, 22, "Smart Fit", "SMARTFIT MENSAL", "Saúde", nubank, -119.90m, true, serieSmartFit);

        var futuros = new List<LancamentoFuturo>();

        void AdicionarFixo(int serie, int dia, string nome, string categoria, ContaBanco conta, decimal valor)
        {
            for (var deslocamento = 1; deslocamento <= ServicoContaFixa.MesesSeguintes; deslocamento++)
            {
                var mes = mesAtual.AddMonths(deslocamento);
                futuros.Add(new LancamentoFuturo
                {
                    nCdConta = conta.nCdConta,
                    nCdCategoria = categorias[categoria].nCdCategoria,
                    tDtVencimento = Dia(mes, dia),
                    sNmLancamento = nome,
                    dVlLancamento = valor,
                    nCdTipo = TipoLancamentoFuturo.ContaFixa,
                    nCdSerie = serie,
                    tDtCriacao = agora
                });
            }
        }

        AdicionarFixo(serieAluguel, 1, "Aluguel", "Moradia", itau, -1850.00m);
        AdicionarFixo(serieNetflix, 2, "Netflix", "Assinaturas", nubank, -39.90m);
        AdicionarFixo(serieSpotify, 7, "Spotify", "Assinaturas", nubank, -21.90m);
        AdicionarFixo(serieSmartFit, 22, "Smart Fit", "Saúde", nubank, -119.90m);

        var mesDentista = mesAtual.AddMonths(1);
        futuros.Add(new LancamentoFuturo
        {
            nCdConta = itau.nCdConta,
            nCdCategoria = categorias["Saúde"].nCdCategoria,
            tDtVencimento = Dia(mesDentista, 18),
            sNmLancamento = "Dentista",
            dVlLancamento = -280.00m,
            nCdTipo = TipoLancamentoFuturo.Avulso,
            tDtCriacao = agora
        });

        var mesPresentes = mesAtual.AddMonths(2);
        futuros.Add(new LancamentoFuturo
        {
            nCdConta = nubank.nCdConta,
            nCdCategoria = categorias["Lazer"].nCdCategoria,
            tDtVencimento = Dia(mesPresentes, 10),
            sNmLancamento = "Presentes",
            dVlLancamento = -350.00m,
            nCdTipo = TipoLancamentoFuturo.Avulso,
            tDtCriacao = agora
        });

        db.Lancamentos.AddRange(lancamentos);
        db.LancamentosFuturos.AddRange(futuros);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static DateOnly Dia(DateOnly mes, int dia) =>
        new(mes.Year, mes.Month, Math.Min(dia, DateTime.DaysInMonth(mes.Year, mes.Month)));
}
