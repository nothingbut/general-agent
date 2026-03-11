# General Agent V2 - Rust 架构设计

**版本:** 1.0
**日期:** 2026-03-09
**状态:** 设计阶段

---

## 目录

1. [架构概览](#架构概览)
2. [核心模块设计](#核心模块设计)
3. [数据模型](#数据模型)
4. [错误处理](#错误处理)
5. [并发模型](#并发模型)
6. [API 设计](#api-设计)
7. [性能优化](#性能优化)

---

## 架构概览

### 设计原则

1. **分层架构** - 清晰的职责分离
2. **领域驱动设计 (DDD)** - 核心领域模型独立于基础设施
3. **依赖注入** - 使用 trait 和泛型实现松耦合
4. **异步优先** - 全栈异步，使用 Tokio
5. **类型安全** - 利用 Rust 类型系统防止错误

### 架构层次

```rust
// 展示层（Presentation Layer）
agent-tui/     // TUI 界面
agent-cli/     // CLI 工具
agent-api/     // REST API

         ↓ 调用

// 应用层（Application Layer）
// 协调不同领域服务，处理用例
- ChatUseCase
- SkillExecutionUseCase
- WorkflowExecutionUseCase

         ↓ 使用

// 领域层（Domain Layer）
agent-core/    // 领域模型和业务逻辑
- models/      // 实体和值对象
- services/    // 领域服务
- traits/      // 接口定义

         ↓ 依赖

// 基础设施层（Infrastructure Layer）
agent-storage/   // 数据持久化
agent-llm/       // LLM 集成
agent-skills/    // 技能实现
agent-workflow/  // 工作流引擎
```

---

## 核心模块设计

### 1. agent-core（核心领域模型）

**职责:** 定义核心领域概念和业务规则

```rust
// crates/agent-core/src/lib.rs
pub mod models;     // 领域实体
pub mod traits;     // 核心接口
pub mod error;      // 错误定义
pub mod events;     // 领域事件

// crates/agent-core/src/models/message.rs
use serde::{Deserialize, Serialize};
use chrono::{DateTime, Utc};
use uuid::Uuid;

/// 消息实体
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Message {
    pub id: Uuid,
    pub session_id: Uuid,
    pub role: MessageRole,
    pub content: String,
    pub created_at: DateTime<Utc>,
    pub metadata: Option<MessageMetadata>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum MessageRole {
    User,
    Assistant,
    System,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MessageMetadata {
    pub tokens: Option<u32>,
    pub model: Option<String>,
    pub finish_reason: Option<String>,
}

// crates/agent-core/src/models/session.rs
/// 会话实体
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Session {
    pub id: Uuid,
    pub title: Option<String>,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
    pub context: SessionContext,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct SessionContext {
    pub variables: HashMap<String, String>,
    pub max_tokens: Option<u32>,
    pub temperature: Option<f32>,
}
```

**核心 Traits:**

```rust
// crates/agent-core/src/traits/repository.rs
use async_trait::async_trait;
use crate::{error::Result, models::*};

/// 仓储模式 - 会话仓储
#[async_trait]
pub trait SessionRepository: Send + Sync {
    async fn create(&self, session: Session) -> Result<Session>;
    async fn find_by_id(&self, id: Uuid) -> Result<Option<Session>>;
    async fn list(&self, limit: u32, offset: u32) -> Result<Vec<Session>>;
    async fn update(&self, session: Session) -> Result<Session>;
    async fn delete(&self, id: Uuid) -> Result<()>;
}

/// 仓储模式 - 消息仓储
#[async_trait]
pub trait MessageRepository: Send + Sync {
    async fn create(&self, message: Message) -> Result<Message>;
    async fn list_by_session(
        &self,
        session_id: Uuid,
        limit: u32,
    ) -> Result<Vec<Message>>;
    async fn delete_by_session(&self, session_id: Uuid) -> Result<()>;
}

// crates/agent-core/src/traits/llm.rs
/// LLM 客户端接口
#[async_trait]
pub trait LLMClient: Send + Sync {
    async fn complete(&self, request: CompletionRequest) -> Result<CompletionResponse>;
    async fn stream(&self, request: CompletionRequest) -> Result<CompletionStream>;
}

#[derive(Debug, Clone)]
pub struct CompletionRequest {
    pub messages: Vec<Message>,
    pub model: String,
    pub temperature: Option<f32>,
    pub max_tokens: Option<u32>,
}

pub struct CompletionResponse {
    pub content: String,
    pub model: String,
    pub usage: TokenUsage,
}

#[derive(Debug, Clone)]
pub struct TokenUsage {
    pub prompt_tokens: u32,
    pub completion_tokens: u32,
    pub total_tokens: u32,
}
```

---

### 2. agent-storage（存储层）

**职责:** 数据持久化

```rust
// crates/agent-storage/src/lib.rs
pub mod db;
pub mod repository;
pub mod migrations;

// crates/agent-storage/src/db.rs
use sqlx::{SqlitePool, sqlite::SqliteConnectOptions};
use std::str::FromStr;

pub struct Database {
    pool: SqlitePool,
}

impl Database {
    pub async fn new(database_url: &str) -> Result<Self> {
        let options = SqliteConnectOptions::from_str(database_url)?
            .create_if_missing(true)
            .journal_mode(sqlx::sqlite::SqliteJournalMode::Wal);

        let pool = SqlitePool::connect_with(options).await?;

        // 运行迁移
        sqlx::migrate!("./migrations").run(&pool).await?;

        Ok(Self { pool })
    }

    pub fn pool(&self) -> &SqlitePool {
        &self.pool
    }
}

// crates/agent-storage/src/repository/session.rs
use agent_core::{
    models::Session,
    traits::SessionRepository,
    error::Result,
};
use sqlx::SqlitePool;
use async_trait::async_trait;

pub struct SqliteSessionRepository {
    pool: SqlitePool,
}

impl SqliteSessionRepository {
    pub fn new(pool: SqlitePool) -> Self {
        Self { pool }
    }
}

#[async_trait]
impl SessionRepository for SqliteSessionRepository {
    async fn create(&self, session: Session) -> Result<Session> {
        sqlx::query!(
            r#"
            INSERT INTO sessions (id, title, created_at, updated_at, context)
            VALUES (?, ?, ?, ?, ?)
            "#,
            session.id,
            session.title,
            session.created_at,
            session.updated_at,
            serde_json::to_string(&session.context)?
        )
        .execute(&self.pool)
        .await?;

        Ok(session)
    }

    async fn find_by_id(&self, id: Uuid) -> Result<Option<Session>> {
        let row = sqlx::query!(
            r#"
            SELECT id, title, created_at, updated_at, context
            FROM sessions WHERE id = ?
            "#,
            id
        )
        .fetch_optional(&self.pool)
        .await?;

        Ok(row.map(|r| Session {
            id: Uuid::parse_str(&r.id).unwrap(),
            title: r.title,
            created_at: r.created_at,
            updated_at: r.updated_at,
            context: serde_json::from_str(&r.context).unwrap(),
        }))
    }

    // ... 其他方法
}
```

---

### 3. agent-llm（LLM 客户端）

**职责:** 与各种 LLM 提供商集成

```rust
// crates/agent-llm/src/lib.rs
pub mod openai;
pub mod anthropic;
pub mod ollama;
pub mod router;

// crates/agent-llm/src/openai.rs
use agent_core::{
    traits::LLMClient,
    models::{CompletionRequest, CompletionResponse},
    error::Result,
};
use async_openai::{Client as OpenAIClient, types::*};
use async_trait::async_trait;

pub struct OpenAIProvider {
    client: OpenAIClient,
}

impl OpenAIProvider {
    pub fn new(api_key: String) -> Self {
        let client = OpenAIClient::new().with_api_key(api_key);
        Self { client }
    }
}

#[async_trait]
impl LLMClient for OpenAIProvider {
    async fn complete(&self, request: CompletionRequest) -> Result<CompletionResponse> {
        let chat_request = CreateChatCompletionRequestArgs::default()
            .model(&request.model)
            .messages(convert_messages(request.messages))
            .temperature(request.temperature.unwrap_or(0.7))
            .max_tokens(request.max_tokens)
            .build()?;

        let response = self.client
            .chat()
            .create(chat_request)
            .await?;

        let choice = response.choices.first()
            .ok_or(Error::NoCompletion)?;

        Ok(CompletionResponse {
            content: choice.message.content.clone().unwrap_or_default(),
            model: response.model,
            usage: TokenUsage {
                prompt_tokens: response.usage.prompt_tokens,
                completion_tokens: response.usage.completion_tokens,
                total_tokens: response.usage.total_tokens,
            },
        })
    }

    async fn stream(&self, request: CompletionRequest) -> Result<CompletionStream> {
        // 实现流式响应
        todo!()
    }
}

// crates/agent-llm/src/router.rs
/// LLM 路由器 - 根据配置选择合适的提供商
pub struct LLMRouter {
    providers: HashMap<String, Box<dyn LLMClient>>,
    default_provider: String,
}

impl LLMRouter {
    pub fn new(config: LLMConfig) -> Result<Self> {
        let mut providers: HashMap<String, Box<dyn LLMClient>> = HashMap::new();

        // 注册提供商
        if let Some(openai_key) = config.openai_api_key {
            providers.insert(
                "openai".to_string(),
                Box::new(OpenAIProvider::new(openai_key)),
            );
        }

        if let Some(ollama_url) = config.ollama_base_url {
            providers.insert(
                "ollama".to_string(),
                Box::new(OllamaProvider::new(ollama_url)),
            );
        }

        Ok(Self {
            providers,
            default_provider: config.default_provider,
        })
    }

    pub async fn complete(&self, request: CompletionRequest) -> Result<CompletionResponse> {
        let provider = self.providers
            .get(&self.default_provider)
            .ok_or(Error::ProviderNotFound)?;

        provider.complete(request).await
    }
}
```

---

### 4. agent-api（REST API）

**职责:** HTTP API 端点

```rust
// crates/agent-api/src/lib.rs
use axum::{
    Router,
    routing::{get, post},
    extract::State,
    Json,
};
use std::sync::Arc;

pub mod routes;
pub mod handlers;
pub mod middleware;
pub mod state;

// crates/agent-api/src/state.rs
use agent_storage::Database;
use agent_llm::LLMRouter;

#[derive(Clone)]
pub struct AppState {
    pub db: Database,
    pub llm: Arc<LLMRouter>,
}

// crates/agent-api/src/routes/chat.rs
use axum::{
    extract::{State, Json},
    response::IntoResponse,
};
use serde::{Deserialize, Serialize};

#[derive(Deserialize)]
pub struct ChatRequest {
    pub message: String,
    pub session_id: Option<String>,
}

#[derive(Serialize)]
pub struct ChatResponse {
    pub response: String,
    pub session_id: String,
}

pub async fn chat_handler(
    State(state): State<AppState>,
    Json(request): Json<ChatRequest>,
) -> Result<Json<ChatResponse>, ApiError> {
    // 1. 获取或创建会话
    let session_id = request.session_id
        .map(|id| Uuid::parse_str(&id))
        .transpose()?
        .unwrap_or_else(Uuid::new_v4);

    // 2. 保存用户消息
    let user_message = Message {
        id: Uuid::new_v4(),
        session_id,
        role: MessageRole::User,
        content: request.message.clone(),
        created_at: Utc::now(),
        metadata: None,
    };
    state.message_repo.create(user_message).await?;

    // 3. 调用 LLM
    let completion_request = CompletionRequest {
        messages: vec![/* 历史消息 + 新消息 */],
        model: "gpt-4".to_string(),
        temperature: Some(0.7),
        max_tokens: None,
    };
    let response = state.llm.complete(completion_request).await?;

    // 4. 保存助手消息
    let assistant_message = Message {
        id: Uuid::new_v4(),
        session_id,
        role: MessageRole::Assistant,
        content: response.content.clone(),
        created_at: Utc::now(),
        metadata: Some(MessageMetadata {
            tokens: Some(response.usage.total_tokens),
            model: Some(response.model),
            finish_reason: None,
        }),
    };
    state.message_repo.create(assistant_message).await?;

    Ok(Json(ChatResponse {
        response: response.content,
        session_id: session_id.to_string(),
    }))
}

// crates/agent-api/src/lib.rs - 路由配置
pub fn app(state: AppState) -> Router {
    Router::new()
        .route("/api/health", get(health_check))
        .route("/api/chat", post(chat_handler))
        .route("/api/sessions", get(list_sessions))
        .route("/api/sessions/:id", get(get_session))
        .with_state(state)
        .layer(middleware::logging())
        .layer(middleware::cors())
}
```

---

### 5. agent-tui（TUI 界面）

**职责:** 终端用户界面

```rust
// crates/agent-tui/src/main.rs
use ratatui::{
    backend::CrosstermBackend,
    Terminal,
    widgets::{Block, Borders, List, ListItem, Paragraph},
    layout::{Layout, Constraint, Direction},
};
use crossterm::{
    event::{self, Event, KeyCode},
    terminal::{enable_raw_mode, disable_raw_mode},
};

mod app;
mod ui;
mod events;

use app::App;

#[tokio::main]
async fn main() -> Result<()> {
    // 初始化终端
    enable_raw_mode()?;
    let mut terminal = Terminal::new(CrosstermBackend::new(std::io::stdout()))?;
    terminal.clear()?;

    // 创建应用
    let mut app = App::new().await?;

    // 主循环
    loop {
        terminal.draw(|f| ui::draw(f, &app))?;

        if let Event::Key(key) = event::read()? {
            match key.code {
                KeyCode::Char('q') => break,
                KeyCode::Enter => app.send_message().await?,
                KeyCode::Char(c) => app.input.push(c),
                KeyCode::Backspace => { app.input.pop(); },
                _ => {}
            }
        }
    }

    disable_raw_mode()?;
    Ok(())
}

// crates/agent-tui/src/app.rs
pub struct App {
    pub input: String,
    pub messages: Vec<DisplayMessage>,
    pub session_id: Option<Uuid>,
    client: reqwest::Client,
    api_base: String,
}

impl App {
    pub async fn new() -> Result<Self> {
        Ok(Self {
            input: String::new(),
            messages: Vec::new(),
            session_id: None,
            client: reqwest::Client::new(),
            api_base: "http://localhost:8000".to_string(),
        })
    }

    pub async fn send_message(&mut self) -> Result<()> {
        let message = self.input.clone();
        self.input.clear();

        // 显示用户消息
        self.messages.push(DisplayMessage {
            role: "User".to_string(),
            content: message.clone(),
        });

        // 调用 API
        let response = self.client
            .post(format!("{}/api/chat", self.api_base))
            .json(&serde_json::json!({
                "message": message,
                "session_id": self.session_id.map(|id| id.to_string()),
            }))
            .send()
            .await?
            .json::<ChatResponse>()
            .await?;

        // 显示助手消息
        self.messages.push(DisplayMessage {
            role: "Assistant".to_string(),
            content: response.response,
        });

        self.session_id = Some(Uuid::parse_str(&response.session_id)?);

        Ok(())
    }
}
```

---

## 错误处理

### 统一错误类型

```rust
// crates/agent-core/src/error.rs
use thiserror::Error;

pub type Result<T> = std::result::Result<T, Error>;

#[derive(Error, Debug)]
pub enum Error {
    #[error("Database error: {0}")]
    Database(#[from] sqlx::Error),

    #[error("LLM error: {0}")]
    LLM(String),

    #[error("Skill not found: {0}")]
    SkillNotFound(String),

    #[error("Session not found: {0}")]
    SessionNotFound(String),

    #[error("Invalid input: {0}")]
    InvalidInput(String),

    #[error("Configuration error: {0}")]
    Config(String),

    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("Serialization error: {0}")]
    Serde(#[from] serde_json::Error),
}

// 自动转换为 HTTP 响应
impl IntoResponse for Error {
    fn into_response(self) -> Response {
        let (status, message) = match self {
            Error::SessionNotFound(_) => (StatusCode::NOT_FOUND, self.to_string()),
            Error::InvalidInput(_) => (StatusCode::BAD_REQUEST, self.to_string()),
            _ => (StatusCode::INTERNAL_SERVER_ERROR, self.to_string()),
        };

        (status, Json(json!({ "error": message }))).into_response()
    }
}
```

---

## 并发模型

### Tokio Runtime

```rust
// main.rs
use tokio::runtime::Builder;

fn main() -> Result<()> {
    let runtime = Builder::new_multi_thread()
        .worker_threads(4)
        .thread_name("agent-worker")
        .enable_all()
        .build()?;

    runtime.block_on(async_main())
}

async fn async_main() -> Result<()> {
    // 启动服务器
    let app = agent_api::app(state);
    let listener = tokio::net::TcpListener::bind("0.0.0.0:8000").await?;

    axum::serve(listener, app).await?;

    Ok(())
}
```

### 任务并发

```rust
// 使用 tokio::spawn 处理独立任务
tokio::spawn(async move {
    // 后台任务
});

// 使用 tokio::select! 处理多个异步操作
tokio::select! {
    result = task1 => { /* 处理 task1 */ },
    result = task2 => { /* 处理 task2 */ },
}

// 使用 futures::future::join_all 并发执行多个任务
let tasks = vec![task1(), task2(), task3()];
let results = futures::future::join_all(tasks).await;
```

---

## 性能优化

### 1. 数据库连接池

```rust
// 使用 SQLx 连接池
let pool = SqlitePoolOptions::new()
    .max_connections(10)
    .min_connections(2)
    .connect(database_url)
    .await?;
```

### 2. 缓存

```rust
// 使用 moka 实现内存缓存
use moka::future::Cache;

let cache: Cache<String, Session> = Cache::builder()
    .max_capacity(1000)
    .time_to_live(Duration::from_secs(3600))
    .build();
```

### 3. 零拷贝

```rust
// 使用 Bytes 避免不必要的复制
use bytes::Bytes;

async fn send_data(data: Bytes) {
    // data 可以被多次共享而无需复制
}
```

---

## 下一步

1. 创建 Cargo 工作区和基础 crate 结构
2. 实现 agent-core 核心模型
3. 实现 agent-storage 数据层
4. 实现 agent-api 基础路由

---

**文档维护者:** V2 架构团队
**最后更新:** 2026-03-09
