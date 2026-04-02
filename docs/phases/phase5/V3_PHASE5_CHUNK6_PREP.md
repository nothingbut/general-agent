# V3 Phase 5 Chunk 6 准备文档 - 用户体验增强

**准备日期**: 2026-03-25
**状态**: 准备开始
**前置条件**: Chunk 1-5 已完成

---

## 🎯 Chunk 6 目标

完成 Phase 5 的最后部分，提升 REPL 的用户体验：
1. 命令别名系统
2. 快捷键支持
3. 彩色输出优化
4. 错误恢复机制
5. 集成测试和文档

---

## 📋 任务清单

### Task 26: 实现命令别名系统 ⏳

**目标**: 允许用户自定义命令快捷方式

**实现要点**:
```csharp
// AliasManager.cs
public class AliasManager
{
    private Dictionary<string, string> _aliases;

    // 加载别名（从 ~/.agent/aliases.json）
    public void LoadAliases();

    // 保存别名
    public void SaveAliases();

    // 添加别名
    public void AddAlias(string alias, string command);

    // 移除别名
    public void RemoveAlias(string alias);

    // 解析别名（在命令执行前）
    public string ResolveAlias(string input);

    // 列出所有别名
    public Dictionary<string, string> GetAllAliases();
}
```

**预定义别名**:
```json
{
  "n": "new",
  "ls": "list",
  "s": "session",
  "del": "delete",
  "q": "quit",
  "h": "help"
}
```

**集成到 AgentRepl**:
```csharp
// 在 HandleCommandAsync 开始时
var resolvedInput = _aliasManager.ResolveAlias(input);
```

**测试用例** (5-8 个):
- 加载别名
- 解析别名
- 添加/删除别名
- 持久化测试
- 循环别名检测

---

### Task 27: 快捷键支持 ⏳

**目标**: 实现常用快捷键

**关键快捷键**:
- `Ctrl+L`: 清屏
- `Ctrl+D`: 退出（如果输入为空）
- `Ctrl+C`: 取消当前输入

**实现方式**:
```csharp
// 由于 ReadLine 限制，可能需要结合 Console.TreatControlCAsInput
// 或者在 ReadLine 层面监听

// 在 RunAsync 主循环中
if (Console.KeyAvailable)
{
    var key = Console.ReadKey(true);
    if (key.Key == ConsoleKey.L && key.Modifiers == ConsoleModifiers.Control)
    {
        Console.Clear();
        DisplayWelcome();
    }
}
```

**注意**: ReadLine 库本身处理了很多快捷键，我们主要补充它不支持的。

---

### Task 28: 彩色输出优化 ⏳

**目标**: 统一和优化彩色输出

**颜色规范**:
```csharp
// 成功操作
AnsiConsole.MarkupLine("[green]✓ 操作成功[/]");

// 警告信息
AnsiConsole.MarkupLine("[yellow]⚠ 警告信息[/]");

// 错误信息
AnsiConsole.MarkupLine("[red]✗ 错误信息[/]");

// 提示信息
AnsiConsole.MarkupLine("[blue]ℹ 提示信息[/]");

// 强调信息
AnsiConsole.MarkupLine("[bold cyan]重要内容[/]");

// 次要信息
AnsiConsole.MarkupLine("[dim]次要内容[/]");
```

**全局审查**:
- 检查 AgentRepl.cs 中的所有输出
- 统一使用颜色规范
- 添加图标（✓ ✗ ⚠ ℹ）

---

### Task 29: 错误恢复和友好提示 ⏳

**目标**: 改进错误处理和用户提示

**错误处理模式**:
```csharp
try
{
    // 操作
}
catch (SpecificException ex)
{
    _logger.LogError(ex, "操作失败");
    AnsiConsole.MarkupLine("[red]✗ 操作失败: {0}[/]", ex.Message);
    AnsiConsole.MarkupLine("[dim]💡 提示: 具体的解决建议[/]");
}
```

**友好提示**:
```csharp
// 命令不存在时
AnsiConsole.MarkupLine("[red]✗ 未知命令: {0}[/]", command);
AnsiConsole.MarkupLine("[dim]💡 提示: 使用 /help 查看可用命令[/]");

// 参数错误时
AnsiConsole.MarkupLine("[red]✗ 参数错误[/]");
AnsiConsole.MarkupLine("[dim]用法: /session <session-id>[/]");
AnsiConsole.MarkupLine("[dim]示例: /session 12345678[/]");
```

**需要优化的地方**:
- 所有 `AnsiConsole.MarkupLine($"[red]错误: ...")` 调用
- 添加操作提示
- 改进帮助信息

---

### Task 30: 集成测试和文档 ⏳

**集成测试**:
```csharp
// EndToEndTests.cs
public class ReplEndToEndTests
{
    [Fact]
    public async Task CompleteWorkflow_ShouldWork()
    {
        // 启动 REPL
        // 创建会话
        // 发送消息
        // 搜索
        // 清理
    }

    [Fact]
    public void AliasWorkflow_ShouldWork()
    {
        // 添加别名
        // 使用别名
        // 验证结果
    }
}
```

**文档更新**:

1. **CLI_GUIDE.md** - 更新使用指南
   - 添加别名使用说明
   - 添加快捷键列表
   - 添加彩色输出说明

2. **CLI_REFERENCE.md** - 更新命令参考
   - 更新命令列表
   - 添加别名配置
   - 添加快捷键参考

3. **V3_PHASE5_COMPLETION_REPORT.md** - 创建完成报告
   - 所有 6 个 Chunks 的总结
   - 最终统计数据
   - 验收结果
   - 已知问题
   - 后续建议

---

## 📊 验收标准

### 功能验收

#### 别名系统
```bash
# 添加别名
You> /alias add n new
✓ 已添加别名: n -> new

# 使用别名
You> /n 测试会话
✓ 已创建新会话: 测试会话

# 列出别名
You> /alias list
n -> new
ls -> list
...
```

#### 快捷键
- Ctrl+L 清屏 ✓
- Ctrl+D 退出（输入为空时）✓
- 历史浏览（上下箭头）✓（已完成）

#### 彩色输出
- 成功：绿色 + ✓
- 错误：红色 + ✗
- 警告：黄色 + ⚠
- 提示：蓝色 + ℹ

#### 错误恢复
- 所有错误都有友好消息
- 提供操作建议
- 不会崩溃退出

---

## 🔧 实现建议

### 文件结构
```
v3/src/GeneralAgent.Hosts.Console/
├── Repl/
│   ├── AliasManager.cs          # 新增
│   └── ShortcutHandler.cs       # 新增（如需要）
└── AgentRepl.cs                 # 更新

v3/tests/GeneralAgent.Hosts.Console.Tests/
├── Repl/
│   └── AliasManagerTests.cs     # 新增
└── Integration/
    └── ReplEndToEndTests.cs     # 新增
```

### 别名配置文件
```
~/.agent/
├── repl_history.txt             # 已有
└── aliases.json                 # 新增
```

### 配置格式
```json
{
  "aliases": {
    "n": "new",
    "ls": "list",
    "s": "session",
    "del": "delete",
    "q": "quit",
    "h": "help"
  },
  "version": "1.0"
}
```

---

## 🎨 用户体验改进要点

### 1. 一致性
- 所有成功操作：绿色 + ✓
- 所有错误：红色 + ✗
- 所有提示：使用图标

### 2. 可发现性
- 错误消息包含操作建议
- 帮助信息易于访问
- 别名可以列出和管理

### 3. 效率
- 别名减少输入
- 快捷键快速操作
- 清晰的视觉反馈

### 4. 容错性
- 优雅的错误处理
- 不会因错误退出
- 提供恢复建议

---

## 📈 预期工作量

| 任务 | 代码量 | 测试量 | 预计时间 |
|------|--------|--------|---------|
| Task 26 (别名) | 150 行 | 8 测试 | 1 小时 |
| Task 27 (快捷键) | 50 行 | - | 0.5 小时 |
| Task 28 (彩色) | 50 行 | - | 0.5 小时 |
| Task 29 (错误) | 100 行 | - | 1 小时 |
| Task 30 (测试文档) | 100 行 | 10 测试 | 1.5 小时 |
| **总计** | **450 行** | **18 测试** | **4.5 小时** |

---

## ✅ 完成标志

Chunk 6 和整个 Phase 5 完成的标志：

1. ✅ 所有 30 个任务完成
2. ✅ 所有测试通过（目标 100+ 测试）
3. ✅ 0 编译警告
4. ✅ 手动验收 100% 通过
5. ✅ 文档完整更新
6. ✅ 性能指标达标：
   - REPL 响应时间 < 50ms
   - 搜索响应时间 < 200ms
   - 历史加载时间 < 100ms

---

## 🚀 快速开始指令（新会话）

```
继续 Phase 5 Chunk 6: 用户体验增强

前置条件：
- Chunk 1-5 已完成（命令历史、自动补全、多行输入、搜索、性能优化）
- 当前代码：2,267 行，150 个测试通过
- 剩余任务：Task 26-30

请开始实现：
1. AliasManager.cs - 命令别名系统
2. 快捷键支持（Ctrl+L, Ctrl+D）
3. 彩色输出优化
4. 错误恢复和友好提示
5. 集成测试和文档更新

参考文档：
- V3_PHASE5_PROGRESS_SUMMARY.md
- V3_PHASE5_CHUNK6_PREP.md
```

---

**文档创建**: 2026-03-25
**创建者**: Claude Sonnet 4.5
**准备开始**: Chunk 6 - 用户体验增强
