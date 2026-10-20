using CaioFinance.Data;
using CaioFinance.Models;
using CaioFinance.Services.Pluggy;
using Microsoft.EntityFrameworkCore;

namespace CaioFinance.Services;

public class SincronizacaoBanco(CaioFinanceContexto db, ClientePluggy pluggy) : ISincronizacaoBanco
{
    public async Task<ResultadoSincronizacao> AtualizarContaAsync(int nCdConta, CancellationToken cancellationToken = default)
    {
        var conta = await db.ContasBanco
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.nCdConta == nCdConta, cancellationToken)
            ?? throw new InvalidOperationException("Conta não encontrada.");

        if (string.IsNullOrWhiteSpace(conta.sIdItemPluggy))
            throw new InvalidOperationException("Esta conta é de exemplo e não está ligada à Pluggy.");

        await pluggy.AtualizarItemAsync(conta.sIdItemPluggy, cancellationToken);
        return await ImportarItemAsync(conta.sIdItemPluggy, cancellationToken);
    }

    public async Task ExcluirConexaoAsync(int nCdConta, CancellationToken cancellationToken = default)
    {
        var conta = await db.ContasBanco
            .FirstOrDefaultAsync(item => item.nCdConta == nCdConta, cancellationToken)
            ?? throw new InvalidOperationException("Conta não encontrada.");

        var contas = string.IsNullOrWhiteSpace(conta.sIdItemPluggy)
            ? [conta]
            : await db.ContasBanco
                .Where(item => item.sIdItemPluggy == conta.sIdItemPluggy)
                .ToListAsync(cancellationToken);

        var ids = contas.Select(item => item.nCdConta).ToList();
        var lancamentos = await db.Lancamentos
            .Where(item => ids.Contains(item.nCdConta))
            .ToListAsync(cancellationToken);
        var futuros = await db.LancamentosFuturos
            .Where(item => ids.Contains(item.nCdConta))
            .ToListAsync(cancellationToken);

        db.Lancamentos.RemoveRange(lancamentos);
        db.LancamentosFuturos.RemoveRange(futuros);
        if (!string.IsNullOrWhiteSpace(conta.sIdItemPluggy))
            await ExcluirCartoesDoItemAsync(conta.sIdItemPluggy, cancellationToken);
        db.ContasBanco.RemoveRange(contas);
        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(conta.sIdItemPluggy))
            await pluggy.ExcluirItemAsync(conta.sIdItemPluggy, cancellationToken);
    }

    public async Task<ResultadoSincronizacao> ImportarItemAsync(string sIdItem, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(sIdItem, out _))
            throw new ExcecaoPluggy("Identificador da conexão inválido.");

        var item = await pluggy.AguardarItemAsync(sIdItem, cancellationToken);
        if (string.IsNullOrWhiteSpace(item.Id))
            item.Id = sIdItem;
        if (item.Status is "LOGIN_ERROR")
            throw new ExcecaoPluggy(item.Error?.Message ?? "O banco recusou o acesso. Tente reconectar.");

        if (item.Status is "OUTDATED")
            throw new ExcecaoPluggy("A conexão expirou. Use Reconectar.");

        var contasApi = item.Status == "UPDATED"
            ? await pluggy.ListarContasAsync(sIdItem, cancellationToken)
            : [];

        if (contasApi.Count == 0)
        {
            await GarantirPlaceholderAsync(item, cancellationToken);
            if (item.Status == "UPDATED")
                await ImportarCartoesDoItemAsync(sIdItem, cancellationToken);
            var pendente = item.Status == "WAITING_USER_INPUT"
                || (item.ExecutionStatus?.Contains("AUTHORIZATION", StringComparison.OrdinalIgnoreCase) ?? false);
            var mensagem = pendente
                ? "Conexão criada, mas o banco ainda precisa da sua autorização. Depois de autorizar, use Atualizar."
                : "Conexão registrada, mas o banco ainda não devolveu conta corrente. Use Atualizar daqui a pouco.";
            return new ResultadoSincronizacao(0, 0, mensagem);
        }

        var outros = await db.Categorias
            .FirstOrDefaultAsync(categoria => categoria.sNmCategoria == NomesPadrao.CategoriaOutros, cancellationToken)
            ?? throw new InvalidOperationException("A categoria Outros não foi encontrada.");

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var desde = hoje.AddMonths(-12);
        var novas = 0;
        DateTime? desdeCriacao = null;
        var nomeConector = string.IsNullOrWhiteSpace(item.Connector?.Name) ? "Banco" : item.Connector.Name;

        var placeholders = await db.ContasBanco
            .Where(conta => conta.sIdItemPluggy == sIdItem && conta.sIdContaPluggy == null)
            .ToListAsync(cancellationToken);
        db.ContasBanco.RemoveRange(placeholders);

        var existentes = await db.Lancamentos
            .Where(lancamento => lancamento.sIdTransacaoPluggy != null)
            .ToDictionaryAsync(lancamento => lancamento.sIdTransacaoPluggy!, cancellationToken);

        foreach (var contaApi in contasApi)
        {
            var local = await db.ContasBanco
                .FirstOrDefaultAsync(conta => conta.sIdContaPluggy == contaApi.Id, cancellationToken);

            if (local is null)
            {
                local = new ContaBanco { sIdContaPluggy = contaApi.Id };
                db.ContasBanco.Add(local);
            }

            var marco = local.tDtAtualizacao;
            var incremental = local.nCdConta != 0 && await db.Lancamentos.AnyAsync(
                lancamento => lancamento.nCdConta == local.nCdConta && lancamento.sIdTransacaoPluggy != null,
                cancellationToken);
            if (incremental && (desdeCriacao is null || marco < desdeCriacao))
                desdeCriacao = marco;

            local.sNmInstituicao = NomeDaConta(nomeConector, contaApi);
            local.dVlSaldo = contaApi.Balance;
            local.sIdItemPluggy = sIdItem;
            local.nCdConectorPluggy = item.Connector?.Id;
            local.nCdSituacao = SituacaoConta.Conectado;
            local.tDtAtualizacao = Calendario.Agora();
            await db.SaveChangesAsync(cancellationToken);

            var transacoes = await pluggy.ListarTransacoesAsync(
                contaApi.Id,
                desde,
                hoje,
                cancellationToken,
                incremental ? marco : null);

            foreach (var transacao in transacoes)
            {
                if (string.IsNullOrWhiteSpace(transacao.Id))
                    continue;

                if (!string.IsNullOrWhiteSpace(transacao.Status)
                    && !transacao.Status.Equals("POSTED", StringComparison.OrdinalIgnoreCase))
                    continue;

                var dia = DateOnly.FromDateTime(transacao.Date);
                if (dia < desde || dia > hoje)
                    continue;

                var valor = Valor(transacao);
                if (valor == 0)
                    continue;

                var nome = NomeDaTransacao(transacao);
                if (existentes.TryGetValue(transacao.Id, out var existente))
                {
                    var nomeAnterior = existente.sNmOriginal;
                    existente.sNmOriginal = nome;
                    if (existente.bFlOculto || existente.nCdOrigem == OrigemLancamento.Manual)
                        continue;

                    if (existente.sNmExibicao == nomeAnterior)
                        existente.sNmExibicao = nome;

                    existente.dVlLancamento = valor;
                    existente.tDtOcorrencia = dia;
                    existente.nCdConta = local.nCdConta;
                    continue;
                }

                var lancamento = new Lancamento
                {
                    nCdConta = local.nCdConta,
                    nCdCategoria = outros.nCdCategoria,
                    tDtOcorrencia = dia,
                    sNmOriginal = nome,
                    sNmExibicao = nome,
                    dVlLancamento = valor,
                    nCdOrigem = OrigemLancamento.Banco,
                    sIdTransacaoPluggy = transacao.Id,
                    tDtCriacao = Calendario.Agora()
                };
                db.Lancamentos.Add(lancamento);
                existentes[transacao.Id] = lancamento;
                novas++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        if (item.Status == "UPDATED")
            await ImportarCartoesDoItemAsync(sIdItem, cancellationToken);

        return new ResultadoSincronizacao(contasApi.Count, novas, Mensagem(contasApi.Count, novas, desdeCriacao));
    }

    public async Task<ResultadoSincronizacao> AtualizarTudoAsync(CancellationToken cancellationToken = default)
    {
        var contas = await db.ContasBanco
            .AsNoTracking()
            .Where(conta => conta.sIdItemPluggy != null)
            .ToListAsync(cancellationToken);

        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contasAtualizadas = 0;
        var lancamentos = 0;
        string? ultima = null;

        foreach (var conta in contas)
        {
            if (!vistas.Add(conta.sIdItemPluggy!))
                continue;

            var resultado = await AtualizarContaAsync(conta.nCdConta, cancellationToken);
            contasAtualizadas += resultado.Contas;
            lancamentos += resultado.LancamentosNovos;
            ultima = resultado.Mensagem;
        }

        if (contas.Count == 0)
            return new ResultadoSincronizacao(0, 0, "Nenhum banco conectado para atualizar.");

        return new ResultadoSincronizacao(contasAtualizadas, lancamentos, ultima ?? "Bancos atualizados.");
    }

    public async Task<string> ImportarCartoesConectadosAsync(CancellationToken cancellationToken = default)
    {
        var itens = await db.ContasBanco
            .AsNoTracking()
            .Where(conta => conta.sIdItemPluggy != null)
            .Select(conta => conta.sIdItemPluggy!)
            .Distinct()
            .ToListAsync(cancellationToken);

        DateTime? desde = null;
        foreach (var item in itens)
        {
            var marco = await ImportarCartoesDoItemAsync(item, cancellationToken);
            if (marco is DateTime data && (desde is null || data < desde))
                desde = data;
        }

        return desde is DateTime inicio
            ? $"Cartões atualizados a partir de {inicio:dd/MM/yyyy HH:mm}."
            : "Faturas dos cartões relidas.";
    }

    private async Task<DateTime?> ImportarCartoesDoItemAsync(string sIdItem, CancellationToken cancellationToken)
    {
        var cartoesApi = await pluggy.ListarCartoesAsync(sIdItem, cancellationToken);
        var instituicao = await InstituicaoDoItemAsync(sIdItem, cancellationToken);
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var desde = hoje.AddMonths(-12);
        var ate = hoje.AddMonths(12);
        var idsRecebidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DateTime? marcoUsado = null;

        foreach (var cartaoApi in cartoesApi)
        {
            if (string.IsNullOrWhiteSpace(cartaoApi.Id))
                continue;

            idsRecebidos.Add(cartaoApi.Id);
            var local = await db.Cartoes
                .FirstOrDefaultAsync(cartao => cartao.sIdContaPluggy == cartaoApi.Id, cancellationToken);

            if (local?.bFlOculto == true)
                continue;

            if (local is null)
            {
                local = new Cartao { sIdContaPluggy = cartaoApi.Id };
                db.Cartoes.Add(local);
            }

            var nome = NomeDoCartao(cartaoApi);
            if (!local.bFlManual)
            {
                local.sIdItemPluggy = sIdItem;
                local.sNmInstituicao = string.IsNullOrWhiteSpace(instituicao) ? nome : instituicao;
                local.sNmCartao = nome;
                local.sNmBandeira = Bandeira(cartaoApi);
                local.sNrFinal = Final(cartaoApi.Number);
                local.sDsCor = Cor(local.sNmInstituicao, local.sNmBandeira);
            }

            var completo = true;

            if (local.nNrDiaVencimento is null && cartaoApi.CreditData?.BalanceDueDate is DateTime vencimentoDoCartao)
                local.nNrDiaVencimento = vencimentoDoCartao.Day;

            local.tDtAtualizacao = Calendario.Agora();
            await db.SaveChangesAsync(cancellationToken);

            if (completo)
            {
                var remover = await db.GastosCartao
                    .Where(gasto => gasto.nCdCartao == local.nCdCartao && !gasto.bFlManual && !gasto.bFlEditado && !gasto.bFlOculto && !gasto.bFlContaFixa)
                    .ToListAsync(cancellationToken);
                db.GastosCartao.RemoveRange(remover);
                await db.SaveChangesAsync(cancellationToken);
            }

            var faturas = await pluggy.ListarFaturasAsync(cartaoApi.Id, cancellationToken);
            if (local.nNrDiaVencimento is null && faturas.Count > 0)
                local.nNrDiaVencimento = DateOnly.FromDateTime(faturas.MaxBy(fatura => fatura.DueDate)!.DueDate).Day;

            var transacoes = await pluggy.ListarTransacoesAsync(cartaoApi.Id, desde, ate, cancellationToken);
            var existentes = await db.GastosCartao
                .Where(gasto => gasto.nCdCartao == local.nCdCartao)
                .ToDictionaryAsync(gasto => gasto.sIdTransacaoPluggy, cancellationToken);

            var vencimentoAberto = cartaoApi.CreditData?.BalanceDueDate is DateTime aberta
                ? DateOnly.FromDateTime(aberta)
                : (DateOnly?)null;

            foreach (var transacao in transacoes)
            {
                if (!EhGastoDeCartao(transacao))
                    continue;

                var compra = DateOnly.FromDateTime(transacao.Date);
                var valor = Math.Abs(transacao.Amount);
                if (valor == 0)
                    continue;

                var billId = transacao.CreditCardMetadata?.BillId;
                var fatura = FaturaPeloFechamento(compra, billId, faturas, vencimentoAberto);
                var idFatura = string.IsNullOrWhiteSpace(billId) ? "aberta" : billId;

                var parcela = transacao.CreditCardMetadata?.InstallmentNumber;
                var totalParcelas = transacao.CreditCardMetadata?.TotalInstallments;
                if (totalParcelas is null or <= 1)
                {
                    parcela = null;
                    totalParcelas = null;
                }

                if (existentes.TryGetValue(transacao.Id, out var existente))
                {
                    if (existente.bFlOculto || existente.bFlManual)
                        continue;
                    if (existente.bFlEditado)
                    {
                        existente.sIdFaturaPluggy ??= idFatura;
                        continue;
                    }

                    existente.tDtCompra = compra;
                    existente.tDtFatura = fatura;
                    existente.sIdFaturaPluggy = idFatura;
                    existente.sNmDescricao = NomeDaTransacao(transacao);
                    existente.sNmCategoria = MapaCategoriaCartao.Traduzir(transacao.Category);
                    existente.nNrParcela = parcela;
                    existente.nNrTotalParcelas = totalParcelas;
                    existente.dVlGasto = valor;
                    continue;
                }

                var gasto = new GastoCartao
                {
                    nCdCartao = local.nCdCartao,
                    sIdTransacaoPluggy = transacao.Id,
                    sIdFaturaPluggy = idFatura,
                    tDtCompra = compra,
                    tDtFatura = fatura,
                    sNmDescricao = NomeDaTransacao(transacao),
                    sNmCategoria = MapaCategoriaCartao.Traduzir(transacao.Category),
                    nNrParcela = parcela,
                    nNrTotalParcelas = totalParcelas,
                    dVlGasto = valor,
                    tDtCriacao = Calendario.Agora()
                };
                db.GastosCartao.Add(gasto);
                existentes[transacao.Id] = gasto;
            }

            if (completo)
            {
                var pendentes = await db.GastosCartao
                    .Where(gasto => gasto.nCdCartao == local.nCdCartao && gasto.sIdFaturaPluggy == null)
                    .ToListAsync(cancellationToken);
                foreach (var pendente in pendentes)
                    pendente.sIdFaturaPluggy = "aberta";
            }
        }

        var locais = await db.Cartoes
            .Where(cartao => cartao.sIdItemPluggy == sIdItem)
            .ToListAsync(cancellationToken);
        var ausentes = locais
            .Where(cartao => !cartao.bFlManual && !cartao.bFlOculto && !idsRecebidos.Contains(cartao.sIdContaPluggy))
            .ToList();
        foreach (var ausente in ausentes)
            await ExcluirCartaoAsync(ausente, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return marcoUsado;
    }

    private static DateOnly DiaDoMes(DateOnly mes, int dia)
    {
        var limite = DateTime.DaysInMonth(mes.Year, mes.Month);
        return new DateOnly(mes.Year, mes.Month, Math.Clamp(dia, 1, limite));
    }

    private static DateOnly FaturaPeloFechamento(
        DateOnly compra,
        string? billId,
        IReadOnlyList<FaturaPluggy> faturas,
        DateOnly? vencimentoAberto)
    {
        if (!string.IsNullOrWhiteSpace(billId))
        {
            var ligada = faturas.FirstOrDefault(fatura =>
                string.Equals(fatura.Id, billId, StringComparison.OrdinalIgnoreCase));
            if (ligada is not null)
                return DataPluggy(ligada.DueDate);
        }

        var ordenadas = faturas
            .Select(fatura => new
            {
                Vencimento = DataPluggy(fatura.DueDate),
                Fechamento = fatura.BillClosingDate is DateTime fecha ? DataPluggy(fecha) : (DateOnly?)null
            })
            .OrderBy(fatura => fatura.Vencimento)
            .ToList();

        var ultima = ordenadas.LastOrDefault();
        if (ultima?.Fechamento is DateOnly fechou && compra >= fechou)
        {
            var ciclos = 1;
            var cursor = fechou;
            while (compra >= cursor.AddMonths(1) && ciclos < 24)
            {
                cursor = cursor.AddMonths(1);
                ciclos++;
            }

            return ultima.Vencimento.AddMonths(ciclos);
        }

        var coberta = ordenadas.FirstOrDefault(fatura => fatura.Fechamento is DateOnly fecha && compra < fecha);
        if (coberta is not null)
            return coberta.Vencimento;

        if (vencimentoAberto is DateOnly aberto)
            return compra >= aberto ? aberto.AddMonths(1) : aberto;

        return compra;
    }

    private static DateOnly DataPluggy(DateTime data) => DateOnly.FromDateTime(data);

    private async Task ExcluirCartoesDoItemAsync(string sIdItem, CancellationToken cancellationToken)
    {
        var cartoes = await db.Cartoes
            .Where(cartao => cartao.sIdItemPluggy == sIdItem)
            .ToListAsync(cancellationToken);
        foreach (var cartao in cartoes)
            await ExcluirCartaoAsync(cartao, cancellationToken);
    }

    private async Task ExcluirCartaoAsync(Cartao cartao, CancellationToken cancellationToken)
    {
        var gastos = await db.GastosCartao
            .Where(gasto => gasto.nCdCartao == cartao.nCdCartao)
            .ToListAsync(cancellationToken);
        db.GastosCartao.RemoveRange(gastos);
        db.Cartoes.Remove(cartao);
    }

    private async Task GarantirPlaceholderAsync(ItemPluggy item, CancellationToken cancellationToken)
    {
        var existe = await db.ContasBanco.AnyAsync(
            conta => conta.sIdItemPluggy == item.Id,
            cancellationToken);
        if (existe)
            return;

        db.ContasBanco.Add(new ContaBanco
        {
            sNmInstituicao = string.IsNullOrWhiteSpace(item.Connector?.Name) ? "Banco" : item.Connector.Name,
            dVlSaldo = 0,
            nCdSituacao = SituacaoConta.Conectado,
            tDtAtualizacao = Calendario.Agora(),
            sIdItemPluggy = item.Id,
            nCdConectorPluggy = item.Connector?.Id
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Mensagem(int contas, int novas, DateTime? desdeCriacao)
    {
        var textoContas = contas == 1 ? "1 conta" : $"{contas} contas";
        var textoNovas = novas == 1 ? "1 lançamento novo" : $"{novas} lançamentos novos";
        var periodo = desdeCriacao is DateTime marco
            ? $"a partir de {marco:dd/MM/yyyy HH:mm}"
            : "nos últimos 12 meses";
        return $"Saldo e extrato atualizados. {textoContas}, {textoNovas} {periodo}.";
    }

    private static string NomeDaConta(string conector, ContaPluggy conta)
    {
        var detalhe = string.IsNullOrWhiteSpace(conta.MarketingName) ? conta.Name : conta.MarketingName;
        var texto = string.IsNullOrWhiteSpace(detalhe) ? conector : $"{conector} — {detalhe}";
        return texto.Length <= 80 ? texto : texto[..80];
    }

    private static string NomeDaTransacao(TransacaoPluggy transacao)
    {
        var nome = string.IsNullOrWhiteSpace(transacao.Description)
            ? transacao.DescriptionRaw
            : transacao.Description;
        nome = string.IsNullOrWhiteSpace(nome) ? "Lançamento" : nome.Trim();
        return nome.Length <= 160 ? nome : nome[..160];
    }

    private async Task<string> InstituicaoDoItemAsync(string sIdItem, CancellationToken cancellationToken)
    {
        var nome = await db.ContasBanco
            .AsNoTracking()
            .Where(conta => conta.sIdItemPluggy == sIdItem && conta.sIdContaPluggy != null)
            .Select(conta => conta.sNmInstituicao)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(nome))
            return "";

        const string prefixo = "MeuPluggy — ";
        if (nome.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
            nome = nome[prefixo.Length..];

        nome = EncurtarInstituicao(nome.Trim());
        return nome.Length <= 80 ? nome : nome[..80];
    }

    private static string EncurtarInstituicao(string nome)
    {
        var texto = nome.ToLowerInvariant();
        if (texto.Contains("nubank") || texto.Contains("nu pag"))
            return "Nubank";
        if (texto.Contains("ita"))
            return "Itaú";
        if (texto.Contains("c6"))
            return "C6 Bank";
        if (texto.Contains("mercado"))
            return "Mercado Pago";

        var parentese = nome.IndexOf('(');
        return parentese > 0 ? nome[..parentese].Trim() : nome;
    }

    private static string NomeDoCartao(ContaPluggy cartao)
    {
        var nome = string.IsNullOrWhiteSpace(cartao.MarketingName) ? cartao.Name : cartao.MarketingName;
        nome = string.Join(' ', (nome ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(nome))
            nome = "Cartão";
        return nome.Length <= 80 ? nome : nome[..80];
    }

    private static string Bandeira(ContaPluggy cartao)
    {
        var bandeira = cartao.CreditData?.Brand?.Trim();
        if (string.IsNullOrWhiteSpace(bandeira))
            return "Cartão";

        var nivel = cartao.CreditData?.Level?.Trim();
        var texto = string.IsNullOrWhiteSpace(nivel) ? bandeira : $"{bandeira} {nivel}";
        return texto.Length <= 40 ? texto : texto[..40];
    }

    private static string Final(string? numero)
    {
        var digitos = new string((numero ?? "").Where(char.IsDigit).ToArray());
        return digitos.Length >= 4 ? digitos[^4..] : "";
    }

    private static string Cor(string instituicao, string bandeira)
    {
        var texto = instituicao.ToLowerInvariant();
        if (texto.Contains("nubank") || texto.Contains("nu pag"))
            return "#820AD1";
        if (texto.Contains("ita"))
            return "#EC7000";
        if (texto.Contains("c6"))
            return "#242424";
        if (texto.Contains("mercado"))
            return "#009EE3";
        if (texto.Contains("inter"))
            return "#EA4E0F";
        if (bandeira.Contains("VISA", StringComparison.OrdinalIgnoreCase))
            return "#1A1F71";
        if (bandeira.Contains("MASTER", StringComparison.OrdinalIgnoreCase))
            return "#1C1C1C";
        return "#0F766E";
    }

    private static bool EhGastoDeCartao(TransacaoPluggy transacao)
    {
        if (string.IsNullOrWhiteSpace(transacao.Id))
            return false;
        if (!string.Equals(transacao.Type, "DEBIT", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.Equals(transacao.Category, "Credit card payment", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static decimal Valor(TransacaoPluggy transacao)
    {
        var valor = transacao.Amount;
        if (string.Equals(transacao.Type, "DEBIT", StringComparison.OrdinalIgnoreCase))
            return valor > 0 ? -valor : valor;

        if (string.Equals(transacao.Type, "CREDIT", StringComparison.OrdinalIgnoreCase))
            return valor < 0 ? -valor : valor;

        return valor;
    }
}
