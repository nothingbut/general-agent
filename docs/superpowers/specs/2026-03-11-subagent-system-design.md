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
- ✅ **智能生命周期** - 自动关闭，支持保持活跃，可唤醒已关闭会话
- ✅ **分阶段编排** - Orchestrator 控制多阶段执行
- ✅ **卡片式 TUI** - 进度条、状态、预估时间，实时更新
- ✅ **有限上下文共享** - 启动时指定共享内容（消息、变量）
- ✅ **智能结果汇总** - 评估、决策、编排下一步
- ✅ **智能 LLM 选择** - 根据任务类型自动选择模型

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

```rust
pub struct Session {
    // 现有字段
    pub id: Uuid,
    pub title: Option<String>,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
    pub context: SessionContext,

    // 新增字段
    pub parent_id: Option<Uuid>,           // 父会话 ID（subagent 专用）
    pub session_type: SessionType,         // Main | Subagent
    pub status: SessionStatus,             // Running | Completed | Failed
    pub stage_id: Option<String>,          // 所属阶段 ID
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
}

pub enum StageStrategy {
    Parallel,                    // 并发执行所有任务
    Sequential,                  // 顺序执行
    ParallelWithLimit(usize),    // 限制并发数
}
```

### 3.3 SubagentConfig（启动配置）

```rust
pub struct SubagentConfig {
    pub title: String,
    pub initial_prompt: String,
    pub shared_context: SharedContext,
    pub llm_config: LLMConfig,
    pub keep_alive: bool,              // 是否保持活跃
    pub timeout: Option<Duration>,
}

pub struct SharedContext {
    pub recent_messages: Option<usize>,    // 共享最近 N 条消息
    pub variables: HashMap<String, String>, // 共享变量
    pub system_prompt: Option<String>,
}
```

### 3.4 数据库迁移

```sql
-- Session 表扩展
ALTER TABLE sessions
  ADD COLUMN parent_id TEXT,
  ADD COLUMN session_type TEXT DEFAULT 'Main',
  ADD COLUMN status TEXT DEFAULT 'Idle',
  ADD COLUMN stage_id TEXT;

-- 添加索引
CREATE INDEX idx_sessions_parent_id ON sessions(parent_id);
CREATE INDEX idx_sessions_stage_id ON sessions(stage_id);
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
4. 为每个任务：
    a. 在数据库创建 Session（parent_id 指向主会话）
    b. 初始化 SubagentState → DashMap
    c. 创建 SubagentTaskConfig
    d. 生成 SharedContext（从主会话提取）
    e. 启动 tokio::spawn(task.run())
    ↓
5. 返回 stage_id 给主会话
    ↓
6. TUI 开始轮询 DashMap，显示进度
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

### 5.1 消息队列设计

```rust
// Orchestrator 内部
pub struct OrchestratorChannels {
    command_tx: mpsc::Sender<SubagentCommand>,
    result_rx: mpsc::Receiver<SubagentResult>,
}

pub enum SubagentCommand {
    Start(SubagentTaskConfig),
    Cancel(Uuid),
    UpdateConfig(Uuid, SubagentConfig),
}

pub struct SubagentResult {
    pub session_id: Uuid,
    pub status: SessionStatus,
    pub output: String,
    pub metadata: ResultMetadata,
}
```

### 5.2 进度估算算法

```rust
impl SubagentTask {
    fn estimate_progress(&self, message_count: usize) -> f32 {
        // 基于消息数量的简单估算
        // 假设平均任务需要 100 个消息块
        let estimated_total = 100.0;
        let raw_progress = message_count as f32 / estimated_total;

        // 永远不显示 100%，直到真正完成
        raw_progress.min(0.95)
    }

    fn estimate_remaining_time(&self, current_progress: f32) -> Option<Duration> {
        if current_progress <= 0.01 {
            return None; // 数据不足
        }

        let elapsed = Utc::now() - self.state.started_at;
        let total_estimated = elapsed.num_seconds() as f32 / current_progress;
        let remaining = total_estimated * (1.0 - current_progress);

        Some(Duration::from_secs(remaining as u64))
    }
}
```

### 5.3 智能 LLM 配置选择

```rust
pub enum TaskComplexity {
    Simple,      // 简单任务（如格式转换、简单分析）
    Medium,      // 中等任务（如代码审查、文档生成）
    Complex,     // 复杂任务（如架构设计、深度研究）
    Custom(LLMConfig),  // 用户指定
}

impl TaskComplexity {
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
}
```

### 5.4 共享上下文加载

```rust
impl SubagentTask {
    async fn load_shared_context(&self, flow: &ConversationFlow) -> Result<()> {
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

---

## 6. 错误处理

### 6.1 Subagent 失败策略

```rust
pub enum FailureStrategy {
    IgnoreAndContinue,      // 忽略失败，继续其他任务
    FailStage,              // 整个 Stage 失败
    RetryOnce,              // 重试一次
    AskUser,                // 提示用户决定
}

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

## 7. 测试策略

### 7.1 单元测试（目标覆盖率 > 80%）

```rust
#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_subagent_state_updates() {
        let mut state = SubagentState::new(Uuid::new_v4());
        state.update_progress(0.5);
        assert_eq!(state.progress, 0.5);
        assert_eq!(state.status, SessionStatus::Running);
    }

    #[test]
    fn test_progress_estimation() {
        let task = SubagentTask::new(/* ... */);
        assert_eq!(task.estimate_progress(50), 0.5);
        assert!(task.estimate_progress(200) <= 0.95);
    }

    #[test]
    fn test_llm_config_selection() {
        let config = TaskComplexity::Simple.select_llm_config();
        assert_eq!(config.model, "qwen2.5:0.5b");
    }
}
```

### 7.2 集成测试

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

### 7.3 TUI 测试（手工验收）

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

## 8. 实施计划

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

## 9. 风险和缓解

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

## 10. 兼容性和迁移

### 10.1 向后兼容

- ✅ **Session 表扩展使用默认值** - 现有 Session 自动设置为 `Main` 类型
- ✅ **功能可选启用** - 通过 feature flag `subagent` 控制
- ✅ **主会话功能不受影响** - 所有现有功能正常工作
- ✅ **API 保持兼容** - 新增接口，不修改现有接口

### 10.2 配置迁移

```toml
# Cargo.toml
[features]
default = ["subagent"]  # 默认启用
subagent = []           # Subagent 功能

# 用户可选择禁用
cargo build --no-default-features
```

### 10.3 数据迁移

```rust
// 迁移脚本（自动执行）
async fn migrate_sessions(pool: &SqlitePool) -> Result<()> {
    sqlx::query(
        "ALTER TABLE sessions
         ADD COLUMN parent_id TEXT,
         ADD COLUMN session_type TEXT DEFAULT 'Main',
         ADD COLUMN status TEXT DEFAULT 'Idle',
         ADD COLUMN stage_id TEXT"
    )
    .execute(pool)
    .await?;

    // 创建索引
    sqlx::query("CREATE INDEX idx_sessions_parent_id ON sessions(parent_id)")
        .execute(pool)
        .await?;

    Ok(())
}
```

---

## 11. 性能指标

### 11.1 目标指标

- **并发性能：** 支持至少 10 个并发 subagent
- **TUI 响应：** 状态更新延迟 < 1 秒
- **内存占用：** 每个 subagent < 50MB
- **启动延迟：** 启动 subagent < 500ms
- **测试覆盖：** > 80%

### 11.2 基准测试

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

## 12. 后续优化方向

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

## 13. 总结

### 13.1 关键决策回顾

✅ **混合触发模式** - LLM 智能提议 + 用户确认
✅ **智能生命周期** - 自动清理 + 支持保持活跃
✅ **分阶段编排** - Orchestrator 集中管理
✅ **卡片式 TUI** - 进度、状态、预估时间
✅ **有限上下文共享** - 灵活且安全
✅ **智能结果汇总** - 评估、决策、编排
✅ **智能 LLM 选择** - 根据任务复杂度自动选择
✅ **混合架构** - 消息队列 + DashMap 共享状态

### 13.2 预期成果

- **代码量：** ~2000-2500 行新代码
- **开发时间：** 2-3 周（全职开发）
- **测试覆盖：** > 80%
- **性能：** 支持 10+ 并发 subagent
- **用户体验：** 实时进度、清晰状态、智能编排

### 13.3 成功标准

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
