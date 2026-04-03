# Week 2 执行计划：集成现有功能

**日期**: 2026-03-14 - 2026-03-20
**目标**: 集成 LLM/Skills/MCP，添加状态持久化和控制信号
**前置**: Week 1 已完成（DAG 编排器和执行引擎）

---

## 🎯 本周目标

将 Workflow 系统与现有的 LLM、Skills、MCP 模块集成：

1. ✅ TaskType::LLMCall - 调用 LLM
2. ✅ TaskType::SkillExecution - 执行技能
3. ✅ TaskType::MCPToolCall - 调用 MCP 工具
4. ✅ 状态持久化 - SQLite 存储
5. ✅ 控制信号 - 取消/暂停/恢复

---

## 📅 Day-by-Day 计划

### Day 1: LLMCall 集成（3-4 小时）

#### 目标
实现 TaskType::LLMCall，调用 agent-llm 客户端

#### 实现步骤

1. **修改 executor.rs**

```rust
// 添加 LLM 客户端依赖
use agent_llm::{AnthropicClient, OllamaClient};

pub struct TaskExecutor {
    llm_client: Option<Arc<dyn LLMClient>>, // 新增
}

impl TaskExecutor {
    pub fn with_llm_client(mut self, client: Arc<dyn LLMClient>) -> Self {
        self.llm_client = Some(client);
        self
    }
}

async fn execute_task_once(&self, task: &Task) -> Result<String> {
    match &task.task_type {
        TaskType::LLMCall => {
            let client = self.llm_client.as_ref()
                .ok_or_else(|| anyhow!("LLM client not configured"))?;

            // 从 task.metadata 获取参数
            let prompt = task.metadata.get("prompt")
                .ok_or_else(|| anyhow!("Missing 'prompt' in metadata"))?;

            // 调用 LLM
            let response = client.send_message(prompt).await?;
            Ok(response)
        }
        // ... 其他类型
    }
}
```

2. **添加测试**

```rust
#[tokio::test]
async fn test_llm_call_task() {
    let llm_client = Arc::new(MockLLMClient::new());
    let executor = TaskExecutor::new().with_llm_client(llm_client);

    let mut task = Task::new("llm-task", "LLM Task", TaskType::LLMCall);
    task.metadata.insert("prompt".to_string(), "Hello".to_string());

    let result = executor.execute_task(&task).await;
    assert_eq!(result.status, TaskStatus::Completed);
}
```

3. **更新 Cargo.toml**

```toml
[dependencies]
agent-llm = { path = "../agent-llm" }
```

#### 验收标准
- [ ] LLMCall 任务可以执行
- [ ] 支持 Anthropic 和 Ollama
- [ ] 错误处理完善
- [ ] 测试通过

---

### Day 2: SkillExecution 集成（3-4 小时）

#### 目标
实现 TaskType::SkillExecution，调用 agent-skills 执行器

#### 实现步骤

1. **修改 executor.rs**

```rust
use agent_skills::SkillExecutor;

pub struct TaskExecutor {
    llm_client: Option<Arc<dyn LLMClient>>,
    skill_executor: Option<Arc<SkillExecutor>>, // 新增
}

async fn execute_task_once(&self, task: &Task) -> Result<String> {
    match &task.task_type {
        TaskType::SkillExecution => {
            let executor = self.skill_executor.as_ref()
                .ok_or_else(|| anyhow!("Skill executor not configured"))?;

            // 从 metadata 获取技能名称和参数
            let skill_name = task.metadata.get("skill_name")
                .ok_or_else(|| anyhow!("Missing 'skill_name'"))?;
            let params_json = task.metadata.get("params")
                .unwrap_or(&"{}".to_string());
            let params: serde_json::Value = serde_json::from_str(params_json)?;

            // 执行技能
            let result = executor.execute(skill_name, params).await?;
            Ok(result)
        }
        // ... 其他类型
    }
}
```

2. **添加测试**

```rust
#[tokio::test]
async fn test_skill_execution_task() {
    let skill_executor = Arc::new(SkillExecutor::new(/*...*/));
    let executor = TaskExecutor::new()
        .with_skill_executor(skill_executor);

    let mut task = Task::new("skill-task", "Skill Task", TaskType::SkillExecution);
    task.metadata.insert("skill_name".to_string(), "greeting".to_string());
    task.metadata.insert("params".to_string(), r#"{"name":"Alice"}"#.to_string());

    let result = executor.execute_task(&task).await;
    assert_eq!(result.status, TaskStatus::Completed);
}
```

#### 验收标准
- [ ] SkillExecution 任务可执行
- [ ] 参数传递正确
- [ ] 技能执行结果正确
- [ ] 测试通过

---

### Day 3: MCPToolCall 集成（3-4 小时）

#### 目标
实现 TaskType::MCPToolCall，调用 agent-mcp 客户端

#### 实现步骤

1. **修改 executor.rs**

```rust
#[cfg(feature = "mcp")]
use agent_mcp::MCPClient;

pub struct TaskExecutor {
    llm_client: Option<Arc<dyn LLMClient>>,
    skill_executor: Option<Arc<SkillExecutor>>,
    #[cfg(feature = "mcp")]
    mcp_client: Option<Arc<MCPClient>>, // 新增
}

async fn execute_task_once(&self, task: &Task) -> Result<String> {
    match &task.task_type {
        #[cfg(feature = "mcp")]
        TaskType::MCPToolCall => {
            let client = self.mcp_client.as_ref()
                .ok_or_else(|| anyhow!("MCP client not configured"))?;

            // 从 metadata 获取服务器和工具信息
            let server = task.metadata.get("server")
                .ok_or_else(|| anyhow!("Missing 'server'"))?;
            let tool = task.metadata.get("tool")
                .ok_or_else(|| anyhow!("Missing 'tool'"))?;
            let args_json = task.metadata.get("args")
                .unwrap_or(&"{}".to_string());
            let args: serde_json::Value = serde_json::from_str(args_json)?;

            // 调用 MCP 工具
            let result = client.call_tool(server, tool, args).await?;
            Ok(serde_json::to_string(&result)?)
        }
        // ... 其他类型
    }
}
```

2. **添加测试**

```rust
#[cfg(feature = "mcp")]
#[tokio::test]
async fn test_mcp_tool_call_task() {
    let mcp_client = Arc::new(MCPClient::new(/*...*/));
    let executor = TaskExecutor::new()
        .with_mcp_client(mcp_client);

    let mut task = Task::new("mcp-task", "MCP Task", TaskType::MCPToolCall);
    task.metadata.insert("server".to_string(), "filesystem".to_string());
    task.metadata.insert("tool".to_string(), "read_file".to_string());
    task.metadata.insert("args".to_string(), r#"{"path":"test.txt"}"#.to_string());

    let result = executor.execute_task(&task).await;
    assert_eq!(result.status, TaskStatus::Completed);
}
```

#### 验收标准
- [ ] MCPToolCall 任务可执行
- [ ] JSON-RPC 调用正确
- [ ] 错误处理完善
- [ ] 测试通过

---

### Day 4: 状态持久化（4-5 小时）

#### 目标
将工作流状态持久化到 SQLite

#### 实现步骤

1. **数据库 Schema**

```sql
-- 工作流表
CREATE TABLE workflow_executions (
    id TEXT PRIMARY KEY,
    workflow_id TEXT NOT NULL,
    status TEXT NOT NULL,  -- 'running', 'completed', 'failed', 'cancelled'
    started_at DATETIME NOT NULL,
    completed_at DATETIME,
    error TEXT,
    created_at DATETIME NOT NULL
);

-- 任务执行表
CREATE TABLE task_executions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    workflow_execution_id TEXT NOT NULL,
    task_id TEXT NOT NULL,
    status TEXT NOT NULL,
    output TEXT,
    error TEXT,
    execution_time_ms INTEGER,
    started_at DATETIME,
    completed_at DATETIME,
    FOREIGN KEY (workflow_execution_id) REFERENCES workflow_executions(id)
);

CREATE INDEX idx_workflow_executions_status ON workflow_executions(status);
CREATE INDEX idx_task_executions_workflow ON task_executions(workflow_execution_id);
```

2. **实现 WorkflowStorage**

```rust
pub struct WorkflowStorage {
    pool: SqlitePool,
}

impl WorkflowStorage {
    pub async fn save_workflow_execution(&self, execution: &WorkflowExecution) -> Result<()> {
        sqlx::query!(
            "INSERT INTO workflow_executions (id, workflow_id, status, started_at, created_at)
             VALUES (?, ?, ?, ?, ?)",
            execution.id,
            execution.workflow_id,
            execution.status.to_string(),
            execution.started_at,
            execution.created_at
        )
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    pub async fn save_task_result(&self, execution_id: &str, result: &TaskResult) -> Result<()> {
        sqlx::query!(
            "INSERT INTO task_executions (workflow_execution_id, task_id, status, output, error, execution_time_ms)
             VALUES (?, ?, ?, ?, ?, ?)",
            execution_id,
            result.task_id,
            result.status.to_string(),
            result.output,
            result.error,
            result.execution_time_ms as i64
        )
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    pub async fn load_workflow_execution(&self, id: &str) -> Result<WorkflowExecution> {
        // 实现加载逻辑
    }
}
```

3. **集成到 Orchestrator**

```rust
impl WorkflowOrchestrator {
    pub async fn execute_with_persistence(
        &self,
        executor: &TaskExecutor,
        storage: &WorkflowStorage
    ) -> Result<WorkflowResult> {
        let execution_id = Uuid::new_v4().to_string();

        // 保存初始状态
        storage.save_workflow_execution(&WorkflowExecution {
            id: execution_id.clone(),
            workflow_id: self.workflow_id.clone(),
            status: WorkflowStatus::Running,
            started_at: Utc::now(),
            created_at: Utc::now(),
        }).await?;

        // 执行工作流
        let result = self.execute(executor).await;

        // 保存结果
        match &result {
            Ok(workflow_result) => {
                for (task_id, task_result) in &workflow_result.task_results {
                    storage.save_task_result(&execution_id, task_result).await?;
                }
            }
            Err(e) => {
                // 保存失败状态
            }
        }

        result
    }
}
```

#### 验收标准
- [ ] 工作流状态可保存
- [ ] 任务结果可保存
- [ ] 工作流可恢复执行
- [ ] 测试通过

---

### Day 5: 控制信号（3-4 小时）

#### 目标
实现取消、暂停、恢复功能

#### 实现步骤

1. **定义控制信号**

```rust
pub enum ControlSignal {
    Pause,
    Resume,
    Cancel,
}

pub struct ControlChannel {
    tx: mpsc::UnboundedSender<ControlSignal>,
    rx: mpsc::UnboundedReceiver<ControlSignal>,
}
```

2. **修改 Orchestrator**

```rust
impl WorkflowOrchestrator {
    pub async fn execute_with_control(
        &self,
        executor: &TaskExecutor,
        control_rx: mpsc::UnboundedReceiver<ControlSignal>
    ) -> Result<WorkflowResult> {
        let mut paused = false;
        let mut cancelled = false;

        loop {
            // 检查控制信号
            if let Ok(signal) = control_rx.try_recv() {
                match signal {
                    ControlSignal::Pause => paused = true,
                    ControlSignal::Resume => paused = false,
                    ControlSignal::Cancel => {
                        cancelled = true;
                        break;
                    }
                }
            }

            // 处理暂停
            while paused && !cancelled {
                tokio::time::sleep(Duration::from_millis(100)).await;
                if let Ok(signal) = control_rx.try_recv() {
                    match signal {
                        ControlSignal::Resume => paused = false,
                        ControlSignal::Cancel => {
                            cancelled = true;
                            break;
                        }
                        _ => {}
                    }
                }
            }

            if cancelled {
                bail!("Workflow cancelled");
            }

            // 原有的执行逻辑
            // ...
        }
    }
}
```

3. **添加测试**

```rust
#[tokio::test]
async fn test_workflow_cancel() {
    let (tx, rx) = mpsc::unbounded_channel();

    // 启动工作流
    let handle = tokio::spawn(async move {
        orchestrator.execute_with_control(&executor, rx).await
    });

    // 发送取消信号
    tokio::time::sleep(Duration::from_millis(10)).await;
    tx.send(ControlSignal::Cancel).unwrap();

    // 验证工作流被取消
    let result = handle.await.unwrap();
    assert!(result.is_err());
}
```

#### 验收标准
- [ ] 支持取消工作流
- [ ] 支持暂停/恢复
- [ ] 状态转换正确
- [ ] 测试通过

---

## 📊 Week 2 验收标准

### 功能完整性
- [ ] LLMCall 任务可执行
- [ ] SkillExecution 任务可执行
- [ ] MCPToolCall 任务可执行（feature gate）
- [ ] 工作流状态可持久化
- [ ] 工作流可从数据库恢复
- [ ] 支持取消操作
- [ ] 支持暂停/恢复

### 代码质量
- [ ] 所有测试通过（单元 + 集成）
- [ ] Clippy 无警告
- [ ] 测试覆盖率 > 80%
- [ ] 文档完整

### 性能要求
- [ ] 持久化不影响性能（< 10% 开销）
- [ ] 控制信号响应快（< 100ms）

---

## 🧪 测试策略

### 单元测试

每个任务类型至少 3 个测试：
1. 正常执行
2. 参数缺失错误
3. 执行失败错误

### 集成测试

1. **混合任务类型工作流**
   - LLM + Skill + MCP 组合

2. **持久化测试**
   - 保存和恢复工作流

3. **控制信号测试**
   - 取消、暂停、恢复

---

## 🎯 关键决策

### 1. 参数传递方式

使用 `task.metadata` HashMap 传递参数：
- 优点：灵活、类型无关
- 缺点：需要运行时解析

### 2. 持久化时机

在任务完成后立即保存：
- 优点：状态一致性好
- 缺点：可能影响性能

### 3. 控制信号机制

使用 tokio mpsc channel：
- 优点：异步、非阻塞
- 缺点：需要轮询检查

---

## ⚠️ 风险和缓解

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| LLM 客户端接口不兼容 | 高 | 提前验证接口 |
| 持久化性能开销大 | 中 | 批量保存 |
| 控制信号响应慢 | 低 | 减少轮询间隔 |
| 测试环境依赖 | 中 | 使用 Mock |

---

## 📚 参考文档

- Week 1 完成总结：`WEEK1_COMPLETE_SUMMARY.md`
- Phase 3 总计划：`PHASE3_MIGRATION_PLAN.md`
- 技术交接文档：`HANDOFF_PHASE3.md`
- Python 实现参考：`src/workflow/`

---

**创建日期**: 2026-03-13
**计划执行**: 2026-03-14
**预计完成**: 2026-03-20
