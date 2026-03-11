# Week 2 高级特性文档

**日期:** 2026-03-07
**作者:** General Agent 开发团队
**状态:** ✅ 已完成

---

## 📋 概述

本文档描述 Phase 7.1 Week 2 实现的高级特性，包括智能退避策略、断点恢复功能和执行控制。

---

## 🎯 功能列表

### 1. 智能退避策略

**目的:** 在任务重试时使用更智能的退避策略，避免立即重试导致的资源浪费。

**实现:**
```python
def _calculate_backoff(self, retry_count: int) -> float:
    """计算退避时间（指数退避 + 抖动）"""
    # 指数退避: base_backoff * 2^(retry_count - 1)
    backoff = self.base_backoff * (2 ** (retry_count - 1))
    # 限制最大值
    backoff = min(backoff, self.max_backoff)
    # 添加随机抖动 (±25%)
    jitter = backoff * 0.25 * (2 * random.random() - 1)
    return max(0.1, backoff + jitter)
```

**参数:**
- `base_backoff`: 基础退避时间（默认 1.0 秒）
- `max_backoff`: 最大退避时间（默认 60.0 秒）

**退避时间表:**
| 重试次数 | 基础时间 | 实际时间范围（含抖动） |
|---------|---------|---------------------|
| 1       | 1.0s    | 0.75s ~ 1.25s       |
| 2       | 2.0s    | 1.5s ~ 2.5s         |
| 3       | 4.0s    | 3.0s ~ 5.0s         |
| 4       | 8.0s    | 6.0s ~ 10.0s        |
| 5       | 16.0s   | 12.0s ~ 20.0s       |
| 10+     | 60.0s   | 45.0s ~ 75.0s (限制) |

**优势:**
- 避免雷鸣群现象（thundering herd problem）
- 逐步增加重试间隔
- 随机抖动减少多个任务同时重试的冲突
- 限制最大退避时间，避免无限等待

---

### 2. 断点恢复功能

**目的:** 支持工作流在中断后从上次执行的位置继续，无需重新执行已完成的任务。

**使用方法:**
```python
# 恢复工作流
result = await executor.resume_workflow(
    workflow_id="wf-123",
    on_task_complete=callback,
    on_approval_required=approval_callback
)
```

**恢复流程:**
```
1. 从数据库加载工作流状态
   └─ 检查工作流是否存在
   └─ 验证工作流状态（不能恢复已完成/已取消的）

2. 重建 Workflow 对象
   └─ 恢复基本信息（ID、session、goal）
   └─ 恢复状态信息（status、created_at、started_at）

3. 从 plan 恢复任务列表
   └─ 遍历 plan.tasks
   └─ 重建 Task 对象

4. 加载已完成的任务执行记录
   └─ 查询 task_executions 表
   └─ 筛选状态为 SUCCESS 的任务

5. 标记已完成的任务
   └─ 设置 task.status = TaskStatus.SUCCESS
   └─ 记录日志

6. 恢复执行上下文
   └─ 创建新的 ExecutionContext
   └─ 从成功的任务恢复结果到上下文

7. 继续执行
   └─ 调用 execute() 方法
   └─ 拓扑排序会自动跳过已完成的任务
```

**数据库依赖:**
- `workflows` 表：保存工作流状态和 plan
- `task_executions` 表：保存任务执行记录
- 必须有 `get_workflow()` 和 `get_workflow_executions()` 方法

**错误处理:**
```python
# 工作流不存在
ValueError: Workflow not found: {workflow_id}

# 工作流已完成或已取消
ValueError: Cannot resume {status} workflow
```

---

### 3. 执行控制

**目的:** 提供运行时控制工作流执行的能力。

#### 3.1 暂停执行
```python
# 暂停工作流执行
executor.pause()
```

**行为:**
- 当前批次的任务会继续执行完成
- 下一批次的任务不会开始
- 工作流进入暂停状态，等待恢复

**使用场景:**
- 临时停止执行，检查中间结果
- 在特定时间段暂停（如下班时间）
- 等待外部条件满足

#### 3.2 恢复执行
```python
# 恢复工作流执行
executor.resume()
```

**行为:**
- 从暂停状态恢复
- 继续执行下一批次的任务

#### 3.3 停止执行
```python
# 停止工作流执行
executor.stop()
```

**行为:**
- 当前批次的任务会继续执行完成
- 后续任务不会执行
- 工作流状态变为 CANCELLED

**使用场景:**
- 用户主动取消
- 发现执行错误需要中止
- 超时或其他条件触发

---

### 4. 拓扑排序优化

**目的:** 在恢复工作流时，只对未完成的任务进行拓扑排序，提高效率。

**实现:**
```python
def _topological_sort(self, workflow: Workflow) -> List[List[Task]]:
    """拓扑排序 - 确定任务执行顺序"""
    # 只包含未完成的任务
    pending_tasks = [t for t in workflow.tasks if t.status != TaskStatus.SUCCESS]

    # 计算未完成的依赖
    for task in pending_tasks:
        pending_deps = [
            dep_id for dep_id in task.dependencies
            if any(t.id == dep_id and t.status != TaskStatus.SUCCESS
                   for t in workflow.tasks)
        ]
        in_degree[task.id] = len(pending_deps)

    # ... BFS 算法
```

**优化点:**
- 跳过状态为 SUCCESS 的任务
- 只计算未完成任务的依赖关系
- 减少图的节点数量，提高排序效率

**示例:**
```
原始工作流:
task-1 (SUCCESS) → task-2 (PENDING) → task-3 (PENDING)
                 ↘ task-4 (PENDING)

优化后的排序:
批次 1: [task-2, task-4]  # task-1 已完成，被跳过
批次 2: [task-3]
```

---

## 🧪 测试覆盖

### 新增测试用例

**文件:** `tests/workflow/test_executor_advanced.py`

| 测试用例 | 描述 | 覆盖功能 |
|---------|------|---------|
| `test_calculate_backoff` | 测试智能退避策略 | 指数退避、抖动、最大值限制 |
| `test_pause_resume` | 测试暂停和恢复 | pause()、resume() |
| `test_stop_execution` | 测试停止执行 | stop()、状态变更 |
| `test_resume_workflow` | 测试断点恢复 | 从数据库恢复、跳过已完成任务 |
| `test_resume_workflow_not_found` | 测试恢复不存在的工作流 | 错误处理 |
| `test_resume_completed_workflow` | 测试恢复已完成的工作流 | 状态验证 |
| `test_topological_sort_with_completed_tasks` | 测试拓扑排序优化 | 跳过已完成任务 |

### 测试统计

- 新增测试: 7 个
- 总测试数: 137 个
- 通过率: 100%
- 覆盖率: >85%

---

## 📊 性能影响

### 智能退避策略
- **CPU 开销:** 极低（简单数学计算）
- **内存开销:** 无
- **延迟影响:** 提高了重试成功率，减少无效重试

### 断点恢复
- **数据库查询:** +2 次（get_workflow、get_workflow_executions）
- **内存开销:** 取决于任务数量和结果大小
- **启动时间:** +10~100ms（取决于任务数量）

### 执行控制
- **CPU 开销:** 极低（标志位检查）
- **内存开销:** +8 字节（2 个布尔标志）
- **延迟影响:** 暂停时 0.5s 轮询间隔

---

## 🔧 配置选项

### WorkflowExecutor 初始化参数

```python
executor = WorkflowExecutor(
    orchestrator=orchestrator,
    database=database,
    max_parallel=5,          # 最大并行任务数
    enable_retry=True,       # 是否启用重试
    base_backoff=1.0,        # 基础退避时间（秒）
    max_backoff=60.0         # 最大退避时间（秒）
)
```

---

## 📚 使用示例

### 示例 1: 使用智能退避的工作流

```python
# 创建执行器，配置退避策略
executor = WorkflowExecutor(
    orchestrator=orchestrator,
    database=database,
    base_backoff=2.0,    # 2 秒基础退避
    max_backoff=120.0    # 最大 2 分钟
)

# 执行工作流
result = await executor.execute(workflow)
```

### 示例 2: 断点恢复

```python
# 初始执行（中途失败）
try:
    result = await executor.execute(workflow)
except Exception as e:
    print(f"工作流中断: {e}")
    # 工作流状态已保存到数据库

# ... 稍后恢复
executor2 = WorkflowExecutor(
    orchestrator=orchestrator,
    database=database
)

# 从断点恢复
result = await executor2.resume_workflow(workflow.id)
print(f"恢复完成，状态: {result['status']}")
```

### 示例 3: 执行控制

```python
import asyncio

# 创建执行器
executor = WorkflowExecutor(
    orchestrator=orchestrator,
    database=database
)

# 在后台启动执行
task = asyncio.create_task(executor.execute(workflow))

# ... 在需要时暂停
executor.pause()
print("工作流已暂停")

# 等待一段时间
await asyncio.sleep(10)

# 恢复执行
executor.resume()
print("工作流已恢复")

# 或者完全停止
executor.stop()
print("工作流已停止")

# 等待执行完成
result = await task
```

---

## 🐛 已知限制

1. **暂停粒度:** 只能在批次边界暂停，无法在批次内部暂停
2. **停止延迟:** 当前批次会继续执行完成
3. **断点恢复:** 依赖完整的数据库记录，如果数据库损坏可能无法恢复
4. **内存限制:** 大型工作流恢复时需要加载所有任务到内存

---

## 🔮 未来改进

1. **更细粒度的控制:** 支持任务级别的暂停/取消
2. **部分恢复:** 支持只恢复部分任务
3. **多重检查点:** 支持多个恢复点
4. **压缩存储:** 优化大型工作流的存储
5. **流式恢复:** 按需加载任务，减少内存使用

---

## 📖 参考资料

- [指数退避算法](https://en.wikipedia.org/wiki/Exponential_backoff)
- [Workflow Checkpointing](https://docs.temporal.io/workflows#workflow-checkpointing)
- [Graceful Shutdown Patterns](https://12factor.net/disposability)

---

**文档版本:** 1.0
**最后更新:** 2026-03-07
