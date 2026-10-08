# Sistema de Gestão de Pedidos

Desafio técnico: criação, listagem e visualização de pedidos, com processamento assíncrono via mensageria e um worker dedicado.

Arquitetura detalhada e diagrama em [`docs/architecture.md`](docs/architecture.md).

## Stack e justificativas

| Camada | Escolha | Por quê |
|---|---|---|
| Backend | .NET 9 (ASP.NET Core Web API) | Obrigatório pelo desafio; arquitetura em camadas (Domain/Application/Infrastructure/Api/Worker) para separar regras de negócio de infraestrutura e facilitar testes. |
| Acesso a dados | Entity Framework Core + Npgsql | Migrations automáticas (requisito do desafio), produtividade e mapeamento objeto-relacional maduro para PostgreSQL. |
| Banco de dados | PostgreSQL 16 | Obrigatório pelo desafio. |
| Mensageria | RabbitMQ (management UI incluída) | Modelo de exchange/queue simples e adequado ao escopo; management UI facilita inspeção durante o desenvolvimento/avaliação. |
| Frontend | Next.js 16 (App Router) | React é obrigatório; Next.js agrega SSR para a listagem/detalhes (sem "flash" de loading) e roteamento por arquivos, acelerando o desenvolvimento. |
| Estilização | Tailwind CSS | Consistência visual rápida sem CSS customizado extenso. |
| Real-time | SignalR + fallback de polling | Cumpre o diferencial "atualização em tempo real com fallback" sem exigir infraestrutura extra (usa o mesmo processo da Api). |
| Confiabilidade | Outbox Pattern | Garante que a persistência do pedido e a publicação do evento nunca fiquem inconsistentes entre si. |
| Orquestração | Docker Compose | Único comando para subir toda a stack, com healthchecks e dependências explícitas. |

## Como rodar

Pré-requisitos: Docker Desktop.

```bash
cp .env.example .env
docker compose up --build
```

Serviços expostos:

| Serviço | URL |
|---|---|
| Frontend | http://localhost:3000 |
| Api | http://localhost:8080 |
| Swagger | http://localhost:8080/swagger |
| Health check | http://localhost:8080/health |
| RabbitMQ management | http://localhost:15672 (usuário/senha em `.env`) |
| pgAdmin | http://localhost:5050 (login em `.env`) |

A Api aplica as migrations do banco automaticamente no startup — não é necessário nenhum passo manual de setup do schema.

## Variáveis de ambiente

Veja [`.env.example`](.env.example) (raiz, para o Docker Compose) e [`src/Frontend/order-management-web/.env.example`](src/Frontend/order-management-web/.env.example) (para rodar o frontend fora do Docker). Nenhuma credencial fica hardcoded no código-fonte.

## Endpoints da API

| Método | Rota | Descrição |
|---|---|---|
| POST | `/orders` | Cria um novo pedido (`customer`, `product`, `amount`) |
| GET | `/orders` | Lista todos os pedidos |
| GET | `/orders/{id}` | Detalhes de um pedido, incluindo histórico de status |
| GET | `/health` | Health check (Postgres + RabbitMQ) |

Regras de negócio:
- Status possíveis: `Pendente`, `Processando`, `Finalizado`, sempre nessa sequência (sem pular etapas nem retroceder).
- Ao criar um pedido, a Api persiste os dados e enfileira o evento `OrderCreated` via Outbox Pattern (ver `docs/architecture.md`).
- O Worker consome o evento, move o pedido para `Processando` e, 5 segundos depois, para `Finalizado`, com o `CorrelationId` da mensagem igual ao `OrderId` e `EventType = OrderCreated`.
- O consumidor é idempotente (duas camadas: id da mensagem do broker + checagem de estado do domínio).

## Decisões e trade-offs

- **Delay de 5s via `Task.Delay` + sweeper de segurança**, em vez de um plugin de delayed-exchange do RabbitMQ: mais simples de operar e não exige imagem customizada do broker, ao custo de depender de um sweeper (`StuckOrdersSweeper`) para cobrir o caso de reinício do Worker durante o delay.
- **Migrations aplicadas no startup da Api** (não do Worker, para evitar duas instâncias migrando ao mesmo tempo): simples e suficiente para este escopo; uma evolução possível seria um container `migrator` dedicado.
- **Outbox publisher como `BackgroundService` dentro da própria Api** (não um processo separado): reduz a quantidade de serviços no compose sem comprometer a garantia de atomicidade, que vem da transação do EF Core, não de onde o publisher roda.
- **Módulo de IA/Analytics (opcional no desafio) não foi implementado** nesta entrega, para priorizar robustez do fluxo obrigatório (mensageria, idempotência, real-time).

## Testes automatizados

```bash
cd src/Backend
dotnet test
```

- **Unitários**: regras de transição de status do domínio (`Order`), e `OrderService` (outbox gravado corretamente na criação, 404 ao buscar pedido inexistente).
- **Integração**: `OrdersApiTests` sobe um PostgreSQL real e efêmero via Testcontainers e valida que `POST /orders` persiste `Order` + `OrderStatusHistory` + `OutboxMessage` de forma atômica, através da própria Api (`WebApplicationFactory`).

## Estrutura do repositório

```
src/
  Backend/            Solução .NET (Domain, Application, Infrastructure, Api, Worker, Tests)
  Frontend/
    order-management-web/   Next.js App Router
docs/
  architecture.md     Diagrama e detalhes de arquitetura
docker-compose.yml
.env.example
```

## Diferenciais implementados

- ✅ Outbox Pattern (mensageria transacional)
- ✅ Histórico de status do pedido
- ✅ Atualização de status em tempo real no frontend, com fallback de polling
- ✅ Dead-letter queue para mensagens com falha recorrente
- ✅ Testes de integração com dependências reais (PostgreSQL via Testcontainers), em ambiente isolado e descartável

Não implementados nesta entrega: módulo de IA/Analytics, tracing ponta a ponta, golden tests.
