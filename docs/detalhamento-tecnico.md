# Detalhamento tecnico

## Arquitetura

O projeto foi dividido em tres partes:

- Frontend Angular, responsavel pelas telas de cadastro, listagem e impressao.
- Microsservico de Estoque, responsavel por produtos e saldos.
- Microsservico de Faturamento, responsavel por notas fiscais e impressao.

Cada microsservico possui seu proprio banco PostgreSQL. Essa separacao evita acoplamento direto entre as bases e deixa a comunicacao entre servicos explicita por HTTP.

## Angular

Foi utilizado Angular 22 com componentes standalone. O componente principal concentra o fluxo do teste para manter a navegacao simples durante a demonstracao.

Ciclos de vida utilizados:

- `ngOnInit`: usado para carregar produtos e notas ao abrir a aplicacao.
- `ngOnDestroy`: usado para encerrar subscriptions criadas no componente.

Uso de RxJS:

- `forkJoin`: carrega produtos e notas em paralelo.
- `catchError`: transforma erros HTTP em mensagens exibidas na tela.
- `finalize`: remove indicadores de carregamento ao final das chamadas.
- `Subscription`: agrupa subscriptions para limpeza no `ngOnDestroy`.

Bibliotecas visuais:

- Nao foi usada biblioteca visual externa. A interface foi feita com HTML e SCSS para manter o projeto mais simples.

## Backend

Os dois microsservicos foram criados com ASP.NET Core 8 usando Minimal APIs.

Frameworks e bibliotecas:

- ASP.NET Core: criacao dos endpoints HTTP.
- Entity Framework Core: mapeamento e persistencia dos dados.
- Npgsql Entity Framework Core Provider: conexao com PostgreSQL.
- Swashbuckle: documentacao Swagger em ambiente local.

## Banco de dados

O projeto usa PostgreSQL via Docker Compose:

- `estoque_db`: armazena produtos.
- `faturamento_db`: armazena notas fiscais e itens.

As migrations do EF Core ficam nos respectivos microsservicos. Ao iniciar a API, `Database.Migrate()` aplica as migrations pendentes.

## Regras implementadas

Produtos:

- Codigo obrigatorio.
- Descricao obrigatoria.
- Saldo nao pode ser negativo.
- Codigo de produto e unico.

Notas fiscais:

- Numeracao sequencial.
- Status inicial `Aberta`.
- Permite multiplos itens.
- Apenas notas abertas podem ser impressas.
- Ao imprimir, o estoque e baixado e a nota passa para `Fechada`.

## Tratamento de falhas

O faturamento chama o estoque por HTTP no momento da impressao. Se o estoque estiver indisponivel ou retornar erro, a nota permanece aberta e o frontend mostra uma mensagem de erro.

Tambem foi criada uma opcao de simulacao de falha no frontend. Quando marcada, a chamada de impressao envia `simularFalha: true`, o estoque responde com erro `503` e o faturamento devolve feedback apropriado.

## Excecoes e erros

Os erros foram tratados com respostas HTTP claras:

- `400`: dados invalidos.
- `404`: produto ou nota nao encontrados.
- `409`: conflito de regra de negocio, como saldo insuficiente ou nota ja fechada.
- `503`: falha de comunicacao ou indisponibilidade do microsservico de estoque.

No backend, excecoes de chamada HTTP sao capturadas com `try/catch` e registradas com `ILogger`.

## LINQ

Foi usado LINQ no backend C# em consultas e transformacoes, por exemplo:

- `OrderBy` e `OrderByDescending` para ordenar listas.
- `AnyAsync` e `MaxAsync` para gerar a numeracao sequencial.
- `Select` para transformar itens de nota em itens de baixa de estoque.
- `FirstOrDefaultAsync` para buscar produto ou nota.

## Idempotencia

A regra de impressao bloqueia notas com status diferente de `Aberta`. Isso evita que a mesma nota seja impressa novamente e baixe estoque mais de uma vez.

## Concorrencia

O servico de estoque executa a baixa dentro de uma transacao. O fluxo valida saldo antes de salvar a alteracao. Para um cenario produtivo com alta concorrencia, a proxima melhoria seria adicionar controle de versao com coluna `xmin`/row version ou isolamento transacional mais restritivo.
