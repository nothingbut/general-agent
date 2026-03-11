# 工作流审批 TUI 界面

**功能概述:** 提供美观的终端用户界面（TUI）用于工作流任务审批

**模块:** `src/workflow/approval_ui.py`

---

## 功能特性

### 1. 审批请求展示

使用 Rich 库创建美观的审批界面：

```
┌─ 🔔 审批请求 - 需要读取文件 ────────────────┐
│                                              │
│  审批 ID      test-123...                    │
│  工作流 ID    workflow-456...                │
│  任务名称     读取配置文件                   │
│  工具         mcp:filesystem:read_file       │
│  参数         path: /etc/config.yaml         │
│                                              │
└──────────────────────────────────────────────┘
```

### 2. 用户交互

支持三种操作：
- **[Y] 批准** - 允许执行任务
- **[N] 拒绝** - 拒绝执行任务
- **[I] 更多信息** - 显示帮助说明

可选添加审批评论。

### 3. 审批结果显示

清晰显示审批决策：

```
┌─────────────────────────┐
│ ✓ 已批准                │
│ 理由: 已检查路径安全    │
└─────────────────────────┘
```

### 4. 审批历史

以表格形式显示审批记录：

```
┌─ 审批历史 ─────────────────────────────────────┐
│ 时间     │ 任务         │ 工具      │ 状态  │ 理由 │
├──────────┼──────────────┼───────────┼───────┼──────┤
│ 10:30:15 │ 读取文件     │ read_file │ ✓ 批准│ -    │
│ 10:30:45 │ 删除临时文件 │ delete    │ ✗ 拒绝│ 不安全│
└────────────────────────────────────────────────┘
```

---

## 使用方式

### 基本用法

```python
from src.workflow import ApprovalUI, ApprovalRequest

# 创建审批界面
ui = ApprovalUI()

# 创建审批请求
request = ApprovalRequest(
    approval_id="demo-001",
    workflow_id="workflow-123",
    task_id="task-1",
    task_name="读取配置文件",
    tool_name="mcp:filesystem:read_file",
    params={"path": "/etc/config.yaml"},
    reason="读取系统配置"
)

# 处理审批（等待用户输入）
result = await ui.handle_approval(request)

# 检查结果
if result.approved:
    print("任务已批准")
else:
    print(f"任务被拒绝: {result.comment}")
```

### 与审批管理器集成

```python
from src.workflow import ApprovalManager, ApprovalUI

# 创建审批管理器
manager = ApprovalManager(database=db)

# 注册 TUI 处理器
ui = ApprovalUI()
manager.register_handler(ui.handle_approval)

# 请求审批（自动调用 TUI）
result = await manager.request_approval(task, workflow)
```

### 自定义 Console

```python
from rich.console import Console

# 自定义 Console（例如输出到文件）
console = Console(file=open("approval.log", "w"))
ui = ApprovalUI(console=console)
```

---

## API 参考

### ApprovalUI

#### 构造函数

```python
ApprovalUI(console: Optional[Console] = None)
```

**参数:**
- `console`: Rich Console 实例（可选）

#### handle_approval

```python
async def handle_approval(request: ApprovalRequest) -> ApprovalResult
```

处理审批请求，显示界面并等待用户决策。

**参数:**
- `request`: ApprovalRequest 实例

**返回:**
- `ApprovalResult`: 包含审批决策和评论

#### display_approval_history

```python
def display_approval_history(approvals: list[dict]) -> None
```

显示审批历史记录。

**参数:**
- `approvals`: 审批记录列表

---

## 辅助函数

### create_approval_handler

```python
async def create_approval_handler(
    console: Optional[Console] = None
) -> callable
```

创建可注册到 ApprovalManager 的处理函数。

**返回:**
- 异步审批处理函数

### default_approval_handler

```python
async def default_approval_handler(request: ApprovalRequest) -> ApprovalResult
```

使用默认 UI 实例的审批处理函数。

---

## 示例程序

运行演示程序：

```bash
python examples/workflow_approval_demo.py
```

演示场景：
1. **基本审批流程** - 简单的文件读取审批
2. **危险操作审批** - 删除文件的审批
3. **与审批管理器集成** - 完整的审批流程
4. **审批历史显示** - 查看审批记录

---

## 设计决策

### 为什么使用 Rich 库？

- **美观**: 提供专业的终端 UI
- **跨平台**: 支持 Windows/macOS/Linux
- **易用**: 简洁的 API，易于集成
- **功能丰富**: Panel、Table、Markdown 等组件

### 为什么使用异步 I/O？

- **一致性**: 与审批管理器的异步接口匹配
- **非阻塞**: 支持并发多个审批请求
- **可扩展**: 未来可以支持异步通知

### 用户决策的线程处理

用户输入是同步阻塞的，因此使用 `run_in_executor` 在单独的线程中执行，避免阻塞事件循环。

```python
loop = asyncio.get_event_loop()
approved, comment = await loop.run_in_executor(
    None,
    self._get_user_decision
)
```

---

## 测试覆盖

**测试文件:** `tests/workflow/test_approval_ui.py`

**测试数量:** 22 个

**覆盖内容:**
- ✅ UI 初始化和配置
- ✅ 参数格式化（包括长值截断）
- ✅ 审批请求显示
- ✅ 用户决策处理（批准/拒绝/帮助）
- ✅ 审批结果显示
- ✅ 审批历史显示
- ✅ 处理函数创建
- ✅ 集成测试

**测试覆盖率:** 100% ✅

---

## 后续改进

### 短期（Week 3-4）

- [ ] 支持批量审批（一次批准多个任务）
- [ ] 添加审批超时倒计时显示
- [ ] 支持快捷键（Ctrl+Y/Ctrl+N）
- [ ] 添加任务详情展开/折叠

### 长期（Phase 7.2+）

- [ ] Web 审批界面
- [ ] 移动端推送通知
- [ ] 审批规则引擎（自动审批符合规则的任务）
- [ ] 审批委托（转发给其他用户）
- [ ] 审批历史搜索和过滤

---

## 相关文档

- [审批管理器](./approval.md) - 审批核心逻辑
- [工作流执行器](./executor.md) - 任务执行引擎
- [Phase 7 设计文档](../plans/2026-03-06-phase7-agent-workflow.md)

---

**版本:** v1.0
**创建日期:** 2026-03-08
**维护者:** Phase 7 Team
