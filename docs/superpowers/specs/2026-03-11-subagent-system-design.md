# Subagent 系统设计文档

**项目：** General Agent V2
**功能：** Subagent（后台会话）系统
**日期：** 2026-03-11
**版本：** 1.0

---

## 1. 概述

### 1.1 目标

为 General Agent V2 添加 subagent 支持，使主会话能够并发执行多个独立任务，实现：

- 并发执行独立子任务
- 分阶段编排和监控
- TUI 中树形展示（卡片式 + 实时进度）
- 智能资源管理和结果汇总

### 1.2 用户场景

**场景 1：并发代码分析**
```
用户："分析这三个 Rust 文件的代码质量"
主会话：识别可并发任务
    ↓ 提议启动 3 个 subagent
用户：确认
主会话：启动 Stage 1，并发分析
    ├─ Subagent 1: 分析 auth.rs
    ├─ Subagent 2: 分析 db.rs
    └─ Subagent 3: 分析 api.rs
主会话：收集结果，生成综合报告
```

**场景 2：多阶段研究任务**
```
用户："研究 Rust 异步生态系统"
主会话：规划多阶段研究
Stage 1（并发）：
    ├─ Subagent 1: 研究 tokio
    ├─ Subagent 2: 研究 async-std
    └─ Subagent 3: 研究 smol
Stage 2（基于 Stage 1 结果）：
    └─ Subagent 4: 对比分析优缺点
主会话：生成最终研究报告
```

### 1.3 核心特性

- ✅ **混合触发模式** - LLM 提议 + 用户确认，或用户直接触发
- ✅ **自动生命周期管理** - 自动关闭，支持保持活跃，可唤醒已关闭会话
- ✅ **分阶段编排** - Orchestrator 控制多阶段执行
- ✅ **卡片式 TUI** - 进度条、状态、预估时间，实时更新
- ✅ **有限上下文共享** - 启动时指定共享内容（消息、变量）
- ✅ **模板化结果汇总** - 评估、决策、编排下一步
- ✅ **基于规则的 LLM 选择** - 根据任务类型和复杂度自动选择模型

---

## 2. 架构设计

### 2.1 系统架构

```
┌─────────────────────────────────────────────────────────┐
│                    TUI 层 (agent-tui)                   │
│         SessionCardWidget - 卡片式会话树显示            │
└────────────────────┬────────────────────────────────────┘
                     │ 读取状态 / 发送命令
┌────────────────────┴────────────────────────────────────┐
│              应用层 (agent-workflow)                     │
├─────────────────────────────────────────────────────────┤
│  ConversationFlow (主会话)                              │
│      ↓ 调用                                             │
│  SubagentOrchestrator (编排器)                          │
│      ├─ 管理生命周期                                     │
│      ├─ 分阶段执行                                       │
│      └─ 结果汇总                                         │
│      ↓ 通信                                             │
│  消息队列 (tokio::mpsc)                                  │
│      ├─ Command 通道                                     │
│      └─ Result 通道                                      │
│      ↓ 并发执行                                         │
│  SubagentTask (独立协程)                                │
│      ├─ 独立 ConversationFlow                           │
│      ├─ 更新状态到 DashMap                              │
│      └─ 返回结果                                         │
├─────────────────────────────────────────────────────────┤
│  共享状态: Arc<DashMap<Uuid, SubagentState>>            │
│      - 无锁并发访问                                      │
│      - 实时进度跟踪                                      │
└─────────────────────────────────────────────────────────┘
```

### 2.2 核心组件

#### 2.2.1 SubagentOrchestrator（编排器）

**职责：**
- 管理 subagent 生命周期（创建、启动、监控、清理）
- 分阶段执行控制
- 智能调度和资源分配
- 结果收集和汇总

**关键方法：**
```rust
async fn start_stage(&self, parent_id: Uuid, stage: Stage) -> Result<String>;
async fn get_stage_status(&self, stage_id: &str) -> Result<StageStatus>;
async fn cancel_stage(&self, stage_id: &str) -> Result<()>;
fn get_subagent_state(&self, session_id: Uuid) -> Option<SubagentState>;
async fn collect_results(&self, stage_id: &str) -> Result<Vec<SubagentResult>>;
```

#### 2.2.2 SubagentTask（执行单元）

**职责：**
- 在独立 tokio task 中运行
- 维护独立的 ConversationFlow
- 定期更新进度到 DashMap
- 完成后通过队列返回结果

**生命周期：**
```
创建 → 加载共享上下文 → 执行任务 → 更新进度 → 返回结果 → 清理
```

#### 2.2.3 SubagentState（共享状态）

**存储内容：**
```rust
pub struct SubagentState {
    pub session_id: Uuid,
    pub parent_id: Uuid,
    pub stage_id: String,
    pub status: SessionStatus,        // Running | Completed | Failed
    pub progress: f32,                 // 0.0 - 1.0
    pub started_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
    pub estimated_remaining: Option<Duration>,
    pub message_count: usize,
    pub error: Option<String>,
}
```

**访问方式：**
- 通过 `Arc<DashMap<Uuid, SubagentState>>` 无锁并发访问
- SubagentTask 写入，TUI/Orchestrator 读取
- 高性能，无锁竞争

**安全更新模式：**
```rust
impl SubagentTask {
    /// 原子更新状态
    fn update_state<F>(&self, updater: F)
    where
        F: FnOnce(&mut SubagentState),
    {
        self.state_map.alter(&self.session_id, |_, mut state| {
            updater(&mut state);
            state.updated_at = Utc::now();
            state
        });
    }

    /// 读取状态（返回克隆）
    fn get_state(&self) -> Option<SubagentState> {
        self.state_map.get(&self.session_id).map(|r| r.clone())
    }
}
```

#### 2.2.4 SessionCardWidget（TUI 组件）

**渲染方式：**
```
┌─────────────────────────────────────────────┐
│ ▼ 分析三个文件              [3 subagents]   │
├─────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  │
│  │⏳ auth.rs │  │✅ db.rs   │  │⏳ api.rs  │  │
│  │进度: 45% │  │已完成    │  │进度: 20%  │  │
│  └──────────┘  └──────────┘  └──────────┘  │
│  整体: 2/3 完成 | 预计剩余: 2分钟            │
└─────────────────────────────────────────────┘
```

**功能：**
- 卡片式展示，支持折叠
- 进度条实时更新
- 状态图标（⏳✅❌）
- 预估剩余时间

---

## 3. 数据模型

### 3.1 Session 模型扩展

为保持向后兼容，不修改现有 Session 结构，而是创建 SubagentSession 包装器：

```rust
/// Subagent 专用会话包装器
pub struct SubagentSession {
    pub session: Session,              // 复用现有 Session
    pub parent_id: Uuid,               // 父会话 ID
    pub session_type: SessionType,     // 会话类型
    pub status: SessionStatus,         // 执行状态
    pub stage_id: String,              // 所属阶段 ID
}

impl SubagentSession {
    pub fn new(session: Session, parent_id: Uuid, stage_id: String) -> Self {
        Self {
            session,
            parent_id,
            session_type: SessionType::Subagent,
            status: SessionStatus::Idle,
            stage_id,
        }
    }

    pub fn session_id(&self) -> Uuid {
        self.session.id
    }
}

pub enum SessionType {
    Main,       // 主会话
    Subagent,   // 子会话
}

pub enum SessionStatus {
    Idle,       // 空闲
    Running,    // 运行中
    Completed,  // 成功完成
    Failed,     // 失败
    Cancelled,  // 被取消
}
```

### 3.2 Stage（执行阶段）

```rust
pub struct Stage {
    pub id: String,
    pub name: String,
    pub tasks: Vec<SubagentTaskConfig>,
    pub strategy: StageStrategy,
    pub failure_strategy: FailureStrategy,
}

pub enum StageStrategy {
    Parallel,                    // 并发执行所有任务
    Sequential,                  // 顺序执行
    ParallelWithLimit(usize),    // 限制并发数
}

pub enum FailureStrategy {
    IgnoreAndContinue,      // 忽略失败，继续其他任务
    FailStage,              // 整个 Stage 失败
    RetryOnce,              // 重试一次
    AskUser,                // 提示用户决定
}
```

### 3.3 SubagentTaskConfig（任务配置）

```rust
/// Subagent 任务配置（用于编排器）
pub struct SubagentTaskConfig {
    pub id: Uuid,                      // 任务唯一 ID
    pub config: SubagentConfig,        // 执行配置
    pub parent_id: Uuid,               // 父会话 ID
    pub stage_id: String,              // 所属阶段 ID
    pub priority: u8,                  // 优先级 (0-255)
    pub task_type: TaskType,           // 任务类型（用于进度估算）
}

/// Subagent 执行配置
pub struct SubagentConfig {
    pub title: String,
    pub initial_prompt: String,
    pub shared_context: SharedContext,
    pub llm_config: LLMConfig,
    pub keep_alive: bool,              // 是否保持活跃
    pub timeout: Option<Duration>,
}

/// 共享上下文配置
pub struct SharedContext {
    pub recent_messages: Option<usize>,    // 共享最近 N 条消息
    pub variables: HashMap<String, String>, // 共享变量
    pub system_prompt: Option<String>,
}

/// 任务类型（用于进度估算和 LLM 选择）
pub enum TaskType {
    CodeReview,       // 代码审查
    Research,         // 研究调研
    Analysis,         // 数据分析
    Documentation,    // 文档生成
    Testing,          // 测试执行
    Custom,           // 自定义
}
```

### 3.4 数据库模式

由于使用 SubagentSession 包装器而非修改 Session 表，需要创建新的 subagent_sessions 表：

```sql
-- 创建 subagent_sessions 表
CREATE TABLE IF NOT EXISTS subagent_sessions (
    session_id TEXT PRIMARY KEY,
    parent_id TEXT NOT NULL,
    session_type TEXT NOT NULL DEFAULT 'Subagent',
    status TEXT NOT NULL DEFAULT 'Idle',
    stage_id TEXT NOT NULL,
    created_at DATETIME NOT NULL,
    updated_at DATETIME NOT NULL,
    FOREIGN KEY (session_id) REFERENCES sessions(id) ON DELETE CASCADE,
    FOREIGN KEY (parent_id) REFERENCES sessions(id) ON DELETE CASCADE
);

-- 添加索引
CREATE INDEX idx_subagent_sessions_parent_id ON subagent_sessions(parent_id);
CREATE INDEX idx_subagent_sessions_stage_id ON subagent_sessions(stage_id);
CREATE INDEX idx_subagent_sessions_status ON subagent_sessions(status);

-- 创建 stages 表
CREATE TABLE IF NOT EXISTS stages (
    id TEXT PRIMARY KEY,
    parent_session_id TEXT NOT NULL,
    name TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'Running',
    created_at DATETIME NOT NULL,
    completed_at DATETIME,
    FOREIGN KEY (parent_session_id) REFERENCES sessions(id) ON DELETE CASCADE
);
```

---

## 4. 核心工作流

### 4.1 Subagent 启动流程

```
1. 用户输入 / LLM 识别可并发任务
    ↓
2. 主会话调用 orchestrator.start_stage()
    ↓
3. Orchestrator 创建 Stage，解析任务配置
    ↓
4. 开启数据库事务：
    a. 插入 Stage 记录
    b. 为每个任务：
       - 创建 Session 记录
       - 创建 SubagentSession 记录
    c. 提交事务（失败则全部回滚）
    ↓
5. 为每个任务：
    a. 初始化 SubagentState → DashMap
    b. 创建 SubagentTaskConfig
    c. 生成 SharedContext（从主会话提取）
    d. 启动 tokio::spawn(task.run_with_recovery())
    ↓
6. 返回 stage_id 给主会话
    ↓
7. TUI 开始轮询 DashMap（每 500ms），显示进度
```

**事务保证：**
```rust
impl SubagentOrchestrator {
    async fn create_stage_with_sessions(
        &self,
        parent_id: Uuid,
        stage: &Stage,
    ) -> SubagentResult<Vec<Uuid>> {
        let mut tx = self.pool.begin().await?;

        // 插入 Stage
        sqlx::query(
            "INSERT INTO stages (id, parent_session_id, name, status, created_at)
             VALUES (?, ?, ?, 'Running', ?)"
        )
        .bind(&stage.id)
        .bind(parent_id.to_string())
        .bind(&stage.name)
        .bind(Utc::now())
        .execute(&mut *tx)
        .await?;

        let mut session_ids = Vec::new();

        // 为每个任务创建 Session 和 SubagentSession
        for task_config in &stage.tasks {
            let session_id = Uuid::new_v4();

            // 创建基础 Session
            sqlx::query(
                "INSERT INTO sessions (id, title, created_at, updated_at)
                 VALUES (?, ?, ?, ?)"
            )
            .bind(session_id.to_string())
            .bind(&task_config.config.title)
            .bind(Utc::now())
            .bind(Utc::now())
            .execute(&mut *tx)
            .await?;

            // 创建 SubagentSession 记录
            sqlx::query(
                "INSERT INTO subagent_sessions
                 (session_id, parent_id, session_type, status, stage_id, created_at, updated_at)
                 VALUES (?, ?, 'Subagent', 'Idle', ?, ?, ?)"
            )
            .bind(session_id.to_string())
            .bind(parent_id.to_string())
            .bind(&stage.id)
            .bind(Utc::now())
            .bind(Utc::now())
            .execute(&mut *tx)
            .await?;

            session_ids.push(session_id);
        }

        // 提交事务
        tx.commit().await?;

        Ok(session_ids)
    }
}
```

### 4.2 Subagent 执行流程

```rust
async fn run(self) -> Result<SubagentResult> {
    // 1. 创建独立的 ConversationFlow
    let flow = ConversationFlow::new(
        session_manager,
        llm_client,
        self.config.llm_config
    );

    // 2. 加载共享上下文
    self.load_shared_context(&flow).await?;

    // 3. 更新状态为 Running
    self.update_state(|state| {
        state.status = SessionStatus::Running;
    });

    // 4. 发送初始提示，获取流式响应
    let mut stream = flow.send_message_stream(
        self.session_id,
        self.config.initial_prompt
    ).await?;

    // 5. 流式接收，定期更新进度
    let mut message_count = 0;
    let mut full_response = String::new();

    while let Some(chunk) = stream.next().await {
        match chunk {
            Ok(text) => {
                full_response.push_str(&text);
                message_count += 1;

                // 每 N 个 chunk 更新一次进度
                if message_count % 10 == 0 {
                    let progress = self.estimate_progress(message_count);
                    self.update_state(|state| {
                        state.progress = progress;
                        state.message_count = message_count;
                        state.updated_at = Utc::now();
                    });
                }
            }
            Err(e) => {
                // 错误处理
                self.update_state(|state| {
                    state.status = SessionStatus::Failed;
                    state.error = Some(e.to_string());
                });
                return Err(e);
            }
        }
    }

    // 6. 完成，更新状态
    self.update_state(|state| {
        state.status = SessionStatus::Completed;
        state.progress = 1.0;
    });

    // 7. 返回结果
    Ok(SubagentResult {
        session_id: self.session_id,
        status: SessionStatus::Completed,
        output: full_response,
        metadata: self.collect_metadata(),
    })
}
```

### 4.3 主会话监控和汇总

```rust
pub async fn execute_with_subagents(
    &self,
    session_id: Uuid,
    stages: Vec<Stage>,
) -> Result<String> {
    let mut all_results = Vec::new();

    for (idx, stage) in stages.into_iter().enumerate() {
        info!("Starting Stage {}: {}", idx + 1, stage.name);

        // 1. 启动阶段
        let stage_id = self.orchestrator
            .start_stage(session_id, stage)
            .await?;

        // 2. 等待完成（带超时和取消）
        let timeout = Duration::from_secs(300); // 5 分钟
        let status = tokio::time::timeout(
            timeout,
            self.orchestrator.wait_for_stage(&stage_id)
        ).await??;

        // 3. 收集结果
        let results = self.orchestrator
            .collect_results(&stage_id)
            .await?;

        // 4. 处理失败
        let failed = results.iter()
            .filter(|r| r.status == SessionStatus::Failed)
            .collect::<Vec<_>>();

        if !failed.is_empty() {
            warn!("Stage {} has {} failed subagents", idx + 1, failed.len());
            // 策略：继续执行，但记录失败
        }

        all_results.extend(results);

        // 5. 智能评估：是否需要下一阶段
        if idx < stages.len() - 1 {
            let should_continue = self
                .evaluate_stage_results(&all_results)
                .await?;

            if !should_continue {
                info!("Early termination: no need for next stage");
                break;
            }
        }
    }

    // 6. 汇总所有结果
    self.summarize_results(session_id, all_results).await
}

async fn summarize_results(
    &self,
    session_id: Uuid,
    results: Vec<SubagentResult>,
) -> Result<String> {
    // 构造汇总提示词
    let summary_prompt = format!(
        "Based on the following results from {} subagents, provide a comprehensive summary:\n\n{}",
        results.len(),
        results.iter()
            .map(|r| format!("- {}: {}", r.session_id, r.output))
            .collect::<Vec<_>>()
            .join("\n")
    );

    // 使用主会话的 LLM 生成汇总
    self.send_message(session_id, summary_prompt).await
}
```

### 4.4 TUI 更新循环

```rust
impl SessionCardWidget {
    pub async fn update_loop(&mut self) {
        let mut interval = tokio::time::interval(Duration::from_millis(500));

        loop {
            interval.tick().await;

            // 1. 从 DashMap 读取所有 subagent 状态
            let states = self.orchestrator.get_all_states();

            // 2. 按 stage_id 分组
            let grouped = self.group_by_stage(states);

            // 3. 重新渲染卡片
            for (stage_id, stage_states) in grouped {
                self.render_stage_card(stage_id, stage_states);
            }

            // 4. 检查是否全部完成
            if self.all_completed() {
                info!("All subagents completed, stopping update loop");
                break;
            }
        }
    }

    fn render_stage_card(&mut self, stage_id: String, states: Vec<SubagentState>) {
        let total = states.len();
        let completed = states.iter()
            .filter(|s| s.status == SessionStatus::Completed)
            .count();
        let avg_progress = states.iter()
            .map(|s| s.progress)
            .sum::<f32>() / total as f32;

        // 渲染卡片（伪代码）
        self.cards.push(Card {
            title: format!("Stage: {}", stage_id),
            progress: avg_progress,
            status: format!("{}/{} completed", completed, total),
            subagents: states.iter().map(|s| SubagentCard {
                title: s.session_id.to_string(),
                status: s.status.clone(),
                progress: s.progress,
            }).collect(),
        });
    }
}
```

---

## 5. 关键技术实现

### 5.1 错误类型定义

```rust
use thiserror::Error;

/// Subagent 错误类型
#[derive(Error, Debug)]
pub enum SubagentError {
    #[error("Stage {0} failed")]
    StageFailed(String),

    #[error("Subagent {0} timed out")]
    Timeout(Uuid),

    #[error("Too many concurrent subagents (max: {0})")]
    TooManyConcurrentSubagents(usize),

    #[error("Too many stages (max: {0})")]
    TooManyStages(usize),

    #[error("Task creation failed: {0}")]
    TaskCreationFailed(String),

    #[error("Channel closed unexpectedly")]
    ChannelClosed,

    #[error("Database error: {0}")]
    DatabaseError(#[from] sqlx::Error),

    #[error("LLM error: {0}")]
    LLMError(String),

    #[error("Serialization error: {0}")]
    SerializationError(#[from] serde_json::Error),

    #[error("Panic in subagent: {0}")]
    PanicError(String),

    #[error("Permission denied")]
    PermissionDenied,

    #[error("Path not allowed: {0}")]
    PathNotAllowed(PathBuf),

    #[error("Configuration error: {0}")]
    ConfigError(String),

    #[error("Shutdown requested")]
    ShutdownRequested,
}

/// 为 SubagentError 实现 Result 别名
pub type SubagentResult<T> = Result<T, SubagentError>;
```

### 5.2 消息队列设计

```rust
// Orchestrator 内部
pub struct OrchestratorChannels {
    command_tx: mpsc::Sender<SubagentCommand>,
    result_rx: mpsc::Receiver<TaskResult>,
}

pub enum SubagentCommand {
    Start(SubagentTaskConfig),
    Cancel(Uuid),
    UpdateConfig(Uuid, SubagentConfig),
}

/// 任务执行结果
pub struct TaskResult {
    pub session_id: Uuid,
    pub status: SessionStatus,
    pub output: String,
    pub metadata: ResultMetadata,
}

/// 结果元数据
pub struct ResultMetadata {
    pub execution_time: Duration,
    pub token_count: usize,
    pub model_used: String,
    pub error_count: usize,
    pub tool_calls: Vec<String>,
    pub memory_used: usize,  // 内存占用（字节）
}
```

### 5.3 进度估算算法

基于任务类型和对数曲线的智能进度估算：

```rust
impl SubagentTask {
    /// 估算任务进度（0.0 - 1.0）
    fn estimate_progress(&self, message_count: usize) -> f32 {
        // 基于任务类型选择不同的估算参数
        let estimated_total = match self.config.task_type {
            TaskType::CodeReview => 50.0,      // 代码审查较快
            TaskType::Research => 150.0,       // 研究需要更多轮次
            TaskType::Analysis => 100.0,       // 分析任务中等
            TaskType::Documentation => 80.0,   // 文档生成较快
            TaskType::Testing => 120.0,        // 测试执行较慢
            TaskType::Custom => 100.0,         // 默认值
        };

        // 使用对数曲线，使进度更符合实际感受
        // 前期进度快，后期慢
        let raw = (message_count as f32 + 1.0).ln() / (estimated_total + 1.0).ln();

        // 永远不显示 100%，直到真正完成，最多显示 95%
        raw.min(0.95).max(0.0)
    }

    /// 估算剩余时间
    fn estimate_remaining_time(&self, current_progress: f32) -> Option<Duration> {
        // 至少需要 5% 进度才能估算
        if current_progress <= 0.05 {
            return None;
        }

        let elapsed = Utc::now() - self.state.started_at;
        let elapsed_secs = elapsed.num_seconds() as f32;

        // 使用加权平均，避免估算波动过大
        let total_estimated = elapsed_secs / current_progress;
        let remaining = total_estimated * (1.0 - current_progress);

        // 限制最大估算时间为 1 小时
        let capped = remaining.min(3600.0).max(0.0);

        Some(Duration::from_secs(capped as u64))
    }
}
```

### 5.4 基于任务复杂度的 LLM 选择

根据任务类型和复杂度自动选择合适的 LLM 模型：

```rust
pub enum TaskComplexity {
    Simple,      // 简单任务（如格式转换、简单分析）
    Medium,      // 中等任务（如代码审查、文档生成）
    Complex,     // 复杂任务（如架构设计、深度研究）
    Custom(LLMConfig),  // 用户指定
}

impl TaskComplexity {
    /// 根据任务复杂度选择 LLM 配置
    pub fn select_llm_config(&self) -> LLMConfig {
        match self {
            Self::Simple => LLMConfig {
                provider: "ollama",
                model: "qwen2.5:0.5b",
                max_tokens: 1024,
                temperature: 0.3,
            },
            Self::Medium => LLMConfig {
                provider: "ollama",
                model: "qwen2.5:7b",
                max_tokens: 2048,
                temperature: 0.5,
            },
            Self::Complex => LLMConfig {
                provider: "anthropic",
                model: "claude-3-5-sonnet-20241022",
                max_tokens: 4096,
                temperature: 0.7,
            },
            Self::Custom(config) => config.clone(),
        }
    }

    /// 从任务类型推断复杂度
    pub fn from_task_type(task_type: &TaskType) -> Self {
        match task_type {
            TaskType::CodeReview | TaskType::Analysis => Self::Medium,
            TaskType::Research => Self::Complex,
            TaskType::Documentation | TaskType::Testing => Self::Simple,
            TaskType::Custom => Self::Medium,
        }
    }
}
```

### 5.5 共享上下文加载

```rust
impl SubagentTask {
    async fn load_shared_context(&self, flow: &ConversationFlow) -> SubagentResult<()> {
        let parent_messages = if let Some(n) = self.config.shared_context.recent_messages {
            // 从父会话加载最近 N 条消息
            self.session_manager
                .get_recent_messages(self.parent_id, n)
                .await?
        } else {
            vec![]
        };

        // 添加共享消息到当前会话
        for msg in parent_messages {
            self.session_manager
                .add_message(self.session_id, msg)
                .await?;
        }

        // 设置共享变量
        for (key, value) in &self.config.shared_context.variables {
            flow.set_variable(key.clone(), value.clone());
        }

        Ok(())
    }
}
```

### 5.6 Panic 恢复和错误传播

```rust
use std::panic::{self, AssertUnwindSafe};

impl SubagentTask {
    /// 在独立任务中运行，带 panic 恢复
    pub async fn spawn_with_recovery(self) -> JoinHandle<SubagentResult<TaskResult>> {
        tokio::spawn(async move {
            // 捕获 panic
            let result = panic::catch_unwind(AssertUnwindSafe(|| {
                // 在同步上下文中设置 panic hook
                panic::set_hook(Box::new(|info| {
                    error!("Subagent panic: {:?}", info);
                }));
            }));

            if result.is_err() {
                return Err(SubagentError::PanicError(
                    "Task panicked during initialization".to_string()
                ));
            }

            // 运行任务
            self.run_with_timeout().await
        })
    }
}
```

### 5.7 安全的 Arc 克隆模式

```rust
impl SubagentOrchestrator {
    pub async fn start_stage(&self, parent_id: Uuid, stage: Stage) -> SubagentResult<String> {
        // 在进入 async 块前克隆所有 Arc 引用
        let state_map = Arc::clone(&self.state_map);
        let session_manager = Arc::clone(&self.session_manager);
        let llm_client = Arc::clone(&self.llm_client);
        let result_tx = self.result_tx.clone();

        for task_config in stage.tasks {
            let state_map = Arc::clone(&state_map);
            let session_manager = Arc::clone(&session_manager);
            let llm_client = Arc::clone(&llm_client);
            let result_tx = result_tx.clone();

            tokio::spawn(async move {
                let task = SubagentTask::new(
                    task_config,
                    state_map,
                    session_manager,
                    llm_client,
                );

                let result = task.run().await;

                // 发送结果，带超时避免死锁
                let _ = timeout(Duration::from_secs(5), result_tx.send(result))
                    .await
                    .map_err(|_| warn!("Result channel timeout, dropping result"));
            });
        }

        Ok(stage.id)
    }
}
```

### 5.8 通道死锁防护

```rust
use tokio::time::timeout;

impl SubagentOrchestrator {
    async fn send_result_with_timeout(
        &self,
        result: TaskResult,
    ) -> SubagentResult<()> {
        match timeout(Duration::from_secs(5), self.result_tx.send(result)).await {
            Ok(Ok(_)) => Ok(()),
            Ok(Err(_)) => {
                warn!("Result channel closed");
                Err(SubagentError::ChannelClosed)
            }
            Err(_) => {
                error!("Result channel timeout - potential deadlock");
                Err(SubagentError::ChannelClosed)
            }
        }
    }
}
```

---

## 6. 错误处理

### 6.1 Subagent 失败策略

FailureStrategy 定义见第 3.2 节。实现如下：

```rust
impl SubagentOrchestrator {
    async fn handle_subagent_failure(
        &self,
        result: SubagentResult,
        strategy: FailureStrategy,
    ) -> Result<()> {
        match strategy {
            FailureStrategy::IgnoreAndContinue => {
                warn!("Subagent {} failed, continuing", result.session_id);
                Ok(())
            }
            FailureStrategy::FailStage => {
                error!("Subagent {} failed, failing entire stage", result.session_id);
                Err(SubagentError::StageFailed(result.session_id))
            }
            FailureStrategy::RetryOnce => {
                info!("Retrying failed subagent {}", result.session_id);
                self.retry_subagent(result.session_id).await
            }
            FailureStrategy::AskUser => {
                // 通过 TUI 提示用户
                self.prompt_user_for_decision(result).await
            }
        }
    }
}
```

### 6.2 超时处理

```rust
impl SubagentTask {
    async fn run_with_timeout(self) -> Result<SubagentResult> {
        let timeout = self.config.timeout
            .unwrap_or(Duration::from_secs(300)); // 默认 5 分钟

        match tokio::time::timeout(timeout, self.run()).await {
            Ok(result) => result,
            Err(_) => {
                // 超时，标记为失败
                self.update_state(|state| {
                    state.status = SessionStatus::Failed;
                    state.error = Some("Timeout".to_string());
                });

                Err(SubagentError::Timeout(self.session_id))
            }
        }
    }
}
```

### 6.3 资源限制

```rust
pub struct OrchestratorConfig {
    pub max_concurrent_subagents: usize,  // 默认 10
    pub max_stages: usize,                // 默认 5
    pub default_timeout: Duration,        // 默认 5 分钟
    pub memory_limit: Option<usize>,      // 内存限制（字节）
}

impl SubagentOrchestrator {
    async fn start_stage(&self, parent_id: Uuid, stage: Stage) -> Result<String> {
        // 检查并发限制
        if self.active_subagents_count() >= self.config.max_concurrent_subagents {
            return Err(SubagentError::TooManyConcurrentSubagents);
        }

        // 检查阶段数量限制
        if self.active_stages_count() >= self.config.max_stages {
            return Err(SubagentError::TooManyStages);
        }

        // 启动阶段...
    }
}
```

---

## 7. 安全性考虑

### 7.1 资源隔离

```rust
/// 资源限制配置
pub struct ResourceLimits {
    pub max_memory: usize,           // 最大内存（字节）
    pub max_cpu_time: Duration,      // 最大 CPU 时间
    pub max_file_handles: usize,     // 最大文件句柄数
}

impl SubagentTask {
    /// 应用资源限制
    async fn apply_resource_limits(&self) -> SubagentResult<()> {
        // 在 Unix 系统上使用 rlimit
        #[cfg(unix)]
        {
            use rlimit::{setrlimit, Resource};

            // 限制虚拟内存
            setrlimit(
                Resource::AS,
                self.limits.max_memory as u64,
                self.limits.max_memory as u64,
            ).map_err(|e| SubagentError::TaskCreationFailed(e.to_string()))?;
        }

        Ok(())
    }
}
```

### 7.2 数据隔离

- **独立会话**：每个 subagent 有独立的 Session 和 ConversationFlow
- **有限上下文共享**：只共享明确指定的消息和变量
- **内存边界**：通过 Arc 共享，但不允许修改父会话数据
- **数据库隔离**：使用外键约束，防止数据泄漏

### 7.3 权限控制

```rust
pub struct SubagentPermissions {
    pub can_read_files: bool,
    pub can_write_files: bool,
    pub can_execute_commands: bool,
    pub can_access_network: bool,
    pub allowed_directories: Vec<PathBuf>,
}

impl SubagentTask {
    /// 检查权限
    fn check_permission(&self, operation: Operation) -> SubagentResult<()> {
        match operation {
            Operation::ReadFile(path) => {
                if !self.permissions.can_read_files {
                    return Err(SubagentError::PermissionDenied);
                }
                // 检查路径是否在允许列表中
                if !self.is_path_allowed(&path) {
                    return Err(SubagentError::PathNotAllowed(path));
                }
            }
            // 其他操作检查...
        }
        Ok(())
    }
}
```

---

## 8. 日志和可观测性

### 8.1 结构化日志

```rust
use tracing::{info, warn, error, debug, instrument};

#[instrument(skip(self), fields(session_id = %self.session_id, stage_id = %self.stage_id))]
impl SubagentTask {
    async fn run(&self) -> SubagentResult<TaskResult> {
        info!("Starting subagent task");

        // 任务执行...

        debug!(
            message_count = self.message_count,
            progress = %self.progress,
            "Task progress update"
        );

        info!(
            execution_time = ?self.execution_time,
            token_count = self.token_count,
            "Task completed successfully"
        );

        Ok(result)
    }
}
```

### 8.2 指标收集

```rust
use prometheus::{IntCounter, Histogram, Registry};

pub struct SubagentMetrics {
    pub tasks_started: IntCounter,
    pub tasks_completed: IntCounter,
    pub tasks_failed: IntCounter,
    pub execution_time: Histogram,
    pub memory_usage: Histogram,
}

impl SubagentMetrics {
    pub fn new(registry: &Registry) -> Self {
        let tasks_started = IntCounter::new(
            "subagent_tasks_started_total",
            "Total number of subagent tasks started"
        ).unwrap();
        registry.register(Box::new(tasks_started.clone())).unwrap();

        // 注册其他指标...

        Self {
            tasks_started,
            tasks_completed,
            tasks_failed,
            execution_time,
            memory_usage,
        }
    }
}
```

### 8.3 分布式追踪

```rust
use opentelemetry::trace::{Tracer, TracerProvider};
use tracing_opentelemetry::OpenTelemetrySpanExt;

impl SubagentOrchestrator {
    #[instrument(name = "start_stage", skip(self))]
    pub async fn start_stage(&self, parent_id: Uuid, stage: Stage) -> SubagentResult<String> {
        let span = tracing::Span::current();
        span.set_attribute("parent_id", parent_id.to_string());
        span.set_attribute("stage_id", stage.id.clone());
        span.set_attribute("task_count", stage.tasks.len() as i64);

        // 执行...
    }
}
```

---

## 9. 配置管理

### 9.1 配置文件结构

```toml
# config/subagent.toml

[orchestrator]
max_concurrent_subagents = 10
max_stages = 5
default_timeout_secs = 300
tui_update_interval_ms = 500

[resource_limits]
max_memory_mb = 150
max_cpu_time_secs = 600
max_file_handles = 100

[llm]
default_provider = "ollama"
fallback_provider = "anthropic"

[llm.simple]
provider = "ollama"
model = "qwen2.5:0.5b"
max_tokens = 1024
temperature = 0.3

[llm.medium]
provider = "ollama"
model = "qwen2.5:7b"
max_tokens = 2048
temperature = 0.5

[llm.complex]
provider = "anthropic"
model = "claude-3-5-sonnet-20241022"
max_tokens = 4096
temperature = 0.7

[database]
url = "sqlite:data/sessions.db"
max_connections = 10

[monitoring]
enable_metrics = true
enable_tracing = true
metrics_port = 9090
```

### 9.2 配置加载

```rust
use config::{Config, File};
use serde::Deserialize;

#[derive(Debug, Deserialize)]
pub struct SubagentConfig {
    pub orchestrator: OrchestratorConfig,
    pub resource_limits: ResourceLimits,
    pub llm: LLMConfigs,
    pub database: DatabaseConfig,
    pub monitoring: MonitoringConfig,
}

impl SubagentConfig {
    pub fn load() -> SubagentResult<Self> {
        let config = Config::builder()
            .add_source(File::with_name("config/subagent"))
            .add_source(config::Environment::with_prefix("SUBAGENT"))
            .build()?;

        config.try_deserialize()
            .map_err(|e| SubagentError::ConfigError(e.to_string()))
    }
}
```

---

## 10. 优雅关闭

### 10.1 关闭流程

```rust
use tokio::signal;
use tokio::sync::broadcast;

pub struct ShutdownCoordinator {
    shutdown_tx: broadcast::Sender<()>,
}

impl ShutdownCoordinator {
    pub fn new() -> Self {
        let (shutdown_tx, _) = broadcast::channel(16);
        Self { shutdown_tx }
    }

    pub async fn wait_for_shutdown_signal(&self) {
        signal::ctrl_c().await.expect("Failed to listen for Ctrl+C");
        info!("Shutdown signal received, starting graceful shutdown");

        // 广播关闭信号
        let _ = self.shutdown_tx.send(());
    }

    pub fn subscribe(&self) -> broadcast::Receiver<()> {
        self.shutdown_tx.subscribe()
    }
}
```

### 10.2 Subagent 优雅关闭

```rust
impl SubagentTask {
    async fn run_with_shutdown(
        self,
        mut shutdown_rx: broadcast::Receiver<()>,
    ) -> SubagentResult<TaskResult> {
        tokio::select! {
            result = self.run() => result,
            _ = shutdown_rx.recv() => {
                info!("Shutdown signal received, cleaning up");
                self.cleanup().await?;
                Err(SubagentError::ShutdownRequested)
            }
        }
    }

    async fn cleanup(&self) -> SubagentResult<()> {
        // 保存进度到数据库
        self.save_state().await?;

        // 更新状态
        self.update_state(|state| {
            state.status = SessionStatus::Cancelled;
        });

        // 关闭资源
        self.close_resources().await?;

        info!("Subagent cleanup completed");
        Ok(())
    }
}
```

### 10.3 Orchestrator 优雅关闭

```rust
impl SubagentOrchestrator {
    pub async fn shutdown(&self) -> SubagentResult<()> {
        info!("Starting orchestrator shutdown");

        // 1. 停止接受新任务
        self.accepting_tasks.store(false, Ordering::SeqCst);

        // 2. 等待所有运行中的任务完成（最多 30 秒）
        let timeout = Duration::from_secs(30);
        match tokio::time::timeout(timeout, self.wait_all_tasks()).await {
            Ok(_) => info!("All tasks completed gracefully"),
            Err(_) => {
                warn!("Timeout waiting for tasks, cancelling remaining");
                self.cancel_all_tasks().await?;
            }
        }

        // 3. 关闭通道
        drop(self.command_tx.clone());
        drop(self.result_tx.clone());

        // 4. 保存状态到数据库
        self.save_all_states().await?;

        info!("Orchestrator shutdown complete");
        Ok(())
    }
}
```

---

## 11. 测试策略

### 11.1 Mock 实现

为测试隔离提供 Mock 实现：

```rust
#[cfg(test)]
pub mod mocks {
    use super::*;
    use async_trait::async_trait;
    use std::sync::Arc;
    use tokio::sync::Mutex;

    /// Mock SessionManager
    pub struct MockSessionManager {
        pub sessions: Arc<Mutex<HashMap<Uuid, Vec<Message>>>>,
    }

    impl MockSessionManager {
        pub fn new() -> Self {
            Self {
                sessions: Arc::new(Mutex::new(HashMap::new())),
            }
        }
    }

    #[async_trait]
    impl SessionManagerTrait for MockSessionManager {
        async fn get_recent_messages(
            &self,
            session_id: Uuid,
            count: usize,
        ) -> SubagentResult<Vec<Message>> {
            let sessions = self.sessions.lock().await;
            let messages = sessions
                .get(&session_id)
                .map(|msgs| msgs.iter().rev().take(count).cloned().collect())
                .unwrap_or_default();
            Ok(messages)
        }

        async fn add_message(&self, session_id: Uuid, message: Message) -> SubagentResult<()> {
            let mut sessions = self.sessions.lock().await;
            sessions
                .entry(session_id)
                .or_insert_with(Vec::new)
                .push(message);
            Ok(())
        }

        async fn create_session(&self, title: String) -> SubagentResult<Uuid> {
            let id = Uuid::new_v4();
            let mut sessions = self.sessions.lock().await;
            sessions.insert(id, Vec::new());
            Ok(id)
        }
    }

    /// Mock LLMClient
    pub struct MockLLMClient {
        pub responses: Arc<Mutex<Vec<String>>>,
    }

    impl MockLLMClient {
        pub fn new(responses: Vec<String>) -> Self {
            Self {
                responses: Arc::new(Mutex::new(responses)),
            }
        }
    }

    #[async_trait]
    impl LLMClientTrait for MockLLMClient {
        async fn send_message_stream(
            &self,
            _config: LLMConfig,
            _prompt: String,
        ) -> SubagentResult<MessageStream> {
            let mut responses = self.responses.lock().await;
            let response = responses.pop().unwrap_or_default();

            // 创建简单的流
            let stream = futures::stream::iter(
                response
                    .chars()
                    .map(|c| Ok(c.to_string()))
                    .collect::<Vec<_>>(),
            );

            Ok(Box::pin(stream))
        }
    }
}
```

### 11.2 单元测试（目标覆盖率 > 80%）

```rust
#[cfg(test)]
mod tests {
    use super::*;
    use crate::mocks::*;

    #[test]
    fn test_subagent_state_updates() {
        let state_map = Arc::new(DashMap::new());
        let session_id = Uuid::new_v4();
        let parent_id = Uuid::new_v4();

        // 初始化状态
        let state = SubagentState {
            session_id,
            parent_id,
            stage_id: "test-stage".to_string(),
            status: SessionStatus::Idle,
            progress: 0.0,
            started_at: Utc::now(),
            updated_at: Utc::now(),
            estimated_remaining: None,
            message_count: 0,
            error: None,
        };
        state_map.insert(session_id, state);

        // 模拟更新
        state_map.alter(&session_id, |_, mut state| {
            state.progress = 0.5;
            state.status = SessionStatus::Running;
            state
        });

        // 验证
        let updated = state_map.get(&session_id).unwrap();
        assert_eq!(updated.progress, 0.5);
        assert_eq!(updated.status, SessionStatus::Running);
    }

    #[test]
    fn test_progress_estimation() {
        let task_config = SubagentTaskConfig {
            id: Uuid::new_v4(),
            task_type: TaskType::CodeReview,
            parent_id: Uuid::new_v4(),
            stage_id: "test".to_string(),
            priority: 0,
            config: SubagentConfig {
                title: "Test".to_string(),
                initial_prompt: "Test".to_string(),
                shared_context: SharedContext::default(),
                llm_config: LLMConfig::default(),
                keep_alive: false,
                timeout: None,
            },
        };

        // 测试进度估算
        let progress_25 = estimate_progress_for_type(TaskType::CodeReview, 25);
        assert!(progress_25 > 0.4 && progress_25 < 0.7);

        // 测试上限
        let progress_max = estimate_progress_for_type(TaskType::CodeReview, 1000);
        assert!(progress_max <= 0.95);
    }

    #[test]
    fn test_llm_config_selection() {
        let simple_config = TaskComplexity::Simple.select_llm_config();
        assert_eq!(simple_config.model, "qwen2.5:0.5b");

        let complex_config = TaskComplexity::from_task_type(&TaskType::Research)
            .select_llm_config();
        assert_eq!(complex_config.model, "claude-3-5-sonnet-20241022");
    }

    #[tokio::test]
    async fn test_mock_session_manager() {
        let manager = MockSessionManager::new();
        let session_id = manager.create_session("Test".to_string()).await.unwrap();

        let message = Message {
            role: "user".to_string(),
            content: "Hello".to_string(),
        };

        manager.add_message(session_id, message.clone()).await.unwrap();

        let messages = manager.get_recent_messages(session_id, 10).await.unwrap();
        assert_eq!(messages.len(), 1);
        assert_eq!(messages[0].content, "Hello");
    }
}
```

### 11.3 集成测试

```rust
#[tokio::test]
async fn test_parallel_subagent_execution() {
    // 初始化
    let orchestrator = setup_orchestrator().await;
    let parent_id = Uuid::new_v4();

    // 创建 3 个并发任务的 Stage
    let stage = Stage {
        id: "test-stage".to_string(),
        name: "Test Stage".to_string(),
        tasks: vec![
            create_test_task("task1"),
            create_test_task("task2"),
            create_test_task("task3"),
        ],
        strategy: StageStrategy::Parallel,
    };

    // 启动
    let stage_id = orchestrator.start_stage(parent_id, stage).await.unwrap();

    // 等待完成
    let status = orchestrator.wait_for_stage(&stage_id).await.unwrap();
    assert_eq!(status, StageStatus::Completed);

    // 验证结果
    let results = orchestrator.collect_results(&stage_id).await.unwrap();
    assert_eq!(results.len(), 3);
    assert!(results.iter().all(|r| r.status == SessionStatus::Completed));
}

#[tokio::test]
async fn test_stage_cancellation() {
    let orchestrator = setup_orchestrator().await;
    let parent_id = Uuid::new_v4();

    let stage = create_long_running_stage();
    let stage_id = orchestrator.start_stage(parent_id, stage).await.unwrap();

    // 短暂等待后取消
    tokio::time::sleep(Duration::from_millis(100)).await;
    orchestrator.cancel_stage(&stage_id).await.unwrap();

    // 验证所有 subagent 被取消
    let states = orchestrator.get_stage_states(&stage_id);
    assert!(states.iter().all(|s| s.status == SessionStatus::Cancelled));
}

#[tokio::test]
async fn test_subagent_failure_handling() {
    let orchestrator = setup_orchestrator().await;

    // 创建一个会失败的任务
    let stage = Stage {
        tasks: vec![create_failing_task()],
        strategy: StageStrategy::Parallel,
        ..Default::default()
    };

    let stage_id = orchestrator.start_stage(Uuid::new_v4(), stage).await.unwrap();
    let status = orchestrator.wait_for_stage(&stage_id).await.unwrap();

    assert_eq!(status, StageStatus::PartiallyFailed);

    let results = orchestrator.collect_results(&stage_id).await.unwrap();
    assert_eq!(results[0].status, SessionStatus::Failed);
}
```

### 11.4 TUI 测试（手工验收）

**测试脚本：**
```bash
#!/bin/bash
# test-subagent-tui.sh

echo "=== Subagent TUI 验收测试 ==="

# 1. 启动 Agent TUI
cargo run --release --bin agent-tui &
TUI_PID=$!

sleep 2

# 2. 模拟用户输入（使用 tmux send-keys 或 expect）
# 场景 1：启动 3 个并发 subagent
echo "测试场景 1: 并发执行 3 个 subagent"
# ... 发送命令 ...

# 3. 验证 TUI 显示
echo "检查 TUI 显示："
echo "  ✅ 卡片正确显示"
echo "  ✅ 进度条更新"
echo "  ✅ 状态图标正确"
echo "  ✅ 完成后汇总显示"

# 清理
kill $TUI_PID
```

---

## 12. 实施计划

### Phase 1: 核心功能（Week 1，40-50 小时）

**Day 1-2: 数据模型和基础设施**
- [ ] Session 模型扩展（parent_id, status, stage_id）
- [ ] SubagentState 定义
- [ ] Stage 和 SubagentConfig 结构
- [ ] 数据库迁移脚本
- [ ] 单元测试

**交付物：** 数据模型完整，测试通过

**Day 3-4: SubagentOrchestrator**
- [ ] 创建 orchestrator.rs
- [ ] 实现 start_stage()
- [ ] 实现 wait_for_stage()
- [ ] 实现 cancel_stage()
- [ ] DashMap 集成
- [ ] 消息队列设置
- [ ] 单元测试

**交付物：** Orchestrator 核心功能可用

**Day 5-7: SubagentTask**
- [ ] 创建 task.rs
- [ ] 实现 run() 方法
- [ ] 共享上下文加载
- [ ] 进度更新逻辑
- [ ] 结果返回
- [ ] 错误处理
- [ ] 集成测试

**交付物：** 可以启动和执行 subagent，无 TUI

### Phase 2: TUI 集成（Week 2，30-40 小时）

**Day 1-2: SessionCardWidget 基础**
- [ ] 创建 session_card.rs
- [ ] 卡片基础渲染
- [ ] 状态图标显示
- [ ] 基础布局

**Day 3-4: 进度显示和动画**
- [ ] 进度条组件
- [ ] 实时更新循环
- [ ] 预估时间显示
- [ ] 网格布局子卡片

**Day 5: 交互逻辑**
- [ ] 展开/折叠功能
- [ ] 键盘导航
- [ ] 选中状态管理

**交付物：** 完整的 TUI 界面

### Phase 3: 高级特性（Week 2-3，20-30 小时）

**Day 1: 智能特性**
- [ ] LLM 配置智能选择
- [ ] 任务复杂度评估
- [ ] 结果汇总算法

**Day 2: 分阶段编排**
- [ ] 多阶段执行逻辑
- [ ] 阶段间结果传递
- [ ] 智能评估决策

**Day 3: 错误处理和重试**
- [ ] 失败策略实现
- [ ] 超时处理
- [ ] 重试机制
- [ ] 用户提示集成

**Day 4: 性能优化**
- [ ] 并发限制
- [ ] 内存优化
- [ ] 状态更新优化

**交付物：** 生产就绪

### Phase 4: 文档和验收（Week 3，10-15 小时）

**Day 1-2: 文档**
- [ ] 用户使用指南
- [ ] API 文档（Rust Doc）
- [ ] 架构文档更新

**Day 3: 验收测试**
- [ ] 手工验收测试脚本
- [ ] 完整场景测试
- [ ] 性能基准测试

**Day 4: 修复和优化**
- [ ] 根据验收结果修复问题
- [ ] 性能调优

**交付物：** 完整文档，验收通过

---

## 13. 风险和缓解

### 9.1 技术风险

**风险 1：DashMap 性能瓶颈**
- **描述：** 高并发下 DashMap 读写可能成为瓶颈
- **概率：** 中等
- **影响：** TUI 更新延迟
- **缓解：**
  - 限制 TUI 轮询频率（500ms）
  - 批量读取状态
  - 如果必要，切换到 RwLock<HashMap>

**风险 2：内存占用过高**
- **描述：** 大量并发 subagent 导致内存占用高
- **概率：** 低
- **影响：** 系统性能下降
- **缓解：**
  - 实施并发数量限制（默认 10）
  - 自动清理已完成的 subagent
  - 监控内存使用

**风险 3：消息队列阻塞**
- **描述：** 结果队列满导致 subagent 阻塞
- **概率：** 低
- **影响：** 系统挂起
- **缓解：**
  - 使用有界队列（bounded channel）
  - 实现背压机制
  - 超时自动丢弃

### 9.2 用户体验风险

**风险 4：TUI 复杂度过高**
- **描述：** 卡片式界面学习成本高
- **概率：** 中等
- **影响：** 用户困惑
- **缓解：**
  - 提供详细的使用文档
  - 内置帮助提示
  - 提供简化视图选项

**风险 5：进度估算不准确**
- **描述：** 进度条显示不准确，用户困惑
- **概率：** 高
- **影响：** 用户体验下降
- **缓解：**
  - 明确标注"预估"
  - 使用保守估算（永远不显示 100% 直到完成）
  - 收集反馈改进算法

---

## 14. 兼容性和迁移

### 14.1 向后兼容

- ✅ **Session 表扩展使用默认值** - 现有 Session 自动设置为 `Main` 类型
- ✅ **功能可选启用** - 通过 feature flag `subagent` 控制
- ✅ **主会话功能不受影响** - 所有现有功能正常工作
- ✅ **API 保持兼容** - 新增接口，不修改现有接口

### 14.2 配置迁移

```toml
# Cargo.toml
[features]
default = ["subagent"]  # 默认启用
subagent = []           # Subagent 功能

# 用户可选择禁用
cargo build --no-default-features
```

### 14.3 数据迁移

```rust
// 迁移脚本（自动执行）
async fn migrate_subagent_tables(pool: &SqlitePool) -> SubagentResult<()> {
    let mut tx = pool.begin().await?;

    // 创建 subagent_sessions 表
    sqlx::query(
        "CREATE TABLE IF NOT EXISTS subagent_sessions (
            session_id TEXT PRIMARY KEY,
            parent_id TEXT NOT NULL,
            session_type TEXT NOT NULL DEFAULT 'Subagent',
            status TEXT NOT NULL DEFAULT 'Idle',
            stage_id TEXT NOT NULL,
            created_at DATETIME NOT NULL,
            updated_at DATETIME NOT NULL,
            FOREIGN KEY (session_id) REFERENCES sessions(id) ON DELETE CASCADE,
            FOREIGN KEY (parent_id) REFERENCES sessions(id) ON DELETE CASCADE
        )"
    )
    .execute(&mut *tx)
    .await?;

    // 创建索引
    sqlx::query("CREATE INDEX idx_subagent_sessions_parent_id ON subagent_sessions(parent_id)")
        .execute(&mut *tx)
        .await?;

    sqlx::query("CREATE INDEX idx_subagent_sessions_stage_id ON subagent_sessions(stage_id)")
        .execute(&mut *tx)
        .await?;

    // 创建 stages 表
    sqlx::query(
        "CREATE TABLE IF NOT EXISTS stages (
            id TEXT PRIMARY KEY,
            parent_session_id TEXT NOT NULL,
            name TEXT NOT NULL,
            status TEXT NOT NULL DEFAULT 'Running',
            created_at DATETIME NOT NULL,
            completed_at DATETIME,
            FOREIGN KEY (parent_session_id) REFERENCES sessions(id) ON DELETE CASCADE
        )"
    )
    .execute(&mut *tx)
    .await?;

    tx.commit().await?;
    Ok(())
}
```

---

## 15. 性能指标

### 15.1 目标指标

- **并发性能：** 支持至少 10 个并发 subagent
- **TUI 响应：** 状态更新延迟 < 1.5 秒（500ms 轮询 + 最多 1s 处理）
- **内存占用：** 每个 subagent < 150MB（包含 ConversationFlow + LLM client + 消息历史）
- **启动延迟：** 启动 subagent < 500ms（不含首次 LLM 调用）
- **测试覆盖：** > 80%
- **DashMap 读取：** < 100µs（无竞争情况）
- **通道吞吐：** > 1000 msg/s

### 15.2 基准测试

```rust
#[tokio::test]
async fn benchmark_concurrent_subagents() {
    let start = Instant::now();

    // 启动 10 个并发 subagent
    let orchestrator = setup_orchestrator().await;
    let stage = create_stage_with_n_tasks(10);

    let stage_id = orchestrator.start_stage(Uuid::new_v4(), stage).await.unwrap();
    orchestrator.wait_for_stage(&stage_id).await.unwrap();

    let duration = start.elapsed();

    // 验证性能
    assert!(duration < Duration::from_secs(60)); // 应在 1 分钟内完成
    println!("10 concurrent subagents completed in {:?}", duration);
}
```

---

## 16. 后续优化方向

**Phase 2+ 增强特性（未来考虑）：**

1. **Subagent 市场**
   - 预定义的 subagent 模板
   - 社区共享和评分

2. **动态资源调度**
   - 根据系统负载自动调整并发数
   - 优先级队列

3. **Subagent 间通信**
   - 允许 subagent 之间交换消息
   - 构建 DAG 式任务依赖

4. **可视化编排器**
   - Web UI 拖拽式任务编排
   - 实时监控仪表板

5. **持久化 Stage 模板**
   - 保存常用的 Stage 配置
   - 一键重用

---

## 17. 总结

### 17.1 关键决策回顾

✅ **混合触发模式** - LLM 提议 + 用户确认
✅ **自动生命周期管理** - 自动清理 + 支持保持活跃
✅ **分阶段编排** - Orchestrator 集中管理
✅ **卡片式 TUI** - 进度、状态、预估时间
✅ **有限上下文共享** - 灵活且安全
✅ **模板化结果汇总** - 评估、决策、编排
✅ **基于规则的 LLM 选择** - 根据任务类型和复杂度自动选择
✅ **混合架构** - 消息队列 + DashMap 共享状态
✅ **Panic 恢复机制** - 保证系统稳定性
✅ **事务边界** - 保证数据一致性

### 17.2 预期成果

- **代码量：** ~2000-2500 行新代码
- **开发时间：** 2-3 周（全职开发）
- **测试覆盖：** > 80%
- **性能：** 支持 10+ 并发 subagent
- **用户体验：** 实时进度、清晰状态、智能编排

### 17.3 成功标准

✅ 用户可以轻松启动和监控 subagent
✅ TUI 界面直观、响应快速
✅ 并发执行稳定可靠
✅ 智能汇总生成高质量结果
✅ 错误处理健壮，恢复机制完善
✅ 文档完整，易于理解

---

**文档版本：** 1.0
**最后更新：** 2026-03-11
**负责人：** General Agent V2 开发团队
**审核状态：** 待审核
