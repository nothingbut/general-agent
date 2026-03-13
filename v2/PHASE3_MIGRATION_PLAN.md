# Phase 3: Workflow 系统迁移计划

**版本**: v0.3.0
**日期**: 2026-03-13
**预计完成**: 2026-04-24 (6 周)
**状态**: 准备开始

---

## 📋 总览

### 目标

将 Python 版本的完整 Workflow 编排系统（10,000+ 行代码）迁移到 Rust V2，实现功能对等。

### 成功标准

- ✅ 完整的 DAG 工作流编排器
- ✅ 任务执行引擎（重试、超时、取消、暂停/恢复）
- ✅ 审批管理系统（Manual/Auto/Threshold）
- ✅ 多渠道通知系统
- ✅ 性能监控框架（指标收集、追踪、告警）
- ✅ 80%+ 测试覆盖率
- ✅ 完整的集成测试和文档

### 关键指标

| 指标 | Python V1 | 目标 Rust V2 |
|------|-----------|--------------|
| 代码量 | ~10,000 行 | ~8,000 行（更简洁）|
| 工作流启动延迟 | ~100ms | < 50ms |
| 并行任务数 | 10 | 100+ |
| 内存占用 | ~150MB | < 80MB |
| 测试覆盖率 | ~75% | 80%+ |

---

## 🗓️ 6 周路线图

### Week 1-2: 核心编排器和执行器（基础）

**目标**: 实现基础的 DAG 编排和任务执行

#### Week 1: 模型和 DAG 编排

**交付物**：
- `agent-workflow/src/workflow/models.rs` - 核心数据模型
- `agent-workflow/src/workflow/orchestrator.rs` - DAG 编排器
- `agent-workflow/src/workflow/executor.rs` - 基础任务执行器
- 单元测试 + 简单集成测试

**详细任务** (参考 `MIGRATION_WEEK1_PLAN.md`):
- Day 1: 项目设置和模型定义
- Day 2: DAG 依赖解析（petgraph）
- Day 3: 任务执行器框架
- Day 4: 集成 Orchestrator + Executor
- Day 5: 测试和文档

**验收标准**:
- ✅ 能执行 3-5 个任务的简单 DAG
- ✅ 支持任务重试（指数退避）
- ✅ 支持任务超时
- ✅ 并行执行独立任务
- ✅ 循环依赖检测

#### Week 2: 执行器完善和集成

**目标**: 集成现有的 LLM/Skills/MCP 功能

**任务**:
1. **任务类型集成** (Day 1-2)
   ```rust
   // TaskType::LLMCall 集成 agent-llm
   async fn execute_llm_call(&self, config: &LLMConfig) -> Result<String> {
       self.llm_client.send_message(...).await
   }

   // TaskType::SkillExecution 集成 agent-skills
   async fn execute_skill(&self, skill_name: &str, params: &SkillParams) -> Result<String> {
       self.skill_executor.execute(...).await
   }

   // TaskType::MCPToolCall 集成 agent-mcp
   async fn execute_mcp_tool(&self, server: &str, tool: &str, args: Value) -> Result<Value> {
       self.mcp_client.call_tool(...).await
   }
   ```

2. **状态持久化** (Day 2-3)
   ```rust
   // 工作流状态持久化到 SQLite
   impl WorkflowStorage {
       async fn save_workflow(&self, workflow: &Workflow) -> Result<()>;
       async fn load_workflow(&self, id: &str) -> Result<Workflow>;
       async fn update_task_status(&self, task_id: &str, status: TaskStatus) -> Result<()>;
   }
   ```

3. **取消和暂停支持** (Day 3-4)
   ```rust
   // 使用 tokio::sync::mpsc 实现控制信号
   enum ControlSignal {
       Pause,
       Resume,
       Cancel,
   }

   impl WorkflowExecutor {
       async fn handle_control_signals(&mut self) {
           // 监听控制信号并响应
       }
   }
   ```

4. **错误处理和恢复** (Day 4-5)
   - 任务失败后的清理逻辑
   - 部分任务失败时的工作流恢复
   - 详细的错误上下文

5. **测试和文档** (Day 5)
   - 集成测试：完整工作流执行
   - 文档：使用示例和 API 文档

**交付物**:
- 完整的任务执行器（支持所有任务类型）
- 状态持久化模块
- 控制信号处理
- 80%+ 单元测试覆盖率
- 完整的集成测试

**验收标准**:
- ✅ 支持 LLM/Skills/MCP 任务类型
- ✅ 工作流状态可持久化和恢复
- ✅ 支持暂停/恢复/取消
- ✅ 任务失败后可以恢复执行

---

### Week 3: 审批和通知系统

**目标**: 实现交互式审批和多渠道通知

#### 审批管理系统

**任务**:
1. **审批策略** (Day 1)
   ```rust
   pub enum ApprovalStrategy {
       Manual,              // 总是需要人工审批
       Auto,                // 自动审批
       Threshold {          // 条件审批
           risk_level: RiskLevel,
           requires_approval_above: RiskLevel,
       },
   }

   pub struct ApprovalManager {
       strategy: ApprovalStrategy,
       pending_approvals: Vec<ApprovalRequest>,
   }
   ```

2. **审批请求处理** (Day 1-2)
   ```rust
   pub struct ApprovalRequest {
       task_id: String,
       task_name: String,
       tool: String,
       params: Value,
       reason: String,
       risk_level: RiskLevel,
   }

   impl ApprovalManager {
       async fn request_approval(&mut self, request: ApprovalRequest) -> Result<ApprovalDecision>;
       async fn approve(&mut self, task_id: &str) -> Result<()>;
       async fn reject(&mut self, task_id: &str, reason: String) -> Result<()>;
   }
   ```

3. **集成到 TUI** (Day 2-3)
   - 在 agent-tui 中添加审批弹窗
   - 显示任务详情和风险等级
   - 支持批量审批

   ```rust
   // agent-tui/src/components/approval_dialog.rs
   pub struct ApprovalDialog {
       request: ApprovalRequest,
       state: DialogState,
   }

   impl Component for ApprovalDialog {
       fn render(&self, f: &mut Frame, area: Rect);
       fn handle_input(&mut self, key: KeyEvent) -> Action;
   }
   ```

4. **审批历史记录** (Day 3)
   - 存储审批决策到数据库
   - 审计日志

**交付物**:
- `agent-workflow/src/approval/` 模块
- TUI 审批界面
- 审批历史和审计日志
- 单元测试和集成测试

#### 通知系统

**任务**:
1. **通知渠道抽象** (Day 4)
   ```rust
   #[async_trait]
   pub trait NotificationChannel: Send + Sync {
       async fn send(&self, notification: &Notification) -> Result<()>;
   }

   pub struct TerminalChannel;    // 终端输出
   pub struct DesktopChannel;     // 桌面通知（notify-rust）
   pub struct LogChannel;         // 日志记录
   ```

2. **通知路由** (Day 4-5)
   ```rust
   pub struct NotificationRouter {
       channels: HashMap<NotificationType, Vec<Box<dyn NotificationChannel>>>,
   }

   impl NotificationRouter {
       pub async fn notify(&self, notification: Notification) {
           // 根据通知类型路由到对应渠道
       }
   }
   ```

3. **集成到工作流** (Day 5)
   - 任务开始/完成/失败通知
   - 工作流状态变更通知
   - 审批请求通知

**交付物**:
- `agent-workflow/src/notification/` 模块
- 终端、桌面、日志三种通知渠道
- 通知路由系统
- 测试和文档

**验收标准**:
- ✅ 审批系统可以处理 Manual/Auto/Threshold 三种策略
- ✅ TUI 中可以交互式审批任务
- ✅ 审批决策被记录到数据库
- ✅ 支持终端、桌面、日志三种通知渠道
- ✅ 关键事件自动发送通知

---

### Week 4: 性能监控框架

**目标**: 实现完整的性能监控、追踪和告警系统

#### 指标收集器

**任务**:
1. **指标定义** (Day 1)
   ```rust
   pub enum Metric {
       TaskExecutionTime { task_id: String, duration_ms: u64 },
       TaskMemoryUsage { task_id: String, bytes: u64 },
       WorkflowThroughput { tasks_per_second: f64 },
       ErrorRate { errors_per_minute: f64 },
       // ... 更多指标
   }

   pub struct MetricCollector {
       metrics: Vec<Metric>,
       storage: Arc<MetricStorage>,
   }
   ```

2. **指标存储** (Day 1-2)
   ```rust
   // 使用 SQLite 存储时序指标
   CREATE TABLE metrics (
       id INTEGER PRIMARY KEY,
       timestamp DATETIME NOT NULL,
       metric_type TEXT NOT NULL,
       metric_name TEXT NOT NULL,
       value REAL NOT NULL,
       labels TEXT,  -- JSON
       workflow_id TEXT,
       task_id TEXT
   );

   impl MetricStorage {
       async fn save_metric(&self, metric: &Metric) -> Result<()>;
       async fn query_metrics(&self, query: MetricQuery) -> Result<Vec<Metric>>;
   }
   ```

#### 链路追踪

**任务**:
1. **Trace 上下文** (Day 2-3)
   ```rust
   pub struct TraceContext {
       trace_id: String,
       span_id: String,
       parent_span_id: Option<String>,
       start_time: DateTime<Utc>,
       attributes: HashMap<String, String>,
   }

   pub struct Span {
       context: TraceContext,
       name: String,
       duration_ms: Option<u64>,
       events: Vec<SpanEvent>,
   }
   ```

2. **自动追踪注入** (Day 3)
   ```rust
   // 使用宏自动注入追踪
   #[traced]
   async fn execute_task(&self, task: &Task) -> Result<TaskResult> {
       // 自动创建 Span 并记录
   }
   ```

#### 监控面板和报告

**任务**:
1. **实时监控面板** (Day 3-4)
   - 集成到 TUI（类似 htop）
   - 显示实时指标（CPU、内存、任务吞吐）
   - 历史趋势图（使用 Ratatui Sparkline）

   ```rust
   // agent-tui/src/components/performance_dashboard.rs
   pub struct PerformanceDashboard {
       metrics: Vec<Metric>,
       chart_data: Vec<(f64, f64)>,
   }
   ```

2. **报告生成器** (Day 4-5)
   ```rust
   pub struct PerformanceReport {
       summary: ReportSummary,
       task_metrics: Vec<TaskMetrics>,
       bottlenecks: Vec<Bottleneck>,
       recommendations: Vec<String>,
   }

   impl Reporter {
       pub fn generate_markdown_report(&self, workflow_id: &str) -> Result<String>;
       pub fn generate_json_report(&self, workflow_id: &str) -> Result<Value>;
   }
   ```

#### 告警系统

**任务**:
1. **告警规则引擎** (Day 5)
   ```rust
   pub enum AlertRule {
       ThresholdExceeded { metric: String, threshold: f64 },
       ErrorRateHigh { rate: f64, window_minutes: u32 },
       TaskTimeout { task_id: String, expected_duration_ms: u64 },
       MemoryLeak { growth_rate_mb_per_min: f64 },
       // ... 更多规则
   }

   pub struct AlertManager {
       rules: Vec<AlertRule>,
       alert_history: Vec<Alert>,
       notification_router: Arc<NotificationRouter>,
   }
   ```

**交付物**:
- `agent-workflow/src/performance/` 模块（6 个子模块）
- 指标收集和存储
- 链路追踪系统
- TUI 监控面板
- Markdown/JSON 报告生成器
- 智能告警系统
- 完整测试和文档

**验收标准**:
- ✅ 自动收集任务执行指标
- ✅ 链路追踪可以关联完整的调用链
- ✅ TUI 显示实时性能监控面板
- ✅ 可以生成 Markdown 和 JSON 格式的性能报告
- ✅ 告警系统可以检测异常并发送通知

---

### Week 5: Subagent 系统完善

**目标**: 完善 Subagent 编排和监控功能

#### 后端集成

**任务**:
1. **SubagentOrchestrator** (Day 1-2)
   ```rust
   pub struct SubagentOrchestrator {
       active_subagents: HashMap<String, SubagentTask>,
       executor: Arc<WorkflowExecutor>,
       storage: Arc<Storage>,
   }

   impl SubagentOrchestrator {
       pub async fn start_subagent(&mut self, task: SubagentTask) -> Result<String>;
       pub async fn get_status(&self, subagent_id: &str) -> Result<SubagentStatus>;
       pub async fn list_active(&self) -> Vec<SubagentInfo>;
       pub async fn cancel(&mut self, subagent_id: &str) -> Result<()>;
   }
   ```

2. **/subagent 命令解析** (Day 2)
   ```rust
   // agent-workflow/src/commands/subagent_parser.rs
   pub fn parse_subagent_command(input: &str) -> Result<SubagentCommand> {
       // /subagent start "任务1" "任务2" "任务3"
       // /subagent list
       // /subagent cancel <id>
   }
   ```

3. **后台任务调度** (Day 2-3)
   - 使用 tokio::spawn 在后台执行
   - 定期更新状态到数据库
   - 完成后发送通知

4. **TUI 数据绑定** (Day 3-4)
   - SubagentOverlay 从数据库查询实时状态
   - 自动刷新（每 1 秒）
   - 支持全局视图和当前会话视图

5. **测试和文档** (Day 4-5)
   - 单元测试：命令解析、状态管理
   - 集成测试：完整的 Subagent 生命周期
   - 文档：使用指南和最佳实践

**交付物**:
- `agent-workflow/src/subagent/` 模块
- 命令解析器
- 后台任务调度
- TUI 数据绑定
- 测试和文档

**验收标准**:
- ✅ 可以通过 `/subagent start` 创建并行任务
- ✅ SubagentOverlay 显示实时状态
- ✅ 支持取消正在运行的 Subagent
- ✅ 完成后自动发送通知
- ✅ 数据库持久化 Subagent 历史

---

### Week 6: 集成测试、文档和验收

**目标**: 确保系统稳定性和功能完整性

#### 集成测试套件

**任务**:
1. **端到端工作流测试** (Day 1-2)
   ```rust
   #[tokio::test]
   async fn test_complete_workflow_lifecycle() {
       // 1. 创建复杂工作流（10+ 任务，多层依赖）
       // 2. 执行工作流
       // 3. 验证审批流程
       // 4. 检查性能指标
       // 5. 验证通知发送
       // 6. 测试暂停/恢复
       // 7. 测试取消
   }
   ```

2. **错误场景测试** (Day 2)
   - 任务超时
   - 任务失败重试
   - 循环依赖检测
   - 部分任务失败恢复
   - 工作流取消后的清理

3. **性能测试** (Day 2-3)
   - 大规模工作流（100+ 任务）
   - 并发工作流执行
   - 内存泄漏检测
   - 长时间运行稳定性

4. **兼容性测试** (Day 3)
   - Python 版本功能对等验证
   - API 兼容性测试
   - 数据库 schema 兼容性

#### 文档完善

**任务**:
1. **架构文档** (Day 3-4)
   - 更新 `v2/docs/ARCHITECTURE.md`
   - 添加 Workflow 系统架构图
   - 数据流图和状态机图

2. **用户指南** (Day 4)
   - 工作流定义指南
   - 审批策略配置
   - 性能监控使用手册
   - 故障排查指南

3. **API 文档** (Day 4)
   - 生成 Rustdoc
   - 添加使用示例
   - 集成测试作为示例代码

4. **迁移指南** (Day 4-5)
   - Python → Rust 迁移步骤
   - API 差异说明
   - 配置迁移工具

#### 验收测试

**任务**:
1. **功能验收** (Day 5)
   - 对照 MIGRATION_GAP_ANALYSIS.md 逐项验证
   - Python 和 Rust 版本对比测试
   - 记录所有差异和已知限制

2. **性能验收** (Day 5)
   - 测量关键指标
   - 与 Python 版本对比
   - 生成性能报告

3. **代码审查** (Day 5)
   - Clippy 检查
   - 代码覆盖率报告
   - 安全审计

**交付物**:
- 完整的集成测试套件
- 性能测试报告
- 更新的架构文档
- 用户指南和 API 文档
- 迁移指南
- 验收报告

**验收标准**:
- ✅ 所有集成测试通过
- ✅ 测试覆盖率 > 80%
- ✅ 性能指标达到目标
- ✅ 文档完整且准确
- ✅ 功能对等验证通过

---

## 🎯 关键里程碑

### Milestone 1: 核心功能就绪 (Week 2 结束)
- ✅ DAG 编排器和执行器完成
- ✅ 支持所有任务类型
- ✅ 状态持久化
- ✅ 基础测试通过

### Milestone 2: 完整功能集 (Week 4 结束)
- ✅ 审批和通知系统完成
- ✅ 性能监控框架完成
- ✅ 核心功能完整

### Milestone 3: 生产就绪 (Week 6 结束)
- ✅ Subagent 系统完善
- ✅ 集成测试完整
- ✅ 文档齐全
- ✅ 性能达标
- ✅ 功能对等验证通过

---

## 📊 技术选型

### 核心依赖

```toml
[dependencies]
# 已有
tokio = { version = "1.0", features = ["full"] }
sqlx = { version = "0.7", features = ["sqlite", "runtime-tokio-native-tls"] }

# 新增
petgraph = "0.6"           # DAG 图结构
metrics = "0.21"           # 指标收集
tracing = "0.1"            # 链路追踪
tracing-subscriber = "0.3"
notify-rust = "4.0"        # 桌面通知
```

### 设计模式

1. **Actor 模式** - 工作流执行器使用消息传递控制
2. **Strategy 模式** - 审批策略可插拔
3. **Observer 模式** - 通知系统订阅工作流事件
4. **Builder 模式** - 工作流构建器

---

## ⚠️ 风险和缓解

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|----------|
| petgraph 性能不足 | 高 | 低 | 提前进行性能测试，必要时自实现 |
| 并发 bug 难调试 | 中 | 中 | 充分的单元测试，使用 tracing 追踪 |
| TUI 集成复杂 | 中 | 中 | 先实现后端逻辑，TUI 可以后续优化 |
| 时间估算不准 | 高 | 中 | 分阶段交付，优先核心功能 |
| Python 功能演进 | 低 | 低 | 冻结 Python 版本功能，只修 bug |

---

## 📝 每日站会检查项

**每天回答的三个问题**:
1. 昨天完成了什么？
2. 今天计划做什么？
3. 遇到什么阻碍？

**每周回顾**:
- 对照计划检查进度
- 更新 MIGRATION_WEEK<N>_PROGRESS.md
- 调整下周计划

---

## 🎉 完成标准

### 功能完整性
- [ ] 所有 Python Workflow 功能已迁移
- [ ] 功能对等测试通过
- [ ] 性能达到或超过 Python 版本

### 代码质量
- [ ] 测试覆盖率 > 80%
- [ ] Clippy 无警告
- [ ] 所有文档完成

### 可维护性
- [ ] 代码结构清晰
- [ ] API 文档齐全
- [ ] 有完整的使用示例

### 用户体验
- [ ] TUI 审批流畅
- [ ] 错误信息清晰
- [ ] 监控面板直观

---

**创建日期**: 2026-03-13
**最后更新**: 2026-03-13
**负责人**: Claude + 用户
**评审人**: 待定
