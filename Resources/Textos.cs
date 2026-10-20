namespace CaioFinance.Resources;

public static class Textos
{
    public const string Salvar = "Salvar";
    public const string Incluir = "Incluir";
    public const string Remover = "Remover";
    public const string Atualizar = "Atualizar";
    public const string Editar = "Editar";
    public const string Excluir = "Excluir";
    public const string MarcarContaFixa = "Marcar como conta fixa";
    public const string DesmarcarContaFixa = "Desmarcar conta fixa";
    public const string NomeNoBanco = "Nome no banco";
    public const string ContaFixa = "Conta fixa";
    public const string Avulso = "Avulso";
    public const string Cartao = "Cartão";
    public const string Conectado = "Conectado";
    public const string Gasto = "Gasto";
    public const string Entrada = "Entrada";
    public const string Receitas = "Receitas";
    public const string Despesas = "Despesas";

    public const string DataInvalida = "Informe uma data válida.";
    public const string NomeObrigatorio = "Informe o nome do lançamento.";
    public const string NomeCategoriaObrigatorio = "Informe o nome da categoria.";
    public const string NomeExibicaoObrigatorio = "Informe um nome de exibição.";
    public const string ApelidoObrigatorio = "Informe o apelido da conta fixa.";
    public const string JaEhContaFixa = "Este lançamento já é conta fixa.";
    public const string NaoEhContaFixa = "Este lançamento não é conta fixa.";
    public const string ValorInvalido = "Informe um valor maior que zero.";
    public const string TipoInvalido = "Escolha se é gasto ou entrada.";
    public const string ContaCategoriaObrigatorias = "Escolha a conta e a categoria.";
    public const string CategoriaObrigatoria = "Escolha uma categoria.";
    public const string LancamentoNaoEncontrado = "Lançamento não encontrado.";
    public const string CategoriaNaoEncontrada = "Categoria não encontrada.";
    public const string SoGastoPodeSerFixo = "Só um gasto pode ser marcado como conta fixa.";
    public const string FuturoAntesDoMesAtual = "O lançamento futuro precisa ser deste mês em diante.";
    public const string RepeticaoInvalida = "Escolha se o lançamento se repete.";
    public const string FuturoNaoEncontrado = "Lançamento futuro não encontrado.";
    public const string BancoNaoEncontrado = "Banco não encontrado.";
    public const string PluggySemCredencial = "Informe o Client ID e o Client Secret da Pluggy em appsettings.Development.json, na seção Pluggy.";
    public const string ContaExemplo = "Esta conta é de exemplo e não está ligada à Pluggy.";
    public const string CategoriaDuplicada = "Já existe uma categoria com esse nome.";
    public const string NaoExcluirOutros = "A categoria Outros não pode ser excluída.";

    public const string LancamentoAtualizado = "Lançamento atualizado.";
    public const string LancamentoExcluido = "Lançamento excluído.";
    public const string ContaFixaMarcada = "Conta fixa marcada. Os meses entram nos lançamentos futuros.";
    public const string ContaFixaJaPrevista = "Conta fixa marcada. Os meses já estavam previstos.";
    public const string ContaFixaDesmarcada = "Conta fixa desmarcada. Os lançamentos futuros dela foram removidos.";
    public const string FuturoMensalIncluido = "Lançamento mensal incluído para os próximos 6 meses.";
    public const string FuturoIncluido = "Lançamento futuro incluído.";
    public const string OcorrenciaRemovida = "Ocorrência removida. Os outros meses desta conta fixa foram mantidos.";
    public const string SerieRemovida = "Todos os meses restantes deste lançamento foram excluídos.";
    public const string FuturoRemovido = "Lançamento futuro removido.";
    public const string MesesContaFixaInvalidos = "Informe a quantidade de meses entre 1 e 60.";
    public const string CategoriaIncluida = "Categoria incluída.";
    public const string CategoriaAtualizada = "Categoria atualizada.";
    public const string CategoriaExcluida = "Categoria excluída. Os lançamentos foram para Outros.";
    public const string ExcluirTudo = "Excluir tudo";
}
