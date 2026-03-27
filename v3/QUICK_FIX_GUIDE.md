# 快速修复指南 - Phase 6 编译错误

**错误数量**: 5 个
**预计修复时间**: 10 分钟

---

## 修复清单

### ✅ 修复 1: Logger 注入（4 处错误）

**文件**: `src/GeneralAgent.Hosts.Console/AgentRepl.cs`

**位置**: Line 37-75

**步骤**:

1. 在构造函数参数中添加 `ILoggerFactory`：

```csharp
public AgentRepl(
    SessionService sessionService,
    ConversationService conversationService,
    IMessageRepository messageRepository,
    SkillService skillService,
    ContextCompressionService contextCompressionService,
    SearchCommand searchCommand,
    TagCommand tagCommand,
    IOptions<LLMOptions> llmOptions,
    ILoggerFactory loggerFactory,  // ← 添加这一行
    ILogger<AgentRepl> logger)
```

2. 修改 Line 62-75 的初始化代码：

**替换前**:
```csharp
_historyManager = new ReplHistoryManager(historyPath, logger: logger);
_completionHandler = new AutoCompletionHandler(sessionService, skillService, logger);
_multiLineHandler = new MultiLineInputHandler(logger);
_aliasManager = new AliasManager(aliasPath, logger);
```

**替换后**:
```csharp
_historyManager = new ReplHistoryManager(historyPath,
    logger: loggerFactory.CreateLogger<ReplHistoryManager>());

_completionHandler = new AutoCompletionHandler(sessionService, skillService,
    loggerFactory.CreateLogger<AutoCompletionHandler>());

_multiLineHandler = new MultiLineInputHandler(
    loggerFactory.CreateLogger<MultiLineInputHandler>());

var aliasPath = Path.Combine(agentDir, "aliases.json");
_aliasManager = new AliasManager(aliasPath,
    loggerFactory.CreateLogger<AliasManager>());
```

---

### ✅ 修复 2: 添加缺失方法（1 处错误）

**文件**: `src/GeneralAgent.Application/Services/ContextCompressionService.cs`

**位置**: 在类的末尾添加方法

**代码**:

```csharp
/// <summary>
/// 获取或创建会话的压缩配置
/// </summary>
public async Task<CompressionConfig> GetOrCreateConfigAsync(
    Guid sessionId,
    CancellationToken cancellationToken = default)
{
    return await _compressionService.GetOrCreateConfigAsync(sessionId, cancellationToken);
}
```

**插入位置**: 在 `AutoCompressIfNeededAsync` 方法之后，类结束大括号 `}` 之前。

---

## 验证

```bash
# 1. 清理
dotnet clean

# 2. 编译
dotnet build

# 期望输出:
# 已成功生成。
#     0 个警告
#     0 个错误
```

---

## 完成后测试

```bash
cd src/GeneralAgent.Hosts.Console
dotnet run

# 在 REPL 中输入:
You> /context status
```

期望看到上下文状态面板显示。

---

**修复完成后删除此文件，查看 V3_PHASE6_HANDOFF.md 了解完整信息。**
