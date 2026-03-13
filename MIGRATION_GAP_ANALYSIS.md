# Python → Rust V2 迁移差距分析

**日期**: 2026-03-13
**当前状态**: Python 版本领先，需要迁移

---

## 📊 功能对比表

| 功能模块 | Python 版本 | Rust V2 版本 | 差距 | 优先级 |
|---------|------------|-------------|------|--------|
| **基础功能** |
| 会话管理 | ✅ Phase 1 | ✅ Phase 1 | 无 | - |
| LLM 集成 (Anthropic + Ollama) | ✅ | ✅ | 无 | - |
| 流式响应 | ✅ | ✅ | 无 | - |
| SQLite 持久化 | ✅ | ✅ | 无 | - |
| **扩展功能** |
| Skills 系统 | ✅ Phase 2 | ✅ Phase 1 | 无 | - |
| MCP 集成 | ✅ Phase 3-4 | ✅ Phase 2 Stage 4 | 无 | - |
| RAG 系统 | ✅ Phase 5 | ✅ Phase 2 Stage 5 | 无 | - |
| TUI 界面 | ✅ Phase 6 (Textual) | ✅ Phase 2 Stage 3 (Ratatui) | 无 | - |
| CLI 工具 | ✅ | ✅ | 无 | - |
| **核心缺失** |
| **Workflow 系统** | ✅ Phase 7 | ❌ **缺失** | **大** | 🔴 P0 |
| **Subagent 系统** | ✅ 刚完成 | ⚠️ **部分** | **中** | 🟡 P1 |
| Web API 服务 | ✅ FastAPI | ❌ 计划中 | 中 | 🟡 P1 |
| Phase 8 (Multi-Agent) | ⏳ 设计完成 | ❌ | 大 | 🟢 P2 |

---

## 🔴 P0: Workflow 系统（最大差距）

### Python 版本功能（Phase 7）

**核心模块** (16 个 Python 文件，~10,000+ 行代码)：

```
src/workflow/
├── models.py (13KB)           # 工作流核心模型
├── orchestrator.py (16KB)     # DAG 编排器
├── executor.py (21KB)         # 任务执行引擎
├── planner.py (15KB)          # 工作流规划器
├── approval.py (12KB)         # 审批管理系统
├── approval_ui.py (8KB)       # Rich TUI 审批界面
├── notification.py (9KB)      # 多渠道通知系统
└── performance/               # 性能监控框架
    ├── monitor.py             # 性能监控器
    ├── collector.py           # 指标收集器
    ├── tracer.py              # 链路追踪器
    ├── storage.py             # 指标存储
    ├── reporter.py            # 报告生成器
    ├── alerts.py              # 告警管理器
    └── dashboard.py           # 实时监控面板
```

**功能清单**：
1. **工作流编排器**
   - DAG 依赖解析
   - 并行任务调度
   - 条件执行
   - 动态任务生成

2. **任务执行引擎**
   - 重试机制（指数退避）
   - 超时控制
   - 取消支持
   - 暂停/恢复
   - 优先级调度

3. **审批管理系统**
   - Manual/Auto/Threshold 三种策略
   - Rich TUI 审批界面
   - 多选项交互
   - 审批历史记录

4. **通知系统**
   - 终端通知
   - 桌面通知
   - 日志记录
   - 多渠道路由

5. **性能监控框架**
   - 指标收集（CPU、内存、延迟）
   - 链路追踪
   - 实时监控面板
   - Markdown/JSON 报告生成
   - 智能告警系统（6 种规则）

### Rust V2 现状

**agent-workflow crate**：
- ✅ 基础会话管理 (SessionManager)
- ✅ 对话流程 (ConversationFlow)
- ✅ Skills/MCP/RAG 集成
- ❌ **完全缺失 Workflow 编排器**
- ❌ **完全缺失任务执行引擎**
- ❌ **完全缺失审批系统**
- ❌ **完全缺失性能监控**

### 迁移估算

**工作量**: 4-6 周（全职）

**分阶段迁移**：

#### Stage 1: 核心编排器（1.5-2 周）
```
agent-workflow/src/
├── orchestrator.rs         # DAG 编排器
├── executor.rs             # 任务执行引擎
├── models.rs              # 工作流模型
└── planner.rs             # 工作流规划器
```

**关键决策**：
- 使用 `petgraph` 做 DAG 解析
- 使用 `tokio::task` 做并行调度
- 参考 Python 版本的 API 设计

#### Stage 2: 审批和通知（1 周）
```
agent-workflow/src/
├── approval/
│   ├── manager.rs         # 审批管理器
│   ├── strategies.rs      # 审批策略
│   └── ui.rs              # TUI 审批界面（集成到 agent-tui）
└── notification/
    ├── channels.rs        # 通知渠道
    └── router.rs          # 通知路由
```

#### Stage 3: 性能监控（1.5-2 周）
```
agent-workflow/src/performance/
├── monitor.rs             # 性能监控器
├── collector.rs           # 指标收集器
├── tracer.rs              # 链路追踪
├── storage.rs             # 指标存储
├── reporter.rs            # 报告生成器
├── alerts.rs              # 告警系统
└── dashboard.rs           # 监控面板（集成到 agent-tui）
```

#### Stage 4: 测试和文档（1 周）
- 单元测试（目标 80% 覆盖率）
- 集成测试
- 性能测试
- 文档和示例

---

## 🟡 P1: Subagent 系统

### Python 版本功能

**核心组件**：
```
src/core/subagent_orchestrator.py    # Subagent 编排器
src/workflow/command_parser.py       # /subagent 命令解析
src/cli/tui/components/subagent_overlay.py  # TUI 监控界面
```

**功能**：
- `/subagent start` 命令支持
- 并行子任务创建
- 独立会话管理
- 实时状态监控（Pending/Running/Completed/Failed）
- Ctrl+S 切换监控 Overlay
- CurrentSession/Global 视图切换

### Rust V2 现状

**agent-tui crate**：
- ✅ SubagentOverlay UI 组件已实现
- ✅ 键盘事件处理（Ctrl+S, Tab, Esc）
- ✅ 状态渲染（彩色指示器）
- ⚠️ 数据库查询集成（部分完成）

**缺失**：
- ❌ SubagentOrchestrator 执行逻辑
- ❌ /subagent 命令解析器
- ❌ 后台任务调度

### 迁移估算

**工作量**: 1-1.5 周

**实施计划**：
```rust
// agent-workflow/src/subagent/
mod orchestrator;   // 编排器
mod executor;       // 执行器
mod parser;         // 命令解析

// agent-tui/src/
// SubagentOverlay 已完成，只需集成后端数据
```

---

## 🟡 P1: Web API 服务

### Python 版本

```
src/main.py           # FastAPI 应用
src/api/routes.py     # API 路由
```

**功能**：
- POST /api/chat - 聊天接口
- GET /api/sessions - 会话列表
- Web 界面（templates/）

### Rust V2 现状

**agent-api crate**：
- 📦 Crate 已创建
- ❌ 完全空白，待实现

### 迁移估算

**工作量**: 1 周

**技术栈**：
- Axum (已在 workspace dependencies)
- Tower/Tower-HTTP (已配置)

---

## 🗓️ 迁移路线图

### 第一阶段：Workflow 核心（4 周）
- **Week 1-2**: Orchestrator + Executor
- **Week 3**: Approval + Notification
- **Week 4**: Performance Monitoring

### 第二阶段：完善功能（2 周）
- **Week 5**: Subagent 系统完善
- **Week 6**: Web API + 集成测试

### 第三阶段：对齐和验收（1 周）
- **Week 7**: 功能对齐验证、文档更新

**总计**: 7 周（全职开发）

---

## 📋 迁移优先级决策

### 方案 A: 全力迁移 Workflow（推荐）

**理由**：
- Workflow 是最大的技术债务
- Python Phase 8 依赖 Workflow
- 迁移完成后两个版本功能对等
- 后续可以暂停 Python 开发，只维护 Rust 版本

**时间轴**：
- Week 1-4: Workflow 迁移
- Week 5-6: Subagent + Web API
- Week 7: 验收和文档
- Week 8+: Rust 版本继续开发 Phase 8

### 方案 B: 快速对齐核心功能

只迁移核心 Workflow 功能，跳过性能监控和审批 UI：
- Week 1-2: Orchestrator + Executor（核心）
- Week 3: Subagent 完善
- Week 4: 集成测试和文档

**时间轴**: 4 周完成基本对等

### 方案 C: 放弃迁移，继续 Python

继续在 Python 开发 Phase 8，Rust V2 作为实验性项目。

**不推荐理由**：
- 维护两个版本成本高
- Python 性能瓶颈明显
- Rust 版本会被放弃

---

## 🎯 推荐行动

### 立即开始（今天）

1. **确认迁移方案**
   - 推荐：方案 A（全力迁移）
   - 备选：方案 B（快速对齐核心）

2. **创建迁移分支**
   ```bash
   cd v2/
   git checkout -b feature/workflow-migration
   ```

3. **创建任务追踪**
   ```bash
   # 使用 GitHub Issues 或 .planning/ 目录
   # 记录每周的迁移进展
   ```

### 本周目标（Week 1）

```bash
# 创建基础结构
cd v2/crates/agent-workflow/src

# 1. 工作流模型定义
touch workflow/models.rs

# 2. DAG 编排器骨架
touch workflow/orchestrator.rs

# 3. 任务执行引擎骨架
touch workflow/executor.rs

# 4. 添加依赖
# 编辑 Cargo.toml，添加 petgraph
```

---

## 📚 技术参考

### Rust 生态选型

| 功能 | Python 库 | Rust 替代 |
|------|-----------|----------|
| DAG 图结构 | networkx | petgraph |
| 异步运行时 | asyncio | tokio |
| 任务调度 | asyncio.Queue | tokio::sync::mpsc |
| 时间处理 | datetime | chrono |
| 监控指标 | 自定义 | metrics crate |
| 审批 UI | rich | ratatui (已有) |
| 通知 | plyer | notify-rust |

### 关键设计决策

1. **并发模型**:
   - Python: asyncio Task
   - Rust: tokio::task + Arc<Mutex<>>

2. **DAG 表示**:
   - Python: Dict[str, Task] + 邻接表
   - Rust: petgraph::Graph

3. **状态管理**:
   - Python: 内存 + SQLite
   - Rust: Arc<RwLock<State>> + SQLx

---

## ⚠️ 风险和缓解

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| 迁移时间超预期 | 高 | 分阶段，先迁移核心 |
| API 不兼容 | 中 | 参考 Python 设计，保持一致 |
| 性能问题 | 低 | Rust 性能通常更好 |
| 测试覆盖不足 | 中 | 每个 Stage 完成后立即测试 |
| Python 版本继续演进 | 高 | 冻结 Python 功能，只修 bug |

---

## 📊 成功标准

### Workflow 迁移完成标准

- [ ] Orchestrator 可以执行简单 DAG
- [ ] Executor 支持重试、超时、取消
- [ ] 审批系统基本可用（至少支持 Manual 模式）
- [ ] 通知系统可以发送终端通知
- [ ] 性能监控可以收集基本指标
- [ ] 80% 测试覆盖率
- [ ] 有完整的集成测试
- [ ] 文档和示例完整

### 功能对等验证

对比测试：
```bash
# Python 版本
python examples/workflow_complete_demo.py

# Rust V2 版本
cargo run --example workflow_demo

# 输出应该一致：
# - 任务执行顺序正确
# - 审批流程相同
# - 监控数据类似
```

---

**创建日期**: 2026-03-13
**下次更新**: Week 1 迁移进展
