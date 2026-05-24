# CallsTrend

Sistema acadêmico de Help Desk com triagem automática de chamados por IA.

## 1. Entendimento consolidado do projeto

### 1.1 Objetivo do produto

O projeto tem como foco reduzir a triagem manual inicial de chamados técnicos. A ideia central é permitir que o usuário abra um chamado com texto livre e que o sistema atribua automaticamente:

- uma **categoria**;
- uma **prioridade**;
- um **status inicial coerente com o fluxo**.

O administrador continua responsável pela governança do atendimento, podendo revisar, corrigir e evoluir o chamado ao longo do processo.

### 1.2 Escopo funcional

Os requisitos funcionais descritos no material original levam a um fluxo principal bem definido:

1. cadastro e autenticação de usuários;
2. abertura de chamado com título e descrição;
3. classificação automática por categoria;
4. classificação automática por prioridade;
5. consulta do status do chamado;
6. gestão administrativa dos chamados;
7. atualização do status de atendimento.

### 1.3 Escopo não funcional

O README original deixa claro que o projeto deve ser:

- simples de usar;
- acessível via navegador;
- implementado com tecnologias gratuitas ou open-source;
- demonstrável em ambiente local;
- organizado com boas práticas de arquitetura e versionamento.

Além disso, a triagem automática deve ter caráter **demonstrativo**, não comercial, e responder em tempo adequado para uma apresentação acadêmica.

### 1.4 Limites do escopo

Ficam fora do escopo nesta fase:

- integrações com ferramentas corporativas reais;
- notificações por e-mail, SMS ou apps externos;
- SLAs reais de operação;
- alta disponibilidade e produção;
- autenticação corporativa avançada;
- monitoramento em tempo real.

### 1.5 Arquitetura pretendida

O material modela a solução em **4 camadas**:

1. **Apresentação**: interface web do usuário e painel administrativo;
2. **Aplicação / Backend**: autenticação, regras de negócio, orquestração dos chamados;
3. **Inteligência Artificial**: módulo isolado responsável pela classificação textual;
4. **Dados**: persistência de usuários, chamados e logs.

Essa arquitetura favorece evolução incremental e está alinhada com uma implementação baseada em **SOLID**, principalmente por permitir:

- separar responsabilidades;
- isolar contratos entre camadas;
- trocar a implementação da triagem sem reescrever o domínio;
- manter baixo acoplamento entre API, regras de negócio e infraestrutura.

### 1.6 Stack sugerida pelo próprio material

O README aponta a seguinte direção tecnológica:

- **Frontend**: React.js;
- **Backend**: Python com FastAPI ou Flask;
- **Módulo de IA**: Scikit-learn ou spaCy;
- **Banco de dados**: PostgreSQL ou SQLite.

### 1.7 Interpretação prática para a execução

Com base no escopo e na arquitetura, a evolução mais fluida do projeto é:

1. provar o fluxo principal com uma **POC funcional**;
2. manter a triagem como um módulo substituível;
3. validar o processo fim a fim antes de investir em autenticação robusta e frontend completo;
4. só depois acoplar persistência relacional definitiva e modelo de IA treinado.

---

## 2. Steps de desenvolvimento para um projeto fluido

## Fase 0 — Fundação do produto

- consolidar escopo, regras de negócio e atores;
- transformar a documentação acadêmica em backlog técnico;
- definir convenções de pastas, camadas e contratos;
- decidir o menor recorte demonstrável da aplicação.

## Fase 1 — POC do fluxo principal

- criar backend inicial;
- expor endpoint para abertura de chamados;
- implementar triagem automática demonstrativa;
- persistir em memória ou SQLite simples;
- listar chamados e permitir atualização administrativa;
- documentar execução local.

**Objetivo da fase:** provar que o coração do produto funciona.

## Fase 2 — Consolidação do domínio

- introduzir autenticação básica;
- separar claramente entidades, casos de uso e adaptadores;
- registrar logs de classificação;
- padronizar enums, DTOs e contratos de resposta;
- ampliar testes automatizados de domínio e API.

**Objetivo da fase:** reduzir débito técnico e preparar escalabilidade.

## Fase 3 — Interface web

- criar tela de abertura de chamado;
- criar tela de acompanhamento do usuário;
- criar painel administrativo;
- integrar frontend com a API;
- tratar feedback de carregamento, sucesso e erro.

**Objetivo da fase:** tornar a experiência demonstrável para apresentação.

## Fase 4 — Persistência e auditoria

- migrar para banco relacional definitivo;
- armazenar usuários, chamados e histórico;
- registrar confiança e justificativa da classificação;
- permitir trilha de auditoria das correções administrativas.

**Objetivo da fase:** aproximar a solução do desenho arquitetural completo.

## Fase 5 — Evolução real do módulo de IA

- substituir heurísticas por pipeline de NLP;
- preparar conjunto de exemplos;
- treinar e validar modelo;
- medir precisão por categoria e prioridade;
- manter fallback para classificação segura.

**Objetivo da fase:** evoluir a POC para um classificador academicamente consistente.

## Fase 6 — Qualidade de entrega

- ampliar cobertura de testes;
- revisar segurança mínima;
- revisar performance de triagem;
- preparar roteiro de apresentação;
- fechar documentação final da disciplina.

---

## 3. Primeira POC iniciada nesta branch

Esta branch inicia a aplicação com um recorte pequeno, mas demonstrável:

- backend em **FastAPI**;
- arquitetura em camadas com foco em **SOLID**;
- domínio de chamados separado da infraestrutura;
- serviço de classificação demonstrativo baseado em palavras-chave;
- endpoints para criar, listar e atualizar chamados;
- testes automatizados do fluxo principal.

### Decisões desta POC

- a triagem é **heurística**, para acelerar a prova de conceito;
- a persistência está **em memória**, para reduzir complexidade inicial;
- autenticação completa foi deixada para a próxima fase;
- a API foi priorizada antes do frontend, porque ela representa o núcleo do sistema.

### Como SOLID foi aplicado

- **S**: entidades, casos de uso, API e repositório estão separados;
- **O**: o classificador pode ser substituído sem alterar os casos de uso;
- **L**: implementações concretas respeitam os contratos do domínio;
- **I**: portas pequenas e específicas para repositório e classificador;
- **D**: casos de uso dependem de abstrações, não de classes concretas.

---

## 4. Como executar a POC

### Requisitos

- Python 3.11+

### Instalação

```bash
cd /home/runner/work/CallsTrend/CallsTrend
python -m pip install -e .[dev]
```

### Subir a API

```bash
cd /home/runner/work/CallsTrend/CallsTrend
uvicorn callstrend.main:app --app-dir src --reload
```

### Rodar testes

```bash
cd /home/runner/work/CallsTrend/CallsTrend
pytest
```

### Endpoints da POC

- `GET  /health`
- `POST /api/v1/tickets`
- `GET  /api/v1/tickets`
- `PATCH /api/v1/tickets/{ticket_id}`

---

## API — Documentação dos Endpoints

### Swagger interativo

Com a API em execução, acesse a documentação interativa nos endereços abaixo:

| Interface | URL |
|---|---|
| **Swagger UI** (recomendado) | `http://localhost:8000/docs` |
| **ReDoc** | `http://localhost:8000/redoc` |
| **OpenAPI JSON** | `http://localhost:8000/openapi.json` |

Arquivo separado (somente API): `src/callstrend/api/doc/swagger.md`

---

### Enums e valores permitidos

| Enum | Campo | Valores aceitos |
|---|---|---|
| **Categoria** | `category` | `Hardware`, `Software`, `Rede`, `Acesso`, `Outros` |
| **Prioridade** | `priority` | `Baixa`, `Media`, `Alta` |
| **Status** | `status` | `EmTriagem`, `Aberto`, `EmAtendimento`, `Resolvido`, `Fechado` |

---

### GET /health

Verifica se a API está no ar.

**Autenticação:** não necessária

**Response 200 — OK**
```json
{
  "status": "ok"
}
```

---

### POST /api/v1/tickets

Abre um novo chamado técnico e executa a triagem automática por IA.

**Autenticação:** não necessária (fase 1 — POC)

#### Fluxo interno

1. O chamado é registrado com status `EmTriagem`.
2. O módulo de IA analisa título e descrição via palavras-chave ponderadas.
3. O chamado é atualizado para `Aberto` com categoria e prioridade inferidas.
4. A resposta retorna o chamado classificado.

#### Request body

```json
{
  "title": "VPN sem conectar",
  "description": "Não consigo acessar o sistema interno pela VPN desde a manhã.",
  "requester_name": "Maria Silva",
  "requester_email": "maria@empresa.com"
}
```

| Campo | Tipo | Obrigatório | Regras |
|---|---|---|---|
| `title` | `string` | ✅ | 3–120 caracteres |
| `description` | `string` | ✅ | 10–2000 caracteres |
| `requester_name` | `string` | ✅ | 3–80 caracteres |
| `requester_email` | `string` | ✅ | 5–160 caracteres |

#### Response 201 — Created

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "VPN sem conectar",
  "description": "Não consigo acessar o sistema interno pela VPN desde a manhã.",
  "requester_name": "Maria Silva",
  "requester_email": "maria@empresa.com",
  "created_at": "2026-05-12T21:00:00Z",
  "status": "Aberto",
  "category": "Rede",
  "priority": "Alta",
  "classification_confidence": 0.83,
  "classification_rationale": "Categoria sugerida: Rede (score 2). Prioridade sugerida: Alta (score 2)."
}
```

| Campo | Tipo | Descrição |
|---|---|---|
| `id` | `string` (UUID v4) | Identificador único do chamado |
| `title` | `string` | Título informado |
| `description` | `string` | Descrição informada |
| `requester_name` | `string` | Nome do solicitante |
| `requester_email` | `string` | E-mail do solicitante |
| `created_at` | `datetime` (ISO 8601 UTC) | Data/hora de abertura |
| `status` | `string` (enum) | Status atual do chamado |
| `category` | `string` (enum) | Categoria atribuída pela IA |
| `priority` | `string` (enum) | Prioridade atribuída pela IA |
| `classification_confidence` | `float` (0.0–1.0) | Nível de confiança da classificação |
| `classification_rationale` | `string` | Justificativa textual da IA |

#### Outros códigos

| Código | Situação |
|---|---|
| `422 Unprocessable Entity` | Campos inválidos ou ausentes |

---

### GET /api/v1/tickets

Retorna todos os chamados registrados com sua classificação atual.

**Autenticação:** não necessária (fase 1 — POC)

#### Response 200 — OK

Array de objetos `TicketResponse` (mesma estrutura do `POST`).

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "VPN sem conectar",
    "description": "Não consigo acessar o sistema interno pela VPN desde a manhã.",
    "requester_name": "Maria Silva",
    "requester_email": "maria@empresa.com",
    "created_at": "2026-05-12T21:00:00Z",
    "status": "Aberto",
    "category": "Rede",
    "priority": "Alta",
    "classification_confidence": 0.83,
    "classification_rationale": "Categoria sugerida: Rede (score 2). Prioridade sugerida: Alta (score 2)."
  }
]
```

Quando não há chamados: retorna `[]`.

---

### PATCH /api/v1/tickets/{ticket_id}

Atualiza um chamado existente — uso administrativo.

**Autenticação:** não necessária (fase 1 — POC)

#### Path parameter

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `ticket_id` | `string` (UUID) | ID do chamado a ser atualizado |

#### Request body

Todos os campos são **opcionais**. Apenas os campos enviados serão alterados.

```json
{
  "status": "EmAtendimento",
  "category": "Rede",
  "priority": "Alta"
}
```

| Campo | Tipo | Obrigatório | Valores aceitos |
|---|---|---|---|
| `status` | `string` (enum) | ❌ | `EmTriagem`, `Aberto`, `EmAtendimento`, `Resolvido`, `Fechado` |
| `category` | `string` (enum) | ❌ | `Hardware`, `Software`, `Rede`, `Acesso`, `Outros` |
| `priority` | `string` (enum) | ❌ | `Baixa`, `Media`, `Alta` |

#### Response 200 — OK

Objeto `TicketResponse` com os dados atualizados.

#### Outros códigos

| Código | Situação |
|---|---|
| `404 Not Found` | `ticket_id` não encontrado |
| `422 Unprocessable Entity` | Valor de enum inválido |

---

### Exemplos com curl

**Abrir chamado:**
```bash
curl -X POST http://localhost:8000/api/v1/tickets \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Impressora não imprime",
    "description": "A impressora do setor financeiro parou de funcionar após atualização do driver.",
    "requester_name": "Carlos Mendes",
    "requester_email": "carlos@empresa.com"
  }'
```

**Listar chamados:**
```bash
curl http://localhost:8000/api/v1/tickets
```

**Atualizar status e corrigir prioridade:**
```bash
curl -X PATCH http://localhost:8000/api/v1/tickets/{ticket_id} \
  -H "Content-Type: application/json" \
  -d '{"status": "EmAtendimento", "priority": "Alta"}'
```

---

## 5. Próximos passos recomendados

1. adicionar cadastro e autenticação;
2. persistir chamados em SQLite;
3. registrar logs de classificação;
4. criar frontend para usuário e administrador;
5. trocar o classificador heurístico por modelo de NLP;
6. ampliar histórico e auditoria administrativa.

---

## 6. Base conceitual do projeto

## 6.1 Entregas acadêmicas esperadas

- TAP;
- cronograma inicial;
- declaração de escopo;
- EAP/WBS;
- requisitos funcionais e não funcionais;
- diagramas do sistema;
- protótipo funcional;
- módulo de triagem automática com IA;
- relatório de testes;
- documentação final.

## 6.2 Requisitos funcionais de referência

- **RF01** – cadastro e autenticação;
- **RF02** – abertura de chamados com descrição textual;
- **RF03** – classificação automática por categoria;
- **RF04** – classificação automática por prioridade;
- **RF05** – acompanhamento do status;
- **RF06** – gestão administrativa;
- **RF07** – atualização do status do chamado.

## 6.3 Requisitos não funcionais de referência

- interface simples e intuitiva;
- resposta adequada da triagem;
- uso de tecnologias open-source;
- acesso via navegador;
- organização do código e versionamento.

## 6.4 Critérios de aceitação de referência

- as funcionalidades do fluxo principal devem estar implementadas;
- a triagem automática precisa ser demonstrável;
- categoria e prioridade devem ser visíveis;
- o sistema deve funcionar localmente;
- a documentação deve estar organizada.

## 6.5 Restrições e premissas

- desenvolvimento individual;
- orçamento zero;
- base de dados reduzida ou simulada;
- uso acadêmico;
- ambiente local;
- IA com caráter demonstrativo.

---

## 7. Diagramas

## 7.1 Diagrama de Classes

```mermaid
classDiagram
direction LR

class Usuario {
  +UUID id
  +string nome
  +string email
  +string senhaHash
  +autenticar(email, senha)
  +abrirChamado(titulo, descricao)
  +consultarChamados()
}

class Administrador {
  +validarClassificacao(chamadoId)
  +corrigirCategoria(chamadoId, categoria)
  +corrigirPrioridade(chamadoId, prioridade)
  +alterarStatus(chamadoId, status)
  +visualizarPainel()
}

Usuario <|-- Administrador

class Chamado {
  +UUID id
  +string titulo
  +string descricao
  +datetime dataHoraAbertura
  +StatusChamado status
  +Categoria categoria
  +Prioridade prioridade
  +registrar()
  +atualizarStatus(status)
  +atualizarClassificacao(categoria, prioridade)
}

class ClassificacaoIA {
  +Categoria categoriaSugerida
  +Prioridade prioridadeSugerida
  +float confianca
  +datetime dataHoraClassificacao
}

class LogClassificacao {
  +UUID id
  +datetime dataHora
  +string textoEntrada
  +Categoria categoriaGerada
  +Prioridade prioridadeGerada
  +float tempoProcessamento
  +string observacao
}

class Backend {
  +abrirChamado(usuarioId, titulo, descricao)
  +solicitarTriagem(texto)
  +salvarChamado(chamado)
  +autenticarUsuario(email, senha)
  +atualizarChamado(chamadoId, dados)
}

class ModuloIA {
  +classificarChamado(titulo, descricao)
  +processarTexto(texto)
}

class AutenticacaoService {
  +login(email, senha)
  +gerarHash(senha)
  +validarSenha(senha, hash)
}

class UsuarioRepository {
  +salvar(usuario)
  +buscarPorEmail(email)
  +buscarPorId(id)
}

class ChamadoRepository {
  +salvar(chamado)
  +buscarPorId(id)
  +listarPorUsuario(usuarioId)
  +atualizar(chamado)
}

class LogRepository {
  +salvar(log)
}

class Categoria {
  <<enumeration>>
  Hardware
  Software
  Rede
  Acesso
  Outros
}

class Prioridade {
  <<enumeration>>
  Baixa
  Media
  Alta
}

class StatusChamado {
  <<enumeration>>
  Aberto
  EmTriagem
  EmAtendimento
  Resolvido
  Fechado
}

Usuario "1" --> "0..*" Chamado : abre
Chamado "1" --> "0..1" ClassificacaoIA : possui
Chamado "1" --> "0..*" LogClassificacao : gera

Backend --> ModuloIA : solicita classificação
Backend --> ChamadoRepository : persiste chamado
Backend --> UsuarioRepository : consulta usuário
Backend --> LogRepository : registra logs
Backend --> AutenticacaoService : autentica

ModuloIA --> ClassificacaoIA : produz
AutenticacaoService --> Usuario : valida acesso
Administrador --> Chamado : gerencia
```

## 7.2 Diagrama de Sequência

```mermaid
sequenceDiagram
autonumber
actor U as Usuário Final
participant W as Interface Web
participant B as Backend
participant IA as Módulo de IA
participant DB as Banco de Dados
actor A as Administrador

U->>W: Preenche título e descrição do chamado
W->>B: enviarChamado(titulo, descricao, usuarioId)

B->>B: validarDados()
B->>DB: registrar chamado(status=EmTriagem, dataHoraAbertura)
DB-->>B: chamadoId

B->>IA: classificarChamado(titulo, descricao)
IA->>IA: processarTexto()
IA-->>B: categoria, prioridade, confiança

B->>B: associarClassificacaoAoChamado()
B->>DB: atualizar chamado(categoria, prioridade, status=Aberto)
B->>DB: salvar log de classificação
DB-->>B: confirmação

B-->>W: chamado registrado com classificação automática
W-->>U: exibir número, categoria e prioridade

A->>W: acessar painel administrativo
W->>B: listar chamados()
B->>DB: consultar chamados
DB-->>B: lista de chamados
B-->>W: retornar chamados

A->>W: corrigir classificação / alterar status
W->>B: atualizarChamado(chamadoId, status, categoria, prioridade)
B->>DB: persistir alterações
DB-->>B: confirmação
B-->>W: atualização concluída
W-->>A: exibir sucesso
```

## 7.3 Diagrama de Atividades

```mermaid
flowchart TD
    A[Início] --> B[Usuário autentica no sistema]
    B --> C[Acessa formulário de abertura]
    C --> D[Informa título e descrição]
    D --> E[Backend valida dados]
    E --> F[Registrar chamado com data/hora e status EmTriagem]
    F --> G[Enviar título e descrição ao Módulo de IA]
    G --> H[IA processa texto]
    H --> I[Retornar categoria e prioridade sugeridas]
    I --> J[Backend salva classificação e log]
    J --> K[Atualizar chamado para status Aberto]
    K --> L[Exibir resultado ao usuário]

    L --> M{Administrador irá revisar?}
    M -- Não --> N[Fim]
    M -- Sim --> O[Administrador acessa painel]
    O --> P[Visualiza chamado e sugestão da IA]
    P --> Q{Classificação está correta?}
    Q -- Sim --> R[Administrador altera apenas status se necessário]
    Q -- Não --> S[Administrador corrige categoria e/ou prioridade]
    R --> T[Salvar alterações]
    S --> T
    T --> N[Fim]
```

## 7.4 Diagrama de Componentes

```mermaid
flowchart LR
    subgraph AP[Camada de Apresentação]
        UI[Interface Web]
        ADM[Painel Administrativo]
    end

    subgraph APP[Camada de Aplicação / Backend]
        AUTH[Serviço de Autenticação]
        CHAM[Serviço de Chamados]
        ORQ[Orquestrador de Triagem]
        API[API Backend]
    end

    subgraph IA[Camada de Inteligência Artificial]
        NLP[Módulo de Processamento de Linguagem Natural]
        CLASS[Motor de Classificação\nCategoria + Prioridade]
    end

    subgraph DADOS[Camada de Dados]
        USERDB[(Tabela Usuários)]
        TICKETDB[(Tabela Chamados)]
        LOGDB[(Tabela Logs de Classificação)]
    end

    UI --> API
    ADM --> API

    API --> AUTH
    API --> CHAM
    CHAM --> ORQ
    ORQ --> NLP
    NLP --> CLASS
    CLASS --> ORQ

    AUTH --> USERDB
    CHAM --> TICKETDB
    ORQ --> LOGDB
    ORQ --> TICKETDB
```
