# Phase 7.1: Agent 工作流与任务编排

**创建日期:** 2026-03-06
**预计工期:** 4-6 周
**优先级:** P0（最高）

---

## 概述

Phase 7 实现 Agent 工作流与任务编排能力，让 Agent 能够自主规划和执行多步骤复杂任务，充分利用现有的 MCP 工具、RAG 知识库和 Skills 技能系统。

### 核心目标

1. **任务自动分解** - 将复杂目标分解为可执行的子任务
2. **智能工具编排** - 自动选择和调用 MCP/Skills/RAG
3. **执行状态跟踪** - 实时监控和持久化执行状态
4. **人机协同** - 关键决策点支持用户审批
5. **容错机制** - 支持错误重试和任务回滚

---

## 功能设计

### 7.1 任务规划器（Task Planner）

**目标:** 将用户目标分解为结构化的执行计划

**核心功能:**
- 目标理解和意图识别
- 任务分解（生成子任务 DAG）
- 工具能力映射（匹配可用工具）
- 执行顺序优化（考虑依赖关系）

**输入示例:**
```
用户: 帮我分析 docs/ 目录下的所有 Markdown 文件，总结技术要点，并生成思维导图
```

**输出计划:**
```json
{
  "goal": "分析文档并生成思维导图",
  "tasks": [
    {
      "id": "task-1",
      "name": "列出文档文件",
      "tool": "mcp:filesystem:list_directory",
      "params": {"path": "docs/", "pattern": "*.md"},
      "dependencies": []
    },
    {
      "id": "task-2",
      "name": "读取文档内容",
      "tool": "mcp:filesystem:read_file",
      "params": {"paths": "${task-1.output.files}"},
      "dependencies": ["task-1"]
    },
    {
      "id": "task-3",
      "name": "RAG 文档索引",
      "tool": "rag:index_documents",
      "params": {"documents": "${task-2.output}"},
      "dependencies": ["task-2"]
    },
    {
      "id": "task-4",
      "name": "技术要点提取",
      "tool": "llm:analyze",
      "params": {"query": "总结技术要点", "context": "rag"},
      "dependencies": ["task-3"]
    },
    {
      "id": "task-5",
      "name": "生成思维导图",
      "tool": "skill:mindmap",
      "params": {"content": "${task-4.output}"},
      "dependencies": ["task-4"],
      "requires_approval": true
    }
  ]
}
```

**实现要点:**
- 使用 LLM（Claude/GPT-4）生成计划
- Prompt 工程：提供工具列表和能力描述
- 计划验证：检查依赖关系、参数类型

---

### 7.2 执行引擎（Execution Engine）

**目标:** 按计划执行任务，支持并行和错误处理

**核心功能:**
- DAG 遍历和调度
- 并行任务执行（无依赖任务）
- 变量替换（${task-id.output.field}）
- 执行超时控制
- 实时进度通知

**执行流程:**
```
┌──────────────┐
│ 加载执行计划  │
└──────┬───────┘
       │
       ↓
┌──────────────┐
│ 拓扑排序任务  │ (确定执行顺序)
└──────┬───────┘
       │
       ↓
┌──────────────┐     YES    ┌──────────────┐
│ 是否需要审批？│ ─────────→ │ 等待用户确认  │
└──────┬───────┘            └──────┬───────┘
       │ NO                        │
       ↓                           ↓
┌──────────────┐            ┌──────────────┐
│ 解析参数依赖  │            │ 用户批准？    │
└──────┬───────┘            └──────┬───────┘
       │                           │ YES
       ↓                           ↓
┌──────────────┐            ┌──────────────┐
│ 调用工具执行  │ ←──────────│ 继续执行      │
└──────┬───────┘
       │                    ┌──────────────┐
       │ 成功               │ 用户拒绝      │
       ↓                    └──────┬───────┘
┌──────────────┐                  │
│ 保存执行结果  │                  │
└──────┬───────┘                  ↓
       │              ┌──────────────────┐
       ↓              │ 标记任务取消/失败 │
┌──────────────┐      └──────────────────┘
│ 是否有错误？  │
└──────┬───────┘
       │ NO
       ↓
┌──────────────┐
│ 还有待执行？  │
└──────┬───────┘
       │ YES → 返回"是否需要审批"
       │
       ↓ NO
┌──────────────┐
│ 执行完成      │
└──────────────┘
```

**实现要点:**
- 使用 asyncio 并行执行
- 状态机模式（PENDING/RUNNING/SUCCESS/FAILED/CANCELLED）
- Context 对象传递任务输出

---

### 7.3 工具编排器（Tool Orchestrator）

**目标:** 统一调用 MCP、Skills、RAG 和 LLM

**工具注册表:**
```python
{
  "mcp:filesystem:read_file": {
    "type": "mcp",
    "server": "filesystem",
    "tool": "read_file",
    "description": "读取文件内容",
    "params": {"path": "string"}
  },
  "skill:summarize": {
    "type": "skill",
    "name": "summarize",
    "description": "总结文本",
    "params": {"text": "string", "length": "enum"}
  },
  "rag:search": {
    "type": "rag",
    "method": "search",
    "description": "搜索知识库",
    "params": {"query": "string", "top_k": "int"}
  },
  "llm:chat": {
    "type": "llm",
    "method": "chat",
    "description": "对话生成",
    "params": {"messages": "array"}
  }
}
```

**调用接口:**
```python
async def execute_tool(
    tool_name: str,
    params: Dict[str, Any],
    context: ExecutionContext
) -> ToolResult:
    """统一工具调用接口"""

    tool_type, *tool_path = tool_name.split(":")

    if tool_type == "mcp":
        return await execute_mcp_tool(tool_path, params)
    elif tool_type == "skill":
        return await execute_skill(tool_path[0], params)
    elif tool_type == "rag":
        return await execute_rag_method(tool_path[0], params)
    elif tool_type == "llm":
        return await execute_llm_method(tool_path[0], params, context)
    else:
        raise ValueError(f"Unknown tool type: {tool_type}")
```

---

### 7.4 状态管理器（State Manager）

**目标:** 持久化执行状态，支持恢复和回滚

**数据模型:**
```sql
-- 工作流表
CREATE TABLE workflows (
    id TEXT PRIMARY KEY,
    session_id TEXT NOT NULL,
    goal TEXT NOT NULL,
    plan JSON NOT NULL,
    status TEXT NOT NULL, -- pending/running/completed/failed/cancelled
    created_at TIMESTAMP,
    updated_at TIMESTAMP,
    completed_at TIMESTAMP,
    FOREIGN KEY (session_id) REFERENCES sessions(id)
);

-- 任务执行表
CREATE TABLE task_executions (
    id TEXT PRIMARY KEY,
    workflow_id TEXT NOT NULL,
    task_id TEXT NOT NULL,
    task_name TEXT NOT NULL,
    tool_name TEXT NOT NULL,
    params JSON NOT NULL,
    status TEXT NOT NULL, -- pending/running/success/failed/cancelled
    result JSON,
    error TEXT,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    retry_count INTEGER DEFAULT 0,
    FOREIGN KEY (workflow_id) REFERENCES workflows(id)
);

-- 审批记录表
CREATE TABLE workflow_approvals (
    id TEXT PRIMARY KEY,
    workflow_id TEXT NOT NULL,
    task_id TEXT NOT NULL,
    status TEXT NOT NULL, -- pending/approved/rejected
    user_comment TEXT,
    created_at TIMESTAMP,
    responded_at TIMESTAMP,
    FOREIGN KEY (workflow_id) REFERENCES workflows(id)
);
```

**状态恢复:**
- 系统崩溃后自动恢复
- 支持暂停和继续
- 失败任务重试

---

### 7.5 人机协同（Human-in-the-loop）

**目标:** 关键步骤需要用户审批

**审批触发条件:**
```python
# 任务定义时标记
{
  "id": "task-delete-files",
  "name": "删除临时文件",
  "tool": "mcp:filesystem:delete",
  "requires_approval": True,  # 危险操作需审批
  "approval_reason": "将删除 10 个文件"
}
```

**审批界面（Web）:**
```
┌───────────────────────────────────────┐
│ 🔔 任务审批请求                        │
├───────────────────────────────────────┤
│ 工作流: 清理临时文件                   │
│ 任务: task-delete-files               │
│                                       │
│ 即将执行:                             │
│ • 工具: filesystem:delete             │
│ • 参数:                               │
│   - paths: ["/tmp/a.txt", ...]       │
│   - count: 10 个文件                  │
│                                       │
│ ⚠️ 此操作不可撤销                     │
│                                       │
│ [✓ 批准] [✗ 拒绝] [⏸ 暂停工作流]     │
└───────────────────────────────────────┘
```

**审批界面（TUI）:**
- 使用 Textual 的 ModalScreen
- 显示任务详情和参数
- 支持批准/拒绝/暂停

---

## 实施计划

### 第一阶段：核心架构（2 周）

**Week 1: 数据模型和任务规划器**
- Task 7.1.1: 设计数据库表结构
- Task 7.1.2: 实现 Workflow/Task 模型
- Task 7.1.3: 实现工具注册表
- Task 7.1.4: 实现任务规划器（LLM Prompt）
- Task 7.1.5: 单元测试

**Week 2: 执行引擎**
- Task 7.2.1: 实现 DAG 调度器
- Task 7.2.2: 实现工具编排器
- Task 7.2.3: 实现参数变量替换
- Task 7.2.4: 实现状态持久化
- Task 7.2.5: 单元测试

**交付物:**
- 核心执行引擎
- 基础 API 接口
- 单元测试覆盖 80%+

---

### 第二阶段：人机协同（1.5 周）

**Week 3-4 (前半): 审批机制**
- Task 7.3.1: 实现审批请求生成
- Task 7.3.2: 实现 Web 审批界面
- Task 7.3.3: 实现 TUI 审批界面
- Task 7.3.4: 实现审批状态轮询
- Task 7.3.5: 集成测试

**交付物:**
- Web/TUI 审批界面
- 审批流程完整可用

---

### 第三阶段：容错与优化（1.5 周）

**Week 4 (后半)-5: 错误处理**
- Task 7.4.1: 实现任务重试机制
- Task 7.4.2: 实现工作流暂停/恢复
- Task 7.4.3: 实现任务回滚（可选）
- Task 7.4.4: 实现超时控制
- Task 7.4.5: 性能优化（并行执行）

**交付物:**
- 完整的错误处理
- 工作流恢复能力

---

### 第四阶段：文档与测试（1 周）

**Week 6: 完善与验收**
- Task 7.5.1: 端到端测试
- Task 7.5.2: 性能测试
- Task 7.5.3: 用户文档（README + 使用指南）
- Task 7.5.4: API 文档
- Task 7.5.5: 示例工作流

**交付物:**
- 完整文档
- 示例工作流 5+
- 验收报告

---

## 技术架构

### 目录结构
```
src/
├── workflow/
│   ├── __init__.py
│   ├── planner.py           # 任务规划器
│   ├── executor.py          # 执行引擎
│   ├── orchestrator.py      # 工具编排器
│   ├── state.py             # 状态管理
│   ├── approval.py          # 审批机制
│   └── models.py            # 数据模型
├── api/
│   └── workflow_routes.py   # API 路由
└── cli/
    └── workflow_ui.py       # TUI 审批界面

tests/
└── workflow/
    ├── test_planner.py
    ├── test_executor.py
    ├── test_orchestrator.py
    └── test_integration.py

docs/
└── workflow-guide.md        # 用户指南
```

### 核心类设计

```python
# src/workflow/models.py
@dataclass
class Task:
    id: str
    name: str
    tool: str
    params: Dict[str, Any]
    dependencies: List[str]
    requires_approval: bool = False
    status: TaskStatus = TaskStatus.PENDING
    result: Optional[Any] = None
    error: Optional[str] = None

@dataclass
class Workflow:
    id: str
    session_id: str
    goal: str
    tasks: List[Task]
    status: WorkflowStatus
    current_task_id: Optional[str] = None

# src/workflow/executor.py
class WorkflowExecutor:
    async def execute(self, workflow: Workflow) -> WorkflowResult:
        """执行工作流"""

    async def execute_task(self, task: Task, context: Context) -> TaskResult:
        """执行单个任务"""

    async def request_approval(self, task: Task) -> ApprovalResult:
        """请求用户审批"""

# src/workflow/planner.py
class WorkflowPlanner:
    async def plan(self, goal: str, context: PlanContext) -> Workflow:
        """生成执行计划"""
```

---

## API 设计

### 创建工作流
```http
POST /api/workflows
Content-Type: application/json

{
  "session_id": "session-123",
  "goal": "分析文档并生成报告",
  "auto_approve": false
}

Response 201:
{
  "workflow_id": "workflow-456",
  "status": "pending",
  "plan": {
    "tasks": [...]
  }
}
```

### 启动执行
```http
POST /api/workflows/{workflow_id}/start

Response 200:
{
  "status": "running",
  "current_task": "task-1"
}
```

### 获取状态
```http
GET /api/workflows/{workflow_id}

Response 200:
{
  "workflow_id": "workflow-456",
  "status": "waiting_approval",
  "progress": {
    "completed": 3,
    "total": 5
  },
  "pending_approval": {
    "task_id": "task-4",
    "task_name": "删除文件",
    "details": {...}
  }
}
```

### 审批任务
```http
POST /api/workflows/{workflow_id}/approvals/{task_id}
Content-Type: application/json

{
  "action": "approve",  // approve/reject
  "comment": "确认删除"
}

Response 200:
{
  "status": "approved",
  "workflow_status": "running"
}
```

---

## 示例场景

### 场景 1: 代码审查工作流
```
用户目标: 审查 PR #123 的代码变更

执行计划:
1. [mcp:github] 获取 PR 信息
2. [mcp:github] 获取文件变更列表
3. [mcp:filesystem] 读取变更的文件
4. [llm] 分析代码质量
5. [llm] 生成审查意见
6. [需审批] [mcp:github] 发布评论
```

### 场景 2: 文档分析与总结
```
用户目标: 分析项目文档，生成技术总结

执行计划:
1. [mcp:filesystem] 列出 docs/ 目录文件
2. [mcp:filesystem] 批量读取 Markdown 文件
3. [rag] 索引文档内容
4. [rag] 搜索关键技术点
5. [llm] 生成结构化总结
6. [skill:export] 导出为 PDF
```

### 场景 3: 数据处理流水线
```
用户目标: 处理 CSV 数据并生成报告

执行计划:
1. [mcp:filesystem] 读取 data.csv
2. [mcp:python] 数据清洗和转换
3. [mcp:python] 统计分析
4. [llm] 生成分析报告
5. [需审批] [mcp:filesystem] 保存结果
```

---

## 风险与挑战

### 技术风险
1. **LLM 规划质量** - 生成的计划可能不准确
   - 缓解: 计划验证 + 用户审查

2. **工具调用失败** - 外部工具可能出错
   - 缓解: 重试机制 + 降级策略

3. **状态一致性** - 并发执行和崩溃恢复
   - 缓解: 事务管理 + 状态日志

### 用户体验风险
1. **审批频繁** - 过多审批影响效率
   - 缓解: 智能判断 + 批量审批

2. **进度不透明** - 长时间执行无反馈
   - 缓解: 实时进度通知

---

## 成功标准

### 功能完整性
- ✅ 支持 5+ 种工具类型编排
- ✅ 支持 DAG 任务调度
- ✅ 支持人工审批流程
- ✅ 支持错误重试和恢复

### 性能指标
- ✅ 规划延迟 < 5 秒
- ✅ 任务调度延迟 < 100ms
- ✅ 支持 10+ 任务并行执行

### 质量指标
- ✅ 单元测试覆盖率 ≥ 80%
- ✅ 端到端测试 5+ 场景
- ✅ 文档完整（API + 用户指南）

---

## 参考资料

### 类似项目
- **AutoGPT** - 任务分解和自主执行
- **BabyAGI** - 目标驱动的任务生成
- **LangChain Agents** - 工具调用和编排
- **Semantic Kernel** - 微软的 Agent 框架

### 技术栈
- **LLM:** Claude 3.5 Sonnet（规划）
- **执行引擎:** asyncio + DAG
- **状态管理:** SQLite + JSON
- **Web UI:** FastAPI + React
- **TUI:** Textual

---

**文档版本:** v1.0
**最后更新:** 2026-03-06
