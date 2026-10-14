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
| Tracing | OpenTelemetry + Jaeger | Trace único atravessando Api -> RabbitMQ -> Worker -> RabbitMQ -> Api, incluindo o salto assíncrono do Outbox. |
| IA/Analytics | Claude (Anthropic) via tool use | A LLM escolhe qual agregação rodar entre ferramentas parametrizadas; ela nunca escreve SQL. |
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
| Jaeger (tracing) | http://localhost:16686 |

A Api aplica as migrations do banco automaticamente no startup — não é necessário nenhum passo manual de setup do schema.

## Variáveis de ambiente

Veja [`.env.example`](.env.example) (raiz, para o Docker Compose) e [`src/Frontend/order-management-web/.env.example`](src/Frontend/order-management-web/.env.example) (para rodar o frontend fora do Docker). Nenhuma credencial fica hardcoded no código-fonte.

O módulo de IA/Analytics é opcional e lê `ANTHROPIC_API_KEY`. Sem a variável, a stack sobe normalmente, `/analytics/status` retorna `enabled: false` e o frontend exibe o módulo como desativado.

## Endpoints da API

| Método | Rota | Descrição |
|---|---|---|
| GET | `/dashboard` | Snapshot do painel: tiles, série por hora e pedidos em andamento |
| GET | `/products` | Catálogo de produtos disponíveis (`id`, `name`, `price`) |
| POST | `/orders` | Cria um novo pedido (`customer`, `productId`, e opcionalmente `processingSeconds` + `outcome`) |
| GET | `/orders` | Lista paginada, com busca, filtro por status e ordenação |
| GET | `/orders/{id}` | Detalhes de um pedido, incluindo histórico de status |
| POST | `/analytics/ask` | Pergunta em linguagem natural sobre os pedidos (`question`) |
| GET | `/analytics/status` | Informa se o módulo de IA está configurado |
| GET | `/health` | Health check (Postgres + RabbitMQ) |

Regras de negócio:
- Um pedido é sempre criado a partir de um produto do catálogo: o cliente envia `productId`, e o **valor vem do banco, nunca da requisição** — não há como um cliente decidir quanto o próprio pedido vale. O pedido guarda uma cópia do nome e do preço no momento da compra, então um reajuste futuro não reescreve o histórico.
- Sequência obrigatória: `Pendente` → `Processando` → `Finalizado`, sem pular etapas nem retroceder. Esse é o único caminho até `Finalizado`.
- Dois estados finais adicionais existem para o simulador: `Falhou` e `Cancelado`. Nada sai deles.
- Ao criar um pedido, a Api persiste os dados e enfileira o evento `OrderCreated` via Outbox Pattern (ver `docs/architecture.md`).
- O Worker consome o evento, move o pedido para `Processando` e, 5 segundos depois, para `Finalizado`, com o `CorrelationId` da mensagem igual ao `OrderId` e `EventType = OrderCreated`.
- O consumidor é idempotente (duas camadas: id da mensagem do broker + checagem de estado do domínio).

## Decisões e trade-offs

- **Delay de 5s via `Task.Delay` + sweeper de segurança**, em vez de um plugin de delayed-exchange do RabbitMQ: mais simples de operar e não exige imagem customizada do broker, ao custo de depender de um sweeper (`StuckOrdersSweeper`) para cobrir o caso de reinício do Worker durante o delay.
- **Migrations aplicadas no startup da Api** (não do Worker, para evitar duas instâncias migrando ao mesmo tempo): simples e suficiente para este escopo; uma evolução possível seria um container `migrator` dedicado.
- **Outbox publisher como `BackgroundService` dentro da própria Api** (não um processo separado): reduz a quantidade de serviços no compose sem comprometer a garantia de atomicidade, que vem da transação do EF Core, não de onde o publisher roda.
- **Catálogo semeado por migration** (`ProductConfiguration.HasData`, com ids fixos): um clone novo do repositório já sobe com produtos para pedir, sem passo manual de setup. A FK de `Orders` para `Products` é `Restrict`, então um produto referenciado por pedidos não some por baixo deles.
- **Pedidos de demonstração semeados por migration** (`SeedDemoOrders`): 14 pedidos já encerrados — 11 finalizados, 2 que falharam e 1 cancelado — com datas relativas ao momento em que a migration roda, espalhadas pelas últimas 24 horas. O objetivo é que um clone novo abra num painel com dados em vez de cinco quadros vazios, e que o gráfico de 24h tenha o que mostrar em qualquer dia. Só pedidos em estado final são semeados: um `Pendente` semeado não teria mensagem alguma por trás e ficaria parado para sempre, parecendo defeito. Diferente do catálogo, que é dado de referência do domínio, estes são dados de demonstração — num sistema real não existiriam.
- **Módulo de IA via tool use, não via text-to-SQL**: a LLM recebe um conjunto fechado de ferramentas (contagem, soma, agrupamento por status, tempo de processamento, listagem), cada uma mapeada para uma query parametrizada em EF Core. O modelo escolhe os filtros, nunca a query — então uma pergunta maliciosa não alcança dados ou operações que o endpoint não pretende expor. O custo é não responder perguntas fora do conjunto de ferramentas, e nesse caso o assistente diz isso em vez de adivinhar.
- **Trace context persistido na tabela de Outbox** (coluna `TraceParent`): a publicação acontece depois da requisição, em um background service, então sem guardar o `traceparent` na linha o span de publish abriria um trace novo em vez de continuar o do `POST /orders`.

## Tracing ponta a ponta

```bash
docker compose up --build
curl -X POST http://localhost:8080/orders -H 'Content-Type: application/json'   -d '{"customer":"Maria Silva","product":"Notebook","amount":3500}'
# abra http://localhost:16686, serviço "order-management-api", operação "POST orders"
```

Um pedido gera **um único trace** com os dois processos:

```
POST orders                              [api]
├─ orders                                [api]   (Npgsql: Order + OutboxMessage)
└─ publish order.created                 [api]   (outbox publisher)
   └─ consume orders.created.queue       [worker]
      ├─ orders                          [worker] (Pendente -> Processando)
      ├─ publish order.status-changed    [worker]
      │  └─ consume orders.status-changed.queue   [api] (repasse via SignalR)
      └─ finalize order                  [worker] (5s depois)
         └─ publish order.status-changed [worker]
            └─ consume orders.status-changed.queue [api]
```

Isso exige propagar contexto por dois pontos onde ele normalmente se perde:

1. **Outbox**: a publicação ocorre depois da requisição, em um background service. O `traceparent` é gravado na coluna `TraceParent` da linha de outbox e usado como pai do span de publish.
2. **RabbitMQ**: o contexto vai nos headers AMQP (`traceparent`) e é extraído pelo consumidor — nos dois sentidos (Api→Worker e Worker→Api).

Sem endpoint OTLP configurado (`Otlp:Endpoint`), os spans vão para o console, então o tracing também é observável rodando os projetos com `dotnet run`.

## Painel

A rota `/` é um painel de operações alimentado por `GET /dashboard?hours=1|6|24`, que devolve tudo em uma única chamada (o cliente nunca precisa baixar todos os pedidos para contá-los):

- **Tiles**: em andamento (com valor em jogo), criados hoje, tempo médio até finalizar, taxa de conclusão.
- **Pedidos criados**: colunas empilhadas por status. A altura responde "quantos foram criados" e a pilha responde "e onde foram parar". O tooltip abre o horário, o total e a contagem por status, e avisa quando o intervalo ainda está em curso.
  - A janela é ajustável — **1h / 6h / 24h** — e a granularidade acompanha (5 min / 30 min / 1 hora). Isso existe porque um dia inteiro em baldes de uma hora esconde uma rajada dos últimos minutos: tudo cai numa coluna só. O último balde é marcado como `agora` no eixo, já que ele ainda está enchendo e não é comparável aos fechados.
- **Distribuição por status**: barra proporcional com contagem e valor por estado.
- **Em andamento agora**: um card por pedido em voo, com barra de progresso em tempo real.

### Sobre as cores

A paleta de status foi validada com o script de verificação de paletas (banda de luminosidade, piso de croma, separação sob daltonismo, piso de visão normal e contraste). A primeira versão — uma rampa de um único azul — **falhou**: `Processando` e `Finalizado` ficavam abaixo do piso de visão normal, e `Falhou`/`Cancelado` eram praticamente idênticos em deuteranopia. Daí `Finalizado` ser verde-azulado e `Cancelado` violeta.

Há duas rampas separadas de propósito: `--stage-*` para texto e marcadores da interface, e `--chart-*` para preenchimento de gráfico, que precisa ficar dentro da banda de luminosidade do tema e por isso é mais escura no dark mode. Status nunca é comunicado só por cor — cada um tem também uma forma própria de marcador (anel vazado, anel com núcleo, sólido, X, traço).

## Lista de pedidos

`GET /orders` é sempre paginado (`page`, `pageSize` com teto de 100, `status`, `search`, `sort`, `ascending`). A tabela traz busca com debounce, filtro por status, ordenação por coluna e paginação — a listagem não cresce sem limite.

## Simulador do worker

Por padrão o worker faz o que o enunciado pede: espera 5 segundos e finaliza. Cada pedido, porém, carrega seu **próprio plano de processamento** — duração e desfecho — então dá para exercitar cenários que normalmente são difíceis de reproduzir:

```bash
# falha depois de 5s
curl -X POST http://localhost:8080/orders -H 'Content-Type: application/json'   -d '{"customer":"Maria","productId":"<id>","processingSeconds":5,"outcome":"Falhar"}'

# cancela depois de 10s tentando
curl -X POST http://localhost:8080/orders -H 'Content-Type: application/json'   -d '{"customer":"Maria","productId":"<id>","processingSeconds":10,"outcome":"Cancelar"}'
```

Omitir os dois campos devolve exatamente o comportamento original (5s → `Finalizado`).

No frontend o modal de novo pedido traz atalhos para os cenários mais usados (sucesso, lento, falha, cancelamento) e campos para ajustar tempo e desfecho à mão. A seção **Em andamento** mostra cada pedido em voo num card com barra de progresso em tempo real, o tempo decorrido sobre o total e qual desfecho está por vir.

O `StuckOrdersSweeper` respeita a duração de cada pedido: se o worker reiniciar no meio da espera, ele aplica o desfecho planejado depois do prazo daquele pedido (mais uma folga), em vez de assumir 5s para todo mundo.

## Módulo IA/Analytics

Perguntas em linguagem natural sobre os pedidos, em `/analytics` no frontend ou via `POST /analytics/ask`:

```bash
curl -X POST http://localhost:8080/analytics/ask -H 'Content-Type: application/json'   -d '{"question":"Quantos pedidos estão pendentes?"}'
```

A LLM **não gera SQL**. Ela recebe cinco ferramentas, cada uma ligada a uma query parametrizada:

| Ferramenta | Responde perguntas como |
|---|---|
| `count_orders` | "Quantos pedidos temos hoje?" |
| `sum_order_amounts` | "Qual o valor total de pedidos finalizados este mês?" |
| `orders_by_status` | "Como estão distribuídos os pedidos por status?" |
| `processing_time_seconds` | "Qual o tempo médio para aprovar os pedidos?" |
| `list_orders` | "Quais foram os últimos pedidos e para quem vendemos?" |

O modelo escolhe a ferramenta e os filtros; o loop de tool use tem limite de iterações e toda falha de ferramenta volta como `tool_result` de erro, não como exceção.

### Dois provedores atrás da mesma porta

`IOrderAnalyticsAssistant` é uma porta na camada Application, e existem duas implementações. Trocar de provedor não toca em nada acima dela — nem endpoint, nem UI, nem as ferramentas, nem as queries. As definições das ferramentas e o prompt vivem em `OrderAnalyticsToolset`, compartilhado pelos dois.

| | Claude (padrão) | Ollama (local) |
|---|---|---|
| Custo | ~US$ 0,007–0,03 por pergunta | zero |
| Precisa de conta | sim | não |
| Qualidade | alta, inclusive em perguntas com cálculo de data | boa nas perguntas simples; erra mais nas de data |
| Peso | nenhum | ~2 GB de modelo, respostas mais lentas |

**Claude:** defina `ANTHROPIC_API_KEY` no `.env` e suba normalmente.

**Ollama:** sem chave nenhuma —

```bash
# no .env
ANALYTICS_PROVIDER=Ollama

docker compose --profile local-ai up -d
```

O profile é opcional justamente para não pesar o `docker compose up` padrão: quem não quiser o módulo de IA não baixa modelo nenhum. O serviço `ollama-pull` baixa o modelo na subida, então continua sendo um comando só.

Sem nenhum dos dois configurados o módulo reporta desativado, `/analytics/ask` responde `503` e o resto do sistema não é afetado. A tela mostra qual provedor está respondendo.

Como modelos pequenos são fracos em aritmética de datas, o prompt entrega os limites **já calculados** (início de hoje, início do mês, etc.) em vez de pedir que o modelo os derive — é a diferença entre acertar e errar "este mês" num modelo de 3B.

## Testes automatizados

```bash
cd src/Backend
dotnet test
```

- **Unitários**: regras de transição de status do domínio (`Order`), e `OrderService` (outbox gravado corretamente na criação, 404 ao buscar pedido inexistente).
- **Integração**: `OrdersApiTests` sobe um PostgreSQL real e efêmero via Testcontainers e valida que `POST /orders` persiste `Order` + `OrderStatusHistory` + `OutboxMessage` de forma atômica, através da própria Api (`WebApplicationFactory`). `OrderAnalyticsQueriesTests` valida as agregações do módulo de analytics contra o banco real.
- **Golden tests**: `OrderManagement.Tests/Golden` congela os contratos externos em snapshots versionados — os corpos HTTP (camelCase) e os payloads publicados no broker (PascalCase, status como número). Ids e timestamps são substituídos por placeholders, então o que o teste trava é o formato, não os valores. Um contrato alterado grava um `.received.json` ao lado do `.verified.json` para revisão.

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
- ✅ Tracing ponta a ponta (OpenTelemetry + Jaeger), atravessando os dois saltos pelo RabbitMQ
- ✅ Golden tests dos contratos HTTP e de mensageria
- ✅ Módulo IA/Analytics (perguntas em linguagem natural sobre os pedidos)
