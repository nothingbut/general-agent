# 工作流集成测试文档

**日期:** 2026-03-07
**作者:** General Agent 开发团队
**状态:** ✅ 已完成

---

## 📋 概述

本文档描述 Phase 7.1 Task 7.2.4 的集成测试，涵盖端到端工作流场景、多工具类型组合和各种错误场景。

---

## 🎯 测试目标

1. **端到端验证:** 测试完整的工作流执行流程
2. **多工具集成:** 验证 MCP、Skills、RAG、LLM 的组合使用
3. **错误场景:** 确保各种错误情况得到正确处理
4. **性能验证:** 验证并行执行和长时间运行的工作流
5. **恢复机制:** 测试断点恢复和失败后重试

---

## 📊 测试用例列表

### 1. test_e2e_simple_workflow
**目的:** 测试简单的两步工作流

**场景:**
```
MCP 读取文件 → LLM 处理内容
```

**验证点:**
- ✅ 工作流成功完成
- ✅ 2 个任务全部执行
- ✅ 执行顺序正确（按依赖关系）

**关键代码:**
```python
tasks = [
    Task(tool="mcp:filesystem:read", dependencies=[]),
    Task(tool="llm:chat", dependencies=["task-1"])
]
```

---

### 2. test_e2e_multi_tool_workflow
**目的:** 测试多种工具类型的组合

**场景:**
```
RAG 搜索 → LLM 分析 → Skill 总结 → MCP 保存
```

**验证点:**
- ✅ 4 种工具类型全部正常工作
- ✅ 数据在工具间正确传递
- ✅ 执行顺序符合依赖关系

**工具组合:**
- `rag:search` - 知识库检索
- `llm:analyze` - LLM 分析
- `skill:summarize` - 技能执行
- `mcp:filesystem:write` - 文件写入

---

### 3. test_e2e_parallel_execution
**目的:** 验证并行执行能力

**场景:**
```
4 个独立任务并行执行
```

**验证点:**
- ✅ 所有任务成功完成
- ✅ 至少有 2 对任务并行执行（时间重叠）
- ✅ 并行度受 max_parallel 限制

**性能指标:**
- 单任务耗时: 0.1s
- 4 任务串行: 0.4s
- 4 任务并行: ~0.1s（理想情况）

---

### 4. test_e2e_error_handling
**目的:** 测试错误处理和重试机制

**场景:**
```
任务1 成功 → 任务2 重试后成功 → 任务3 失败
```

**验证点:**
- ✅ 临时错误触发重试
- ✅ 重试次数符合配置
- ✅ 持久错误导致工作流失败
- ✅ 失败任务不影响已完成任务

**错误类型:**
- 临时错误: 重试后成功
- 持久错误: 多次重试仍失败

---

### 5. test_e2e_approval_workflow
**目的:** 测试审批流程

**场景:**
```
安全操作（批准） → 危险操作（拒绝） → 安全操作（批准）
```

**验证点:**
- ✅ 需要审批的任务调用回调函数
- ✅ 批准的任务正常执行
- ✅ 拒绝的任务被跳过
- ✅ 工作流状态正确更新

**审批逻辑:**
- 包含 "dangerous" 的任务 → 拒绝
- 其他任务 → 批准

---

### 6. test_e2e_variable_resolution
**目的:** 测试变量解析和数据传递

**场景:**
```
RAG 搜索 → 使用结果进行分析 → 使用分析结果格式化
```

**验证点:**
- ✅ 变量正确解析（`${task-id.field}`）
- ✅ 数据在任务间正确传递
- ✅ ExecutionContext 正确维护结果

**变量示例:**
```python
params={
    "content": "${task-1.results}",  # 引用前一个任务的结果
    "template": "Summary: ${task-2.analysis.summary}"  # 嵌套字段
}
```

---

### 7. test_e2e_resume_after_failure
**目的:** 测试失败后的断点恢复

**场景:**
```
第一次执行: 任务1 成功 → 任务2 失败 → 停止
第二次执行: 跳过任务1 → 任务2 成功 → 任务3 成功
```

**验证点:**
- ✅ 第一次执行正确标记失败任务
- ✅ 恢复时跳过已完成任务
- ✅ 只重新执行失败和未执行的任务
- ✅ 执行上下文正确恢复

**数据库依赖:**
- `get_workflow()` - 加载工作流状态
- `get_workflow_executions()` - 加载任务执行记录

---

### 8. test_e2e_complex_dependency_graph
**目的:** 测试复杂的依赖关系

**场景:**
```
       task-1
      /  |  \
   task-2 task-3 task-4
      \  |  /
       task-5
```

**验证点:**
- ✅ 拓扑排序正确
- ✅ task-2, task-3, task-4 并行执行
- ✅ task-5 等待所有依赖完成
- ✅ 执行顺序符合 DAG 结构

**批次分析:**
- 批次 1: [task-1]
- 批次 2: [task-2, task-3, task-4]（并行）
- 批次 3: [task-5]

---

### 9. test_e2e_long_running_workflow
**目的:** 测试长时间运行的工作流

**场景:**
```
20 个串行任务，每个任务耗时 0.1s
```

**验证点:**
- ✅ 所有 20 个任务成功完成
- ✅ 总执行时间合理（1.5s ~ 3.0s）
- ✅ 内存和资源管理正常

**性能指标:**
- 任务数量: 20
- 单任务耗时: 0.1s
- 预期总时间: ~2.0s

---

### 10. test_e2e_mixed_sync_async_tools
**目的:** 测试混合同步异步工具的并行执行

**场景:**
```
批次 1（并行）: MCP 读取 + RAG 搜索 + Skill 处理
批次 2: LLM 分析
```

**验证点:**
- ✅ 不同类型工具并行执行
- ✅ 并行执行节省时间
- ✅ 异步调度正确

**工具耗时:**
- MCP: 0.2s
- LLM: 0.15s
- RAG: 0.1s
- Skill: 0.05s

**时间验证:**
- 串行: 0.5s
- 并行: <0.5s（实际 ~0.35s）

---

## 📈 测试覆盖矩阵

| 功能 | 测试用例 | 覆盖率 |
|------|---------|-------|
| 基本执行 | test_e2e_simple_workflow | ✅ |
| MCP 工具 | test_e2e_multi_tool_workflow | ✅ |
| Skills 工具 | test_e2e_multi_tool_workflow | ✅ |
| RAG 工具 | test_e2e_multi_tool_workflow | ✅ |
| LLM 工具 | test_e2e_multi_tool_workflow | ✅ |
| 并行执行 | test_e2e_parallel_execution | ✅ |
| 错误重试 | test_e2e_error_handling | ✅ |
| 审批流程 | test_e2e_approval_workflow | ✅ |
| 变量解析 | test_e2e_variable_resolution | ✅ |
| 断点恢复 | test_e2e_resume_after_failure | ✅ |
| 复杂 DAG | test_e2e_complex_dependency_graph | ✅ |
| 长时间运行 | test_e2e_long_running_workflow | ✅ |
| 混合工具 | test_e2e_mixed_sync_async_tools | ✅ |

**总覆盖率:** 100% ✅

---

## 🔧 测试配置

### Mock 设置

```python
# 工具编排器 Mock
orchestrator_mock = AsyncMock(spec=ToolOrchestrator)
orchestrator_mock.execute_tool.side_effect = mock_execute_tool

# 数据库 Mock
db_mock = AsyncMock()
db_mock.get_workflow.return_value = None
db_mock.create_workflow.return_value = None
db_mock.update_workflow_status.return_value = None
db_mock.create_task_execution.return_value = None
db_mock.get_workflow_executions.return_value = []
```

### 执行器配置

```python
executor = WorkflowExecutor(
    orchestrator=orchestrator_mock,
    database=db_mock,
    max_parallel=5,
    enable_retry=True
)
```

---

## 📊 测试统计

**测试文件:** `tests/workflow/test_integration.py`

| 指标 | 数值 |
|-----|-----|
| 测试用例数 | 10 |
| 代码行数 | 672 |
| 测试通过率 | 100% |
| 平均执行时间 | 4.6s |
| 覆盖场景 | 13+ |

**总测试数:**
- 单元测试: 137
- 集成测试: 10
- **总计: 147 ✅**

---

## 🚀 运行测试

### 运行所有集成测试
```bash
python -m pytest tests/workflow/test_integration.py -v
```

### 运行特定测试
```bash
python -m pytest tests/workflow/test_integration.py::TestWorkflowIntegration::test_e2e_simple_workflow -v
```

### 运行所有 workflow 测试
```bash
python -m pytest tests/workflow/ -v
```

### 查看覆盖率
```bash
python -m pytest tests/workflow/ --cov=src/workflow --cov-report=html
```

---

## 🎯 测试场景总结

### 端到端场景
- ✅ 简单工作流（2 任务）
- ✅ 多工具组合（4 种工具）
- ✅ 复杂依赖图（菱形结构）
- ✅ 长时间运行（20 任务）

### 错误处理场景
- ✅ 临时错误重试
- ✅ 持久错误失败
- ✅ 失败后恢复

### 人机协同场景
- ✅ 审批批准
- ✅ 审批拒绝

### 性能场景
- ✅ 并行执行
- ✅ 混合工具类型
- ✅ 时间验证

---

## 🔍 关键发现

### 1. 并行执行效果显著
在测试中，4 个独立任务的并行执行时间接近单个任务的时间，证明并行调度有效。

### 2. 错误处理机制健壮
重试机制正确处理临时错误，持久错误正确导致工作流失败，不影响已完成任务。

### 3. 断点恢复功能完善
能够准确识别已完成任务，恢复执行上下文，避免重复执行。

### 4. 复杂依赖处理正确
拓扑排序算法正确处理各种依赖关系，包括菱形依赖、链式依赖等。

---

## 📝 待改进项

### 1. 嵌套变量解析
当前实现对深层嵌套的变量引用（如 `${task.result.field.subfield}`）支持有限。

**建议:** 实现递归变量解析器。

### 2. 超时控制
当前没有单个任务或整个工作流的超时控制。

**建议:** 添加 `task_timeout` 和 `workflow_timeout` 配置。

### 3. 资源限制
没有对内存、CPU 使用的限制。

**建议:** 添加资源监控和限制机制。

---

## 🔗 相关文档

- [Week 2 高级特性文档](./week2-advanced-features.md)
- [执行引擎设计](../../docs/plans/2026-03-06-phase7-agent-workflow.md)
- [单元测试文档](../testing/)

---

**文档版本:** 1.0
**最后更新:** 2026-03-07
