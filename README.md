# SlimTrack

> Sistema de gerenciamento de entregas com arquitetura orientada a eventos

## Índice

- [Visão Geral](#visão-geral)
- [Arquitetura](#arquitetura)
- [Tecnologias Utilizadas](#tecnologias-utilizadas)
- [Estratégias de Confiabilidade](#estratégias-de-confiabilidade)
- [Pré-requisitos](#pré-requisitos)
- [Como Executar](#como-executar)
- [Endpoints da API](#endpoints-da-api)
- [Monitoramento](#monitoramento)
- [Estrutura do Projeto](#estrutura-do-projeto)

---

## Visão Geral

Sistema de rastreamento de pedidos que processa entregas de forma assíncrona, emitindo eventos a cada mudança de status. 

### Funcionalidades

- Recebimento de pedidos via API REST
- Processamento assíncrono do fluxo de entrega (separação → transporte → entrega)
- Emissão de eventos para cada mudança de status
- Persistência confiável de pedidos e eventos
- Consulta de histórico completo de eventos por pedido

### Requisitos Atendidos

- Arquitetura orientada a eventos, desacoplada e escalável
- Comunicação via filas de mensagens
- **Consistência total**:  nenhum evento perdido mesmo em caso de falha
- **Baixa latência** no processamento
- **Resiliência**:  recuperação automática sem perda de dados
- Idempotência e retry automático
- Logs estruturados e monitoramento completo

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
- Garante consistência transacional (ACID)
- Migrations aplicadas automaticamente na inicialização

**RabbitMQ**
- Filas duráveis para cada etapa do fluxo
- Mensagens persistentes (sobrevivem a reinicializações, a não ser que o docker seja excluido)
- Confirmação de entrega (publisher confirms)

**Redis**
- Cache distribuído (Não tive o tempo para utilizar ele)

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

---

## Estratégias de Confiabilidade



---

## Pré-requisitos



---

## Como Executar



---

## Endpoints da API



---

## Monitoramento



---

## Estrutura do Projeto



---
