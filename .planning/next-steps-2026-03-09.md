# General Agent - 下一步计划

**日期:** 2026-03-09
**当前状态:** Phase 7 已完成并合并

---

## 📊 当前项目状态

### ✅ 已完成的 Phase
- Phase 1: Foundation（基础设施）
- Phase 2: Skill System（技能系统）
- Phase 3: MCP Integration（MCP 集成）
- Phase 4: Real MCP Server Connections（真实 MCP 连接）
- Phase 5: RAG Engine（RAG 引擎）
- Phase 6: TUI 终端界面
- **Phase 7: Agent Workflow System（工作流系统）** ✨ 新完成

### 📈 项目统计
- **总代码行数:** ~30,000+ 行
- **测试用例:** 484 个（16 个收集错误需修复）
- **核心模块:** 11 个
- **文档完整性:** 良好

---

## 🎯 推荐方案：三阶段渐进式推进

### 阶段 1: 整理和稳定（1-2 天）⚡ 立即执行

**优先级:** 🔴 高

#### 1.1 更新文档
- [ ] 更新 README.md - 添加 Phase 7 内容
- [ ] 创建 Phase 7 使用示例
- [ ] 更新项目 ROADMAP

#### 1.2 修复测试问题
- [ ] 修复 16 个测试收集错误
- [ ] 运行完整测试套件确保 100% 通过
- [ ] 更新测试覆盖率报告

#### 1.3 代码质量提升
- [ ] 修复 dashboard.py 的 RuntimeWarning
- [ ] 运行完整的代码质量检查
- [ ] 优化导入和依赖关系

**预计工作量:** 1-2 天
**预期成果:** 项目处于稳定可发布状态

---

### 阶段 2: Phase 8 核心开发（2-3 周）⭐ 主要方向

**优先级:** 🟡 中高

#### 推荐方向 A: Agent 能力增强（推荐）

**为什么选择这个方向？**
- 构建在 Phase 7 工作流系统基础上
- 自然的功能演进路径
- 市场需求强烈（多 Agent 协作是热点）
- 可以复用现有的工作流基础设施

**功能规划:**

##### 8.1 多 Agent 协作框架
**目标:** 支持多个 Agent 并行工作并协调

**功能:**
- Agent 定义和注册
- Agent 角色和能力描述
- Agent 间消息传递
- 协作模式（并行/顺序/树形）

**技术方案:**
```python
# Agent 定义
class AgentDefinition:
    agent_id: str
    role: str  # "researcher", "writer", "reviewer"
    capabilities: List[str]
    llm_config: LLMConfig
    workflow_id: Optional[str]

# Agent 协作
class AgentCoordinator:
    async def coordinate(
        self,
        agents: List[AgentDefinition],
        task: str,
        strategy: CoordinationStrategy
    ) -> CoordinationResult
```

**预计工作量:** 1 周

---

##### 8.2 Agent 间通信协议
**目标:** 标准化的 Agent 消息格式

**功能:**
- 消息类型定义（请求/响应/通知/查询）
- 消息路由和转发
- 异步消息队列
- 消息持久化和追踪

**技术方案:**
```python
# 消息协议
class AgentMessage:
    message_id: str
    from_agent: str
    to_agent: str
    message_type: MessageType
    content: Dict[str, Any]
    timestamp: datetime
    reply_to: Optional[str]

# 消息总线
class AgentMessageBus:
    async def send(self, message: AgentMessage)
    async def subscribe(self, agent_id: str, handler: Callable)
    async def query_history(self, agent_id: str) -> List[AgentMessage]
```

**预计工作量:** 5 天

---

##### 8.3 共享状态管理
**目标:** Agent 间的数据共享和同步

**功能:**
- 共享内存空间
- 状态版本控制
- 乐观锁和冲突检测
- 状态快照和回滚

**技术方案:**
```python
# 共享状态
class SharedState:
    async def get(self, key: str, agent_id: str) -> Any
    async def set(self, key: str, value: Any, agent_id: str) -> Version
    async def watch(self, key: str, callback: Callable)
    async def transaction(self, operations: List[Operation])

# 冲突解决
class ConflictResolver:
    async def resolve(
        self,
        conflicts: List[StateConflict],
        strategy: ResolutionStrategy
    ) -> ResolvedState
```

**预计工作量:** 5 天

---

##### 8.4 集成测试和示例
**目标:** 完整的端到端测试和示例

**功能:**
- 3+ Agent 协作示例
- 性能测试（100+ Agent 并发）
- 故障恢复测试
- 文档和教程

**预计工作量:** 3 天

**Phase 8A 总工作量:** 2-3 周

---

#### 备选方向 B: 工具生态系统

**功能规划:**
- 工具市场（浏览、搜索、安装）
- 工具版本管理
- 工具兼容性测试
- 工具性能基准

**预计工作量:** 2-3 周

---

#### 备选方向 C: 企业级特性

**功能规划:**
- RBAC 权限控制
- 审计日志和合规
- SLA 监控
- 多租户支持

**预计工作量:** 3-4 周

---

### 阶段 3: 生态和优化（持续）🔄 长期

**优先级:** 🟢 低

#### 3.1 性能优化
- [ ] 数据库查询优化
- [ ] 缓存策略优化
- [ ] 并发性能测试
- [ ] 内存占用优化

#### 3.2 用户体验
- [ ] 更好的错误提示
- [ ] 交互式配置向导
- [ ] 更多的示例和模板
- [ ] 视频教程

#### 3.3 社区建设
- [ ] 贡献指南
- [ ] Issue 模板
- [ ] PR 模板
- [ ] 行为准则

---

## 📋 详细的阶段 1 执行计划

### Task 1: 更新 README（30 分钟）

**文件:** `README.md`

**要添加的内容:**
```markdown
### Phase 7: Agent Workflow System ✅
- 工作流编排器（DAG 依赖解析）
- 任务执行引擎（重试、超时、取消）
- 审批管理系统（Manual/Auto/Threshold）
- Rich TUI 审批界面
- 多渠道通知系统
- 完整性能监控框架
- Markdown/JSON 报告生成
- 智能告警系统（6 种规则）
- 实时监控面板

详细文档见：[docs/workflow/](docs/workflow/)
```

---

### Task 2: 修复测试错误（2-3 小时）

**步骤:**
1. 运行测试诊断：
   ```bash
   pytest tests/ --co -q 2>&1 | grep ERROR
   ```

2. 逐个修复错误模块：
   - `tests/rag/test_storage.py` - Pydantic 配置错误
   - `tests/test_e2e.py` - E2E 测试问题
   - `tests/test_mcp_e2e.py` - MCP E2E 测试问题

3. 确认所有测试通过：
   ```bash
   pytest tests/ -v
   ```

---

### Task 3: 修复 RuntimeWarning（30 分钟）

**文件:** `src/workflow/performance/dashboard.py:97`

**问题:** 异常处理中的 coroutine 未 await

**修复方案:**
```python
# 当前（错误）
except RuntimeError as e:
    self.console.print(f"[red]Error: {e}[/red]")

# 修复后
except RuntimeError as e:
    self.console.print(f"[red]Error: {e}[/red]")
    return  # 确保不会继续执行异步代码
```

---

### Task 4: 创建 Phase 7 使用示例（1-2 小时）

**文件:** `examples/workflow_complete_demo.py`

**内容:**
```python
"""Phase 7 完整功能演示"""
import asyncio
from src.workflow.orchestrator import Orchestrator
from src.workflow.approval import ApprovalManager
from src.workflow.performance import PerformanceMonitor
from src.workflow.performance.dashboard import MonitoringDashboard

async def main():
    # 1. 创建工作流
    # 2. 配置审批策略
    # 3. 启动性能监控
    # 4. 执行工作流
    # 5. 查看监控面板
    # 6. 生成性能报告
    pass

if __name__ == "__main__":
    asyncio.run(main())
```

---

### Task 5: 创建 ROADMAP（30 分钟）

**文件:** `ROADMAP.md`

**内容:**
```markdown
# General Agent Roadmap

## ✅ 已完成
- Phase 1-7（见 README.md）

## 🚧 进行中
- 阶段 1: 整理和稳定

## 📅 计划中
- Phase 8: Agent 能力增强（推荐）
  - 多 Agent 协作
  - 通信协议
  - 共享状态管理

## 🔮 未来考虑
- 工具生态系统
- 企业级特性
- 云原生部署
```

---

## 🎯 成功标准

### 阶段 1（整理和稳定）
- [x] README 包含 Phase 7 内容
- [x] 所有测试通过（484/484）
- [x] 代码质量检查全部通过
- [x] 有 Phase 7 使用示例
- [x] ROADMAP 文档存在

### 阶段 2（Phase 8 开发）
- [ ] 核心功能实现完成
- [ ] 测试覆盖率 > 80%
- [ ] 有完整的文档和示例
- [ ] 性能满足预期

### 阶段 3（持续优化）
- [ ] 性能指标达标
- [ ] 用户反馈良好
- [ ] 社区活跃

---

## 💡 关键决策点

### 决策 1: Phase 8 方向选择（需要确认）

**选项对比:**

| 方向 | 难度 | 价值 | 时间 | 依赖 Phase 7 | 推荐度 |
|------|------|------|------|-------------|--------|
| A. Agent 能力增强 | 中 | 高 | 2-3周 | ✅ | ⭐⭐⭐⭐⭐ |
| B. 工具生态系统 | 中低 | 中 | 2-3周 | ❌ | ⭐⭐⭐ |
| C. 企业级特性 | 高 | 高 | 3-4周 | ⚠️ | ⭐⭐⭐⭐ |

**推荐:** 选择 **A. Agent 能力增强**

**理由:**
1. 自然演进 - 构建在 Phase 7 基础上
2. 市场热点 - 多 Agent 协作是当前研究前沿
3. 技术复用 - 可以复用工作流、监控、审批等功能
4. 差异化 - 提供完整的企业级多 Agent 解决方案

---

### 决策 2: 是否需要先完成阶段 1？（建议：是）

**理由:**
- 确保代码库处于健康状态
- 更新文档帮助未来的开发
- 修复测试防止回归
- 小投入大收益（1-2 天）

---

## 📝 行动建议

### 立即开始（今天）
```bash
# 1. 创建 Phase 8 规划分支
git checkout -b feature/phase8-planning

# 2. 运行测试诊断
pytest tests/ --co -q 2>&1 > test_errors.log

# 3. 更新 README
# 编辑 README.md，添加 Phase 7 内容

# 4. 创建 ROADMAP
# 创建 ROADMAP.md

# 5. 提交更改
git add README.md ROADMAP.md
git commit -m "docs: 更新 README 和创建 ROADMAP"
```

### 本周完成（阶段 1）
- [ ] 修复所有测试错误
- [ ] 修复 RuntimeWarning
- [ ] 创建 Phase 7 示例
- [ ] 更新所有相关文档
- [ ] 合并到 main 分支

### 下周开始（阶段 2）
- [ ] 确认 Phase 8 方向
- [ ] 创建详细设计文档
- [ ] 开始核心功能开发

---

## 📚 参考资源

### 相关研究和项目
- **AutoGPT** - 多 Agent 协作先驱
- **LangChain Agents** - Agent 框架参考
- **Microsoft Autogen** - 多 Agent 对话框架
- **CrewAI** - 角色化 Agent 系统

### 技术文档
- Phase 7 完成报告: `.planning/phase7-completion-report.md`
- 工作流设计: `docs/plans/2026-03-06-phase7-agent-workflow.md`
- 监控设计: `docs/plans/2026-03-08-monitoring-dashboard-design.md`

---

**文档创建时间:** 2026-03-09
**下次更新:** 阶段 1 完成后
**维护者:** Phase 7 开发团队
