# CLAUDE.md

这个文件为 Claude Code (claude.ai/code) 提供在本仓库中工作的指南。

## 项目概述

General Agent V3 是一个功能完整的通用 AI Agent 系统，基于 .NET 10 构建。

- **当前版本**: V3 (.NET 10) - 生产就绪 ⭐
- **历史版本**: v1/ (Python) 和 v2/ (Rust) 为遗留版本，仅供参考

## 重要提示

1. **使用中文**：本项目所有对话、文档和注释都使用中文
2. **只关注 V3**：所有开发工作都在 `v3/` 目录下进行，不主动参考 V1/V2
3. **测试覆盖率**：保持 85%+ 的测试覆盖率，865+ 个测试全部通过

## 构建和测试命令

### 快速验证

```bash
# 运行快速验证脚本（推荐）
cd v3
./quick-test.sh                     # 编译 + 核心测试 + 命令验证（~30秒）
```

### 日常开发命令

```bash
# 工作目录
cd v3

# 构建项目
dotnet build                        # Debug 构建
dotnet build --configuration Release # Release 构建

# 运行 REPL
cd src/GeneralAgent.Hosts.Console
dotnet run                          # 启动交互式 REPL
dotnet run -- --help                # 查看 CLI 帮助

# 测试
dotnet test                         # 运行所有测试（865个）
dotnet test --logger "console;verbosity=detailed"  # 详细输出
dotnet test --filter "FullyQualifiedName~Memory"   # 特定模块
dotnet test --filter "FullyQualifiedName~ScheduledTasks" # 计划任务
dotnet test tests/GeneralAgent.Infrastructure.Tests  # 特定项目
dotnet test --collect:"XPlat Code Coverage"  # 代码覆盖率

# 运行单个测试
dotnet test --filter "MemoryRepositoryTests.CreateAsync_ShouldPersistMemory"

# 代码质量
dotnet format                       # 格式化代码
dotnet build /p:TreatWarningsAsErrors=true  # 警告即错误

# 数据库迁移
cd src/GeneralAgent.Infrastructure
dotnet ef migrations add <MigrationName>
dotnet ef migrations list
dotnet ef database update
```

## 项目架构

V3 采用**分层架构 + 模块化设计**，清晰的职责分离和依赖管理。

### 核心项目结构

```
v3/
├── src/
│   ├── GeneralAgent.Core/                    # 核心抽象层
│   │   ├── Abstractions/                     # 接口定义
│   │   └── Models/                           # 领域模型
│   │
│   ├── GeneralAgent.Infrastructure/          # 基础设施层
│   │   ├── Storage/                          # 数据持久化（EF Core + SQLite）
│   │   └── Repositories/                     # 仓储实现
│   │
│   ├── GeneralAgent.Infrastructure.*/        # 功能模块（独立项目）
│   │   ├── LLM/                              # LLM 集成（Anthropic/Ollama）
│   │   ├── Skills/                           # 技能系统（YAML + Scriban）
│   │   ├── Memory/                           # 长期记忆（向量检索）
│   │   ├── Compression/                      # 上下文压缩（SharpToken）
│   │   ├── FileStorage/                      # 文件上传（权限管理）
│   │   ├── SkillExtraction/                  # 技能抽取（LLM 驱动）
│   │   ├── ScheduledTasks/                   # 计划任务（Cronos）
│   │   ├── Embedding/                        # Embedding 服务
│   │   └── VectorDB/                         # 向量数据库（Qdrant）
│   │
│   ├── GeneralAgent.Application/             # 应用层
│   │   ├── Services/                         # 业务服务
│   │   │   ├── ConversationService.cs        # 对话编排
│   │   │   ├── SkillService.cs               # 技能管理
│   │   │   └── ToolCallingOrchestrator.cs    # 工具调用编排
│   │   └── Extensions/                       # DI 扩展
│   │
│   └── GeneralAgent.Hosts.Console/           # 主机层
│       ├── Commands/                         # CLI 命令
│       │   ├── RootCommand.cs                # 根命令
│       │   ├── TaskCommand.cs                # 计划任务命令组
│       │   └── Task*Command.cs               # 子命令
│       ├── Services/                         # 主机服务
│       │   ├── AgentRepl.cs                  # REPL 实现
│       │   ├── SearchService.cs              # 搜索服务
│       │   └── BackgroundTaskService.cs      # 后台任务调度
│       └── Program.cs                        # 入口点
│
└── tests/
    ├── GeneralAgent.Infrastructure.Tests/    # 基础设施测试（586个）
    ├── GeneralAgent.Infrastructure.FileStorage.Tests/  # 文件存储测试（111个）
    ├── GeneralAgent.Infrastructure.SkillExtraction.Tests/ # 技能抽取测试（56个）
    └── GeneralAgent.Integration.Tests/       # 集成测试（112个）
```

### 分层架构原则

1. **Core 层**：定义接口和模型，无外部依赖
2. **Infrastructure 层**：实现 Core 接口，可依赖第三方库
3. **Application 层**：业务逻辑编排，协调多个 Infrastructure 模块
4. **Hosts 层**：用户界面（CLI/REPL），依赖 Application 层

**依赖方向**：Hosts → Application → Infrastructure → Core

### 关键设计模式

#### 1. 技能系统（Skills）
- **定义格式**：YAML frontmatter + Markdown 模板
- **模板引擎**：Scriban（支持变量替换、条件、循环）
- **调用语法**：`@skill-name` 或 `/skill-name`
- **命名空间**：`personal/greeting.md` → `@personal:greeting`
- **热加载**：开发模式下自动重新加载

#### 2. 对话编排（ConversationService）
- **显式技能调用**：`@skill` / `/skill` → 直接执行
- **隐式工具调用**：LLM 返回 tool_use → ToolCallingOrchestrator
- **文件引用解析**：`@file:filename` → 自动读取并注入上下文
- **流式响应**：`IAsyncEnumerable<string>` 实时输出
- **历史管理**：自动保存用户和助手消息

#### 3. 长期记忆系统（Memory）
- **向量化存储**：Qdrant 向量数据库
- **五种记忆类型**：User, Feedback, Project, Reference, Knowledge
- **混合检索**：关键词搜索 + 语义相似度
- **LLM 驱动提取**：自动从对话中提取记忆片段
- **降级策略**：Qdrant 不可用时降级到 SQLite 全文搜索

#### 4. 计划任务系统（ScheduledTasks）
- **调度语法**：
  - Cron 表达式：`"0 9 * * 1-5"` (工作日 9 点)
  - 自然语言：`"每天早上9点"`, `"每周一下午3点"`
- **三种任务类型**：
  - SkillInvocation：调用技能并保存结果
  - MemoryReminder：创建记忆提醒
  - CustomCommand：执行自定义命令
- **生命周期**：创建 → 运行 → 暂停/恢复 → 完成/失败
- **重试机制**：指数退避，可配置最大重试次数
- **后台服务**：BackgroundTaskService 持续轮询并调度

#### 5. 文件上传系统（FileStorage）
- **三级权限**：Private（仅所有者）、Shared（明确授权）、Public（所有人）
- **版本控制**：每次上传创建新版本，支持回滚
- **类型处理**：20+ 文件类型（文本、代码、配置、日志等）
- **对话引用**：`@file:filename` 自动读取内容
- **权限管理**：授予/撤销读写权限

#### 6. 技能抽取系统（SkillExtraction）
- **LLM 驱动**：分析对话识别可复用模式
- **交互式编辑**：生成后可调整名称、参数、模板
- **确认机制**：预览并确认后才保存
- **抽取历史**：记录每次抽取的元数据和统计
- **缓存优化**：相同会话的重复请求 <10ms

## LLM 配置

V3 支持两种 LLM 提供商，通过 `appsettings.json` 配置。

### 配置文件位置

编辑 `v3/src/GeneralAgent.Hosts.Console/appsettings.json`

### 使用 Anthropic Claude（推荐用于生产）

```json
{
  "LLM": {
    "DefaultProvider": "Anthropic",
    "Providers": {
      "Anthropic": {
        "ApiKey": "sk-ant-your-api-key-here",
        "Model": "claude-3-5-sonnet-20241022",
        "MaxTokens": 8096,
        "Temperature": 0.7
      }
    }
  }
}
```

或通过环境变量：
```bash
export LLM__Providers__Anthropic__ApiKey="sk-ant-xxx"
```

### 使用 Ollama（推荐用于开发）

```json
{
  "LLM": {
    "DefaultProvider": "Ollama",
    "Providers": {
      "Ollama": {
        "BaseUrl": "http://localhost:11434",
        "Model": "qwen2.5:7b",
        "MaxTokens": 8096,
        "Temperature": 0.7
      }
    }
  }
}
```

安装和启动 Ollama：
```bash
# 安装模型
ollama pull qwen2.5:7b              # 对话模型
ollama pull nomic-embed-text        # Embedding 模型（Memory 功能需要）

# 启动服务
ollama serve                        # 默认 http://localhost:11434
```

### 切换 LLM 提供商

修改 `DefaultProvider` 字段即可：
```json
"DefaultProvider": "Anthropic"  // 或 "Ollama"
```

## 技能系统

### 技能定义格式

```markdown
---
name: greeting
description: 向用户问候
parameters:
  - name: user_name
    type: string
    required: true
    description: 用户名称
---

你好 {user_name}！今天有什么我可以帮助你的吗？
```

### 技能调用语法

```bash
# @ 语法（推荐）
@greeting user_name='Alice'
@personal:reminder task='买牛奶' time='5pm'

# / 语法（命令风格）
/greeting user_name='Bob'
/productivity:task title='Review PR' priority='high'
```

### 技能目录结构

```
skills/
├── personal/          # 个人生产力技能
│   ├── greeting.md
│   ├── reminder.md
│   └── note.md
├── productivity/      # 工作任务管理
│   ├── task.md
│   └── meeting.md
└── .ignore           # 忽略模式（类似 .gitignore）
```

## 测试策略

V3 采用 xUnit + FluentAssertions + NSubstitute 测试框架。

### 测试分类

```
总测试数: 865 个
├─ 单元测试: 586 个（67.8%）
│   ├─ Infrastructure.Tests - 基础设施层
│   ├─ LLM.Tests - LLM 集成
│   ├─ Skills.Tests - 技能系统
│   └─ Application.Tests - 业务逻辑
│
├─ 功能测试: 278 个（32.2%）
│   ├─ FileStorage.Tests: 111 个 - 文件存储
│   ├─ SkillExtraction.Tests: 56 个 - 技能抽取
│   └─ Integration.Tests: 111 个 - 端到端集成
│
└─ 通过率: 100%，覆盖率: 85%+
```

### 测试命名规范

```csharp
// 单元测试：MethodName_Scenario_ExpectedBehavior
public class MemoryRepositoryTests
{
    [Fact]
    public async Task CreateAsync_WithValidMemory_ShouldPersistToDatabase()
    
    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldReturnNull()
}
```

### 运行特定测试

```bash
# 按模块
dotnet test --filter "FullyQualifiedName~Memory"
dotnet test --filter "FullyQualifiedName~ScheduledTasks"
dotnet test --filter "FullyQualifiedName~FileStorage"

# 按类
dotnet test --filter "MemoryRepositoryTests"

# 按方法
dotnet test --filter "CreateAsync_ShouldPersistMemory"

# 跳过集成测试（需要外部服务）
dotnet test --filter "Category!=Integration"
```

### 集成测试依赖

某些集成测试需要外部服务：

```bash
# Memory 集成测试需要 Qdrant
docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant

# 或使用 Ollama Embedding（Memory 功能）
ollama pull nomic-embed-text
ollama serve

# 验证 Qdrant 运行状态
curl http://localhost:6333/health
```

## 常见开发任务

### 添加新技能

1. 在 `v3/skills/<namespace>/` 创建 `.md` 文件
2. 添加 YAML frontmatter 定义参数
3. 编写 Scriban 模板（使用 `{{ param }}` 占位符）
4. 在 REPL 中测试：`@namespace:skill-name param="value"`

示例文件 `v3/skills/personal/greeting.md`：
```markdown
---
name: greeting
description: 向用户问候
parameters:
  - name: user_name
    type: string
    required: true
    description: 用户名称
---

你好 {{ user_name }}！今天有什么我可以帮助你的吗？
```

### 添加新的 Infrastructure 模块

1. **创建项目**：
```bash
cd v3/src
dotnet new classlib -n GeneralAgent.Infrastructure.NewFeature
```

2. **添加到解决方案**：
编辑 `v3/GeneralAgent.slnx`，添加项目引用

3. **实现接口**：
在 `GeneralAgent.Core/Abstractions/` 定义接口，在新项目中实现

4. **注册服务**：
创建 `Extensions/ServiceCollectionExtensions.cs`：
```csharp
public static IServiceCollection AddNewFeature(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddSingleton<INewFeatureService, NewFeatureService>();
    return services;
}
```

5. **在 Program.cs 中注册**：
```csharp
builder.Services.AddNewFeature(builder.Configuration);
```

### 添加数据库迁移

```bash
cd v3/src/GeneralAgent.Infrastructure

# 添加迁移
dotnet ef migrations add AddNewFeatureTable

# 查看 SQL
dotnet ef migrations script

# 应用迁移（自动在启动时执行）
dotnet ef database update
```

### 添加 CLI 命令

1. 在 `v3/src/GeneralAgent.Hosts.Console/Commands/` 创建命令类
2. 继承 `Command` 基类，实现 `InvokeAsync`
3. 在 `RootCommand.cs` 中注册

示例：
```csharp
public class MyCommand : Command
{
    public MyCommand() : base("my-command", "命令描述")
    {
        var option = new Option<string>("--name", "参数描述");
        AddOption(option);
    }
    
    public new class Handler : ICommandHandler
    {
        public async Task<int> InvokeAsync(InvocationContext context)
        {
            // 实现命令逻辑
            return 0;
        }
    }
}
```

### 性能分析

```bash
# 查看最慢的测试
dotnet test --logger "console;verbosity=detailed" | grep "ms]"

# 使用 BenchmarkDotNet
cd v3/tests/GeneralAgent.Performance.Tests
dotnet run -c Release

# 查看内存使用
dotnet-counters monitor --process-id <pid>
```

## 数据库模式

V3 使用 EF Core + SQLite，数据库文件位于 `~/.agent/agent.db`。

### 核心表

```sql
-- 会话表
Sessions (
  Id TEXT PRIMARY KEY,
  Title TEXT NOT NULL,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME,
  Metadata TEXT             -- JSON 元数据
)

-- 消息表
Messages (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  SessionId TEXT NOT NULL,
  Role TEXT NOT NULL,       -- 'user' | 'assistant' | 'system' | 'tool'
  Content TEXT NOT NULL,
  Timestamp DATETIME NOT NULL,
  Metadata TEXT,            -- JSON 元数据（tool_use_id, tool_name 等）
  FOREIGN KEY (SessionId) REFERENCES Sessions(Id) ON DELETE CASCADE
)

-- 技能表
Skills (
  Id TEXT PRIMARY KEY,
  Name TEXT NOT NULL,
  Namespace TEXT,
  Description TEXT,
  FilePath TEXT NOT NULL,
  Parameters TEXT,          -- JSON 参数定义
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME
)

-- 长期记忆表
Memories (
  Id TEXT PRIMARY KEY,
  Type TEXT NOT NULL,       -- User, Feedback, Project, Reference, Knowledge
  Content TEXT NOT NULL,
  Source TEXT,
  SessionId TEXT,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME,
  Metadata TEXT,
  FOREIGN KEY (SessionId) REFERENCES Sessions(Id)
)

-- 文件存储表
UploadedFiles (
  Id TEXT PRIMARY KEY,
  OriginalFileName TEXT NOT NULL,
  StoredFileName TEXT NOT NULL,
  FileType TEXT NOT NULL,
  SizeInBytes INTEGER NOT NULL,
  UploadedAt DATETIME NOT NULL,
  AccessLevel TEXT NOT NULL,    -- Private, Shared, Public
  OwnerId TEXT NOT NULL,
  CurrentVersion INTEGER NOT NULL DEFAULT 1
)

-- 文件版本表
FileVersions (
  Id TEXT PRIMARY KEY,
  FileId TEXT NOT NULL,
  Version INTEGER NOT NULL,
  StoredFileName TEXT NOT NULL,
  UploadedAt DATETIME NOT NULL,
  SizeInBytes INTEGER NOT NULL,
  ChangeDescription TEXT,
  FOREIGN KEY (FileId) REFERENCES UploadedFiles(Id) ON DELETE CASCADE
)

-- 计划任务表
ScheduledTasks (
  Id TEXT PRIMARY KEY,
  Name TEXT NOT NULL,
  Description TEXT,
  OwnerId TEXT NOT NULL,
  Schedule TEXT NOT NULL,       -- Cron 或自然语言
  ScheduleType INTEGER NOT NULL, -- 0=Cron, 1=Natural
  TaskType INTEGER NOT NULL,     -- 0=Skill, 1=Reminder, 2=Custom
  TaskPayload TEXT NOT NULL,     -- JSON 负载
  Status INTEGER NOT NULL,       -- 0=Pending, 1=Running, 2=Completed, 3=Failed, 4=Paused
  MaxRetries INTEGER NOT NULL DEFAULT 3,
  TimeoutSeconds INTEGER NOT NULL DEFAULT 300,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME,
  LastExecutionAt DATETIME,
  NextExecutionAt DATETIME,
  ExecutionCount INTEGER NOT NULL DEFAULT 0
)

-- 任务执行历史表
TaskExecutions (
  Id TEXT PRIMARY KEY,
  TaskId TEXT NOT NULL,
  StartedAt DATETIME NOT NULL,
  CompletedAt DATETIME,
  Status INTEGER NOT NULL,      -- 0=Success, 1=Failed, 2=Timeout
  Result TEXT,                  -- 执行结果
  ErrorMessage TEXT,
  RetryCount INTEGER NOT NULL DEFAULT 0,
  FOREIGN KEY (TaskId) REFERENCES ScheduledTasks(Id) ON DELETE CASCADE
)
```

### 查看数据库

```bash
# 使用 sqlite3
sqlite3 ~/.agent/agent.db

# 常用查询
.tables                           # 列出所有表
.schema Sessions                  # 查看表结构
SELECT * FROM Sessions LIMIT 10;  # 查询数据

# 或使用 EF Core 工具
cd v3/src/GeneralAgent.Infrastructure
dotnet ef dbcontext info
dotnet ef migrations list
```

## 常见问题

### 1. 构建失败 "找不到依赖"

确保使用 .NET 10 SDK：
```bash
dotnet --version                     # 应该是 10.x.x
cd v3
dotnet restore                       # 恢复 NuGet 包
dotnet build                         # 重新构建
```

### 2. Ollama 连接失败

检查 Ollama 服务和配置：
```bash
# 检查服务状态
ollama list
curl http://localhost:11434/api/tags

# 检查配置文件
cat v3/src/GeneralAgent.Hosts.Console/appsettings.json

# 确保 DefaultProvider 设置正确
"DefaultProvider": "Ollama"
```

### 3. 数据库迁移错误

```bash
cd v3/src/GeneralAgent.Infrastructure

# 查看当前迁移状态
dotnet ef migrations list

# 删除数据库重新开始
rm ~/.agent/agent.db
dotnet ef database update

# 或者回滚到特定迁移
dotnet ef database update <MigrationName>
```

### 4. Memory 功能不可用（向量搜索失败）

Memory 功能需要 Qdrant 或 Ollama Embedding：

```bash
# 选项 1：使用 Qdrant（推荐）
docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant

# 选项 2：使用 Ollama Embedding
ollama pull nomic-embed-text
ollama serve

# 验证服务
curl http://localhost:6333/health          # Qdrant
curl http://localhost:11434/api/tags       # Ollama

# 系统会自动降级到关键词搜索如果向量服务不可用
```

### 5. 计划任务不执行

检查后台服务和任务状态：
```bash
# 在 REPL 中查看任务
You> agent task list

# 查看任务详情
You> agent task show <task-id>

# 检查日志
tail -f ~/.agent/logs/agent.log

# 确认后台服务已启动（Program.cs 中已配置）
# BackgroundTaskService 应该自动启动
```

### 6. 测试失败 "数据库锁定"

SQLite 并发限制，使用内存数据库测试：
```csharp
// 在测试中使用 InMemory 数据库
services.AddDbContext<AgentDbContext>(options =>
    options.UseInMemoryDatabase("TestDb"));
```

### 7. REPL 启动慢或卡住

检查数据库和服务初始化：
```bash
# 删除损坏的数据库
rm ~/.agent/agent.db

# 禁用详细日志
# 编辑 appsettings.json，确保：
"Logging": {
  "LogLevel": {
    "Default": "Warning",
    "Microsoft.EntityFrameworkCore": "Warning"
  }
}
```

## 相关文档

### 用户指南
- **[V3 README](v3/README.md)** - V3 项目概览和快速开始
- **[CLI 使用指南](v3/docs/guides/CLI_GUIDE.md)** - 详细的命令行使用说明
- **[CLI 命令参考](v3/docs/guides/CLI_REFERENCE.md)** - 所有命令的完整参考
- **[技能系统指南](v3/docs/guides/SKILLS_GUIDE.md)** - 如何创建和使用技能
- **[文件上传指南](v3/docs/guides/FILE_UPLOAD_USER_GUIDE.md)** - 文件上传和管理
- **[计划任务指南](v3/docs/features/scheduled-tasks-user-guide.md)** - 计划任务完整使用指南
- **[长期记忆指南](v3/docs/guides/MEMORY_GUIDE.md)** - 记忆系统使用方法

### 功能设计文档
- **[计划任务设计](v3/docs/features/scheduled-tasks-design.md)** - 计划任务系统架构
- **[计划任务实施计划](v3/docs/features/scheduled-tasks-implementation-plan.md)** - 实施细节
- **[文件上传设计](v3/docs/features/file-upload-plan.md)** - 文件存储架构
- **[跨会话访问设计](v3/docs/features/cross-session-file-access-design.md)** - 权限管理设计
- **[技能抽取使用](v3/docs/features/skill-extraction-usage.md)** - LLM 驱动的技能抽取
- **[上下文压缩设计](v3/docs/features/auto-context-compression-design.md)** - 压缩策略

### 完成总结
- **[计划任务完成总结](v3/docs/features/scheduled-tasks-completion-summary.md)**
- **[技能抽取完成总结](v3/docs/features/skill-extraction-completion-summary.md)**
- **[文件存储 Phase 8 完成](v3/docs/features/file-storage-phase8-completion.md)**
- **[文件存储 Phase 9 完成](v3/docs/features/file-storage-phase9-completion.md)**

### 项目文档
- **[优先功能路线图](v3/docs/features/priority-features.md)** - 5 个优先功能完成情况
- **[发布说明 V3.0.0](RELEASE_NOTES_V3.0.0.md)** - 完整发布说明
- **[项目总结](V3_PROJECT_SUMMARY.md)** - 项目统计和成就
- **[UAT 测试计划](V3_UAT_PLAN.md)** - 用户验收测试
- **[UAT 测试报告](V3_UAT_REPORT.md)** - 测试结果
