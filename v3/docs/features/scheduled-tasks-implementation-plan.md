# 计划任务功能实施计划

## 概述

本文档详细描述计划任务功能的实施计划，包括每个 Phase 的具体任务、验收标准和时间估算。

**相关文档**：
- [计划任务设计文档](./scheduled-tasks-design.md)
- [优先功能路线图](./priority-features.md)

**总预计耗时**：10-14 天（2-3 周）

---

## Phase 1: 数据模型和基础设施（3-4 天）

### 1.1 数据模型定义（1 天）

**任务清单**：
- [ ] 创建 `ScheduledTask` 模型类
  - [ ] 定义所有字段和属性
  - [ ] 添加 XML 文档注释
  - [ ] 实现构造函数和工厂方法
  
- [ ] 创建 `TaskExecution` 模型类
  - [ ] 定义所有字段和属性
  - [ ] 添加 XML 文档注释
  
- [ ] 定义枚举类型
  - [ ] `ScheduleType` (Cron, Natural)
  - [ ] `TaskType` (SkillInvocation, MemoryReminder, CustomCommand)
  - [ ] `TaskStatus` (Pending, Running, Completed, Failed, Paused, Cancelled)
  - [ ] `ExecutionStatus` (Pending, Running, Completed, Failed, Timeout, Cancelled)

- [ ] 创建数据库迁移脚本
  - [ ] `scheduled_tasks` 表创建语句
  - [ ] `task_executions` 表创建语句
  - [ ] 创建所有必要的索引
  - [ ] 外键约束和级联删除

**文件位置**：
```
v3/src/GeneralAgent.Infrastructure.ScheduledTasks/
├── Models/
│   ├── ScheduledTask.cs
│   ├── TaskExecution.cs
│   ├── ScheduleType.cs
│   ├── TaskType.cs
│   ├── TaskStatus.cs
│   └── ExecutionStatus.cs
└── Migrations/
    └── 001_CreateScheduledTasksTables.sql
```

**验收标准**：
- ✅ 所有模型类编译无错误
- ✅ XML 文档注释完整
- ✅ 数据库迁移脚本可执行
- ✅ 索引和约束正确设置

---

### 1.2 仓储层实现（1 天）

**任务清单**：
- [ ] 实现 `IScheduledTaskRepository` 接口
  - [ ] `CreateAsync(ScheduledTask task)` - 创建任务
  - [ ] `UpdateAsync(ScheduledTask task)` - 更新任务
  - [ ] `DeleteAsync(Guid taskId)` - 删除任务
  - [ ] `GetByIdAsync(Guid taskId)` - 获取任务
  - [ ] `ListByOwnerAsync(string ownerId)` - 列出用户任务
  - [ ] `ListByStatusAsync(TaskStatus status)` - 按状态列出
  - [ ] `GetPendingTasksAsync(DateTime before)` - 获取待执行任务

- [ ] 实现 `ITaskExecutionRepository` 接口
  - [ ] `CreateAsync(TaskExecution execution)` - 创建执行记录
  - [ ] `UpdateAsync(TaskExecution execution)` - 更新执行记录
  - [ ] `GetByTaskIdAsync(Guid taskId, int? limit)` - 获取执行历史
  - [ ] `GetLatestByTaskIdAsync(Guid taskId)` - 获取最新执行记录

- [ ] 编写单元测试
  - [ ] 测试所有仓储方法
  - [ ] 测试数据隔离（不同用户的任务）
  - [ ] 测试外键约束（级联删除）

**文件位置**：
```
v3/src/GeneralAgent.Infrastructure.ScheduledTasks/
├── Repositories/
│   ├── IScheduledTaskRepository.cs
│   ├── ScheduledTaskRepository.cs
│   ├── ITaskExecutionRepository.cs
│   └── TaskExecutionRepository.cs
└── tests/
    └── Repositories/
        ├── ScheduledTaskRepositoryTests.cs
        └── TaskExecutionRepositoryTests.cs
```

**验收标准**：
- ✅ 所有仓储接口方法实现
- ✅ 单元测试 100% 通过
- ✅ 测试覆盖率 > 80%
- ✅ 数据库操作正确（CRUD）

---

### 1.3 Cron 和自然语言解析（1-2 天）

**任务清单**：
- [ ] 集成 Cronos 库
  - [ ] 添加 NuGet 包引用
  - [ ] 创建 `ICronParser` 接口
  - [ ] 实现 `CronParser` 类

- [ ] 实现自然语言时间解析
  - [ ] 创建 `INaturalLanguageTimeParser` 接口
  - [ ] 实现解析逻辑（支持 6 种常见模式）
  - [ ] 实现模式匹配和正则表达式

- [ ] 编写单元测试
  - [ ] 测试 cron 表达式解析
  - [ ] 测试下次执行时间计算
  - [ ] 测试自然语言解析（每种模式）
  - [ ] 测试边界情况和错误处理

**支持的自然语言模式**：
```csharp
"每天 9:00"           -> "0 9 * * *"
"每周一 15:30"        -> "30 15 * * 1"
"每月1号 20:00"       -> "0 20 1 * *"
"每小时"              -> "0 * * * *"
"每30分钟"            -> "*/30 * * * *"
"每周五下午5点"       -> "0 17 * * 5"
```

**文件位置**：
```
v3/src/GeneralAgent.Infrastructure.ScheduledTasks/
├── Parsers/
│   ├── ICronParser.cs
│   ├── CronParser.cs
│   ├── INaturalLanguageTimeParser.cs
│   └── NaturalLanguageTimeParser.cs
└── tests/
    └── Parsers/
        ├── CronParserTests.cs
        └── NaturalLanguageTimeParserTests.cs
```

**验收标准**：
- ✅ Cronos 库正确集成
- ✅ cron 表达式解析正确
- ✅ 自然语言解析支持所有 6 种模式
- ✅ 单元测试 100% 通过
- ✅ 测试覆盖率 > 85%

---

## Phase 2: 调度和执行引擎（3-4 天）

### 2.1 调度引擎实现（2 天）

**任务清单**：
- [ ] 实现 `ITaskScheduler` 接口
  - [ ] `StartAsync()` - 启动调度器
  - [ ] `StopAsync()` - 停止调度器
  - [ ] `ScheduleTaskAsync()` - 添加任务
  - [ ] `UnscheduleTaskAsync()` - 移除任务
  - [ ] `PauseTaskAsync()` - 暂停任务
  - [ ] `ResumeTaskAsync()` - 恢复任务
  - [ ] `TriggerTaskAsync()` - 手动触发任务

- [ ] 实现调度逻辑
  - [ ] 使用 `System.Threading.Timer` 实现定时器
  - [ ] 实现任务队列（PriorityQueue 按下次执行时间排序）
  - [ ] 实现任务扫描（每 1 分钟扫描一次待执行任务）
  - [ ] 实现任务触发逻辑

- [ ] 实现线程安全
  - [ ] 使用 `SemaphoreSlim` 保护任务队列
  - [ ] 使用 `ConcurrentDictionary` 管理运行中任务

- [ ] 编写单元测试
  - [ ] 测试调度器启动和停止
  - [ ] 测试任务添加和移除
  - [ ] 测试任务暂停和恢复
  - [ ] 测试并发安全

**文件位置**：
```
v3/src/GeneralAgent.Infrastructure.ScheduledTasks/
├── Services/
│   ├── ITaskScheduler.cs
│   └── TaskScheduler.cs
└── tests/
    └── Services/
        └── TaskSchedulerTests.cs
```

**验收标准**：
- ✅ 调度器可以启动和停止
- ✅ 任务按时间正确调度
- ✅ 暂停/恢复/取消功能正常
- ✅ 线程安全无并发问题
- ✅ 单元测试 100% 通过

---

### 2.2 执行引擎实现（1-2 天）

**任务清单**：
- [ ] 实现 `ITaskExecutor` 接口
  - [ ] `ExecuteAsync(ScheduledTask task)` - 执行任务

- [ ] 实现三种任务类型执行器
  - [ ] `ExecuteSkillAsync()` - 执行技能调用
  - [ ] `ExecuteReminderAsync()` - 执行记忆提醒
  - [ ] `ExecuteCommandAsync()` - 执行自定义命令（带白名单验证）

- [ ] 实现重试和超时逻辑
  - [ ] 超时控制（使用 CancellationTokenSource）
  - [ ] 重试机制（指数退避）
  - [ ] 错误处理和日志记录

- [ ] 编写单元测试
  - [ ] 测试成功执行
  - [ ] 测试超时处理
  - [ ] 测试重试逻辑
  - [ ] 测试三种任务类型

**文件位置**：
```
v3/src/GeneralAgent.Infrastructure.ScheduledTasks/
├── Services/
│   ├── ITaskExecutor.cs
│   └── TaskExecutor.cs
└── tests/
    └── Services/
        └── TaskExecutorTests.cs
```

**验收标准**：
- ✅ 三种任务类型都能正确执行
- ✅ 超时机制正常工作
- ✅ 重试逻辑正确（最多 N 次）
- ✅ 执行记录正确保存
- ✅ 单元测试 100% 通过

---

## Phase 3: 任务管理和 CLI（2-3 天）

### 3.1 任务管理服务（1 天）

**任务清单**：
- [ ] 实现 `ITaskManager` 接口
  - [ ] `CreateTaskAsync()` - 创建任务
  - [ ] `ListTasksAsync()` - 列出任务
  - [ ] `GetTaskAsync()` - 获取任务详情
  - [ ] `UpdateTaskAsync()` - 更新任务
  - [ ] `CancelTaskAsync()` - 取消任务
  - [ ] `DeleteTaskAsync()` - 删除任务
  - [ ] `GetExecutionHistoryAsync()` - 获取执行历史

- [ ] 实现权限验证
  - [ ] 验证用户只能操作自己的任务
  - [ ] 验证任务状态转换合法性

- [ ] 编写单元测试
  - [ ] 测试 CRUD 操作
  - [ ] 测试权限验证
  - [ ] 测试状态转换

**文件位置**：
```
v3/src/GeneralAgent.Infrastructure.ScheduledTasks/
├── Services/
│   ├── ITaskManager.cs
│   └── TaskManager.cs
└── tests/
    └── Services/
        └── TaskManagerTests.cs
```

**验收标准**：
- ✅ 所有管理接口方法实现
- ✅ 权限验证正确
- ✅ 状态转换合法
- ✅ 单元测试 100% 通过

---

### 3.2 CLI 命令实现（1-2 天）

**任务清单**：
- [ ] 创建 `TaskCommand` 主命令
  - [ ] 注册到 CLI 系统

- [ ] 实现 10 个子命令
  - [ ] `schedule` - 创建任务
  - [ ] `list` - 列出任务
  - [ ] `show` - 查看任务详情
  - [ ] `update` - 更新任务
  - [ ] `pause` - 暂停任务
  - [ ] `resume` - 恢复任务
  - [ ] `cancel` - 取消任务
  - [ ] `delete` - 删除任务
  - [ ] `run` - 立即执行任务
  - [ ] `history` - 查看执行历史

- [ ] 实现输出格式
  - [ ] Table 格式（Spectre.Console）
  - [ ] JSON 格式
  - [ ] 颜色编码（状态显示）

- [ ] 实现交互式确认
  - [ ] 删除任务前确认
  - [ ] 取消任务前确认

**文件位置**：
```
v3/src/GeneralAgent.Hosts.Console/Commands/
├── TaskCommand.cs
├── TaskScheduleCommand.cs
├── TaskListCommand.cs
├── TaskShowCommand.cs
├── TaskUpdateCommand.cs
├── TaskPauseCommand.cs
├── TaskResumeCommand.cs
├── TaskCancelCommand.cs
├── TaskDeleteCommand.cs
├── TaskRunCommand.cs
└── TaskHistoryCommand.cs
```

**验收标准**：
- ✅ 所有 CLI 命令可执行
- ✅ 参数解析正确
- ✅ 输出格式美观
- ✅ 交互式确认正常
- ✅ 错误提示友好

---

## Phase 4: 集成和文档（2-3 天）

### 4.1 后台服务集成（1 天）

**任务清单**：
- [ ] 创建 `ScheduledTasksBackgroundService`
  - [ ] 继承 `BackgroundService`
  - [ ] 在 `ExecuteAsync` 中启动 TaskScheduler
  - [ ] 实现优雅关闭（等待运行中任务完成）

- [ ] 注册依赖注入
  - [ ] 在 `ServiceCollectionExtensions` 中注册所有服务
  - [ ] 注册后台服务

- [ ] 集成测试
  - [ ] 测试服务启动和停止
  - [ ] 测试任务自动执行
  - [ ] 测试优雅关闭

**文件位置**：
```
v3/src/GeneralAgent.Infrastructure.ScheduledTasks/
├── Services/
│   └── ScheduledTasksBackgroundService.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

**验收标准**：
- ✅ 后台服务正确启动
- ✅ 任务自动执行
- ✅ 优雅关闭正常
- ✅ 依赖注入正确配置

---

### 4.2 集成测试（1 天）

**任务清单**：
- [ ] E2E 测试场景
  - [ ] 创建任务 → 自动执行 → 查看历史
  - [ ] 暂停任务 → 恢复任务 → 验证下次执行时间
  - [ ] 取消任务 → 验证任务不再执行
  - [ ] 立即执行任务 → 验证执行记录

- [ ] 重试和超时测试
  - [ ] 任务执行失败 → 自动重试 → 最终成功
  - [ ] 任务执行超时 → 记录超时状态
  - [ ] 达到最大重试次数 → 标记为失败

- [ ] 自然语言解析测试
  - [ ] 各种自然语言描述 → 正确转换为 cron
  - [ ] cron 表达式 → 正确计算下次执行时间

**文件位置**：
```
v3/tests/GeneralAgent.Integration.Tests/ScheduledTasks/
├── ScheduledTasksE2ETests.cs
├── TaskExecutionRetryTests.cs
└── NaturalLanguageParsingTests.cs
```

**验收标准**：
- ✅ 所有 E2E 测试通过
- ✅ 重试和超时逻辑正确
- ✅ 自然语言解析准确
- ✅ 测试覆盖率 > 80%

---

### 4.3 文档和示例（1 天）

**任务清单**：
- [ ] 用户指南
  - [ ] CLI 命令使用说明
  - [ ] 自然语言时间描述语法
  - [ ] 常见用例和示例

- [ ] API 文档
  - [ ] 服务接口说明
  - [ ] 数据模型说明
  - [ ] 集成方法

- [ ] 验收测试指南
  - [ ] 手工测试场景
  - [ ] 预期结果验证

- [ ] 更新相关文档
  - [ ] 更新 `priority-features.md`（标记计划任务为已完成）
  - [ ] 更新 `CLI_GUIDE.md`（添加 /task 命令部分）
  - [ ] 更新 `CLI_REFERENCE.md`（添加命令参考）

**文件位置**：
```
v3/docs/features/
├── scheduled-tasks-user-guide.md
├── scheduled-tasks-api-docs.md
└── scheduled-tasks-acceptance-test.md

v3/docs/guides/
├── CLI_GUIDE.md (更新)
└── CLI_REFERENCE.md (更新)
```

**验收标准**：
- ✅ 用户指南完整清晰
- ✅ API 文档准确详细
- ✅ 验收测试场景覆盖全面
- ✅ 所有文档更新完成

---

## 总体验收标准

### 功能验收
- ✅ 支持 cron 表达式调度
- ✅ 支持自然语言时间描述
- ✅ 支持三种任务类型（技能、提醒、命令）
- ✅ 完整的任务生命周期管理（创建、暂停、恢复、取消、删除）
- ✅ 重试和超时机制正常
- ✅ 执行历史正确记录
- ✅ CLI 命令功能完整

### 质量验收
- ✅ 所有单元测试通过（> 50 个）
- ✅ 所有集成测试通过（> 10 个）
- ✅ 测试覆盖率 > 80%
- ✅ 0 编译错误/警告
- ✅ 代码符合项目规范

### 文档验收
- ✅ 设计文档完整
- ✅ 用户指南清晰
- ✅ API 文档准确
- ✅ 验收测试指南完整

---

## 风险和依赖

### 技术风险
1. **Cronos 库兼容性**：需要验证与 .NET 8.0 兼容
2. **自然语言解析准确性**：可能需要多次迭代优化
3. **并发安全**：需要仔细处理多线程问题

### 依赖关系
1. 依赖技能系统（SkillInvocation 任务类型）
2. 依赖记忆系统（MemoryReminder 任务类型）
3. 依赖 CLI 框架（System.CommandLine）

### 缓解措施
1. 在 Phase 1 早期验证 Cronos 库
2. 为自然语言解析预留调整时间
3. 使用成熟的并发控制模式（Semaphore, ConcurrentDictionary）

---

## 时间表

| Phase | 任务 | 预计耗时 | 累计耗时 |
|-------|------|---------|----------|
| **Phase 1** | 数据模型和基础设施 | 3-4 天 | 3-4 天 |
| 1.1 | 数据模型定义 | 1 天 | 1 天 |
| 1.2 | 仓储层实现 | 1 天 | 2 天 |
| 1.3 | Cron 和自然语言解析 | 1-2 天 | 3-4 天 |
| **Phase 2** | 调度和执行引擎 | 3-4 天 | 6-8 天 |
| 2.1 | 调度引擎实现 | 2 天 | 5-6 天 |
| 2.2 | 执行引擎实现 | 1-2 天 | 6-8 天 |
| **Phase 3** | 任务管理和 CLI | 2-3 天 | 8-11 天 |
| 3.1 | 任务管理服务 | 1 天 | 7-9 天 |
| 3.2 | CLI 命令实现 | 1-2 天 | 8-11 天 |
| **Phase 4** | 集成和文档 | 2-3 天 | 10-14 天 |
| 4.1 | 后台服务集成 | 1 天 | 9-12 天 |
| 4.2 | 集成测试 | 1 天 | 10-13 天 |
| 4.3 | 文档和示例 | 1 天 | 10-14 天 |

**总预计耗时**：**10-14 天**（2-3 周）

---

## 下一步行动

1. **立即开始**：Phase 1.1 数据模型定义
2. **第一周目标**：完成 Phase 1 和 Phase 2
3. **第二周目标**：完成 Phase 3 和 Phase 4
4. **验收时间**：第 3 周初

---

**文档版本**: v1.0  
**创建时间**: 2026-04-08  
**维护者**: General Agent Team
