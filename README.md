# Caio Finance

Controle pessoal de gastos, em português e em reais. O aplicativo mostra o extrato do mês, os cartões, o que ainda vai entrar nos próximos meses e as categorias. Cada pessoa roda a própria cópia, com o próprio banco de dados e, se quiser, a própria conta na Pluggy.

Não é um serviço hospedado. Sem configurar o banco, o programa não abre. Sem configurar a Pluggy, as telas manuais funcionam, mas a leitura automática do banco não.

## O que não vai para o GitHub

O código publicado não contém a senha do Supabase, o identificador do projeto, o Client ID nem o Client Secret da Pluggy. Esses valores ficam só no arquivo local `appsettings.Development.json`, que o Git ignora.

O arquivo versionado `appsettings.json` deixa esses campos vazios. O modelo para preencher é `appsettings.Development.example.json`, com textos de exemplo (`SUA_SENHA`, `SEU_CLIENT_ID`, `SEU_CLIENT_SECRET`).

Os lançamentos, saldos e faturas também não estão no código. Eles ficam no banco de quem configurou o aplicativo. Quem baixa o repositório não entra nos dados de outra pessoa.

Na primeira vez, se o banco estiver vazio, o programa cria categorias e alguns lançamentos de exemplo (aluguel, mercado, salário) só para as telas não abrirem em branco. Isso não são dados reais de ninguém.

## Telas

- **Dashboard.** Saldo das contas, gastos e entradas do mês, gráfico dos últimos 12 meses e o extrato, com busca. Dá para ocultar um lançamento, trocar a categoria e marcar uma conta fixa.
- **Cartões.** Faturas pelo mês de vencimento. Compra feita depois do fechamento cai no mês seguinte. Dá para incluir cartão e gasto à mão. Se o gasto tiver parcela e total, as parcelas seguintes são criadas, uma em cada mês.
- **Lançamentos futuros.** Previsão do mês, separada em despesas e receitas. O **Gasto real** é o valor das despesas menos o das receitas. Contas fixas aparecem primeiro; o restante, em ordem alfabética. As listas rolam dentro do painel. O botão Atualizar copia os gastos de cartão já gravados, a partir do mês atual, para o mês da fatura. Se aquele nome já for uma conta fixa, ele não é copiado de novo.
- **Categorias.** Criar, renomear e excluir. Renomear vale para o extrato, para o cartão e para os lançamentos futuros. Excluir manda os lançamentos da categoria para Outros.
- **Conexão.** Liga um banco pelo Meu Pluggy. A primeira leitura traz cerca de 12 meses. Atualizar, depois disso, traz o que a Pluggy registrou desde a última leitura. O saldo e os cartões dessa conexão entram junto.

Não há tela de login. O endereço padrão é só o seu computador (`http://localhost:5080`). Não publique esse endereço na internet: quem abrir a página vê e altera os lançamentos.

## Como o programa é feito

É um site em [ASP.NET Core](https://dotnet.microsoft.com/) (.NET 10), com páginas Razor. O navegador pede uma página, o servidor lê o banco, monta o HTML e devolve. Valores em real usam a cultura `pt-BR`.

O banco é PostgreSQL, no [Supabase](https://supabase.com/). O acesso é feito com Entity Framework Core e o pacote Npgsql. Ao abrir, o programa aplica as migrações de `Data/Migrations` e cria as tabelas se elas ainda não existirem.

A ligação com o banco usa a [Pluggy](https://www.pluggy.ai/) (Open Finance). O Client ID e o Client Secret ficam no servidor. Eles não são escritos na página. O site pede um token temporário à Pluggy e abre o conector no navegador. A leitura seguinte grava contas, extrato e cartões nas tabelas locais.

As tabelas principais:

| Tabela | Uso |
| --- | --- |
| `CONTA_BANCO` | Instituição, saldo e vínculo com a Pluggy |
| `LANCAMENTO` | Extrato do Dashboard |
| `CATEGORIA` | Nome e ordem das categorias |
| `CARTAO` e `GASTO_CARTAO` | Cartões e compras da fatura |
| `LANCAMENTO_FUTURO` | Conta fixa, lançamento avulso e cópia do cartão |

Pastas:

| Pasta | Conteúdo |
| --- | --- |
| `Pages` | Telas e o que cada botão faz |
| `Models` | Formato de cada tabela |
| `Data` | Conexão, migrações e a carga de exemplo |
| `Services` | Pluggy, sincronização, conta fixa, parcelas e lançamentos futuros do cartão |
| `wwwroot` | Visual (`css/site.css`) e o script da tela de conexão |
| `Resources/Textos.cs` | Textos fixos da interface |

A API pública do Supabase (os papéis `anon`, `authenticated` e `service_role`) não lê essas tabelas. A proteção de linha fica ligada e esses acessos são retirados na migração `TravarApiSupabase`. O site entra com o usuário do Postgres informado na sua conexão, que é outro caminho.

## Rodar na sua máquina

Você não precisa saber programar. Siga na ordem. Cada pessoa usa a própria conta do Supabase e, se quiser o banco automático, a própria conta da Pluggy. Nada disso vem pronto no código.

### 1. Instalar o .NET 10

O programa precisa do SDK do .NET 10, que é a ferramenta que executa o projeto.

1. Abra [https://dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Baixe o **SDK** (não basta o Runtime) para o seu sistema.
3. Instale e feche e abra o terminal.
4. Digite `dotnet --version`. Tem que aparecer um número que começa com `10`.

### 2. Criar o banco no Supabase

1. Crie uma conta em [https://supabase.com](https://supabase.com) e um projeto novo. Guarde a senha do banco que o site mostrar. Ela não pode ser recuperada depois; se perder, gere outra no painel.
2. No projeto, abra **Project Settings** e depois **Database**.
3. Copie a connection string no modo URI ou a string do Postgres. O aplicativo espera este formato:

```text
Host=db.SEU_PROJETO.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=SUA_SENHA;SSL Mode=Require;Trust Server Certificate=true
```

Troque `SEU_PROJETO` e `SUA_SENHA` pelos valores do seu projeto. Se o painel mostrar um host de pooler e um usuário no formato `postgres.alguma_coisa`, use esses, desde que a senha seja a do seu banco.

### 3. Criar a chave da Pluggy

Pule este passo se for lançar tudo à mão. Sem a Pluggy o aplicativo abre, e o botão de conectar banco fica desligado.

1. Crie uma conta em [https://www.pluggy.ai](https://www.pluggy.ai).
2. No painel, copie o **Client ID** e o **Client Secret**.
3. Esses dois valores são da sua conta. Não coloque os de outra pessoa e não envie os seus para o GitHub.

### 4. Colocar esses dados num arquivo só seu

Na pasta do projeto já existe `appsettings.Development.example.json`.

1. Duplique esse arquivo.
2. Renomeie a cópia para `appsettings.Development.json`.
3. Abra a cópia e substitua os textos de exemplo pela sua connection string, pelo Client ID e pelo Client Secret.
4. Salve.

Esse arquivo fica só no seu computador. O Git está configurado para não enviá-lo.

### 5. Abrir o aplicativo

No terminal, entre na pasta do projeto e rode:

```bash
dotnet run --launch-profile http
```

Espere a linha `Now listening on: http://localhost:5080`. Abra esse endereço no navegador.

Na primeira execução o programa cria as tabelas sozinho. Se o banco estava vazio, também cria as categorias e os lançamentos de exemplo. Pode apagar o que não quiser pelas próprias telas.

Para encerrar, volte ao terminal e pressione `Ctrl+C`.

### 6. Usar no dia a dia

1. Abra de novo com o mesmo comando.
2. Em **Conexão**, escolha o banco e conecte, se tiver configurado a Pluggy.
3. Use **Atualizar** quando quiser trazer saldo, extrato e cartões que a Pluggy já tiver recebido.
4. Em **Lançamentos futuros**, use **Atualizar** para copiar os gastos de cartão já salvos para os meses das faturas.

Os dados permanecem no seu projeto do Supabase. Outra pessoa só os vê se tiver a senha desse banco ou se você publicar o site na internet.
