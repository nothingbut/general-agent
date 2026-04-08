# 计划任务功能设计

## 概述

用户需要能够创建定时执行的任务，用于自动化重复性工作。任务可以是技能调用、记忆提醒、或自定义命令。系统应支持标准 cron 表达式和自然语言时间描述，提供完整的任务生命周期管理。

## 设计目标

1. **灵活的调度语法**：支持 cron 表达式和自然语言描述
2. **多种任务类型**：技能调用、记忆提醒、自定义命令
3. **完整的生命周期管理**：创建、暂停、恢复、取消、删除
4. **可靠的执行引擎**：重试机制、超时控制、错误处理
5. **详细的执行历史**：记录每次执行的结果和日志
6. **后台服务集成**：与现有系统无缝集成

## 数据模型设计

### 1. ScheduledTask 模型（任务定义）

```csharp
public class ScheduledTask
{
    public Guid Id { get; set; }
    public string Name { get; set; }                  // 任务名称
    public string Description { get; set; }           // 任务描述
    public string OwnerId { get; set; }               // 任务所有者
    
    // 调度配置
    public string Schedule { get; set; }              // cron 表达式或自然语言
    public ScheduleType ScheduleType { get; set; }    // Cron / Natural
    public DateTime? StartAt { get; set; }            // 开始时间（可选）
    public DateTime? EndAt { get; set; }              // 结束时间（可选）
    
    // 任务配置
    public TaskType TaskType { get; set; }            // 任务类型
    public string TaskPayload { get; set; }           // JSON 格式的任务参数
    public int MaxRetries { get; set; }               // 最大重试次数
    public int TimeoutSeconds { get; set; }           // 超时时间（秒）
    
    // 状态管理
    public TaskStatus Status { get; set; }            // 任务状态
    public DateTime CreatedAt { get; set; }           // 创建时间
    public DateTime? UpdatedAt { get; set; }          // 更新时间
    public DateTime? LastExecutionAt { get; set; }    // 上次执行时间
    public DateTime? NextExecutionAt { get; set; }    // 下次执行时间
    public int ExecutionCount { get; set; }           // 已执行次数
    
    // 元数据
    public string? Tags { get; set; }                 // 标签（逗号分隔）
    public string? Metadata { get; set; }             // 其他元数据（JSON）
}

public enum ScheduleType
{
    Cron = 0,       // 标准 cron 表达式："0 9 * * 1-5"
    Natural = 1     // 自然语言："每天早上9点"、"每周一下午3点"
}

public enum TaskType
{
    SkillInvocation = 0,   // 调用技能：@skill-name arg="value"
    MemoryReminder = 1,    // 记忆提醒：发送通知或创建会话
    CustomCommand = 2      // 自定义命令：执行 CLI 命令
}

public enum TaskStatus
{
    Pending = 0,     // 待执行
    Running = 1,     // 执行中
    Completed = 2,   // 已完成（一次性任务）
    Failed = 3,      // 失败（超过最大重试次数）
    Paused = 4,      // 已暂停
    Cancelled = 5    // 已取消
}
```

### 2. TaskExecution 模型（执行历史）

```csharp
public class TaskExecution
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }                  // 关联的任务 ID
    
    // 执行信息
    public DateTime StartedAt { get; set; }           // 开始时间
    public DateTime? CompletedAt { get; set; }        // 完成时间
    public ExecutionStatus Status { get; set; }       // 执行状态
    
    // 结果和日志
    public string? Output { get; set; }               // 执行输出
    public string? Error { get; set; }                // 错误信息
    public int RetryCount { get; set; }               // 重试次数
    public long? DurationMs { get; set; }             // 执行耗时（毫秒）
    
    // 元数据
    public string? Metadata { get; set; }             // 其他元数据（JSON）
}

public enum ExecutionStatus
{
    Pending = 0,     // 待执行
    Running = 1,     // 执行中
    Completed = 2,   // 成功完成
    Failed = 3,      // 失败
    Timeout = 4,     // 超时
    Cancelled = 5    // 取消
}
```

### 3. 数据库架构

**scheduled_tasks 表：**
```sql
CREATE TABLE scheduled_tasks (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    owner_id TEXT NOT NULL,
    
    schedule TEXT NOT NULL,
    schedule_type INTEGER NOT NULL,
    start_at TEXT NULL,
    end_at TEXT NULL,
    
    task_type INTEGER NOT NULL,
    task_payload TEXT NOT NULL,
    max_retries INTEGER NOT NULL DEFAULT 3,
    timeout_seconds INTEGER NOT NULL DEFAULT 300,
    
    status INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL,
    last_execution_at TEXT NULL,
    next_execution_at TEXT NULL,
    execution_count INTEGER NOT NULL DEFAULT 0,
    
    tags TEXT NULL,
    metadata TEXT NULL
);

CREATE INDEX idx_owner_id ON scheduled_tasks(owner_id);
CREATE INDEX idx_status ON scheduled_tasks(status);
CREATE INDEX idx_next_execution ON scheduled_tasks(next_execution_at);
CREATE INDEX idx_created_at ON scheduled_tasks(created_at);
```

**task_executions 表：**
```sql
CREATE TABLE task_executions (
    id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    
    started_at TEXT NOT NULL,
    completed_at TEXT NULL,
    status INTEGER NOT NULL DEFAULT 0,
    
    output TEXT NULL,
    error TEXT NULL,
    retry_count INTEGER NOT NULL DEFAULT 0,
    duration_ms INTEGER NULL,
    
    metadata TEXT NULL,
    
    FOREIGN KEY (task_id) REFERENCES scheduled_tasks(id) ON DELETE CASCADE
);

CREATE INDEX idx_task_id ON task_executions(task_id);
CREATE INDEX idx_started_at ON task_executions(started_at);
CREATE INDEX idx_status ON task_executions(status);
```

## 核心功能设计

### 1. 任务调度引擎

**TaskScheduler（核心调度器）**

```csharp
public interface ITaskScheduler
{
    // 启动调度器
    Task StartAsync(CancellationToken cancellationToken = default);
    
    // 停止调度器
    Task StopAsync(CancellationToken cancellationToken = default);
    
    // 添加任务到调度队列
    Task ScheduleTaskAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    
    // 移除任务
    Task UnscheduleTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    
    // 暂停任务
    Task PauseTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    
    // 恢复任务
    Task ResumeTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    
    // 手动触发任务执行
    Task TriggerTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
}
```

**实现策略：**
- **方案 A（推荐）：Quartz.NET**
  - 优点：功能强大、生产级、社区支持好
  - 缺点：依赖较重（~1MB）
  - 适用场景：需要复杂调度功能（分布式、集群等）

- **方案 B（推荐）：自实现 + System.Threading.Timer**
  - 优点：轻量级、完全控制、无外部依赖
  - 缺点：需要自己实现调度逻辑
  - 适用场景：单机部署、调度需求简单

**建议：采用方案 B（自实现）** 
理由：
1. V3 是单机应用，不需要分布式调度
2. 调度需求相对简单（主要是 cron 表达式）
3. 减少外部依赖，保持项目轻量
4. 可使用 Cronos 库解析 cron 表达式

### 2. 任务执行引擎

**TaskExecutor（执行器）**

```csharp
public interface ITaskExecutor
{
    // 执行任务
    Task<TaskExecution> ExecuteAsync(
        ScheduledTask task,
        CancellationToken cancellationToken = default);
}

public class TaskExecutor : ITaskExecutor
{
    private readonly ISkillRegistry _skillRegistry;
    private readonly IMemoryService _memoryService;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILogger<TaskExecutor> _logger;
    
    public async Task<TaskExecution> ExecuteAsync(
        ScheduledTask task,
        CancellationToken cancellationToken)
    {
        var execution = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            StartedAt = DateTime.UtcNow,
            Status = ExecutionStatus.Running
        };
        
        try
        {
            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(task.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);
            
            // 根据任务类型执行
            string output = task.TaskType switch
            {
                TaskType.SkillInvocation => await ExecuteSkillAsync(task, linkedCts.Token),
                TaskType.MemoryReminder => await ExecuteReminderAsync(task, linkedCts.Token),
                TaskType.CustomCommand => await ExecuteCommandAsync(task, linkedCts.Token),
                _ => throw new NotSupportedException($"Unsupported task type: {task.TaskType}")
            };
            
            execution.Output = output;
            execution.Status = ExecutionStatus.Completed;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            execution.Status = ExecutionStatus.Timeout;
            execution.Error = $"Task timed out after {task.TimeoutSeconds} seconds";
        }
        catch (Exception ex)
        {
            execution.Status = ExecutionStatus.Failed;
            execution.Error = ex.ToString();
        }
        finally
        {
            execution.CompletedAt = DateTime.UtcNow;
            execution.DurationMs = (long)(execution.CompletedAt.Value - execution.StartedAt).TotalMilliseconds;
        }
        
        return execution;
    }
    
    private async Task<string> ExecuteSkillAsync(ScheduledTask task, CancellationToken ct)
    {
        // 解析 task.TaskPayload (JSON) 获取技能名称和参数
        // 调用 ISkillRegistry.ExecuteAsync
        // 返回执行结果
    }
    
    private async Task<string> ExecuteReminderAsync(ScheduledTask task, CancellationToken ct)
    {
        // 解析 task.TaskPayload 获取记忆内容
        // 创建新会话或发送通知
        // 返回提醒结果
    }
    
    private async Task<string> ExecuteCommandAsync(ScheduledTask task, CancellationToken ct)
    {
        // 解析 task.TaskPayload 获取命令字符串
        // 执行命令（需要安全验证）
        // 返回命令输出
    }
}
```

### 3. 任务管理服务

**TaskManager（任务管理）**

```csharp
public interface ITaskManager
{
    // 创建任务
    Task<ScheduledTask> CreateTaskAsync(
        string name,
        string description,
        string schedule,
        ScheduleType scheduleType,
        TaskType taskType,
        string taskPayload,
        string ownerId,
        CreateTaskOptions? options = null,
        CancellationToken cancellationToken = default);
    
    // 列出任务
    Task<List<ScheduledTask>> ListTasksAsync(
        string ownerId,
        TaskStatus? filterByStatus = null,
        CancellationToken cancellationToken = default);
    
    // 获取任务详情
    Task<ScheduledTask?> GetTaskAsync(
        Guid taskId,
        string ownerId,
        CancellationToken cancellationToken = default);
    
    // 更新任务
    Task<ScheduledTask> UpdateTaskAsync(
        Guid taskId,
        string ownerId,
        UpdateTaskOptions options,
        CancellationToken cancellationToken = default);
    
    // 取消任务
    Task CancelTaskAsync(
        Guid taskId,
        string ownerId,
        CancellationToken cancellationToken = default);
    
    // 删除任务
    Task DeleteTaskAsync(
        Guid taskId,
        string ownerId,
        CancellationToken cancellationToken = default);
    
    // 获取执行历史
    Task<List<TaskExecution>> GetExecutionHistoryAsync(
        Guid taskId,
        string ownerId,
        int? limit = null,
        CancellationToken cancellationToken = default);
}

public class CreateTaskOptions
{
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 300;
    public string? Tags { get; set; }
}

public class UpdateTaskOptions
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Schedule { get; set; }
    public ScheduleType? ScheduleType { get; set; }
    public string? TaskPayload { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public int? MaxRetries { get; set; }
    public int? TimeoutSeconds { get; set; }
}
```

### 4. Cron 和自然语言解析

**CronParser（Cron 表达式解析）**

使用 **Cronos** 库：
```bash
dotnet add package Cronos
```

```csharp
using Cronos;

public interface ICronParser
{
    // 解析 cron 表达式
    CronExpression Parse(string cronExpression);
    
    // 计算下次执行时间
    DateTime? GetNextOccurrence(CronExpression cron, DateTime from);
    
    // 验证 cron 表达式
    bool IsValid(string cronExpression);
}
```

**NaturalLanguageTimeParser（自然语言解析）**

```csharp
public interface INaturalLanguageTimeParser
{
    // 解析自然语言时间描述为 cron 表达式
    // "每天早上9点" -> "0 9 * * *"
    // "每周一下午3点" -> "0 15 * * 1"
    // "每月1号晚上8点" -> "0 20 1 * *"
    string ParseToCron(string naturalLanguage);
    
    // 验证自然语言是否可解析
    bool CanParse(string naturalLanguage);
}
```

**支持的自然语言模式：**
```
每天 [HH:mm] -> "0 mm HH * * *"
每周 [星期X] [HH:mm] -> "0 mm HH * * X"
每月 [DD号] [HH:mm] -> "0 mm HH DD * *"
每小时 -> "0 0 * * * *"
每 [N] 分钟 -> "0 */N * * * *"
```

## 用户场景

### 场景 1：创建每日提醒任务

```bash
# CLI 命令
/task schedule "每天 9:00" --type reminder --name "每日站会提醒" \
  --payload '{"message": "10分钟后开始每日站会"}'

# 或使用 cron 表达式
/task schedule "0 9 * * *" --type reminder --name "每日站会提醒" \
  --payload '{"message": "10分钟后开始每日站会"}'
```

### 场景 2：定时调用技能

```bash
# 每周五下午5点生成周报
/task schedule "每周五 17:00" --type skill --name "周报生成" \
  --payload '{"skill": "productivity:weekly-report", "args": {}}'
```

### 场景 3：定时执行命令

```bash
# 每小时检查服务健康状态
/task schedule "0 * * * *" --type command --name "健康检查" \
  --payload '{"command": "curl -f http://localhost:8080/health"}'
```

### 场景 4：管理任务

```bash
# 列出所有任务
/task list

# 查看任务详情
/task show <task-id>

# 暂停任务
/task pause <task-id>

# 恢复任务
/task resume <task-id>

# 取消任务
/task cancel <task-id>

# 查看执行历史
/task history <task-id>

# 立即执行任务（不等待下次调度）
/task run <task-id>
```

## CLI 命令设计

### TaskCommand 结构

```
/task
├── schedule <schedule> --type <type> --name <name> --payload <json>
│   [--start <datetime>] [--end <datetime>]
│   [--max-retries <n>] [--timeout <seconds>]
│   [--tags <tag1,tag2>]
├── list [--status <status>] [--format <table|json>]
├── show <task-id> [--format <table|json>]
├── update <task-id> [--name <name>] [--schedule <schedule>]
│   [--payload <json>] [--max-retries <n>] [--timeout <seconds>]
├── pause <task-id>
├── resume <task-id>
├── cancel <task-id>
├── delete <task-id>
├── run <task-id>  # 立即执行
└── history <task-id> [--limit <n>] [--format <table|json>]
```

### 输出格式示例

**任务列表：**
```
┌──────────────────────────────────────┬────────────────┬─────────────┬──────────────────────┬─────────┐
│ ID                                   │ Name           │ Schedule    │ Next Execution       │ Status  │
├──────────────────────────────────────┼────────────────┼─────────────┼──────────────────────┼─────────┤
│ abc123...                            │ 每日站会提醒    │ 每天 9:00   │ 2026-04-09 09:00:00 │ Pending │
│ def456...                            │ 周报生成       │ 每周五 17:00│ 2026-04-12 17:00:00 │ Pending │
└──────────────────────────────────────┴────────────────┴─────────────┴──────────────────────┴─────────┘
```

**任务详情：**
```
Task Details
────────────────────────────────────────
ID:               abc123...
Name:             每日站会提醒
Description:      每天早上9点提醒开站会
Owner:            user-123
Schedule:         每天 9:00 (0 9 * * *)
Status:           Pending
Next Execution:   2026-04-09 09:00:00
Last Execution:   2026-04-08 09:00:00 (Completed)
Execution Count:  42
Max Retries:      3
Timeout:          300s
Created:          2026-03-01 10:00:00
Tags:             reminder, daily

Task Payload:
────────────────────────────────────────
{
  "message": "10分钟后开始每日站会"
}

Usage:
────────────────────────────────────────
• /task pause abc123     - 暂停任务
• /task run abc123       - 立即执行
• /task history abc123   - 查看历史
```

## 实施计划

### Phase 1: 数据模型和基础设施（3-4 天）

1. **数据模型**（1 天）
   - 创建 ScheduledTask 和 TaskExecution 模型
   - 定义枚举类型（ScheduleType, TaskType, TaskStatus, ExecutionStatus）
   - 创建数据库表和索引

2. **仓储层**（1 天）
   - 实现 IScheduledTaskRepository
   - 实现 ITaskExecutionRepository
   - 单元测试仓储层

3. **Cron 解析**（1-2 天）
   - 集成 Cronos 库
   - 实现 ICronParser
   - 实现 INaturalLanguageTimeParser
   - 单元测试解析逻辑

### Phase 2: 调度和执行引擎（3-4 天）

1. **调度引擎**（2 天）
   - 实现 TaskScheduler（基于 System.Threading.Timer）
   - 实现任务队列和优先级调度
   - 实现任务触发逻辑
   - 单元测试调度引擎

2. **执行引擎**（1-2 天）
   - 实现 TaskExecutor
   - 实现重试和超时逻辑
   - 实现三种任务类型的执行
   - 单元测试执行引擎

### Phase 3: 任务管理和 CLI（2-3 天）

1. **任务管理服务**（1 天）
   - 实现 ITaskManager
   - 实现 CRUD 操作
   - 权限验证（只能操作自己的任务）
   - 单元测试管理服务

2. **CLI 命令**（1-2 天）
   - 实现 TaskCommand 及其 10 个子命令
   - 实现表格和 JSON 输出格式
   - 实现交互式确认（删除、取消等）
   - CLI 命令测试

### Phase 4: 集成和文档（2-3 天）

1. **后台服务集成**（1 天）
   - 将 TaskScheduler 集成到 BackgroundTaskService
   - 实现服务启动和停止逻辑
   - 实现优雅关闭（等待运行中任务完成）

2. **集成测试**（1 天）
   - E2E 测试：创建任务 → 自动执行 → 查看历史
   - E2E 测试：暂停/恢复/取消任务
   - E2E 测试：重试和超时逻辑
   - E2E 测试：自然语言时间解析

3. **文档和示例**（1 天）
   - 用户指南（CLI 使用）
   - API 文档（服务接口）
   - 示例场景（常见用例）
   - 验收测试指南

**总预计耗时**：10-14 天（2-3 周）

## 向后兼容性

1. **现有 BackgroundTaskService**：
   - 保持现有的标签建议功能不变
   - TaskScheduler 作为新的后台服务组件
   - 两者可以并存运行

2. **数据库**：
   - 新增两个表（scheduled_tasks, task_executions）
   - 不影响现有表结构

3. **API**：
   - 新增服务接口（ITaskScheduler, ITaskExecutor, ITaskManager）
   - 不修改现有接口

## 安全考虑

1. **权限验证**：
   - 用户只能操作自己创建的任务
   - CustomCommand 类型需要白名单验证（防止恶意命令）

2. **资源限制**：
   - 限制每个用户的任务数量（如 100 个）
   - 限制任务执行时长（超时机制）
   - 限制任务执行频率（防止无限循环）

3. **日志审计**：
   - 记录所有任务创建、修改、删除操作
   - 记录所有任务执行结果
   - 记录失败和异常情况

## 测试策略

### 单元测试
- CronParser: cron 表达式解析和计算
- NaturalLanguageTimeParser: 自然语言时间解析
- TaskScheduler: 任务调度逻辑
- TaskExecutor: 任务执行和重试逻辑
- TaskManager: 任务管理 CRUD 操作

### 集成测试
- 完整任务生命周期（创建 → 调度 → 执行 → 完成）
- 重试机制（失败后自动重试）
- 超时机制（长时间运行的任务被中断）
- 暂停和恢复（任务状态正确切换）
- 执行历史记录（数据正确保存）

### E2E 测试
- 用户通过 CLI 创建任务 → 自动执行 → 查看结果
- 用户暂停任务 → 恢复任务 → 验证下次执行时间
- 用户取消任务 → 验证任务不再执行
- 用户查看历史 → 验证历史记录正确

## 性能优化

1. **索引优化**：
   - `idx_next_execution`：加速查询即将执行的任务
   - `idx_owner_id`：加速查询用户的任务列表
   - `idx_status`：加速按状态过滤

2. **调度优化**：
   - 使用最小堆（PriorityQueue）管理任务队列
   - 只查询下一小时内需要执行的任务（减少内存占用）
   - 定时刷新任务队列（每 5 分钟）

3. **执行优化**：
   - 使用线程池执行任务（避免阻塞主线程）
   - 限制并发执行数量（如最多 10 个并发任务）
   - 长时间运行的任务使用后台线程

## 未来扩展

1. **任务依赖**：支持任务间的依赖关系（任务 B 在任务 A 完成后执行）
2. **任务组**：支持批量操作（同时暂停/恢复一组任务）
3. **通知集成**：任务完成或失败时发送通知（邮件、Slack 等）
4. **分布式调度**：支持多机部署和负载均衡（使用 Quartz.NET）
5. **Web UI**：提供 Web 界面管理任务（创建、监控、统计）
6. **任务模板**：预定义常用任务模板（快速创建）
7. **条件触发**：除了时间触发，支持事件触发（如文件变化、记忆更新等）

## 技术选型对比

### Quartz.NET vs 自实现

| 对比项 | Quartz.NET | 自实现 + System.Threading.Timer |
|--------|-----------|--------------------------------|
| **功能丰富度** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **学习成本** | 中-高 | 低 |
| **依赖大小** | ~1MB | 0（.NET 内置） |
| **分布式支持** | ✅ | ❌ |
| **持久化支持** | ✅（内置） | 需自实现 |
| **社区支持** | ⭐⭐⭐⭐⭐ | - |
| **控制灵活性** | 中 | 高 |
| **适用场景** | 企业级、分布式 | 单机、轻量级 |

**最终建议**：采用自实现方案
- V3 是单机应用，不需要分布式功能
- 调度逻辑相对简单（主要是 cron）
- 减少外部依赖，保持项目轻量
- 完全控制调度逻辑，易于调试和扩展
- 使用 Cronos 库解析 cron 表达式（轻量级，仅 ~50KB）

## 参考资料

1. **Cronos 库**：https://github.com/HangfireIO/Cronos
2. **Quartz.NET**：https://www.quartz-scheduler.net/
3. **Cron 表达式语法**：https://crontab.guru/
4. **System.Threading.Timer**：https://docs.microsoft.com/en-us/dotnet/api/system.threading.timer

---

**文档版本**: v1.0  
**创建时间**: 2026-04-08  
**维护者**: General Agent Team
