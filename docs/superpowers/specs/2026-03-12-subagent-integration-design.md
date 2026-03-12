# Subagent 系统集成设计文档（Phase 1+2）

**项目：** General Agent V2
**功能：** Subagent 系统应用层集成和 TUI 可视化
**日期：** 2026-03-12
**版本：** 1.0
**阶段：** Phase 1+2（渐进式实现）

---

## 1. 概述

### 1.1 背景

Subagent 系统的核心功能（Tasks 1-10）已在 `feature/subagent-system` 分支完成并合并到 main：
- ✅ SubagentOrchestrator（编排器）
- ✅ SubagentTask（执行单元）
- ✅ 数据库 schema（sessions 扩展 + stages 表）
- ✅ DashMap 共享状态管理
- ✅ 进度估算算法

本设计文档描述如何将 Subagent 系统集成到应用层（CLI/TUI），实现用户可用的完整功能。

### 1.2 目标

**Phase 1+2 范围（本文档）：**
- ✅ AgentRuntime 架构（统一资源管理）
- ✅ 直接命令：`/subagent start "任务1" "任务2" ...`
- ✅ 单 Stage 多 subagent 并发执行
- ✅ SessionCardWidget 弹出层（F2 切换，Tab 视图切换）
- ✅ 基础交互（展开/折叠/查看详情）
- ✅ 状态轮询更新（500ms）

**Phase 3 范围（未来）：**
- ⏳ 多 Stage 编排（预定义 + 动态）
- ⏳ LLM 工具调用（propose_subagent）
- ⏳ 控制操作（取消/暂停/重试）

### 1.3 设计原则

1. **最小侵入** - 不修改现有稳定代码（ConversationFlow、SessionManager）
2. **渐进式实现** - 先验证单 Stage 核心功能，Phase 3 再扩展
3. **关注点分离** - AgentRuntime 统一管理资源，各层职责清晰
4. **用户体验优先** - 直观的命令、流畅的 TUI、清晰的状态显示

---

## 2. 系统架构

### 2.1 整体分层架构

```
┌──────────────────────────────────────────────────────┐
│                   应用层 (App)                        │
│  CLI: agent-cli/src/main.rs                          │
│  TUI: agent-tui/src/app.rs                           │
│       └─ SubagentOverlay (F2 弹出层)                 │
├──────────────────────────────────────────────────────┤
│               核心运行时 (AgentRuntime)               │
│  agent-core/src/runtime.rs                           │
│  ├─ Database                                         │
│  ├─ SessionManager                                   │
│  ├─ SubagentOrchestrator  ← 新增                    │
│  ├─ LLMClient                                        │
│  └─ SkillRegistry                                    │
├──────────────────────────────────────────────────────┤
│              工作流层 (agent-workflow)                │
│  ├─ ConversationFlow (不修改)                        │
│  ├─ SubagentOrchestrator                             │
│  └─ CommandParser ← 新增                             │
├──────────────────────────────────────────────────────┤
│               存储层 (agent-storage)                  │
│  Database (SQLite)                                   │
│  ├─ sessions 表 (扩展)                               │
│  └─ stages 表 (新增)                                 │
└──────────────────────────────────────────────────────┘
```

### 2.2 数据流向

**启动 Subagent 流程：**
```
用户输入 "/subagent start 任务1 任务2"
    ↓
App 层拦截命令
    ↓
CommandParser.parse() → SubagentCommand
    ↓
runtime.orchestrator.create_and_execute_stage()
    ├─ 写入 stages 表
    ├─ 创建 N 个 subagent sessions
    ├─ 写入 sessions 表
    └─ 启动 N 个 tokio::spawn(SubagentTask)
    ↓
SubagentTask 更新 DashMap 状态
    ↓
TUI SubagentOverlay 每 500ms 轮询 DashMap
    ↓
渲染进度卡片
```

### 2.3 关键设计决策

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 资源管理 | AgentRuntime 统一管理 | 扩展性好，避免 App 层职责过重 |
| 触发机制 | 直接命令（Phase 1+2）| 降低复杂度，Phase 3 加 LLM 工具 |
| Stage 编排 | 单 Stage（Phase 1+2）| 验证核心功能，降低初期风险 |
| TUI 布局 | 弹出式覆盖层（F2）| 不占用常规界面，按需显示 |
| 视图模式 | 当前会话 + 全局（Tab）| 混合模型支持会话级和全局级查看 |
| 交互功能 | 基础（展开/详情）| Phase 1+2 专注监控，Phase 3 加控制 |
| 状态同步 | DashMap + 500ms 轮询 | 简单可靠，性能可接受 |

---

## 3. 核心组件设计

### 3.1 AgentRuntime（统一运行时）

**位置：** `crates/agent-core/src/runtime.rs`

**职责：**
- 持有所有共享资源（Database、SessionManager、Orchestrator、LLM、Skills）
- 提供统一的依赖注入入口
- 简化 App 层初始化

**接口：**
```rust
pub struct AgentRuntime {
    db: Arc<Database>,
    session_manager: Arc<SessionManager>,
    orchestrator: Arc<SubagentOrchestrator>,
    llm_client: Arc<dyn LLMClient>,
    skill_registry: Option<Arc<SkillRegistry>>,
}

impl AgentRuntime {
    pub async fn new(
        db: Database,
        llm_client: Arc<dyn LLMClient>,
        skill_registry: Option<Arc<SkillRegistry>>,
    ) -> Result<Self>;

    pub fn session_manager(&self) -> &Arc<SessionManager>;
    pub fn orchestrator(&self) -> &Arc<SubagentOrchestrator>;
    pub fn llm_client(&self) -> &Arc<dyn LLMClient>;
    pub fn skill_registry(&self) -> Option<&Arc<SkillRegistry>>;
}
```

**优势：**
- App 层只需持有 `Arc<AgentRuntime>`
- 后续扩展（RAG、MCP）统一在此管理
- 测试友好（可注入 mock）

---

### 3.2 CommandParser（命令解析器）

**位置：** `crates/agent-workflow/src/command_parser.rs`

**职责：**
- 解析 `/subagent` 命令字符串
- 提取任务列表和选项参数
- 返回结构化的 SubagentCommand

**语法：**
```
/subagent start [选项] <任务1> [任务2] [任务3] ...

选项：
  --timeout <秒>        超时时间（默认 300）
  --model <模型名>      指定模型（默认从配置）

示例：
  /subagent start "总结这个文件"
  /subagent start "分析 auth.rs" "分析 db.rs" "分析 api.rs"
  /subagent start --timeout 600 "长时间任务"
```

**接口：**
```rust
pub enum SubagentCommand {
    Start {
        tasks: Vec<String>,
        timeout_secs: Option<u64>,
        model: Option<String>,
    },
}

pub fn parse_subagent_command(input: &str) -> Result<SubagentCommand>;
```

**解析逻辑：**
1. 验证前缀 `/subagent`
2. 检查子命令 `start`
3. 遍历参数：
   - `--timeout N` → 解析超时
   - `--model M` → 解析模型
   - 其他 → 作为任务描述
4. 移除引号，返回结构化命令

---

### 3.3 SubagentOrchestrator 扩展

**位置：** `crates/agent-workflow/src/subagent/orchestrator.rs`

**新增方法（Phase 1+2）：**

```rust
impl SubagentOrchestrator {
    /// 创建并执行单个 Stage（简化版）
    pub async fn create_and_execute_stage(
        &self,
        parent_session_id: Uuid,
        tasks: Vec<String>,
        config: Option<SubagentConfig>,
    ) -> SubagentResult<Uuid> {
        // 1. 预留槽位
        self.try_reserve_slots(tasks.len())?;

        // 2. 创建 Stage（保存到数据库）
        let stage_id = self.save_stage(...).await?;

        // 3. 为每个任务创建 subagent session
        for task in tasks {
            let session_id = self.create_subagent_session(...).await?;
            self.state_map.insert(session_id, SubagentState { ... });
        }

        // 4. 启动所有 SubagentTask（并发）
        for session_id in subagent_ids {
            tokio::spawn(SubagentTask::new(...).run());
        }

        Ok(stage_id)
    }

    /// 获取指定会话的所有子代理状态
    pub fn get_subagent_states(&self, parent_session_id: Uuid) -> Vec<SubagentState>;

    /// 获取所有子代理状态
    pub fn get_all_states(&self) -> Vec<SubagentState>;

    /// 获取 Stage 统计信息
    pub fn get_stage_stats(&self, stage_id: Uuid) -> StageStats;
}
```

**要点：**
- 单 Stage 简化版（不支持多 Stage 编排）
- 所有 subagent 并发执行
- 状态通过 DashMap 共享
- Phase 3 扩展多 Stage 支持

---

### 3.4 SubagentOverlay（TUI 覆盖层）

**位置：** `crates/agent-tui/src/ui/subagent_overlay.rs`

**职责：**
- 弹出式覆盖层（F2 切换显示/隐藏）
- 支持当前会话/全局视图切换（Tab）
- 实时显示 subagent 状态和进度
- 基础交互（导航、展开、查看详情）

**状态管理：**
```rust
pub struct SubagentOverlay {
    visible: bool,                           // 是否可见
    view_mode: ViewMode,                     // 当前会话 / 全局
    current_session_id: Option<Uuid>,        // 当前会话 ID
    selected_index: usize,                   // 选中索引
    show_details: bool,                      // 是否显示详情
    orchestrator: Arc<SubagentOrchestrator>, // Orchestrator 引用
}

pub enum ViewMode {
    CurrentSession,  // 只显示当前会话的 subagent
    Global,          // 显示所有会话的 subagent
}
```

**渲染结构：**
```
┌─────────────────────────────────────────────┐
│ Subagent 监控 [当前会话] (F2:关闭 Tab:切换) │
├─────────────────────────────────────────────┤
│ ┌───────────────────────────────────────┐   │
│ │ ▼ Stage abc123 | 2/3 完成 | 1 运行中 │   │
│ ├───────────────────────────────────────┤   │
│ │ ┌─────────┐ ┌─────────┐ ┌─────────┐ │   │
│ │ │⏳ 任务1  │ │✅ 任务2  │ │❌ 任务3  │ │   │
│ │ │进度: 45%│ │已完成   │ │失败     │ │   │
│ │ └─────────┘ └─────────┘ └─────────┘ │   │
│ └───────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
```

**关键方法：**
- `toggle_visible()` - 切换显示/隐藏
- `toggle_view_mode()` - 切换视图
- `render()` - 主渲染入口
- `render_stage_cards()` - 渲染 Stage 卡片
- `render_subagent_grid()` - 网格布局子代理

**状态图标：**
- ⏸ Pending（等待）
- ⏳ Running（运行中）
- ✅ Completed（完成）
- ❌ Failed（失败）

---

## 4. 数据库设计

### 4.1 sessions 表扩展

```sql
-- 新增字段
ALTER TABLE sessions ADD COLUMN parent_id TEXT
    REFERENCES sessions(id) ON DELETE CASCADE;

ALTER TABLE sessions ADD COLUMN session_type TEXT
    NOT NULL DEFAULT 'main'
    CHECK(session_type IN ('main', 'subagent'));

ALTER TABLE sessions ADD COLUMN status TEXT
    NOT NULL DEFAULT 'idle'
    CHECK(status IN ('idle', 'pending', 'running', 'completed', 'failed'));

ALTER TABLE sessions ADD COLUMN stage_id TEXT
    REFERENCES stages(id) ON DELETE SET NULL;
```

### 4.2 stages 表（新增）

```sql
CREATE TABLE stages (
    id TEXT PRIMARY KEY,
    parent_session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'pending'
        CHECK(status IN ('pending', 'running', 'completed', 'failed')),
    created_at TEXT NOT NULL,
    completed_at TEXT,
    total_tasks INTEGER NOT NULL DEFAULT 0,
    completed_tasks INTEGER NOT NULL DEFAULT 0,
    failed_tasks INTEGER NOT NULL DEFAULT 0
);
```

### 4.3 索引优化

```sql
-- sessions 表索引
CREATE INDEX idx_sessions_parent_id ON sessions(parent_id);
CREATE INDEX idx_sessions_type ON sessions(session_type);
CREATE INDEX idx_sessions_status ON sessions(status);
CREATE INDEX idx_sessions_stage_id ON sessions(stage_id);

-- stages 表索引
CREATE INDEX idx_stages_parent_session ON stages(parent_session_id);
CREATE INDEX idx_stages_status ON stages(status);
CREATE INDEX idx_stages_created_at ON stages(created_at DESC);
```

### 4.4 级联删除规则

- 删除主会话 → 自动删除所有子会话（CASCADE）
- 删除主会话 → 自动删除所有 Stage（CASCADE）
- 删除 Stage → 子会话的 stage_id 设为 NULL（SET NULL）

---

## 5. CLI 集成

### 5.1 App 结构改造

**修改位置：** `crates/agent-cli/src/main.rs`

**重构前：**
```rust
struct App {
    session_manager: Arc<SessionManager>,
    llm_client: Arc<dyn LLMClient>,
    skill_registry: Option<Arc<SkillRegistry>>,
}
```

**重构后：**
```rust
struct App {
    runtime: Arc<AgentRuntime>,  // 统一运行时
}
```

### 5.2 命令处理流程

```rust
async fn cmd_chat(&self, session_id: Uuid, use_stream: bool) -> Result<()> {
    // 对话循环
    loop {
        let input = read_user_input()?;

        // 检查是否为 subagent 命令
        if input.starts_with("/subagent") {
            self.handle_subagent_command(session_id, &input).await?;
            continue;
        }

        // 正常对话
        let response = conversation_flow.send_message(input).await?;
        println!("{}", response);
    }
}

async fn handle_subagent_command(
    &self,
    parent_session_id: Uuid,
    input: &str,
) -> Result<String> {
    // 1. 解析命令
    let command = parse_subagent_command(input)?;

    // 2. 构建配置
    let mut config = SubagentConfig::default();
    if let Some(timeout) = command.timeout_secs {
        config = config.with_timeout(Duration::from_secs(timeout));
    }

    // 3. 创建并执行 Stage
    let stage_id = self.runtime.orchestrator()
        .create_and_execute_stage(parent_session_id, command.tasks, Some(config))
        .await?;

    // 4. 返回确认消息
    Ok(format!("✓ 已启动 {} 个 subagent (Stage {})",
        command.tasks.len(), stage_id))
}
```

### 5.3 用户体验

**成功示例：**
```
You: /subagent start "分析 auth.rs" "分析 db.rs" "分析 api.rs"
正在启动 subagent...
✓ 已启动 3 个 subagent (Stage abc12345)
提示: 使用 /subagent status 查看进度

AI: 我已经启动了 3 个并发子代理来分析这些文件...
```

**错误示例：**
```
You: /subagent start
错误: 命令解析失败: 至少需要指定一个任务
用法: /subagent start <任务> [任务2] ...
```

---

## 6. TUI 集成

### 6.1 TuiApp 改造

**修改位置：** `crates/agent-tui/src/app.rs`

**主要改动：**
```rust
pub struct TuiApp {
    state: AppState,
    terminal: Terminal<CrosstermBackend<Stdout>>,
    backend_tx: mpsc::UnboundedSender<BackendCommand>,
    backend_rx: mpsc::UnboundedReceiver<BackendUpdate>,
    should_quit: bool,

    // 新增：Subagent 覆盖层
    subagent_overlay: SubagentOverlay,
}

impl TuiApp {
    fn draw(&mut self) -> TuiResult<()> {
        self.terminal.draw(|f| {
            // 渲染常规界面
            ui::render_status_bar(f, ...);
            ui::render_session_list(f, ...);
            ui::render_chat_window(f, ...);
            ui::render_input_box(f, ...);
            ui::render_info_bar(f, ...);

            // 渲染 Subagent 覆盖层（如果可见）
            self.subagent_overlay.render(f, f.size());
        })?;
        Ok(())
    }
}
```

### 6.2 事件处理

**新增事件：**
```rust
pub enum AppEvent {
    // 现有事件...
    ToggleSubagentOverlay,  // F2
    SwitchView,             // Tab（在覆盖层中）
}
```

**事件映射：**
```rust
impl EventHandler {
    pub fn map_key_event(key: KeyEvent, focus: FocusArea) -> Option<AppEvent> {
        match key.code {
            KeyCode::F(2) => Some(AppEvent::ToggleSubagentOverlay),
            KeyCode::Tab if overlay_visible => Some(AppEvent::SwitchView),
            // ...
        }
    }
}
```

**事件处理逻辑：**
```rust
fn handle_app_event(&mut self, event: AppEvent) -> TuiResult<()> {
    // 如果覆盖层可见，优先处理覆盖层事件
    if self.subagent_overlay.is_visible() {
        match event {
            AppEvent::ToggleSubagentOverlay => {
                self.subagent_overlay.toggle_visible();
            }
            AppEvent::SwitchView => {
                self.subagent_overlay.toggle_view_mode();
            }
            AppEvent::MoveUp => {
                self.subagent_overlay.move_up();
            }
            AppEvent::MoveDown => {
                self.subagent_overlay.move_down(max_items);
            }
            // ...
        }
        return Ok(());
    }

    // 正常事件处理...
}
```

### 6.3 状态轮询机制

**主循环：**
```rust
pub async fn run(&mut self) -> TuiResult<()> {
    let mut update_interval = tokio::time::interval(Duration::from_millis(500));

    loop {
        tokio::select! {
            // 渲染 UI（60 FPS）
            _ = tokio::time::sleep(Duration::from_millis(16)) => {
                self.draw()?;
            }

            // 处理键盘事件
            _ = tokio::time::sleep(Duration::from_millis(10)) => {
                self.handle_events().await?;
            }

            // 处理后台更新
            Some(update) = self.backend_rx.recv() => {
                self.handle_backend_update(update);
            }

            // 定时更新 subagent 状态（仅当覆盖层可见）
            _ = update_interval.tick() => {
                if self.subagent_overlay.is_visible() {
                    // SubagentOverlay.render() 会从 DashMap 读取最新状态
                    // 触发重绘即可
                }
            }
        }

        if self.should_quit {
            break;
        }
    }

    Ok(())
}
```

**要点：**
- 使用 `tokio::select!` 多路复用
- 500ms 定时器触发重绘（仅当覆盖层可见）
- SubagentOverlay 每次渲染时从 DashMap 读取最新状态
- 无需额外的状态同步机制

### 6.4 Backend 改造

**修改位置：** `crates/agent-tui/src/backend.rs`

**命令处理：**
```rust
pub struct TuiBackend {
    runtime: Arc<AgentRuntime>,
    command_rx: mpsc::UnboundedReceiver<BackendCommand>,
    update_tx: mpsc::UnboundedSender<BackendUpdate>,
}

impl TuiBackend {
    async fn handle_send_message(&self, session_id: Uuid, content: String) {
        // 检查是否为 subagent 命令
        if content.starts_with("/subagent") {
            self.handle_subagent_command(session_id, &content).await;
            return;
        }

        // 正常消息处理...
    }

    async fn handle_subagent_command(&self, parent_session_id: Uuid, input: &str) {
        match parse_subagent_command(input) {
            Ok(SubagentCommand::Start { tasks, timeout_secs, model }) => {
                let mut config = SubagentConfig::default();
                // 配置...

                match self.runtime.orchestrator()
                    .create_and_execute_stage(parent_session_id, tasks, Some(config))
                    .await
                {
                    Ok(stage_id) => {
                        let _ = self.update_tx.send(BackendUpdate::Info {
                            message: format!("✓ 已启动 subagent (Stage {})", stage_id)
                        });
                    }
                    Err(e) => {
                        let _ = self.update_tx.send(BackendUpdate::Error {
                            session_id: parent_session_id,
                            error: format!("启动失败: {}", e),
                        });
                    }
                }
            }
            Err(e) => {
                let _ = self.update_tx.send(BackendUpdate::Error {
                    session_id: parent_session_id,
                    error: format!("命令解析失败: {}", e),
                });
            }
        }
    }
}
```

---

## 7. 测试策略

### 7.1 测试金字塔

```
        /\
       /  \  E2E（5%）- 手工验收
      /----\
     /      \  集成测试（25%）
    /--------\
   /          \  单元测试（70%）
  /------------\
```

### 7.2 单元测试

**覆盖模块：**
- AgentRuntime（创建、资源访问）
- CommandParser（各种命令格式、边界情况）
- SubagentOverlay（状态管理、导航、视图切换）

**示例：**
```rust
#[test]
fn test_parse_multiple_tasks() {
    let cmd = parse_subagent_command(
        "/subagent start 任务1 任务2 任务3"
    ).unwrap();
    match cmd {
        SubagentCommand::Start { tasks, .. } => {
            assert_eq!(tasks.len(), 3);
        }
    }
}
```

### 7.3 集成测试

**测试场景：**
- 端到端 Stage 执行（创建 → 执行 → 状态更新）
- 并发限制强制执行
- 数据库持久化和级联删除
- Stage 统计信息准确性

**示例：**
```rust
#[tokio::test]
async fn test_create_and_execute_single_stage() {
    let orchestrator = setup_test_orchestrator().await;
    let tasks = vec!["任务1".to_string(), "任务2".to_string()];

    let stage_id = orchestrator
        .create_and_execute_stage(parent_id, tasks, None)
        .await
        .unwrap();

    assert_ne!(stage_id, Uuid::nil());
    assert_eq!(orchestrator.active_count(), 2);
}
```

### 7.4 手工验收测试

**测试清单：**
- [ ] CLI 命令解析和执行
- [ ] TUI F2 弹出/关闭覆盖层
- [ ] Tab 切换视图
- [ ] 状态实时更新（500ms）
- [ ] 状态图标显示正确
- [ ] 进度百分比更新
- [ ] 空状态提示显示
- [ ] 数据库数据正确

**详细清单：** `docs/testing/subagent-acceptance-tests.md`

---

## 8. 实施计划

### 8.1 时间估算

**总工作量：45-55 小时**

#### Week 1: 核心基础设施（20-25h）

**Day 1-2: AgentRuntime + CommandParser（8-10h）**
- [ ] 创建 AgentRuntime（2h）
- [ ] 创建 CommandParser（3h）
- [ ] CLI 集成（3-5h）

**Day 3: 数据库扩展（4-5h）**
- [ ] 验证迁移脚本（1h）
- [ ] Orchestrator 数据库方法（2-3h）
- [ ] 集成测试（1h）

**Day 4: Orchestrator 执行流程（8-10h）**
- [ ] create_and_execute_stage()（4-5h）
- [ ] 状态查询方法（2h）
- [ ] 集成测试（2-3h）

#### Week 2: TUI 集成（25-30h）

**Day 1-2: SubagentOverlay（12-15h）**
- [ ] 核心结构和状态（3-4h）
- [ ] 渲染逻辑（6-8h）
- [ ] 边界情况（2-3h）

**Day 3: TuiApp 集成（6-8h）**
- [ ] 集成 SubagentOverlay（2-3h）
- [ ] 事件处理（2-3h）
- [ ] 状态轮询（2h）

**Day 4: Backend 改造（4-5h）**
- [ ] 使用 Runtime（2h）
- [ ] handle_subagent_command（2-3h）

**Day 5: 测试和调试（3-4h）**
- [ ] 手工验收（2h）
- [ ] Bug 修复（1-2h）

### 8.2 交付物

**代码文件：**
- 新增约 15 个文件
- 修改约 5 个文件
- 总代码量：~2500 行（含测试）

**文档：**
- 本设计文档
- 测试清单
- README 更新

---

## 9. 风险评估

### 9.1 技术风险

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|----------|
| DashMap 性能瓶颈 | 低 | 中 | 限制轮询频率（500ms）|
| TUI 渲染复杂 | 中 | 中 | 简化布局，逐步优化 |
| 数据库迁移失败 | 低 | 高 | 充分测试，提供回滚 |
| 并发竞态 | 低 | 高 | 已用原子操作 |
| 内存占用高 | 低 | 中 | 限制并发数（10）|

### 9.2 进度风险

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|----------|
| TUI 耗时超预期 | 中 | 中 | 预留缓冲（Day 5）|
| 集成问题返工 | 低 | 高 | 尽早集成测试 |
| 测试覆盖不足 | 中 | 中 | 优先核心测试 |

### 9.3 用户体验风险

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|----------|
| 学习成本高 | 低 | 中 | 清晰的键盘提示 |
| 更新延迟感知 | 中 | 低 | 可接受（500ms）|
| 空状态不明显 | 低 | 低 | 友好的提示 |

---

## 10. 成功标准

### 10.1 功能完整性

- ✅ CLI 支持 `/subagent start` 命令
- ✅ TUI 支持 F2 弹出监控面板
- ✅ 支持视图切换（Tab）
- ✅ 实时显示状态和进度
- ✅ 数据库正确存储

### 10.2 性能指标

- ✅ 支持 10 个并发 subagent
- ✅ TUI 更新延迟 < 1.5s
- ✅ 内存占用 < 1.5GB
- ✅ 数据库查询 < 100ms

### 10.3 代码质量

- ✅ 测试覆盖率 > 80%
- ✅ 无 Clippy 警告
- ✅ 遵循 Rust 最佳实践

### 10.4 用户体验

- ✅ 命令使用直观
- ✅ TUI 操作流畅
- ✅ 状态显示清晰

---

## 11. Phase 3 扩展路径

**未来功能（30-40 小时）：**

1. **多 Stage 编排**
   - 预定义模式（自动执行）
   - 动态模式（逐个确认）
   - Stage 间结果传递

2. **LLM 工具调用**
   - 扩展 LLMClient trait
   - propose_subagent 工具
   - ConversationFlow 集成

3. **控制操作**
   - 取消/暂停/重试
   - 查看输出流

4. **高级命令**
   - `/subagent status <id>`
   - `/subagent cancel <id>`
   - `/subagent logs <id>`

---

## 12. 附录

### 12.1 命令示例

```bash
# 单任务
/subagent start "分析代码质量"

# 多任务并发
/subagent start "分析 auth.rs" "分析 db.rs" "分析 api.rs"

# 带超时
/subagent start --timeout 600 "长时间研究任务"

# 指定模型
/subagent start --model gpt-4 "复杂分析任务"
```

### 12.2 快捷键

**TUI 快捷键：**
- `F2` - 切换 Subagent 监控面板
- `Tab` - 切换视图（当前会话/全局）
- `↑↓` - 导航选择
- `Enter` - 展开/折叠（预留）
- `Esc` - 关闭覆盖层

### 12.3 参考文档

- 原始设计：`docs/superpowers/specs/2026-03-11-subagent-system-design.md`
- 实施计划：`docs/superpowers/plans/2026-03-11-subagent-system-implementation.md`
- 交接文档：`HANDOFF-2026-03-11-SESSION2.md`

---

**文档版本：** 1.0
**最后更新：** 2026-03-12
**作者：** General Agent V2 开发团队
**审核状态：** 待审核
