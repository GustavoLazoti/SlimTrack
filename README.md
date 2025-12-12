# SlimTrack

> Sistema de gerenciamento de entregas com arquitetura orientada a eventos.
> Feito por: Gustavo Nunes Lazoti

## Índice

- [Visão Geral](#visão-geral)
- [Arquitetura](#arquitetura)
- [Tecnologias Utilizadas](#tecnologias-utilizadas)
- [Pré-requisitos](#pré-requisitos)
- [Como Executar](#como-executar)
- [Endpoints da API](#endpoints-da-api)
- [Credênciais do PostgreSQL e RabbitMQ](#credênciais-do-postgresql-e-rabbitmq)

---

## Visão Geral

Sistema de rastreamento de pedidos que processa entregas de forma assíncrona, emitindo eventos a cada mudança de status. 

### Funcionalidades

- Recebimento de pedidos via API REST
- Processamento assíncrono do fluxo de entrega (separação -> transporte -> entrega)
- Emissão de eventos para cada mudança de status
- Persistência confiável de pedidos e eventos
- Consulta de histórico completo de eventos por pedido

### Requisitos Atendidos

- Arquitetura orientada a eventos, desacoplada e escalável
- Comunicação via filas de mensagens
- Consistência:  nenhum evento perdido mesmo em caso de falha
- Baixa latência no processamento (Infelizmente o Redis não foi implementado a tempo, porém com ele seria bem mais eficiênte)
- Resiliência:  recuperação automática sem perda de dados (Caso os containeres não sejam excluidos)
- Idempotência e retry automático
- Logs estruturados e monitoramento

---

## Arquitetura
Fluxo Workers/Events:
<img width="1377" height="386" alt="fluxoWorkers drawio" src="https://github.com/user-attachments/assets/55261086-a680-4d3c-a767-3faa30c3e43f" />

Diagrama de Sequência do fluxo:
<img width="2550" height="1558" alt="diagramaDeSequencia" src="https://github.com/user-attachments/assets/bcea3efe-c667-4f51-8994-5e74c3330dd1" />

### Componentes Principais

**API REST**
- Recebe requisições HTTP
- Valida dados de entrada
- Persiste pedidos e eventos no banco
- Salva eventos na tabela Outbox

**OutboxPublisher Worker**
- Executa periodicamente (a cada 5 segundos)
- Busca eventos não publicados na tabela Outbox
- Publica no RabbitMQ
- Marca como publicado após confirmação

**Background Workers**
- Cada worker é responsável por uma transição de estado
- Consome eventos de filas específicas
- Simulam cada etapa (Possuem delay hardcoded)
- Atualiza estado do pedido no banco
- Publica evento para próxima etapa
- Envia ACK apenas após sucesso

**PostgreSQL**
- Armazena pedidos, eventos e mensagens outbox
- ACID
- Migrations aplicadas automaticamente na inicialização

**RabbitMQ**
- Filas duráveis para cada etapa do fluxo
- Mensagens persistentes (sobrevivem a reinicializações, a não ser que o docker seja excluido)
- Confirmação de entrega (publisher confirms)

**Redis**
- Cache (Não tive o tempo para utilizar ele)

**. NET Aspire**
- Orquestração de containers (PostgreSQL, RabbitMQ, Redis)
- Configuração automática de conexões
- Dashboard de observabilidade integrado
  
---

## Tecnologias Utilizadas

**. NET Aspire**
A escolha do Aspire foi pensada em ser uma frente inovadora e mantida pela própria Microsoft, com diversos benefícios.
O principal que reconheci é a capacidade de descobrir as diferentes dependências (como RabbitMQ, Redis, PostgreSQL) e, além da conexão, realizar a inicialização automática via Docker.
Esse ponto é muito benéfico para velocidade de desenvolvimento (não tive que perder tempo fazendo essas configurações), mas também no momento que alguém for testar o sistema, não terá que passar por muitas preocupações.
Sem contar com o dasboard do Aspire que pode ser muito mais explorado, cujo adiciona um grau de observabilidade muito interessante para o processo.

**RabbitMQ**
Foi escolhido por ser reconhecido entre o mundo de desenvolvimento e por possuir biblioteca nativa do Aspire, facilitando o processo de configuração.
A gestão que as filas do RabbitMQ entregam é importante para o projeto:  garante entrega confiável de mensagens através de confirmações (ACKs) e permite reprocessamento automático em caso de falha (NACK com requeue).
Pensando no ponto do documento em relação a "Persistência confiável de pedidos e eventos", as mensagens são persistentes e as filas são duráveis, garantindo que nenhum evento seja perdido mesmo em caso de reinicialização do broker.

**PostgreSQL**
O banco de dados, nesse caso, não é uma decisão tão preocupante a nível de aplicação, porém pensei nele em questão de escalabilidad do processo, já que pode escalar bem.
Outro ponto importante que dscobri é o suporte robusto a transações ACID, essencial para garantir a consistência entre a gravação do pedido e o registro do evento no Outbox.
Também possui biblioteca nativa ao Aspire.

**Entity Framework Core**
Responsável pelas Migrations automáticas aplicadas na inicialização com suporte nativo ao postgresqk, facilitando queries complicadas e relacionamentos!
Claro, também manutenido pela microsoft.
---

## Pré-requisitos

### Obrigatórios

- **[.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)**
- **[Docker Desktop](https://www.docker.com/products/docker-desktop)** 

> **Importante:** Você **não precisa** instalar PostgreSQL, RabbitMQ ou Redis localmente. O . NET Aspire automaticamente baixa as imagens Docker e gerencia os containers de forma automatizada.


## Como Executar
### Passo 1: Clonar o Repositório

```bash
git clone https://github.com/GustavoLazoti/SlimTrack.git
cd SlimTrack
```

### Passo 2: Restaurar Dependências

Baixe todos os pacotes NuGet definidos nos arquivos `.csproj`:

```bash
dotnet restore
```

Isso baixará automaticamente:
- Entity Framework Core e PostgreSQL (Npgsql)
- RabbitMQ
- Bibliotecas do .NET Aspire
- OpenTelemetry

### Passo 3: Executar o Projeto

**Opção A: Via Linha de Comando** (VS Code)

```bash
cd SlimTrack. AppHost
dotnet run
```

**Opção B: Via Visual Studio 2022**

1. Abra o arquivo `SlimTrack.slnx` no Visual Studio
2. Defina `SlimTrack.AppHost` como projeto de inicialização
3. Pressione **F5** ou clique em **Run**

---

### Passo 4: Execução das etapas de configuração e Aspire Dashboard

Aguarde o proceso inicializar containeres Docker, workers, migrations aplicadas, etc.


O dashboard Aspire deve abrir no seu navegador padrão, com essa cara:
<img width="1871" height="827" alt="image" src="https://github.com/user-attachments/assets/8938cef1-a986-43d2-9c5d-814b2e7979b5" />

Pronto! Está operacional!

### Posíveis erros:

**Erro:  "Docker daemon is not running"**

Solução: Inicie o Docker Desktop e aguarde até que esteja completamente inicializado.

**Erro: "Cannot connect to database"**

Causa: O container PostgreSQL ainda está inicializando ou a imagem ainda está sendo baixada. 

Solução: O sistema já possui retry automático (10 tentativas com delay de 3s). Aguarde alguns segundos.  Se persistir: 

## Endpoints da API
### Resumo dos Endpoints

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `POST` | `/api/orders` | Criar um novo pedido |
| `GET` | `/api/orders` | Listar todos os pedidos (paginado) |
| `GET` | `/api/orders/{id}` | Consultar pedido por ID |
| `GET` | `/api/orders/{id}/events` | Histórico de eventos do pedido |

### 1. Criar Pedido

**POST** `/api/orders`

Cria um novo pedido e inicia o fluxo de processamento assíncrono. 

**Request Body:**

```json
{
  "description": "Descrição do pedido"
}
```

**Response:** `201 Created`

```json
{
  "id":  "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "description": "Descrição do pedido",
  "currentStatus": 0,
  "createdAt": "2025-12-12T10:30:00Z",
  "updatedAt": "2025-12-12T10:30:00Z"
}
```


### 2. Consultar Pedido por ID

**GET** `/api/orders/{id}`

Retorna os detalhes de um pedido específico. 

**Path Parameters:**
- `id` (GUID) - ID do pedido

**Response:** `200 OK`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "description": "X",
  "currentStatus": 2,
  "createdAt":  "2025-12-12T10:30:00Z",
  "updatedAt": "2025-12-12T10:35:20Z"
}
```

### 3. Consultar Histórico de Eventos

**GET** `/api/orders/{id}/events`

Retorna o histórico completo de eventos de um pedido, ordenado cronologicamente.

**Response:** `200 OK`

```json
[
  {
    "id": "1a2b3c4d-5e6f-7g8h-9i0j-k1l2m3n4o5p6",
    "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": 0,
    "message": "Pedido recebido com sucesso",
    "timestamp": "2025-12-12T10:30:00Z"
  },
  {
    "id":  "2b3c4d5e-6f7g-8h9i-0j1k-l2m3n4o5p6q7",
    "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": 1,
    "message": "Pedido em processamento/separação",
    "timestamp": "2025-12-12T10:30:03Z"
  }
]
```

### 4. Listar Todos os Pedidos

**GET** `/api/orders`

Retorna uma lista paginada de todos os pedidos. 

**Query Parameters:**
- `page` (int, opcional) - Número da página (padrão: 1)
- `pageSize` (int, opcional) - Itens por página (padrão: 10, máx: 100)

**Response:** `200 OK`

```json
{
  "page": 1,
  "pageSize":  10,
  "total":  25,
  "data":  [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "description":  "X",
      "currentStatus": 4,
      "createdAt": "2025-12-12T10:30:00Z",
      "updatedAt": "2025-12-12T10:35:20Z"
    }
  ]
}
```

### Status do Pedido (Enum)

| Valor | Nome | Descrição | Transição |
|-------|------|-----------|-----------|
| `0` | `Received` | Pedido recebido | Inicial |
| `1` | `Processing` | Em processamento/separação | Após ~3s |
| `2` | `InTransit` | Em transporte | Após ~5s |
| `3` | `OutForDelivery` | Saiu para entrega | Após ~5s |
| `4` | `Delivered` | Entregue | Final (após ~5s) |

---
## Credênciais do PostgreSQL e RabbitMQ
Para descobrir as credênciais, é necessário checar as variáveis de ambiente dos recursos no Aspire dashboard:
### PostgreSQL
<img width="1851" height="920" alt="image" src="https://github.com/user-attachments/assets/56a6815d-fabe-47da-8fe7-3d5e15f980dc" />


### RabbitMQ
<img width="1865" height="925" alt="image" src="https://github.com/user-attachments/assets/35303db9-14ff-4008-85d5-3a6410da6880" />


Que é possível acessar seu próprio dashboard via:
<img width="1863" height="751" alt="image" src="https://github.com/user-attachments/assets/1ce82383-3f3d-41ab-8c9b-ccbfbb915add" />

Dashboard:
<img width="1798" height="872" alt="image" src="https://github.com/user-attachments/assets/1211da56-3b35-42e7-aa64-335fceceb418" />

