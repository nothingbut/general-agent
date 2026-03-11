# General Agent V2 架构文档

**版本:** 0.2.0
**更新日期:** 2026-03-11

---

## 📋 目录

- [概述](#概述)
- [设计原则](#设计原则)
- [分层架构](#分层架构)
- [核心模块](#核心模块)
- [数据流](#数据流)
- [技术决策](#技术决策)
- [扩展性](#扩展性)

---

## 概述

General Agent V2 采用**分层架构**和**依赖注入**设计，确保各层职责清晰、松耦合、易测试。

### 核心目标

1. **类型安全** - 利用 Rust 的类型系统在编译时捕获错误
2. **性能优先** - 零成本抽象，异步 I/O，高效内存使用
3. **可测试性** - Trait 抽象，依赖注入，Mock 友好
4. **可扩展性** - 插件化设计，模块化架构
5. **可维护性** - 清晰的代码组织，完整的文档

---

## 设计原则

### 1. 单一职责原则（SRP）

每个模块只负责一个功能域：

- `agent-core`: 领域模型和抽象
- `agent-storage`: 数据持久化
- `agent-llm`: LLM 通信
- `agent-skills`: 技能系统
- `agent-workflow`: 业务逻辑
- `agent-cli`: 用户界面

### 2. 依赖倒置原则（DIP）

高层模块不依赖低层模块，都依赖抽象（Traits）：

```rust
// 高层模块（agent-workflow）
pub struct SessionManager {
    session_repo: Arc<dyn SessionRepository>,  // 依赖抽象
    message_repo: Arc<dyn MessageRepository>,  // 依赖抽象
}

// 低层模块（agent-storage）实现抽象
impl SessionRepository for SqliteSessionRepository {
    // ...
}
```

### 3. 接口隔离原则（ISP）

小而专注的 Trait，避免臃肿的接口：

```rust
// 分离的 Trait
pub trait SessionRepository { /* ... */ }
pub trait MessageRepository { /* ... */ }

// 而不是一个大的 Repository trait
```

### 4. 开闭原则（OCP）

对扩展开放，对修改封闭：

```rust
// 添加新的 LLM 提供商：只需实现 LLMClient trait
impl LLMClient for NewLLMProvider {
    // ...
}
```

---

## 分层架构

```
┌────────────────────────────────────────────────────┐
│           Presentation Layer (表现层)               │
│          agent-cli + agent-tui (计划中)            │
│            命令行界面 / 终端 UI                     │
└─────────────────────┬──────────────────────────────┘
                      │
┌─────────────────────┴──────────────────────────────┐
│           Application Layer (应用层)                │
│               agent-workflow                        │
│       SessionManager + ConversationFlow             │
│      业务逻辑、流程编排（集成 MCP + RAG）           │
└──────────┬──────────────┬──────────────┬───────────┘
           │              │              │
┌──────────┴──────┬───────┴──────┬───────┴───────────┐
│  Infrastructure Layer (基础设施层)                  │
├─────────────────┼──────────────┼─────────────────────┤
│  agent-storage  │  agent-llm   │  agent-skills       │
│  SQLite 持久化   │  LLM 客户端   │  技能系统           │
├─────────────────┼──────────────┼─────────────────────┤
│   agent-mcp     │  agent-rag   │                     │
│   MCP 协议/客户端│  RAG 检索器   │                     │
└─────────────────┴──────────────┴─────────────────────┘
                      │
┌─────────────────────┴──────────────────────────────┐
│             Domain Layer (领域层)                   │
│                 agent-core                          │
│     领域模型、Traits、错误类型、业务规则             │
└────────────────────────────────────────────────────┘
```

### 层次说明

#### Domain Layer (agent-core)
**职责:** 定义核心概念和抽象

- 领域模型（Session, Message）
- Trait 定义（Repository, LLMClient）
- 错误类型
- 业务规则

**依赖:** 无（最底层）

#### Infrastructure Layer
**职责:** 实现技术细节

**agent-storage:**
- SQLite 数据库连接
- Repository 实现
- 数据库迁移

**agent-llm:**
- LLM 客户端实现（Anthropic, Ollama）
- HTTP 通信
- 流式响应处理

**agent-skills:**
- Markdown 解析
- 技能加载和注册
- 模板渲染

**agent-mcp:**
- JSON-RPC 2.0 协议实现
- Stdio 传输层
- MCP 服务器连接管理
- 工具发现和调用

**agent-rag:**
- 文档加载器（Markdown/PDF/Text）
- 文本分块器（固定/语义/递归）
- Embedding 生成（Ollama）
- 向量存储（Qdrant）
- 语义检索和重排序

**依赖:** agent-core

#### Application Layer (agent-workflow)
**职责:** 业务逻辑和流程

- SessionManager: 会话生命周期管理
- ConversationFlow: 对话流程编排
- 上下文管理
- 技能集成

**依赖:** agent-core + Infrastructure Layer

#### Presentation Layer (agent-cli)
**职责:** 用户交互

- 命令行解析
- 彩色输出
- 流式显示
- 错误展示

**依赖:** 所有下层

---

## 核心模块

### agent-core

**核心模型:**

```rust
// 会话实体
pub struct Session {
    pub id: Uuid,
    pub title: Option<String>,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
}

// 消息实体
pub struct Message {
    pub id: Uuid,
    pub session_id: Uuid,
    pub role: MessageRole,      // User/Assistant/System
    pub content: String,
    pub created_at: DateTime<Utc>,
}
```

**核心 Traits:**

```rust
#[async_trait]
pub trait SessionRepository: Send + Sync {
    async fn create(&self, session: Session) -> Result<Session>;
    async fn find_by_id(&self, id: Uuid) -> Result<Option<Session>>;
    async fn update(&self, session: Session) -> Result<()>;
    async fn delete(&self, id: Uuid) -> Result<()>;
    // ...
}

#[async_trait]
pub trait LLMClient: Send + Sync {
    async fn complete(&self, request: CompletionRequest) -> Result<CompletionResponse>;
    async fn stream(&self, request: CompletionRequest) -> Result<Box<dyn CompletionStream>>;
    async fn list_models(&self) -> Result<Vec<ModelInfo>>;
    fn provider_name(&self) -> &str;
}
```

---

### agent-storage

**数据库设计:**

```sql
-- 会话表
CREATE TABLE sessions (
    id TEXT PRIMARY KEY,
    title TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

-- 消息表
CREATE TABLE messages (
    id TEXT PRIMARY KEY,
    session_id TEXT NOT NULL,
    role TEXT NOT NULL,
    content TEXT NOT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY (session_id) REFERENCES sessions(id)
        ON DELETE CASCADE
);
```

**Repository 实现:**

```rust
pub struct SqliteSessionRepository {
    pool: SqlitePool,
}

#[async_trait]
impl SessionRepository for SqliteSessionRepository {
    async fn create(&self, session: Session) -> Result<Session> {
        sqlx::query(
            "INSERT INTO sessions (id, title, created_at, updated_at)
             VALUES (?, ?, ?, ?)"
        )
        .bind(&session.id.to_string())
        .bind(&session.title)
        .bind(&session.created_at.to_rfc3339())
        .bind(&session.updated_at.to_rfc3339())
        .execute(&self.pool)
        .await?;

        Ok(session)
    }
    // ...
}
```

**关键设计:**
- 使用动态查询（避免编译时依赖）
- 连接池管理
- 事务支持
- 迁移系统

---

### agent-llm

**Anthropic 客户端:**

```rust
pub struct AnthropicClient {
    client: reqwest::Client,
    api_key: String,
    base_url: String,
}

#[async_trait]
impl LLMClient for AnthropicClient {
    async fn complete(&self, request: CompletionRequest) -> Result<CompletionResponse> {
        // 1. 转换请求格式
        let anthropic_req = self.build_anthropic_request(request);

        // 2. 发送 HTTP 请求
        let response = self.client
            .post(&format!("{}/v1/messages", self.base_url))
            .header("x-api-key", &self.api_key)
            .json(&anthropic_req)
            .send()
            .await?;

        // 3. 解析响应
        let anthropic_resp: MessagesResponse = response.json().await?;

        // 4. 转换为统一格式
        Ok(self.convert_response(anthropic_resp))
    }
    // ...
}
```

**Ollama 客户端:**

```rust
pub struct OllamaClient {
    client: reqwest::Client,
    config: OllamaConfig,
}

// 流式响应处理
pub struct OllamaStream {
    stream: Pin<Box<dyn Stream<Item = Result<Bytes>> + Send>>,
    buffer: String,
}

#[async_trait]
impl CompletionStream for OllamaStream {
    async fn next(&mut self) -> Result<Option<StreamChunk>> {
        // 逐行解析 JSON
        while let Some(bytes) = self.stream.next().await {
            let line = String::from_utf8_lossy(&bytes?);
            if let Ok(chunk) = serde_json::from_str::<ChatStreamResponse>(&line) {
                return Ok(Some(StreamChunk {
                    delta: chunk.message.content,
                    is_final: chunk.done,
                    finish_reason: if chunk.done { Some("stop".into()) } else { None },
                }));
            }
        }
        Ok(None)
    }
}
```

---

### agent-skills

**技能定义:**

```markdown
---
name: greeting
description: Greet the user
namespace: personal
parameters:
  - name: user_name
    type: string
    required: true
    description: The user's name
  - name: tone
    type: string
    required: false
    default: friendly
    description: Greeting tone
---

Hello {user_name}!

{#if tone == "formal"}
How may I assist you today?
{else}
What's up? How can I help?
{/if}
```

**加载流程:**

```rust
// 1. 扫描目录
let loader = SkillLoader::new(path);

// 2. 解析文件
let skills = loader.load_all()?;

// 3. 注册到注册表
let mut registry = SkillRegistry::new();
for skill in skills {
    registry.register(skill);
}

// 4. 执行技能
let executor = SkillExecutor::new();
let prompt = executor.execute(skill, parameters)?;
```

**关键特性:**
- Markdown + YAML frontmatter
- 参数验证
- 模板渲染
- 命名空间管理
- .ignore 支持

---

### agent-workflow

**SessionManager:**

```rust
pub struct SessionManager {
    session_repo: Arc<dyn SessionRepository>,
    message_repo: Arc<dyn MessageRepository>,
}

impl SessionManager {
    // 会话管理
    pub async fn create_session(&self, title: Option<String>) -> Result<Session>;
    pub async fn load_session(&self, id: Uuid) -> Result<Session>;
    pub async fn delete_session(&self, id: Uuid) -> Result<()>;
    pub async fn list_sessions(&self, limit: u32, offset: u32) -> Result<Vec<Session>>;
    pub async fn search_sessions(&self, query: &str, limit: u32) -> Result<Vec<Session>>;

    // 消息管理
    pub async fn add_message(&self, session_id: Uuid, message: Message) -> Result<()>;
    pub async fn get_messages(&self, session_id: Uuid, limit: Option<u32>) -> Result<Vec<Message>>;
    pub async fn get_recent_messages(&self, session_id: Uuid, limit: u32) -> Result<Vec<Message>>;
    pub async fn count_messages(&self, session_id: Uuid) -> Result<u32>;
}
```

**ConversationFlow:**

```rust
pub struct ConversationFlow {
    session_manager: Arc<SessionManager>,
    llm_client: Arc<dyn LLMClient>,
    config: ConversationConfig,
    skill_registry: Option<Arc<SkillRegistry>>,
    skill_executor: SkillExecutor,
}

impl ConversationFlow {
    // 核心流程
    pub async fn send_message(&self, session_id: Uuid, content: String) -> Result<String> {
        // 1. 检测技能调用
        let processed_content = if self.is_skill_invocation(&content) {
            self.handle_skill_invocation(&content).await?
        } else {
            content
        };

        // 2. 保存用户消息
        self.session_manager.add_message(session_id, user_message).await?;

        // 3. 构建上下文
        let context = self.build_context(session_id).await?;

        // 4. 调用 LLM
        let response = self.llm_client.complete(request).await?;

        // 5. 保存助手响应
        self.session_manager.add_message(session_id, assistant_message).await?;

        Ok(response.content)
    }

    // 上下文管理
    pub async fn build_context(&self, session_id: Uuid) -> Result<Vec<Message>> {
        if self.config.max_context_messages > 0 {
            self.session_manager.get_recent_messages(session_id, limit).await
        } else {
            self.session_manager.get_messages(session_id, None).await
        }
    }
}
```

---

## 数据流

### 1. 基本对话流程

```
用户输入
    ↓
[CLI] 解析命令
    ↓
[Workflow] send_message()
    ↓
[Workflow] 检测技能调用？
    ├─ Yes → [Skills] 执行技能 → 生成 prompt
    └─ No → 使用原始输入
    ↓
[Workflow] 保存用户消息
    ↓
[Storage] INSERT INTO messages
    ↓
[Workflow] 构建上下文
    ↓
[Storage] SELECT messages WHERE session_id
    ↓
[Workflow] 调用 LLM
    ↓
[LLM] HTTP 请求 → LLM API
    ↓
[LLM] 返回响应
    ↓
[Workflow] 保存助手响应
    ↓
[Storage] INSERT INTO messages
    ↓
[CLI] 显示响应
    ↓
用户看到输出
```

### 2. 流式响应流程

```
用户输入
    ↓
[CLI] --stream 参数
    ↓
[Workflow] send_message_stream()
    ↓
[LLM] 建立 SSE/JSON stream 连接
    ↓
[LLM] Stream → 逐块返回
    ↓
[CLI] 实时显示每个 chunk
    ↓
[Workflow] StreamContext.save_response()
    ↓
[Storage] INSERT 完整响应
```

### 3. 技能执行流程

```
@greeting user_name='Alice'
    ↓
[Workflow] is_skill_invocation() → true
    ↓
[Skills] SkillExecutor.parse_invocation()
    → skill_name = "greeting"
    → params = {user_name: "Alice"}
    ↓
[Skills] SkillRegistry.get("greeting")
    → 获取技能定义
    ↓
[Skills] SkillExecutor.execute()
    ↓
[Skills] 验证参数（required, type）
    ↓
[Skills] 渲染模板
    → "Hello Alice! How can I help you today?"
    ↓
[Workflow] 使用渲染后的 prompt 作为用户消息
    ↓
继续正常流程 ...
```

---

## 技术决策

### 1. 为什么使用 Arc<dyn Trait>？

**优点:**
- 运行时多态（支持不同实现）
- 依赖注入友好
- 测试时可轻松 mock

**缺点:**
- 轻微的性能开销（vtable）
- 不能使用 Trait 的关联类型

**决策:** 可测试性和灵活性更重要

### 2. 为什么使用动态 SQL 查询？

**选项 A: 编译时验证（sqlx::query_as!）**
- ✅ 编译时检查 SQL
- ❌ 需要编译时数据库连接
- ❌ CI/CD 复杂

**选项 B: 动态查询（sqlx::query）**
- ✅ 无编译时依赖
- ✅ CI/CD 简单
- ❌ SQL 错误运行时才发现

**决策:** 选择 B + 完整的测试覆盖

### 3. 为什么技能系统独立？

**分离理由:**
- 技能系统可以独立演进
- 可以在其他项目中复用
- 测试更容易隔离
- 60 个单元测试证明设计正确

### 4. 异步运行时选择

**Tokio vs async-std:**

**选择 Tokio 原因:**
- 生态更成熟（大多数库基于 Tokio）
- 性能更好
- 工具链完善（tokio-console）

---

### agent-mcp

**MCP (Model Context Protocol) 架构:**

```rust
// Trait 定义
#[async_trait]
pub trait MCPClient: Send + Sync {
    async fn list_tools(&self) -> Result<Vec<ToolDefinition>>;
    async fn call_tool(&self, name: &str, args: Value) -> Result<Value>;
}

// JSON-RPC 协议
pub struct JsonRpcRequest {
    pub jsonrpc: String,  // "2.0"
    pub id: String,
    pub method: String,
    pub params: Option<Value>,
}

// Stdio 传输层
pub struct StdioTransport {
    child: Child,
    stdin: ChildStdin,
    stdout: BufReader<ChildStdout>,
}

// MCP 客户端实现
pub struct DefaultMCPClient {
    protocol: Arc<JsonRpcProtocol>,
    transport: Arc<Mutex<StdioTransport>>,
    config: MCPServerConfig,
}
```

**工具调用流程:**

```
1. ConversationFlow 初始化时连接 MCP 服务器
   ↓
2. 列出所有可用工具（list_tools）
   ↓
3. 将工具列表传递给 LLM（作为可用工具）
   ↓
4. LLM 决定调用工具，返回 tool_use
   ↓
5. ConversationFlow 通过 MCP 客户端调用工具
   ↓
6. 工具执行结果返回给 LLM
   ↓
7. LLM 基于工具结果生成最终响应
```

**配置示例:**

```rust
let mcp_config = MCPServerConfig {
    name: "filesystem".to_string(),
    command: "mcp-server-filesystem".to_string(),
    args: vec!["--root".to_string(), "/path/to/files".to_string()],
    env: HashMap::new(),
    timeout: Duration::from_secs(30),
};

let mcp_client = DefaultMCPClient::connect(mcp_config).await?;
let flow = ConversationFlow::new(session_manager, llm_client, config)
    .with_mcp(vec![Arc::new(mcp_client)]);
```

---

### agent-rag

**RAG (Retrieval Augmented Generation) 架构:**

```rust
// Trait 定义
#[async_trait]
pub trait RAGRetriever: Send + Sync {
    async fn retrieve(&self, query: &str, top_k: usize) -> Result<Vec<Document>>;
    async fn index_document(&self, doc: Document) -> Result<()>;
}

// Embedder Trait
#[async_trait]
pub trait Embedder: Send + Sync {
    async fn embed(&self, text: &str) -> Result<Vec<f32>>;
    async fn embed_batch(&self, texts: &[String]) -> Result<Vec<Vec<f32>>>;
}

// Vector Store Trait
#[async_trait]
pub trait VectorStore: Send + Sync {
    async fn create_collection(&self, name: &str, dimension: usize) -> Result<()>;
    async fn insert(&self, collection: &str, id: String, vector: Vec<f32>, metadata: HashMap<String, String>) -> Result<()>;
    async fn search(&self, collection: &str, query: Vec<f32>, top_k: usize) -> Result<Vec<SearchResult>>;
}
```

**RAG 流程:**

```
1. 文档索引（离线）
   DocumentLoader → Chunker → Embedder → VectorStore

2. 检索（实时）
   用户查询 → Embedder → VectorStore.search → 相关文档

3. 增强生成
   相关文档 + 用户查询 → ConversationFlow → LLM → 响应
```

**实现组件:**

- **DocumentLoader**: 加载各种格式文档（Markdown, PDF, Text）
- **Chunker**: 智能分块（固定大小、语义、递归）
- **OllamaEmbedder**: 使用 Ollama 生成 embedding（nomic-embed-text, 768维）
- **QdrantStore**: Qdrant 向量数据库客户端
- **DefaultRAGRetriever**: 完整的 RAG 检索器

**使用示例:**

```rust
// 创建 RAG 检索器
let embedder = Arc::new(OllamaEmbedder::with_defaults());
let vector_store = Arc::new(QdrantStore::with_defaults().await?);
vector_store.create_collection("docs", 768).await?;

let retriever = Arc::new(DefaultRAGRetriever::new(
    embedder,
    vector_store,
    "docs".to_string(),
));

// 索引文档
let loader = MarkdownLoader;
retriever.index_document_with_loader(
    &loader,
    "path/to/doc.md",
    ChunkConfig::default(),
).await?;

// 使用 RAG 增强的对话
let flow = ConversationFlow::new(session_manager, llm_client, config)
    .with_rag(retriever);
```

---

## 扩展性

### 1. 添加新的 LLM 提供商

```rust
// 1. 实现 LLMClient trait
pub struct OpenAIClient { /* ... */ }

#[async_trait]
impl LLMClient for OpenAIClient {
    async fn complete(&self, request: CompletionRequest) -> Result<CompletionResponse> {
        // 实现 OpenAI API 调用
    }
    // ...
}

// 2. 在 CLI 中注册
match cli.provider.as_str() {
    "anthropic" => Arc::new(AnthropicClient::from_env()?),
    "ollama" => Arc::new(OllamaClient::new(config)?),
    "openai" => Arc::new(OpenAIClient::new(config)?),  // 新增
    _ => bail!("Unknown provider"),
}
```

### 2. 添加新的存储后端

```rust
// 1. 实现 Repository traits
pub struct PostgresSessionRepository { /* ... */ }

#[async_trait]
impl SessionRepository for PostgresSessionRepository {
    // 实现 PostgreSQL 操作
}

// 2. 在初始化时选择
let session_repo: Arc<dyn SessionRepository> = match backend {
    "sqlite" => Arc::new(SqliteSessionRepository::new(pool)),
    "postgres" => Arc::new(PostgresSessionRepository::new(pool)),
    _ => bail!("Unknown backend"),
};
```

### 3. 添加新功能模块

遵循分层原则：

1. **Domain (agent-core)**: 定义新的模型和 Trait
2. **Infrastructure**: 实现具体技术
3. **Application (agent-workflow)**: 编排业务逻辑
4. **Presentation (agent-cli)**: 暴露给用户

---

## 总结

General Agent V2 通过：

- ✅ **清晰的分层架构** - 职责分离，易于理解
- ✅ **Trait 抽象** - 松耦合，可测试
- ✅ **依赖注入** - 灵活配置，Mock 友好
- ✅ **异步设计** - 高性能，并发安全
- ✅ **模块化** - 独立开发，易于维护

构建了一个**高性能、类型安全、可扩展**的 AI Agent 框架。

---

**更新日期:** 2026-03-11
**版本:** 0.2.0
