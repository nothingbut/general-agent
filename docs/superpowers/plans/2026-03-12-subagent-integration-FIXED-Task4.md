# Task 4 修复版本 - 数据库持久化方法

这是Task 4的修复版本，使用正确的数据库schema（subagent_sessions表）。

## 修复的关键变更

1. **数据库设计**: 使用`subagent_sessions`表（不是扩展sessions表）
2. **插入流程**: sessions → stages → subagent_sessions（三步插入）
3. **SQL正确性**: 匹配003_subagent_tables.sql迁移

---

### Task 4: 数据库持久化方法

**Files:**
- Modify: `crates/agent-workflow/src/subagent/orchestrator.rs`
- Create: `crates/agent-storage/tests/test_subagent_persistence.rs`

- [ ] **Step 1: 写失败的测试 - Stage 持久化**

```rust
// crates/agent-storage/tests/test_subagent_persistence.rs
use agent_storage::Database;
use uuid::Uuid;

#[tokio::test]
async fn test_save_stage() {
    let db = Database::new(":memory:").await.unwrap();
    db.migrate().await.unwrap();

    let parent_session_id = Uuid::new_v4();
    let stage_id = Uuid::new_v4();

    // 创建主会话（在sessions表中）
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at)
         VALUES (?, ?, datetime('now'), datetime('now'))"
    )
    .bind(parent_session_id.to_string())
    .bind("主会话")
    .execute(db.pool())
    .await
    .unwrap();

    // 创建 Stage
    sqlx::query(
        "INSERT INTO stages (id, parent_session_id, name, status, created_at, total_tasks)
         VALUES (?, ?, ?, 'Running', datetime('now'), 3)"
    )
    .bind(stage_id.to_string())
    .bind(parent_session_id.to_string())
    .bind("Stage 1")
    .execute(db.pool())
    .await
    .unwrap();

    // 查询验证
    let result: (String, i32) = sqlx::query_as(
        "SELECT name, total_tasks FROM stages WHERE id = ?"
    )
    .bind(stage_id.to_string())
    .fetch_one(db.pool())
    .await
    .unwrap();

    assert_eq!(result.0, "Stage 1");
    assert_eq!(result.1, 3);
}

#[tokio::test]
async fn test_save_subagent_session() {
    let db = Database::new(":memory:").await.unwrap();
    db.migrate().await.unwrap();

    let main_session_id = Uuid::new_v4();
    let stage_id = Uuid::new_v4();
    let subagent_id = Uuid::new_v4();

    // 1. 创建主会话
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at)
         VALUES (?, ?, datetime('now'), datetime('now'))"
    )
    .bind(main_session_id.to_string())
    .bind("主会话")
    .execute(db.pool())
    .await
    .unwrap();

    // 2. 创建 Stage
    sqlx::query(
        "INSERT INTO stages (id, parent_session_id, name, status, created_at, total_tasks)
         VALUES (?, ?, ?, 'Running', datetime('now'), 3)"
    )
    .bind(stage_id.to_string())
    .bind(main_session_id.to_string())
    .bind("Stage 1")
    .execute(db.pool())
    .await
    .unwrap();

    // 3. 创建子代理会话记录（在sessions表中）
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at)
         VALUES (?, ?, datetime('now'), datetime('now'))"
    )
    .bind(subagent_id.to_string())
    .bind("Subagent Task 1")
    .execute(db.pool())
    .await
    .unwrap();

    // 4. 创建子代理关联（在subagent_sessions表中）
    sqlx::query(
        "INSERT INTO subagent_sessions (
            session_id, parent_id, session_type, status, stage_id, created_at, updated_at
         ) VALUES (?, ?, 'Subagent', 'Idle', ?, datetime('now'), datetime('now'))"
    )
    .bind(subagent_id.to_string())
    .bind(main_session_id.to_string())
    .bind(stage_id.to_string())
    .execute(db.pool())
    .await
    .unwrap();

    // 5. 查询验证
    let result: (String, String, String) = sqlx::query_as(
        "SELECT session_type, status, stage_id FROM subagent_sessions WHERE session_id = ?"
    )
    .bind(subagent_id.to_string())
    .fetch_one(db.pool())
    .await
    .unwrap();

    assert_eq!(result.0, "Subagent");
    assert_eq!(result.1, "Idle");
    assert_eq!(result.2, stage_id.to_string());
}
```

- [ ] **Step 2: 运行测试验证通过（数据库已迁移）**

```bash
cargo test --test test_subagent_persistence
```

预期：✅ 通过（数据库 schema 已存在）

- [ ] **Step 3: 在 Orchestrator 中添加持久化方法**

```rust
// 在 crates/agent-workflow/src/subagent/orchestrator.rs 中添加

impl SubagentOrchestrator {
    // ... 现有方法 ...

    /// 保存 Stage 到数据库
    async fn save_stage(
        &self,
        stage_id: &str,
        parent_session_id: Uuid,
        stage_name: &str,
        total_tasks: usize,
    ) -> SubagentResult<()> {
        let pool = self.pool.as_ref()
            .ok_or_else(|| SubagentError::ConfigError("Database pool not set".to_string()))?;

        sqlx::query(
            "INSERT INTO stages (id, parent_session_id, name, status, created_at, total_tasks)
             VALUES (?, ?, ?, 'Running', datetime('now'), ?)"
        )
        .bind(stage_id)
        .bind(parent_session_id.to_string())
        .bind(stage_name)
        .bind(total_tasks as i64)
        .execute(pool)
        .await
        .map_err(|e| SubagentError::DatabaseError(e.to_string()))?;

        Ok(())
    }

    /// 创建子代理会话（同时在sessions和subagent_sessions表中插入）
    async fn create_subagent_session(
        &self,
        session_id: Uuid,
        parent_id: Uuid,
        stage_id: &str,
        title: &str,
    ) -> SubagentResult<()> {
        let pool = self.pool.as_ref()
            .ok_or_else(|| SubagentError::ConfigError("Database pool not set".to_string()))?;

        // 1. 在sessions表中创建基础会话记录
        sqlx::query(
            "INSERT INTO sessions (id, title, created_at, updated_at)
             VALUES (?, ?, datetime('now'), datetime('now'))"
        )
        .bind(session_id.to_string())
        .bind(title)
        .execute(pool)
        .await
        .map_err(|e| SubagentError::DatabaseError(e.to_string()))?;

        // 2. 在subagent_sessions表中创建关联记录
        sqlx::query(
            "INSERT INTO subagent_sessions (
                session_id, parent_id, session_type, status, stage_id, created_at, updated_at
             ) VALUES (?, ?, 'Subagent', 'Idle', ?, datetime('now'), datetime('now'))"
        )
        .bind(session_id.to_string())
        .bind(parent_id.to_string())
        .bind(stage_id)
        .execute(pool)
        .await
        .map_err(|e| SubagentError::DatabaseError(e.to_string()))?;

        Ok(())
    }

    /// 更新子代理会话状态
    pub async fn update_subagent_status(
        &self,
        session_id: Uuid,
        status: &str,
    ) -> SubagentResult<()> {
        let pool = self.pool.as_ref()
            .ok_or_else(|| SubagentError::ConfigError("Database pool not set".to_string()))?;

        sqlx::query(
            "UPDATE subagent_sessions SET status = ?, updated_at = datetime('now')
             WHERE session_id = ?"
        )
        .bind(status)
        .bind(session_id.to_string())
        .execute(pool)
        .await
        .map_err(|e| SubagentError::DatabaseError(e.to_string()))?;

        Ok(())
    }
}
```

- [ ] **Step 4: 修改 create_and_execute_stage 添加数据库持久化**

```rust
// 在 crates/agent-workflow/src/subagent/orchestrator.rs 中修改

impl SubagentOrchestrator {
    /// 创建并执行单个 Stage（含数据库持久化）
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

        // 2. 创建 Stage
        let stage_id = Uuid::new_v4();
        let stage_id_str = stage_id.to_string();
        let stage_name = format!("Stage - {}", Utc::now().format("%H:%M:%S"));

        // 3. 保存 Stage 到数据库（如果pool已设置）
        if self.pool.is_some() {
            self.save_stage(&stage_id_str, parent_session_id, &stage_name, tasks.len()).await?;
        }

        // 4. 为每个任务创建 subagent session
        let default_config = config.unwrap_or_default();
        let result_tx = self.result_tx.clone();

        for (idx, task) in tasks.iter().enumerate() {
            let session_id = Uuid::new_v4();
            let title = format!("Task {}: {}", idx + 1, task);

            // 4.1 创建子代理会话（数据库）
            if self.pool.is_some() {
                self.create_subagent_session(
                    session_id,
                    parent_session_id,
                    &stage_id_str,
                    &title,
                ).await?;
            }

            // 4.2 创建内存状态
            let state = SubagentState::new(
                session_id,
                parent_session_id,
                stage_id_str.clone(),
            )?;
            self.state_map.insert(session_id, state);

            // 4.3 构造TaskConfig
            let task_config = SubagentTaskConfig {
                id: session_id,
                config: SubagentConfig {
                    title: title.clone(),
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

            // 4.4 创建并启动任务
            let progress_estimator = ProgressEstimator::new(TaskType::Custom);
            let subagent_task = SubagentTask::new(
                session_id,
                task_config,
                self.state_map.clone(),
                progress_estimator,
            );

            let task_result_tx = result_tx.clone();
            tokio::spawn(async move {
                if let Err(e) = subagent_task.run(task_result_tx).await {
                    eprintln!("SubagentTask {} 失败: {}", session_id, e);
                }
            });
        }

        Ok(stage_id)
    }
}
```

- [ ] **Step 5: 运行测试验证通过**

```bash
# 单元测试
cargo test --test test_subagent_persistence

# 集成测试
cargo test --test integration_subagent_execution
```

预期：✅ 所有测试通过

- [ ] **Step 6: 提交**

```bash
git add crates/agent-workflow/src/subagent/orchestrator.rs \
        crates/agent-storage/tests/test_subagent_persistence.rs
git commit -m "feat(orchestrator): add database persistence for stages and subagents

- Add save_stage() method to persist stages table
- Add create_subagent_session() for dual-table insertion:
  * sessions table (basic session record)
  * subagent_sessions table (subagent metadata)
- Add update_subagent_status() for status tracking
- Integrate persistence into create_and_execute_stage()
- Use correct schema: stages + subagent_sessions tables
- Graceful fallback when pool not set (memory-only mode)

Tests verify stage and subagent session persistence.

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## 数据库架构说明

### Tables关系

```
sessions (基础会话表)
    ├── stages (Stage元数据)
    │   └── subagent_sessions (子代理关联)
    └── subagent_sessions (外键 session_id)
```

### 插入顺序

```
1. INSERT INTO sessions (主会话 - 如果不存在)
2. INSERT INTO stages (Stage记录)
3. FOR EACH subagent:
   a. INSERT INTO sessions (子代理基础记录)
   b. INSERT INTO subagent_sessions (关联记录)
```

### Schema验证

```sql
-- stages表
CREATE TABLE stages (
    id TEXT PRIMARY KEY,
    parent_session_id TEXT NOT NULL,
    name TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'Running',
    created_at TEXT NOT NULL,
    completed_at TEXT,
    total_tasks INTEGER NOT NULL DEFAULT 0,
    completed_tasks INTEGER NOT NULL DEFAULT 0
);

-- subagent_sessions表
CREATE TABLE subagent_sessions (
    session_id TEXT PRIMARY KEY,
    parent_id TEXT NOT NULL,
    session_type TEXT NOT NULL DEFAULT 'Subagent',
    status TEXT NOT NULL DEFAULT 'Idle',
    stage_id TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY (session_id) REFERENCES sessions(id),
    FOREIGN KEY (parent_id) REFERENCES sessions(id),
    FOREIGN KEY (stage_id) REFERENCES stages(id)
);
```

### 关键点

1. **双表插入**: 每个subagent需要在`sessions`和`subagent_sessions`都插入
2. **外键约束**: 必须先创建主会话和Stage，再创建子代理
3. **status值**: 使用'Idle', 'Running', 'Completed', 'Failed', 'Cancelled'
4. **session_type**: 固定为'Subagent'
5. **stage_id**: String类型（不是Uuid）
