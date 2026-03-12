# Subagent Integration Plan - 问题清单

**日期**: 2026-03-12
**审查者**: Claude Sonnet 4.5
**计划文件**: `docs/superpowers/plans/2026-03-12-subagent-integration-plan.md`

---

## 执行摘要

计划审查发现 **3个CRITICAL问题**、**5个HIGH问题**和**多个MEDIUM问题**。主要问题集中在 **Chunk 1 (Tasks 1-3)**，这些问题会导致编译失败。

**建议**: 需要大幅修改 Chunk 1 的实现代码以匹配实际代码库API。

---

## 问题分类

### ❌ CRITICAL - 会导致编译失败

#### 1. [Task 3, Step 3] SubagentState 构造方式错误

**位置**: Lines 560-577（修正后的代码）

**问题**: 计划中使用结构体字面量初始化，但字段名和类型与实际API不匹配。

**计划中的代码**:
```rust
self.state_map.insert(session_id, SubagentState {
    session_id,
    parent_id: parent_session_id,
    stage_id,
    session_type: SessionType::Subagent,  // ❌ 字段不存在
    status: SessionStatus::Pending,        // ❌ Pending变体不存在(应为Idle)
    progress: 0.0,
    task_description: Some(task.clone()), // ❌ 字段不存在
    created_at: Utc::now(),                // ❌ 字段名错误(应为started_at)
    updated_at: Utc::now(),
    completed_at: None,                     // ❌ 字段不存在
    error_message: None,                    // ❌ 字段名错误(应为error)
});
```

**实际API**:
```rust
// SubagentState字段(来自state.rs):
pub struct SubagentState {
    session_id: Uuid,
    parent_id: Uuid,
    stage_id: String,        // ← String类型，不是Uuid
    status: SessionStatus,    // ← 无session_type字段
    progress: f32,
    started_at: DateTime<Utc>,  // ← 不是created_at
    updated_at: DateTime<Utc>,
    estimated_remaining: Option<Duration>,
    message_count: usize,
    error: Option<String>,    // ← 不是error_message，也无completed_at、task_description
}

// 正确的构造方式:
impl SubagentState {
    pub fn new(session_id: Uuid, parent_id: Uuid, stage_id: String) -> SubagentResult<Self>
}
```

**正确代码**:
```rust
let state = SubagentState::new(
    session_id,
    parent_session_id,
    stage_id.to_string()  // Uuid转String
)?;
self.state_map.insert(session_id, state);
```

**影响**: 编译失败 - 所有字段都会报错

---

#### 2. [Task 3, Step 3] SubagentTask::new() 参数不匹配

**位置**: Lines 578-582

**问题**: 参数类型、顺序和数量都不正确。

**计划中的代码**:
```rust
let task = SubagentTask::new(
    session_id,
    self.state_map.clone(),
    llm_client.clone(),              // ❌ 不需要LLM client参数
    config.clone().unwrap_or_default(), // ❌ 类型错误
);
```

**实际API** (来自task.rs):
```rust
pub fn new(
    session_id: Uuid,
    config: SubagentTaskConfig,      // ← 不是SubagentConfig
    state: Arc<DashMap<Uuid, SubagentState>>,
    progress_estimator: ProgressEstimator,  // ← 缺少此参数
) -> Self
```

**正确代码**:
```rust
// 需要构造SubagentTaskConfig和ProgressEstimator
let task_config = SubagentTaskConfig {
    id: session_id,
    config: config.clone().unwrap_or_default(),
    parent_id: parent_session_id,
    stage_id: stage_id.to_string(),
    priority: 0,
    task_type: TaskType::Custom,  // 或根据任务描述推断
};

let progress_estimator = ProgressEstimator::new(TaskType::Custom);

let task = SubagentTask::new(
    session_id,
    task_config,
    self.state_map.clone(),
    progress_estimator,
);
```

**影响**: 编译失败 - 参数类型和数量不匹配

---

#### 3. [Task 3, Step 3] SubagentTask::run() 缺少参数

**位置**: Lines 584-588

**问题**: `run()` 方法需要 `result_tx` 参数。

**计划中的代码**:
```rust
tokio::spawn(async move {
    if let Err(e) = task.run().await {  // ❌ 缺少result_tx参数
        eprintln!("SubagentTask {} 失败: {}", session_id, e);
    }
});
```

**实际API** (来自task.rs):
```rust
pub async fn run(
    self,
    result_tx: mpsc::Sender<TaskResult>,  // ← 需要此参数
) -> SubagentResult<()>
```

**正确代码**:
```rust
let result_tx = self.result_tx.clone();
tokio::spawn(async move {
    if let Err(e) = task.run(result_tx).await {
        eprintln!("SubagentTask {} 失败: {}", session_id, e);
    }
});
```

**影响**: 编译失败 - 缺少必需参数

---

### 🟡 HIGH - 架构/设计问题

#### 4. [Task 1] AgentRuntime 不存在于代码库

**位置**: Task 1 整个任务

**问题**: `AgentRuntime` 是计划中新创建的结构体，代码库中不存在。

**搜索结果**:
```bash
$ rg "pub struct AgentRuntime" v2/crates
# 无结果
```

**分析**:
- Task 1 需要创建全新的 `crates/agent-core/src/runtime.rs` 文件
- 这是正确的（计划本意就是创建新组件）
- 但需要确认 `agent-core` crate 是否存在

**建议**:
- 检查 `crates/agent-core/` 目录是否存在
- 如果不存在，需要先创建 crate
- 考虑是否应该放在 `agent-workflow` 中而不是单独的 crate

---

#### 5. [Task 2] CommandParser 不存在于代码库

**位置**: Task 2 整个任务

**问题**: `CommandParser` 是计划中新创建的模块。

**搜索结果**:
```bash
$ rg "CommandParser|parse_subagent_command" v2/crates
# 无结果
```

**分析**: 同样是新创建的组件，这是预期的。

---

#### 6. [Task 3] Orchestrator 依赖注入设计缺陷

**位置**: Task 1 Step 3, Task 3 Step 3

**问题**: 计划通过setter方法注入依赖，但Orchestrator结构体设计上可能不支持。

**计划设计**:
```rust
// Task 1: 注入依赖
let mut orchestrator = SubagentOrchestrator::new(...);
orchestrator.set_database_pool(db.pool().clone());
orchestrator.set_llm_client(llm_client.clone());
```

**实际Orchestrator结构** (来自orchestrator.rs):
```rust
pub struct SubagentOrchestrator {
    config: OrchestratorConfig,
    state_map: Arc<DashMap<Uuid, SubagentState>>,
    active_count: Arc<AtomicUsize>,
    command_tx: mpsc::Sender<SubagentCommand>,
    command_rx: Option<mpsc::Receiver<SubagentCommand>>,
    result_tx: mpsc::Sender<TaskResult>,
    result_rx: Option<mpsc::Receiver<TaskResult>>,
    shutdown_tx: broadcast::Sender<()>,
    // ← 无pool或llm_client字段
}
```

**问题**:
- 现有结构体没有 `pool` 和 `llm_client` 字段
- 需要扩展结构体定义（计划中已经包含，但在Task 3中）
- Task 1使用这些setter时，它们还不存在

**影响**: 设计上的循环依赖 - Task 1需要Task 3的结构体修改

---

#### 7. [Task 3] stage_id 类型不一致

**位置**: Task 3 Step 3, Lines 515-516

**问题**: 代码生成 `Uuid` 但 `SubagentState` 需要 `String`。

**计划中的代码**:
```rust
let stage_id = Uuid::new_v4();  // ← Uuid类型
// ...
stage_id,  // ← 直接传递给SubagentState
```

**实际API**:
```rust
// SubagentState.stage_id 是 String
stage_id: String
```

**修复**:
```rust
let stage_id = Uuid::new_v4();
let stage_id_str = stage_id.to_string();
// 使用stage_id_str
```

**影响**: 类型不匹配，编译失败

---

#### 8. [Task 4] 数据库 Schema 设计不匹配

**位置**: Task 4 整个任务

**问题**: 计划中修改 `sessions` 表，但实际迁移使用独立的 `subagent_sessions` 表。

**计划设计** (Task 4):
```sql
-- 计划: 扩展sessions表
ALTER TABLE sessions ADD COLUMN parent_id TEXT;
ALTER TABLE sessions ADD COLUMN session_type TEXT;
ALTER TABLE sessions ADD COLUMN stage_id TEXT;
```

**实际Schema** (003_subagent_tables.sql):
```sql
-- 实际: 独立的subagent_sessions表
CREATE TABLE IF NOT EXISTS subagent_sessions (
    session_id TEXT PRIMARY KEY,
    parent_id TEXT NOT NULL,
    session_type TEXT NOT NULL DEFAULT 'Subagent',
    status TEXT NOT NULL DEFAULT 'Idle',
    stage_id TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY (session_id) REFERENCES sessions(id) ON DELETE CASCADE,
    FOREIGN KEY (parent_id) REFERENCES sessions(id) ON DELETE CASCADE,
    FOREIGN KEY (stage_id) REFERENCES stages(id) ON DELETE SET NULL
);
```

**分析**:
- 迁移已经存在并使用不同的设计
- `subagent_sessions` 是一对一关联表，不是扩展 `sessions`
- 这意味着需要：
  1. 同时插入 `sessions` (基础会话)
  2. 插入 `subagent_sessions` (子代理元数据)

**影响**: Task 4的SQL代码需要重写

---

### 🟠 MEDIUM - 需要澄清

#### 9. [Task 3, Step 3] 占位符注释不清晰

**位置**: Line 553 及其他多处

**问题**: `// ... 现有方法保持不变 ...` 等占位符不够明确。

**建议**:
- 改为 `// ... 现有getters/setters保持不变，见lines 64-144 ...`
- 或明确列出保留哪些方法

---

#### 10. [Task 4, Step 5] SubagentTask spawning 代码缺失

**位置**: Task 4 Step 5, Line 968

**问题**: 关键的任务启动代码被省略为 `// ... 现有代码 ...`。

**问题**:
```rust
// 4. 启动所有 SubagentTask（并发）
// ... 现有代码 ...  // ← 这是关键逻辑，不应省略
```

**建议**: 至少提供注释说明 "见Task 3, Step 3, lines 578-588"

---

#### 11. [Task 5, Step 2] Runtime setup 逻辑不清楚

**位置**: Task 5 Step 2, Lines 1072+

**问题**: 注释说 `// (需要在 AgentRuntime 中添加方法)` 像TODO。

**建议**: 明确这是在Task 1中已经实现的逻辑，提供引用

---

#### 12. [Task 6, Step 3] TODO注释在render方法中

**位置**: Task 6 Step 3, Line 1330

**代码**:
```rust
pub fn render(&self, _f: &mut Frame, _area: Rect) {
    // TODO: 下一个任务实现
}
```

**分析**: 这是TDD方法论的一部分 - 先创建空实现，下一个任务填充。可以接受，但应该注释说明这是有意为之。

---

#### 13. [Chunk 2] 多处"现有代码"占位符

**位置**: Task 4 Step 3, Task 5 Step 4

**问题**: 使用 `// ... 现有代码 ...` 但没有清晰的引用。

**示例**:
```rust
// ... 现有代码 ...  // ← 哪段代码？从哪里复制？
```

**建议**: 改为 `// (保持原有的normal dialog处理逻辑不变)`

---

## 数据库设计差异总结

### 计划假设
- 扩展 `sessions` 表添加 `parent_id`, `session_type`, `stage_id`

### 实际实现
- **stages 表**: 独立表存储Stage信息
- **subagent_sessions 表**: 一对一关联表存储子代理元数据
- **sessions 表**: 保持不变，所有会话(main和subagent)都在这里

### 插入流程应该是
```sql
-- 1. 创建Stage
INSERT INTO stages (id, parent_session_id, name, status, ...) VALUES (...);

-- 2. 创建主会话记录
INSERT INTO sessions (id, title, created_at, updated_at) VALUES (...);

-- 3. 创建子代理关联
INSERT INTO subagent_sessions (
    session_id, parent_id, session_type, status, stage_id, ...
) VALUES (...);
```

---

## Chunk 审查结果

| Chunk | 状态 | 严重问题数 | 说明 |
|-------|------|-----------|------|
| **Chunk 1 (Tasks 1-3)** | ❌ **Blocked** | 3 CRITICAL<br>3 HIGH | 需要大幅修改代码 |
| **Chunk 2 (Tasks 4-5)** | ⚠️ **Needs Work** | 1 HIGH<br>3 MEDIUM | 数据库代码需要重写 |
| **Chunk 3 (Tasks 6-9)** | ✅ **Approved** | 0 | 可以直接实施 |
| **Chunk 4 (Tasks 10-12)** | ✅ **Approved** | 0 | 可以直接实施 |

---

## 修复建议

### 选项 A: 大规模修复（推荐）

**范围**: 修改 Chunk 1 和 Chunk 2 所有代码匹配实际API

**工作量估算**:
- Task 1 (AgentRuntime): 需要新建结构和crate，约100行
- Task 2 (CommandParser): 新建模块，约200行
- Task 3 (Orchestrator): 重写整个实现，约150行
- Task 4 (Database): 重写SQL和持久化代码，约100行
- Task 5 (CLI): 调整集成代码，约50行

**总计**: 约600行代码需要重新编写

**优点**:
- 一次性解决所有问题
- 代码与现有API完全匹配
- 可以直接执行

**缺点**:
- 工作量较大
- 需要理解所有现有API

---

### 选项 B: 最小化实现（快速原型）

**范围**: 暂时跳过复杂的依赖注入，实现简化版本

**简化策略**:
1. **Task 3**: 不使用SubagentTask，仅实现slot预留和状态追踪
2. **Task 4**: 暂时只持久化Stage，不持久化subagent sessions
3. **Task 5**: 命令解析返回mock数据
4. **Chunks 3-4**: 完整实施（TUI和文档）

**优点**:
- 快速验证TUI组件
- 减少对现有代码的依赖
- 可以后续增量添加功能

**缺点**:
- 不是完整的功能
- 可能需要后续大规模重构

---

### 选项 C: 分阶段实施

**Phase 1a**: 仅实施Chunks 3-4（TUI + 文档），使用mock数据
**Phase 1b**: 实施基础设施（Tasks 1-2）
**Phase 1c**: 集成执行逻辑（Tasks 3-5）

**优点**:
- 可以先验证UI设计
- 逐步实现，降低风险
- 每个阶段都有可演示的成果

**缺点**:
- 总时间可能更长
- 需要多次集成

---

## 建议的执行路径

**推荐: 选项 A（大规模修复）**

理由：
1. Chunks 3-4 已经完美，价值在于有可工作的TUI
2. 如果不修复Chunk 1-2，TUI无法获取真实数据
3. 一次性修复避免后续技术债务
4. 当前问题清单已经非常明确，修复路径清晰

**执行步骤**:
1. ✅ 审查完成（当前文档）
2. 修复 Task 1: 创建AgentRuntime（考虑放在agent-workflow而非新crate）
3. 修复 Task 2: 创建CommandParser
4. 修复 Task 3: 重写Orchestrator扩展使用正确的API
5. 修复 Task 4: 重写数据库代码使用subagent_sessions表
6. 修复 Task 5: 调整CLI集成
7. 验证 Chunks 3-4（应该无需修改）
8. 执行计划

---

## 附录：API参考速查

### SubagentState API
```rust
// Constructor
pub fn new(session_id: Uuid, parent_id: Uuid, stage_id: String) -> SubagentResult<Self>

// 字段（私有）
session_id: Uuid
parent_id: Uuid
stage_id: String
status: SessionStatus  // Idle, Running, Completed, Failed, Cancelled
progress: f32
started_at: DateTime<Utc>
updated_at: DateTime<Utc>
estimated_remaining: Option<Duration>
message_count: usize
error: Option<String>
```

### SubagentTask API
```rust
// Constructor
pub fn new(
    session_id: Uuid,
    config: SubagentTaskConfig,
    state: Arc<DashMap<Uuid, SubagentState>>,
    progress_estimator: ProgressEstimator,
) -> Self

// Run method
pub async fn run(
    self,
    result_tx: mpsc::Sender<TaskResult>,
) -> SubagentResult<()>
```

### SubagentTaskConfig
```rust
pub struct SubagentTaskConfig {
    pub id: Uuid,
    pub config: SubagentConfig,
    pub parent_id: Uuid,
    pub stage_id: String,
    pub priority: u8,
    pub task_type: TaskType,
}
```

### TaskType
```rust
pub enum TaskType {
    CodeReview,
    Research,
    Analysis,
    Documentation,
    Testing,
    Custom,
}
```

### SessionStatus
```rust
pub enum SessionStatus {
    Idle,      // ← 默认值
    Running,
    Completed,
    Failed,
    Cancelled,
}
```

### Database Schema
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

---

## 总结

计划质量：**Chunks 3-4优秀**，**Chunks 1-2需要大规模修改**

核心问题：实现代码与现有API不匹配，会导致编译失败

推荐行动：执行**选项A（大规模修复）**，一次性解决所有API不匹配问题

预计修复时间：2-3小时（假设熟悉代码库）

修复后可以直接使用superpowers:subagent-driven-development执行。
