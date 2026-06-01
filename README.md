# CallsTrack — Explicação Técnica

## 1. Visão Geral

O **SlimTrack** é um sistema de Help Desk acadêmico com triagem automática de chamados por palavras-chave. O usuário abre um chamado com título e descrição; o sistema classifica automaticamente a **categoria** e a **prioridade** sem intervenção humana. O administrador pode revisar, corrigir e evoluir o chamado ao longo do atendimento.

---

## 2. Tecnologias Utilizadas

| Camada | Tecnologia | Função |
|---|---|---|
| **Plataforma** | .NET 9 / ASP.NET Core | Runtime e framework web principal |
| **Orquestração** | .NET Aspire (`AppHost`) | Gerencia todos os serviços localmente com um único comando |
| **API** | ASP.NET Core Controllers | Expõe os endpoints REST da aplicação |
| **Autenticação** | JWT (JSON Web Token) | Emissão e validação de tokens para autenticação e autorização por papel (`User` / `Admin`) |
| **Banco de Dados** | PostgreSQL + Entity Framework Core | Persistência de usuários, chamados, eventos e logs de classificação |
| **Mensageria** | RabbitMQ (exchange `tickets`, tipo `Topic`) | Comunicação assíncrona entre a API e os workers de processamento |
| **Cache** | Redis | Disponível na infraestrutura; provisionado pelo Aspire |
| **Padrão Outbox** | Tabela `OutboxMessages` no PostgreSQL | Garante entrega confiável de eventos ao RabbitMQ — a mensagem só é publicada após ser persistida no banco |
| **Workers** | `BackgroundService` (.NET Hosted Services) | Processamento assíncrono fora do ciclo de requisição HTTP |
| **Classificação** | `KeywordClassificationService` (heurística) | Serviço substituível por NLP real; implementa `IClassificationService` |
| **Contêineres** | Docker (via Aspire) | Cada serviço de infraestrutura sobe em um contêiner isolado com volume persistente |

---

## 3. Arquitetura

```
┌─────────────────────────────────────────────────────────┐
│                    .NET Aspire AppHost                   │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌────────┐  │
│  │PostgreSQL│  │ RabbitMQ │  │  Redis   │  │SlimTrack│  │
│  └──────────┘  └──────────┘  └──────────┘  └────────┘  │
└─────────────────────────────────────────────────────────┘
```

A aplicação principal (`SlimTrack`) é composta por:

```
SlimTrack/
├── Controllers/         # Endpoints HTTP (Auth, Tickets)
├── Models/              # Entidades de domínio (Ticket, User, TicketEvent, …)
├── DTOs/                # Objetos de entrada e saída da API
├── Services/            # IClassificationService + KeywordClassificationService
│                        # IEventPublisher + RabbitMQEventPublisher
├── Workers/             # OutboxPublisherWorker, TicketClassificationWorker
├── Data/                # AppDbContext (EF Core) + migrations
└── Events/              # Contratos de eventos (TicketCreatedEvent, …)
```

---

## 4. Fluxo Principal — Abertura e Triagem de Chamado

```
Cliente HTTP
    │
    │  POST /api/tickets  { title, description, requester_name, requester_email }
    ▼
TicketsController
    │
    ├─ Cria Ticket com status = EmTriagem
    ├─ Adiciona TicketEvent ("Aguardando triagem")
    ├─ Grava OutboxMessage { event_type: "ticket.created", published: false }
    └─ SaveChangesAsync  ──────────────────────────────► PostgreSQL
           │
           │  (retorna 201 imediatamente — resposta não-bloqueante)
           ▼
OutboxPublisherWorker  (polling a cada 5 s)
    │
    ├─ Busca OutboxMessages onde published = false
    ├─ Publica no RabbitMQ  exchange=tickets  routing_key=ticket.created
    └─ Marca published = true  ────────────────────────► PostgreSQL
           │
           ▼
RabbitMQ  (exchange: tickets / queue: tickets.classification)
           │
           ▼
TicketClassificationWorker  (consumer AMQP)
    │
    ├─ Desserializa TicketCreatedEvent
    ├─ Verifica status ainda = EmTriagem (idempotência)
    ├─ Chama IClassificationService.Classify(title, description)
    │       └─ KeywordClassificationService
    │             ├─ Pontuação por categoria (Hardware/Software/Rede/Acesso/Outros)
    │             ├─ Pontuação por prioridade (Alta > Media > Baixa)
    │             └─ Retorna ClassificationResult { Category, Priority, Confidence, Rationale }
    │
    ├─ UPDATE Tickets SET status=Aberto, category=…, priority=…  (UPDATE atômico com guard de status)
    ├─ Insere TicketEvent ("Triagem automática concluída")
    ├─ Insere ClassificationLog (auditoria)
    ├─ Publica ticket.status_changed no RabbitMQ
    └─ BasicAck  ──────────────────────────────────────► RabbitMQ confirmado
```

---

## 5. Fluxo de Autenticação

```
POST /api/auth/register  → cria usuário com senha hasheada (ASP.NET Core PasswordHasher)
POST /api/auth/login     → valida credenciais → emite JWT (HMAC-SHA256, 8 h de validade)
GET  /api/auth/me        → [Authorize] retorna dados do usuário autenticado
```

O token JWT carrega as claims: `NameIdentifier`, `Name`, `Email`, `Role` (`User` ou `Admin`).  
Apenas usuários com papel `Admin` podem chamar `PATCH /api/tickets/{id}`.

---

## 6. Padrão Outbox

O padrão **Outbox** garante que nenhum evento seja perdido em caso de falha entre o banco de dados e o RabbitMQ:

1. A API grava o chamado **e** a mensagem de evento na **mesma transação** do banco.
2. O `OutboxPublisherWorker` lê as mensagens não publicadas periodicamente e as envia ao RabbitMQ.
3. Somente após a publicação bem-sucedida a mensagem é marcada como `published = true`.
4. Em caso de falha, o contador `retry_count` é incrementado; após 5 tentativas a mensagem é abandonada.

---

## 7. Classificação Heurística

O `KeywordClassificationService` funciona por pontuação:

- **Categoria**: para cada categoria (Hardware, Software, Rede, Acesso, Outros), conta quantas palavras-chave do dicionário aparecem no texto `título + descrição`. A categoria com maior score vence; se nenhuma pontuação, retorna `Outros`.
- **Prioridade**: hierarquia fixa — se o texto contém palavras de Alta prioridade, classifica como Alta; caso contrário, verifica Média; por fim, Baixa. Se nenhuma palavra é encontrada, padrão é Média.
- **Confiança**: `score_da_categoria_vencedora / score_total_de_todas_categorias` (0,0–1,0).
- **Justificativa**: texto legível com score de cada dimensão, armazenado no chamado e no log.

O serviço implementa `IClassificationService`, o que permite substituí-lo por um modelo de NLP real sem alterar nenhuma outra camada.

---

## 8. Endpoints da API

| Método | Rota | Autenticação | Descrição |
|---|---|---|---|
| `POST` | `/api/auth/register` | Anônimo | Cadastrar usuário |
| `POST` | `/api/auth/login` | Anônimo | Autenticar e obter token JWT |
| `GET` | `/api/auth/me` | ****** Dados do usuário logado |
| `POST` | `/api/tickets` | Anônimo | Abrir chamado (triagem assíncrona) |
| `GET` | `/api/tickets` | Anônimo | Listar chamados (paginação, filtro por status) |
| `GET` | `/api/tickets/{id}` | Anônimo | Buscar chamado por ID |
| `GET` | `/api/tickets/{id}/events` | Anônimo | Histórico de eventos do chamado |
| `PATCH` | `/api/tickets/{id}` | ****** Admin | Atualizar status/categoria/prioridade |

---

## 9. Enums

| Enum | Valores |
|---|---|
| `TicketStatus` | `EmTriagem`, `Aberto`, `EmAtendimento`, `Resolvido`, `Fechado` |
| `TicketCategory` | `Hardware`, `Software`, `Rede`, `Acesso`, `Outros` |
| `TicketPriority` | `Baixa`, `Media`, `Alta` |
| `UserRole` | `User`, `Admin` |

---

## 10. Como Executar Localmente

### Pré-requisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (para PostgreSQL, RabbitMQ e Redis)

### Subir tudo com Aspire

```bash
cd SlimTrack.AppHost
dotnet run
```

O Aspire sobe automaticamente os contêineres de infraestrutura e a aplicação, exibindo o dashboard em `http://localhost:15888`.

### Variáveis de configuração

As configurações de JWT ficam em `appsettings.Development.json`:

```json
{
  "Jwt": {
    "Key": "<segredo>",
    "Issuer": "SlimTrack",
    "Audience": "SlimTrack",
    "ExpirationHours": 8
  }
}
```

---

## 11. Decisões de Design

| Decisão | Motivo |
|---|---|
| Triagem assíncrona via RabbitMQ | A API responde imediatamente (201) sem esperar a classificação, melhorando a experiência do usuário |
| Outbox Pattern | Evita perda de eventos em caso de falha de rede ou reinicialização do broker |
| `IClassificationService` como abstração | Permite trocar a heurística por NLP real sem alterar a API ou os workers |
| UPDATE atômico com guard de status | Evita que dois workers classifiquem o mesmo chamado simultaneamente |
| JWT com roles | Diferencia usuário comum (leitura/abertura) de administrador (edição) sem infra adicional |
| .NET Aspire | Orquestra toda a infraestrutura local com um único `dotnet run`, sem escrever `docker-compose` manualmente |
