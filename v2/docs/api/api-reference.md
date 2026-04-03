# General Agent V2 API 文档

**版本:** 0.2.0
**更新日期:** 2026-03-11

---

## 📋 目录

- [核心 API (agent-core)](#核心-api-agent-core)
- [存储 API (agent-storage)](#存储-api-agent-storage)
- [LLM API (agent-llm)](#llm-api-agent-llm)
- [技能 API (agent-skills)](#技能-api-agent-skills)
- [工作流 API (agent-workflow)](#工作流-api-agent-workflow)
- [MCP API (agent-mcp)](#mcp-api-agent-mcp)
- [RAG API (agent-rag)](#rag-api-agent-rag)

---

## 核心 API (agent-core)

### 领域模型

#### Session - 会话

```rust
pub struct Session {
    pub id: Uuid,
    pub title: String,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
}
```

**字段说明:**
- `id`: 全局唯一标识符
- `title`: 会话标题
- `created_at`: 创建时间
- `updated_at`: 最后更新时间

#### Message - 消息

```rust
pub struct Message {
    pub id: Uuid,
    pub session_id: Uuid,
    pub role: MessageRole,
    pub content: String,
    pub created_at: DateTime<Utc>,
}

pub enum MessageRole {
    User,
    Assistant,
    System,
}
```

**字段说明:**
- `id`: 消息唯一标识符
- `session_id`: 所属会话 ID
- `role`: 消息角色（用户/助手/系统）
- `content`: 消息内容
- `created_at`: 创建时间

### 核心 Traits

#### SessionRepository - 会话存储

```rust
#[async_trait]
pub trait SessionRepository: Send + Sync {
    async fn create(&self, title: Option<String>) -> Result<Session>;
    async fn get(&self, id: Uuid) -> Result<Option<Session>>;
    async fn list(&self, limit: u32, offset: u32) -> Result<Vec<Session>>;
    async fn update_title(&self, id: Uuid, title: String) -> Result<()>;
    async fn delete(&self, id: Uuid) -> Result<()>;
    async fn search(&self, query: &str, limit: u32) -> Result<Vec<Session>>;
}
```

#### MessageRepository - 消息存储

```rust
#[async_trait]
pub trait MessageRepository: Send + Sync {
    async fn create(&self, session_id: Uuid, role: MessageRole, content: String) -> Result<Message>;
    async fn get_by_session(&self, session_id: Uuid, limit: Option<u32>) -> Result<Vec<Message>>;
    async fn count_by_session(&self, session_id: Uuid) -> Result<u32>;
    async fn delete_by_session(&self, session_id: Uuid) -> Result<()>;
}
```

#### LLMClient - LLM 客户端

```rust
#[async_trait]
pub trait LLMClient: Send + Sync {
    async fn complete(&self, request: CompletionRequest) -> Result<CompletionResponse>;
    async fn stream(&self, request: CompletionRequest) -> Result<Box<dyn CompletionStream>>;
    async fn list_models(&self) -> Result<Vec<ModelInfo>>;
    fn provider_name(&self) -> &str;
}
```

**示例:**

```rust
use agent_core::traits::LLMClient;

let client: Arc<dyn LLMClient> = Arc::new(AnthropicClient::from_env()?);

// 非流式调用
let response = client.complete(CompletionRequest {
    messages: vec![
        Message { role: MessageRole::User, content: "Hello".to_string() }
    ],
    model: "claude-3-5-sonnet-20241022".to_string(),
    temperature: Some(0.7),
    max_tokens: Some(1024),
}).await?;

// 流式调用
let mut stream = client.stream(request).await?;
while let Some(chunk) = stream.next().await? {
    print!("{}", chunk.delta);
}
```

---

## 存储 API (agent-storage)

### SqliteSessionRepository

```rust
pub struct SqliteSessionRepository {
    pool: SqlitePool,
}

impl SqliteSessionRepository {
    pub fn new(pool: SqlitePool) -> Self;
}
```

### SqliteMessageRepository

```rust
pub struct SqliteMessageRepository {
    pool: SqlitePool,
}

impl SqliteMessageRepository {
    pub fn new(pool: SqlitePool) -> Self;
}
```

### Database

```rust
pub struct Database {
    pool: SqlitePool,
}

impl Database {
    pub async fn new(path: impl AsRef<Path>) -> Result<Self>;
    pub async fn in_memory() -> Result<Self>;
    pub async fn migrate(&self) -> Result<()>;
    pub fn pool(&self) -> &SqlitePool;
}
```

**示例:**

```rust
use agent_storage::{Database, repository::*};

// 创建数据库
let db = Database::new("./agent.db").await?;
db.migrate().await?;

// 创建 repositories
let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));

// 使用
let session = session_repo.create(Some("My Session".to_string())).await?;
let message = message_repo.create(
    session.id,
    MessageRole::User,
    "Hello!".to_string()
).await?;
```

---

## LLM API (agent-llm)

### AnthropicClient

```rust
pub struct AnthropicClient {
    // 私有字段
}

impl AnthropicClient {
    pub fn new(api_key: String) -> Self;
    pub fn from_env() -> Result<Self>;
    pub fn with_config(config: AnthropicConfig) -> Self;
}
```

**配置:**

```rust
pub struct AnthropicConfig {
    pub api_key: String,
    pub base_url: String,  // 默认: "https://api.anthropic.com"
    pub timeout: Duration,  // 默认: 30秒
}
```

### OllamaClient

```rust
pub struct OllamaClient {
    // 私有字段
}

impl OllamaClient {
    pub fn new(config: OllamaConfig) -> Self;
    pub fn with_defaults() -> Self;
}
```

**配置:**

```rust
pub struct OllamaConfig {
    pub base_url: String,  // 默认: "http://localhost:11434"
    pub model: String,      // 默认: "qwen3.5:0.8b"
    pub timeout: Duration,  // 默认: 120秒
}
```

**示例:**

```rust
use agent_llm::{AnthropicClient, OllamaClient};

// Anthropic
let anthropic = AnthropicClient::from_env()?;

// Ollama
let ollama = OllamaClient::with_defaults();

// 自定义配置
let ollama_custom = OllamaClient::new(OllamaConfig {
    base_url: "http://remote-host:11434".to_string(),
    model: "llama3.2:3b".to_string(),
    timeout: Duration::from_secs(60),
});
```

---

## 技能 API (agent-skills)

### SkillRegistry - 技能注册表

```rust
pub struct SkillRegistry {
    // 私有字段
}

impl SkillRegistry {
    pub fn new() -> Self;
    pub fn load_from_directory(&mut self, dir: impl AsRef<Path>) -> Result<()>;
    pub fn register(&mut self, skill: SkillDefinition) -> Result<()>;
    pub fn get(&self, name: &str) -> Result<&SkillDefinition>;
    pub fn list(&self) -> Vec<&SkillDefinition>;
}
```

### SkillExecutor - 技能执行器

```rust
pub struct SkillExecutor {
    // 私有字段
}

impl SkillExecutor {
    pub fn new() -> Self;
    pub fn parse_invocation(&self, input: &str) -> Result<(String, HashMap<String, String>)>;
    pub fn execute(&self, skill: &SkillDefinition, params: HashMap<String, String>) -> Result<String>;
}
```

### SkillDefinition - 技能定义

```rust
pub struct SkillDefinition {
    pub name: String,
    pub description: String,
    pub namespace: Option<String>,
    pub parameters: Vec<SkillParameter>,
    pub template: String,
}

pub struct SkillParameter {
    pub name: String,
    pub param_type: String,
    pub required: bool,
    pub default: Option<String>,
    pub description: Option<String>,
}
```

**示例:**

```rust
use agent_skills::{SkillRegistry, SkillExecutor};

// 加载技能
let mut registry = SkillRegistry::new();
registry.load_from_directory("./skills")?;

// 执行技能
let executor = SkillExecutor::new();
let (skill_name, params) = executor.parse_invocation("@greeting user_name='Alice'")?;
let skill = registry.get(&skill_name)?;
let prompt = executor.execute(skill, params)?;

println!("{}", prompt);  // "Hello Alice! How can I help you today?"
```

---

## 工作流 API (agent-workflow)

### SessionManager - 会话管理器

```rust
pub struct SessionManager {
    // 私有字段
}

impl SessionManager {
    pub fn new(session_repo: Arc<dyn SessionRepository>, message_repo: Arc<dyn MessageRepository>) -> Self;

    // 会话操作
    pub async fn create_session(&self, title: Option<String>) -> Result<Session>;
    pub async fn get_session(&self, id: Uuid) -> Result<Option<Session>>;
    pub async fn list_sessions(&self, limit: u32, offset: u32) -> Result<Vec<Session>>;
    pub async fn search_sessions(&self, query: &str, limit: u32) -> Result<Vec<Session>>;
    pub async fn update_session_title(&self, id: Uuid, title: String) -> Result<()>;
    pub async fn delete_session(&self, id: Uuid) -> Result<()>;

    // 消息操作
    pub async fn add_message(&self, session_id: Uuid, role: MessageRole, content: String) -> Result<Message>;
    pub async fn get_messages(&self, session_id: Uuid, limit: Option<u32>) -> Result<Vec<Message>>;
    pub async fn get_recent_messages(&self, session_id: Uuid, limit: u32) -> Result<Vec<Message>>;
    pub async fn count_messages(&self, session_id: Uuid) -> Result<u32>;
}
```

### ConversationFlow - 对话流程

```rust
pub struct ConversationFlow {
    // 私有字段
}

impl ConversationFlow {
    pub fn new(session_manager: Arc<SessionManager>, llm_client: Arc<dyn LLMClient>, config: ConversationConfig) -> Self;
    pub fn with_defaults(session_manager: Arc<SessionManager>, llm_client: Arc<dyn LLMClient>) -> Self;

    // 扩展功能
    pub fn with_skills(self, registry: Arc<SkillRegistry>) -> Self;

    #[cfg(feature = "mcp")]
    pub fn with_mcp(self, clients: Vec<Arc<dyn MCPClient>>) -> Self;

    #[cfg(feature = "rag")]
    pub fn with_rag(self, retriever: Arc<dyn RAGRetriever>) -> Self;

    // 对话操作
    pub async fn send_message(&self, session_id: Uuid, content: String) -> Result<String>;
    pub async fn send_message_stream(&self, session_id: Uuid, content: String) -> Result<Box<dyn CompletionStream>>;
}
```

**示例:**

```rust
use agent_workflow::{SessionManager, ConversationFlow, ConversationConfig};

// 创建 SessionManager
let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

// 创建 ConversationFlow
let flow = ConversationFlow::new(
    session_manager.clone(),
    llm_client,
    ConversationConfig::default(),
);

// 创建会话并对话
let session = session_manager.create_session(Some("Test".to_string())).await?;
let response = flow.send_message(session.id, "Hello!".to_string()).await?;
println!("{}", response);

// 使用技能
let flow_with_skills = flow.with_skills(skill_registry);

// 使用 MCP
#[cfg(feature = "mcp")]
let flow_with_mcp = flow.with_mcp(vec![mcp_client]);

// 使用 RAG
#[cfg(feature = "rag")]
let flow_with_rag = flow.with_rag(rag_retriever);
```

---

## MCP API (agent-mcp)

### Traits

#### MCPClient

```rust
#[async_trait]
pub trait MCPClient: Send + Sync {
    async fn list_tools(&self) -> Result<Vec<ToolDefinition>>;
    async fn call_tool(&self, name: &str, args: Value) -> Result<Value>;
}
```

### DefaultMCPClient

```rust
pub struct DefaultMCPClient {
    // 私有字段
}

impl DefaultMCPClient {
    pub async fn connect(config: MCPServerConfig) -> Result<Self>;
    pub async fn disconnect(&mut self) -> Result<()>;
}
```

**配置:**

```rust
pub struct MCPServerConfig {
    pub name: String,
    pub command: String,
    pub args: Vec<String>,
    pub env: HashMap<String, String>,
    pub timeout: Duration,
}
```

**示例:**

```rust
use agent_mcp::{DefaultMCPClient, MCPServerConfig};

let config = MCPServerConfig {
    name: "filesystem".to_string(),
    command: "mcp-server-filesystem".to_string(),
    args: vec!["--root".to_string(), "/tmp".to_string()],
    env: HashMap::new(),
    timeout: Duration::from_secs(30),
};

let client = DefaultMCPClient::connect(config).await?;

// 列出工具
let tools = client.list_tools().await?;
for tool in tools {
    println!("{}: {}", tool.name, tool.description);
}

// 调用工具
let result = client.call_tool(
    "read_file",
    json!({"path": "/tmp/test.txt"})
).await?;
```

---

## RAG API (agent-rag)

### Traits

#### RAGRetriever

```rust
#[async_trait]
pub trait RAGRetriever: Send + Sync {
    async fn retrieve(&self, query: &str, top_k: usize) -> Result<Vec<Document>>;
    async fn index_document(&self, doc: Document) -> Result<()>;
}
```

#### Embedder

```rust
#[async_trait]
pub trait Embedder: Send + Sync {
    async fn embed(&self, text: &str) -> Result<Vec<f32>>;
    async fn embed_batch(&self, texts: &[String]) -> Result<Vec<Vec<f32>>>;
}
```

#### VectorStore

```rust
#[async_trait]
pub trait VectorStore: Send + Sync {
    async fn create_collection(&self, name: &str, dimension: usize) -> Result<()>;
    async fn collection_exists(&self, name: &str) -> Result<bool>;
    async fn delete_collection(&self, name: &str) -> Result<()>;

    async fn insert(&self, collection: &str, id: String, vector: Vec<f32>, metadata: HashMap<String, String>) -> Result<()>;
    async fn insert_batch(&self, collection: &str, ids: Vec<String>, vectors: Vec<Vec<f32>>, metadatas: Vec<HashMap<String, String>>) -> Result<()>;

    async fn search(&self, collection: &str, query: Vec<f32>, top_k: usize) -> Result<Vec<SearchResult>>;
}
```

### 实现类

#### OllamaEmbedder

```rust
pub struct OllamaEmbedder {
    // 私有字段
}

impl OllamaEmbedder {
    pub fn new(config: OllamaEmbedderConfig) -> Self;
    pub fn with_defaults() -> Self;  // 默认: localhost:11434, nomic-embed-text
}
```

#### QdrantStore

```rust
pub struct QdrantStore {
    // 私有字段
}

impl QdrantStore {
    pub async fn new(config: QdrantConfig) -> Result<Self>;
    pub async fn with_defaults() -> Result<Self>;  // 默认: localhost:6334
}
```

#### DefaultRAGRetriever

```rust
pub struct DefaultRAGRetriever {
    // 私有字段
}

impl DefaultRAGRetriever {
    pub fn new(embedder: Arc<dyn Embedder>, vector_store: Arc<dyn VectorStore>, collection_name: String) -> Self;

    pub async fn index_document_with_loader(&self, loader: &dyn DocumentLoader, source: &str, chunk_config: ChunkConfig) -> Result<()>;
}
```

**示例:**

```rust
use agent_rag::*;

// 创建组件
let embedder = Arc::new(OllamaEmbedder::with_defaults());
let vector_store = Arc::new(QdrantStore::with_defaults().await?);

// 创建集合
vector_store.create_collection("docs", 768).await?;

// 创建检索器
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

// 检索
let results = retriever.retrieve("What is RAG?", 5).await?;
for doc in results {
    println!("{}: {}", doc.id, doc.content);
}

// 集成到 ConversationFlow
let flow = ConversationFlow::new(session_manager, llm_client, config)
    .with_rag(retriever);
```

---

## 类型定义

### CompletionRequest

```rust
pub struct CompletionRequest {
    pub messages: Vec<Message>,
    pub model: String,
    pub temperature: Option<f32>,
    pub max_tokens: Option<u32>,
    pub stream: bool,
}
```

### CompletionResponse

```rust
pub struct CompletionResponse {
    pub content: String,
    pub model: String,
    pub usage: TokenUsage,
    pub finish_reason: Option<String>,
}

pub struct TokenUsage {
    pub prompt_tokens: u32,
    pub completion_tokens: u32,
    pub total_tokens: u32,
}
```

### StreamChunk

```rust
pub struct StreamChunk {
    pub delta: String,
    pub is_final: bool,
    pub finish_reason: Option<String>,
}
```

---

## 错误处理

所有 API 返回 `Result<T, Error>`，其中 `Error` 定义为：

```rust
#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("Database error: {0}")]
    DatabaseError(String),

    #[error("LLM API error: {0}")]
    LLMError(String),

    #[error("Configuration error: {0}")]
    Config(String),

    #[error("Invalid input: {0}")]
    InvalidInput(String),

    #[error("External service error: {0}")]
    External(String),

    #[error("Skill not found: {0}")]
    SkillNotFound(String),

    #[error("Vector store error: {0}")]
    VectorStoreError(String),

    #[error("MCP protocol error: {0}")]
    MCPError(String),
}
```

**错误处理示例:**

```rust
match session_manager.create_session(Some("Test".to_string())).await {
    Ok(session) => println!("Created: {}", session.id),
    Err(Error::DatabaseError(msg)) => eprintln!("DB error: {}", msg),
    Err(e) => eprintln!("Error: {}", e),
}
```

---

## 最佳实践

### 1. 使用 Arc 共享状态

```rust
let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));
let flow1 = ConversationFlow::new(session_manager.clone(), llm_client1, config);
let flow2 = ConversationFlow::new(session_manager.clone(), llm_client2, config);
```

### 2. 错误处理

```rust
// 使用 ? 运算符
async fn example() -> Result<()> {
    let session = session_manager.create_session(None).await?;
    let message = flow.send_message(session.id, "Hello".to_string()).await?;
    Ok(())
}

// 或者使用 match
match flow.send_message(session_id, content).await {
    Ok(response) => println!("{}", response),
    Err(e) => eprintln!("Error: {}", e),
}
```

### 3. 流式响应

```rust
let mut stream = flow.send_message_stream(session_id, "Hello".to_string()).await?;

while let Some(chunk) = stream.next().await? {
    print!("{}", chunk.delta);
    if chunk.is_final {
        break;
    }
}
```

### 4. 配置管理

```rust
// 使用环境变量
let anthropic = AnthropicClient::from_env()?;

// 使用配置结构
let config = ConversationConfig {
    model: "claude-3-5-sonnet-20241022".to_string(),
    max_context_messages: 20,
    temperature: Some(0.7),
    max_tokens: Some(4096),
    system_prompt: Some("You are a helpful assistant.".to_string()),
};
```

---

**更新日期:** 2026-03-11
**版本:** 0.2.0
