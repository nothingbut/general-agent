# Task 3 修复版本 - SubagentOrchestrator 执行方法

这是Task 3的修复版本，使用正确的API。

## 修复的关键变更

1. **SubagentState**: 使用`SubagentState::new()`构造函数
2. **SubagentTaskConfig**: 创建正确的配置对象
3. **ProgressEstimator**: 添加必需的参数
4. **result_tx**: 传递给task.run()
5. **stage_id**: Uuid转String

---

### Task 3: SubagentOrchestrator 执行方法

**Files:**
- Modify: `crates/agent-workflow/src/subagent/orchestrator.rs`
- Create: `crates/agent-workflow/tests/integration_subagent_execution.rs`

- [ ] **Step 1: 写失败的测试 - 单 Stage 执行**

```rust
// crates/agent-workflow/tests/integration_subagent_execution.rs
use agent_workflow::subagent::{
    OrchestratorConfig, SubagentOrchestrator, SubagentConfig,
};
use std::time::Duration;
use uuid::Uuid;

async fn setup_test_environment() -> (agent_storage::Database, SubagentOrchestrator) {
    let db = agent_storage::Database::new(":memory:").await.unwrap();
    db.migrate().await.unwrap();

    let orchestrator = SubagentOrchestrator::new(OrchestratorConfig::default());

    (db, orchestrator)
}

#[tokio::test]
async fn test_create_and_execute_stage() {
    let (_db, orchestrator) = setup_test_environment().await;
    let parent_session_id = Uuid::new_v4();

    // 准备任务
    let tasks = vec![
        "任务1".to_string(),
        "任务2".to_string(),
        "任务3".to_string(),
    ];

    let config = SubagentConfig::default()
        .with_timeout(Duration::from_secs(10));

    // 创建并执行 Stage
    let stage_id = orchestrator
        .create_and_execute_stage(parent_session_id, tasks, Some(config))
        .await
        .unwrap();

    // 验证 Stage 创建成功
    assert_ne!(stage_id, Uuid::nil());

    // 验证活跃任务数
    assert_eq!(orchestrator.active_count(), 3);

    // 等待一段时间
    tokio::time::sleep(Duration::from_millis(100)).await;

    // 验证可以获取状态
    let states = orchestrator.get_subagent_states(parent_session_id);
    assert_eq!(states.len(), 3);
}

#[tokio::test]
async fn test_concurrent_limit_enforcement() {
    let (_db, orchestrator) = setup_test_environment().await;
    let parent_session_id = Uuid::new_v4();

    // 尝试启动超过限制的任务（默认 10）
    let tasks: Vec<String> = (0..15)
        .map(|i| format!("任务{}", i))
        .collect();

    let result = orchestrator
        .create_and_execute_stage(parent_session_id, tasks, None)
        .await;

    // 应该失败（超过并发限制）
    assert!(result.is_err());
}
```

- [ ] **Step 2: 运行测试验证失败**

```bash
cargo test --test integration_subagent_execution
```

预期：编译失败，`create_and_execute_stage` 方法未定义

- [ ] **Step 3: 扩展 SubagentOrchestrator 结构体并实现方法**

```rust
// 在 crates/agent-workflow/src/subagent/orchestrator.rs 中修改

use agent_storage::Pool;
use tokio::sync::mpsc;

/// Subagent orchestrator
pub struct SubagentOrchestrator {
    config: OrchestratorConfig,
    state_map: Arc<DashMap<Uuid, SubagentState>>,
    active_count: Arc<AtomicUsize>,
    command_tx: mpsc::Sender<SubagentCommand>,
    command_rx: Option<mpsc::Receiver<SubagentCommand>>,
    result_tx: mpsc::Sender<TaskResult>,
    result_rx: Option<mpsc::Receiver<TaskResult>>,
    shutdown_tx: broadcast::Sender<()>,

    // 新增字段（用于Task和数据库持久化）
    pool: Option<Pool>,
}

impl SubagentOrchestrator {
    /// Create new orchestrator
    pub fn new(config: OrchestratorConfig) -> Self {
        let state_map = Arc::new(DashMap::new());
        let active_count = Arc::new(AtomicUsize::new(0));
        let (command_tx, command_rx) = mpsc::channel(100);
        let (result_tx, result_rx) = mpsc::channel(100);
        let (shutdown_tx, _) = broadcast::channel(16);

        Self {
            config,
            state_map,
            active_count,
            command_tx,
            command_rx: Some(command_rx),
            result_tx,
            result_rx: Some(result_rx),
            shutdown_tx,
            pool: None,
        }
    }

    /// 设置数据库连接池（由 AgentRuntime 注入）
    pub fn set_database_pool(&mut self, pool: Pool) {
        self.pool = Some(pool);
    }

    // ... 现有getters/setters保持不变 (lines 64-144) ...

    /// 创建并执行单个 Stage（Phase 1+2 简化版 - 仅内存执行）
    ///
    /// 注意：本方法仅在内存中管理状态，不进行数据库持久化。
    /// 数据库持久化将在 Task 4 中添加。
    ///
    /// # 参数
    /// - `parent_session_id`: 父会话ID
    /// - `tasks`: 任务描述列表
    /// - `config`: 可选的子代理配置
    ///
    /// # 返回
    /// - `Ok(Uuid)`: 创建的Stage ID
    /// - `Err`: 超过并发限制或其他错误
    pub async fn create_and_execute_stage(
        &self,
        parent_session_id: Uuid,
        tasks: Vec<String>,
        config: Option<SubagentConfig>,
    ) -> SubagentResult<Uuid> {
        use chrono::Utc;
        use super::config::{SubagentTaskConfig, TaskType};
        use super::progress::ProgressEstimator;
        use super::task::SubagentTask;

        // 1. 预留槽位
        self.try_reserve_slots(tasks.len())?;

        // 2. 创建 Stage（仅内存，UUID转String）
        let stage_id = Uuid::new_v4();
        let stage_id_str = stage_id.to_string();
        let stage_name = format!("Stage - {}", Utc::now().format("%H:%M:%S"));

        // 3. 为每个任务创建 subagent session（仅内存）
        let default_config = config.unwrap_or_default();
        let result_tx = self.result_tx.clone();

        for (idx, task) in tasks.iter().enumerate() {
            let session_id = Uuid::new_v4();

            // 使用SubagentState::new()构造函数创建状态
            let state = SubagentState::new(
                session_id,
                parent_session_id,
                stage_id_str.clone(),
            )?;

            // 插入状态到 DashMap
            self.state_map.insert(session_id, state);

            // 构造SubagentTaskConfig
            let task_config = SubagentTaskConfig {
                id: session_id,
                config: SubagentConfig {
                    title: format!("Task {}: {}", idx + 1, task),
                    initial_prompt: task.clone(),
                    shared_context: default_config.shared_context.clone(),
                    llm_config: default_config.llm_config.clone(),
                    keep_alive: default_config.keep_alive,
                    timeout: default_config.timeout,
                },
                parent_id: parent_session_id,
                stage_id: stage_id_str.clone(),
                priority: 0,
                task_type: TaskType::Custom,
            };

            // 创建ProgressEstimator
            let progress_estimator = ProgressEstimator::new(TaskType::Custom);

            // 创建SubagentTask
            let subagent_task = SubagentTask::new(
                session_id,
                task_config,
                self.state_map.clone(),
                progress_estimator,
            );

            // 克隆result_tx用于task
            let task_result_tx = result_tx.clone();

            // 启动任务（并发）
            tokio::spawn(async move {
                if let Err(e) = subagent_task.run(task_result_tx).await {
                    eprintln!("SubagentTask {} 失败: {}", session_id, e);
                }
            });
        }

        Ok(stage_id)
    }

    /// 获取指定会话的所有子代理状态
    pub fn get_subagent_states(&self, parent_session_id: Uuid) -> Vec<SubagentState> {
        self.state_map
            .iter()
            .filter(|entry| entry.value().parent_id() == parent_session_id)
            .map(|entry| entry.value().clone())
            .collect()
    }

    /// 获取所有子代理状态
    pub fn get_all_states(&self) -> Vec<SubagentState> {
        self.state_map
            .iter()
            .map(|entry| entry.value().clone())
            .collect()
    }

    /// 获取指定 Stage 的统计信息
    pub fn get_stage_stats(&self, stage_id: &str) -> StageStats {
        let states: Vec<_> = self.state_map
            .iter()
            .filter(|entry| entry.value().stage_id() == stage_id)
            .map(|entry| entry.value().clone())
            .collect();

        let total = states.len();
        let completed = states.iter()
            .filter(|s| matches!(s.status(), SessionStatus::Completed))
            .count();
        let failed = states.iter()
            .filter(|s| matches!(s.status(), SessionStatus::Failed))
            .count();
        let running = states.iter()
            .filter(|s| matches!(s.status(), SessionStatus::Running))
            .count();

        StageStats {
            total,
            completed,
            failed,
            running,
        }
    }
}

/// Stage 统计信息
#[derive(Debug, Clone)]
pub struct StageStats {
    pub total: usize,
    pub completed: usize,
    pub failed: usize,
    pub running: usize,
}
```

**注意事项**：
1. 使用`SubagentState::new()`而非结构体字面量
2. `stage_id`转换为String: `stage_id.to_string()`
3. 创建`SubagentTaskConfig`包含所有必需字段
4. 添加`ProgressEstimator::new(TaskType::Custom)`
5. `task.run(result_tx)`传递result_tx
6. getter方法需要适配SubagentState的私有字段（使用`.parent_id()`, `.stage_id()`, `.status()`）

- [ ] **Step 4: 运行测试验证通过**

```bash
cargo test --test integration_subagent_execution
```

预期：✅ 测试通过（至少编译通过，SubagentTask的实际执行可能需要额外设置）

- [ ] **Step 5: 提交**

```bash
git add crates/agent-workflow/src/subagent/orchestrator.rs \
        crates/agent-workflow/tests/integration_subagent_execution.rs
git commit -m "feat(workflow): add SubagentOrchestrator execution method

- Extend Orchestrator with pool field and setter
- Implement create_and_execute_stage() for single stage execution
- Use correct SubagentState::new() constructor API
- Create SubagentTaskConfig with all required fields
- Add ProgressEstimator for task execution
- Pass result_tx to SubagentTask::run()
- Convert stage_id from Uuid to String
- Add get_subagent_states(), get_all_states(), get_stage_stats()
- Memory-only implementation (database persistence in Task 4)

Tests verify stage creation and concurrent limit enforcement.

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## API使用说明

### SubagentState
```rust
// ✅ 正确：使用构造函数
let state = SubagentState::new(
    session_id,      // Uuid
    parent_id,       // Uuid
    stage_id_str,    // String (不是Uuid!)
)?;

// ❌ 错误：直接构造
SubagentState { ... }  // 字段是私有的
```

### SubagentTaskConfig
```rust
let task_config = SubagentTaskConfig {
    id: session_id,
    config: SubagentConfig { ... },
    parent_id,
    stage_id: stage_id.to_string(),  // String
    priority: 0,
    task_type: TaskType::Custom,
};
```

### SubagentTask
```rust
let task = SubagentTask::new(
    session_id,
    task_config,           // SubagentTaskConfig
    state_map.clone(),
    progress_estimator,    // ProgressEstimator::new(TaskType::Custom)
);

task.run(result_tx).await?;  // 需要result_tx参数
```

### 访问SubagentState字段
```rust
// 字段是私有的，使用getter方法
state.parent_id()   // → Uuid
state.stage_id()    // → &str
state.status()      // → &SessionStatus
state.progress()    // → f32
```
