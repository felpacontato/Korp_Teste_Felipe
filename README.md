# Korp_Teste_Felipe

Sistema simples para cadastro de produtos, emissao de notas fiscais e baixa de estoque apos impressao.

## Tecnologias

- Angular 22
- ASP.NET Core 8
- Entity Framework Core
- PostgreSQL
- Docker Compose

## Estrutura

```text
frontend-angular/      Aplicacao web em Angular
estoque-service/       Microsservico de produtos e saldo
faturamento-service/   Microsservico de notas fiscais
docs/                  Detalhamento tecnico
```

## Como executar

Requisitos:

- .NET SDK 8
- Node.js 24.15 ou superior
- Docker

Subir os bancos:

```bash
docker compose up -d
```

Rodar o servico de estoque:

```bash
dotnet run --project estoque-service
```

Rodar o servico de faturamento:

```bash
dotnet run --project faturamento-service
```

Rodar o frontend:

```bash
cd frontend-angular
npm install
npm start
```

Enderecos:

- Frontend: http://localhost:4200
- Estoque: http://localhost:5001/swagger
- Faturamento: http://localhost:5002/swagger

## Fluxo principal

1. Cadastrar um produto com codigo, descricao e saldo.
2. Criar uma nota fiscal adicionando um ou mais produtos.
3. Imprimir a nota fiscal.
4. A nota e fechada e o saldo dos produtos e atualizado.

## Falha simulada

Na tela de notas existe a opcao "Simular falha no estoque". Com ela marcada, o faturamento tenta imprimir a nota, recebe uma falha do estoque e mantem a nota aberta.
