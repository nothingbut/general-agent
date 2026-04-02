# V3 Phase 5 实施计划 - CLI 增强和性能优化

**项目**: General Agent V3 - CLI 工具完善
**Phase**: Phase 5 - CLI Enhancement & Performance
**开始日期**: 2026-03-25
**预计工期**: 1-2 周
**状态**: 📝 计划中

---

## 📋 Phase 概述

### 目标

完善 Phase 4 构建的 CLI 工具，提升用户体验和性能：
1. **命令历史和自动补全** - 实现类似 Bash/Zsh 的交互体验
2. **性能优化** - 优化数据库查询和大数据集处理
3. **搜索功能** - 全文搜索会话和技能
4. **用户体验增强** - 多行输入、别名、快捷键

### 背景

Phase 4 成功实现了基础的 CLI 工具和 REPL，但有三个任务（Task 22-24）因需要额外的终端交互库支持而延期：
- 多行输入支持
- 命令历史记录
- 自动补全提示

Phase 5 将完成这些延期功能，并进一步优化性能和用户体验。

---

## 🎯 Phase 目标

### 功能目标

#### 1. 命令历史系统
- ✅ 使用上下箭头浏览历史命令
- ✅ 历史持久化到 `~/.agent/repl_history.txt`
- ✅ 历史搜索（Ctrl+R）
- ✅ 历史数量限制（默认 1000 条）

#### 2. 自动补全系统
- ✅ Tab 键触发补全
- ✅ 补全命令名称（/new, /list, /session 等）
- ✅ 补全会话 ID（短 ID）
- ✅ 补全技能名称（namespace:name）
- ✅ 补全文件路径（导出命令）

#### 3. 多行输入支持
- ✅ 检测多行开始标记（`"""`）
- ✅ 多行输入模式提示
- ✅ 多行结束标记（再次输入 `"""` 或空行）
- ✅ 保持缩进和格式

#### 4. 性能优化
- ✅ 数据库查询优化（索引、分页）
- ✅ 会话列表虚拟滚动
- ✅ 大对话的懒加载
- ✅ 技能加载缓存

#### 5. 搜索功能
- ✅ 会话搜索（按标题、内容、时间）
- ✅ 技能搜索（按名称、描述、标签）
- ✅ 全文搜索引擎（基于 SQLite FTS5）

#### 6. 用户体验增强
- ✅ 命令别名系统
- ✅ 快捷键支持
- ✅ 彩色输出优化
- ✅ 错误恢复机制

### 质量目标

- 测试覆盖率：≥ 80%
- REPL 响应时间：< 50ms
- 搜索响应时间：< 200ms
- 历史加载时间：< 100ms
- 0 编译警告

---

## 🔧 技术选型

### 1. 命令历史和自动补全库

#### 选项对比

| 库 | 语言 | 功能 | 跨平台 | .NET 集成 | 推荐度 |
|----|------|------|--------|-----------|--------|
| **ReadLine** (C#) | C# | ⭐⭐⭐⭐ | ✅ 是 | ✅ 原生 | ⭐⭐⭐⭐⭐ |
| System.Console.ReadLine | C# | ⭐⭐ | ✅ 是 | ✅ 内置 | ⭐⭐ |
| Prompt Toolkit (Python) | Python | ⭐⭐⭐⭐⭐ | ✅ 是 | ❌ 跨进程 | ⭐ |

#### 推荐方案：**ReadLine (C#)**

- NuGet 包：`ReadLine` (https://github.com/tonerdo/readline)
- 特点：
  - 纯 C# 实现，无外部依赖
  - 支持命令历史（上下箭头）
  - 支持自动补全（Tab 键）
  - 支持历史搜索（Ctrl+R）
  - 跨平台（Windows/Linux/macOS）
  - MIT 许可证

**使用示例**:
```csharp
using Internal.ReadLine;

// 设置补全处理器
ReadLine.AutoCompletionHandler = new AutoCompletionHandler();

// 设置历史文件
ReadLine.HistoryEnabled = true;
ReadLine.HistoryFilePath = "~/.agent/repl_history.txt";

// 读取输入（自动支持历史和补全）
string input = ReadLine.Read("You> ");
```

### 2. 全文搜索引擎

#### 选项对比

| 方案 | 性能 | 功能 | 复杂度 | 推荐度 |
|------|------|------|--------|--------|
| **SQLite FTS5** | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| Lucene.NET | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ |
| Elasticsearch | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐ |

#### 推荐方案：**SQLite FTS5**

- 理由：
  - 已有 SQLite 依赖，无需额外组件
  - 性能足够（支持 100k+ 文档）
  - 配置简单
  - 支持中文分词（使用 ICU）

**实现方案**:
```sql
-- 创建全文搜索表
CREATE VIRTUAL TABLE sessions_fts USING fts5(
    session_id UNINDEXED,
    title,
    content,
    tokenize='unicode61'
);

-- 搜索
SELECT * FROM sessions_fts WHERE sessions_fts MATCH '量子计算' ORDER BY rank;
```

### 3. 性能优化策略

#### 数据库优化
- 添加索引（会话查询、消息查询）
- 使用预编译语句（Prepared Statements）
- 批量插入优化
- 连接池配置

#### 缓存策略
- 技能列表缓存（内存）
- 会话元数据缓存（LRU）
- 查询结果缓存（TTL）

#### 分页和懒加载
- 会话列表分页（默认 20 条）
- 消息懒加载（按需加载）
- 虚拟滚动（大列表）

---

## 📝 任务分解

### Chunk 1: 命令历史系统 (Day 1-2)

**任务**:
- [ ] Task 1: 集成 ReadLine 库
- [ ] Task 2: 实现历史持久化
- [ ] Task 3: 实现历史搜索（Ctrl+R）
- [ ] Task 4: 历史管理（清理、导入、导出）
- [ ] Task 5: 单元测试

**交付物**:
- ReplHistoryManager.cs - 历史管理器
- 更新的 AgentRepl.cs - 集成 ReadLine
- 历史文件格式定义
- 10+ 单元测试

**验收标准**:
```bash
# 启动 REPL
agent

# 输入命令
You> /new 测试
You> 你好

# 使用上箭头浏览历史
You> ↑  # 显示 "你好"
You> ↑  # 显示 "/new 测试"

# 使用 Ctrl+R 搜索
You> Ctrl+R
(reverse-i-search): new
/new 测试

# 查看历史文件
cat ~/.agent/repl_history.txt
```

---

### Chunk 2: 自动补全系统 (Day 3-4)

**任务**:
- [ ] Task 6: 实现命令补全
- [ ] Task 7: 实现会话 ID 补全
- [ ] Task 8: 实现技能名称补全
- [ ] Task 9: 实现文件路径补全
- [ ] Task 10: 补全优先级和排序

**交付物**:
- AutoCompletionHandler.cs - 补全处理器
- 补全策略接口和实现
- 补全缓存机制
- 8+ 单元测试

**验收标准**:
```bash
You> /ne<Tab>  # 补全为 /new
You> /session 123<Tab>  # 补全会话 ID
You> @personal:gre<Tab>  # 补全为 @personal:greeting
You> /export 123 --output ~/d<Tab>  # 补全文件路径
```

---

### Chunk 3: 多行输入支持 (Day 5-6)

**任务**:
- [ ] Task 11: 实现多行输入模式检测
- [ ] Task 12: 多行输入编辑器
- [ ] Task 13: 多行提示和状态显示
- [ ] Task 14: 语法高亮（可选）
- [ ] Task 15: 单元测试

**交付物**:
- MultiLineInputHandler.cs - 多行输入处理器
- 更新的 AgentRepl.cs
- 6+ 单元测试

**验收标准**:
```bash
You> """
... 这是第一行
... 这是第二行
... 这是第三行
... """

# 或使用空行结束
You> """
... 第一行
... 第二行
...
```

---

### Chunk 4: 搜索功能 (Day 7-8)

**任务**:
- [ ] Task 16: 创建 FTS5 搜索表
- [ ] Task 17: 实现会话搜索命令
- [ ] Task 18: 实现技能搜索命令
- [ ] Task 19: 搜索结果高亮和排序
- [ ] Task 20: 搜索性能优化

**交付物**:
- SearchService.cs - 搜索服务
- SearchCommand.cs - 搜索命令
- 数据库迁移脚本
- 搜索索引维护
- 10+ 单元测试

**验收标准**:
```bash
# 搜索会话
agent search "量子计算" --type session
agent search "python" --type session --limit 10

# 搜索技能
agent skill search "提醒" --namespace personal
agent skill search "任务管理"

# 在 REPL 中搜索
You> /search 量子计算
You> /search --type skill --query "问候"
```

---

### Chunk 5: 性能优化 (Day 9-10)

**任务**:
- [ ] Task 21: 数据库索引优化
- [ ] Task 22: 查询优化和缓存
- [ ] Task 23: 分页和懒加载实现
- [ ] Task 24: 性能基准测试
- [ ] Task 25: 性能监控和日志

**交付物**:
- 数据库迁移（添加索引）
- CacheService.cs - 缓存服务
- PerformanceMonitor.cs - 性能监控
- 性能基准测试
- 优化报告

**验收标准**:
- 会话列表加载：< 100ms（1000+ 会话）
- 搜索响应时间：< 200ms（10000+ 消息）
- 技能加载时间：< 50ms（100+ 技能）
- 内存使用：< 100MB（正常使用）

---

### Chunk 6: 用户体验增强 (Day 11-12)

**任务**:
- [ ] Task 26: 实现命令别名系统
- [ ] Task 27: 快捷键支持
- [ ] Task 28: 彩色输出优化
- [ ] Task 29: 错误恢复和友好提示
- [ ] Task 30: 集成测试和文档

**交付物**:
- AliasManager.cs - 别名管理器
- ShortcutHandler.cs - 快捷键处理器
- 更新的文档
- 集成测试

**验收标准**:
```bash
# 命令别名
agent alias ls=list
agent alias n=new
agent ls  # 执行 list 命令

# 快捷键
Ctrl+L  # 清屏
Ctrl+D  # 退出
Ctrl+C  # 取消当前输入

# 彩色输出
[成功操作] 绿色
[警告信息] 黄色
[错误信息] 红色
[提示信息] 蓝色
```

---

## 🏗️ 架构设计

### 项目结构

```
GeneralAgent.Hosts.Console/
├── Commands/
│   ├── SearchCommand.cs        # 搜索命令（新增）
│   └── ... (现有命令)
├── Repl/
│   ├── AgentRepl.cs            # REPL 主类（更新）
│   ├── ReplHistoryManager.cs  # 历史管理器（新增）
│   ├── AutoCompletionHandler.cs # 自动补全（新增）
│   ├── MultiLineInputHandler.cs # 多行输入（新增）
│   └── AliasManager.cs         # 别名管理（新增）
├── Services/
│   ├── SearchService.cs        # 搜索服务（新增）
│   ├── CacheService.cs         # 缓存服务（新增）
│   └── PerformanceMonitor.cs   # 性能监控（新增）
└── Utils/
    └── ... (现有工具)

GeneralAgent.Infrastructure/
└── Storage/
    ├── Migrations/
    │   └── AddFullTextSearch.cs # FTS5 迁移（新增）
    └── Repositories/
        └── SearchRepository.cs   # 搜索仓储（新增）
```

### 核心类设计

#### 1. ReplHistoryManager

```csharp
public sealed class ReplHistoryManager
{
    private readonly string _historyFilePath;
    private readonly int _maxHistorySize;

    public ReplHistoryManager(string historyFilePath, int maxHistorySize = 1000);

    // 加载历史
    public List<string> LoadHistory();

    // 添加历史项
    public void AddHistoryItem(string command);

    // 搜索历史
    public List<string> SearchHistory(string query);

    // 清理历史
    public void ClearHistory();

    // 导出历史
    public void ExportHistory(string outputPath);
}
```

#### 2. AutoCompletionHandler

```csharp
public sealed class AutoCompletionHandler : IAutoCompleteHandler
{
    private readonly IServiceProvider _serviceProvider;

    // ReadLine 接口实现
    public char[] Separators { get; set; }

    public string[] GetSuggestions(string text, int index);

    // 补全策略
    private string[] CompleteCommand(string prefix);
    private string[] CompleteSessionId(string prefix);
    private string[] CompleteSkillName(string prefix);
    private string[] CompleteFilePath(string prefix);
}
```

#### 3. SearchService

```csharp
public sealed class SearchService
{
    private readonly AgentDbContext _dbContext;

    // 搜索会话
    public Task<PagedResult<Session>> SearchSessionsAsync(
        string query,
        int limit = 20,
        int offset = 0);

    // 搜索技能
    public Task<List<Skill>> SearchSkillsAsync(
        string query,
        string? namespace = null);

    // 搜索消息内容
    public Task<List<Message>> SearchMessagesAsync(
        string query,
        Guid? sessionId = null);
}
```

---

## 🧪 测试策略

### 单元测试（80+ 个）

**历史管理测试**:
- 历史加载和保存
- 历史搜索
- 历史大小限制
- 并发访问

**补全测试**:
- 命令补全
- ID 补全
- 技能名称补全
- 补全优先级

**搜索测试**:
- 全文搜索准确性
- 搜索排序
- 中文搜索支持
- 性能测试

**性能测试**:
- 大数据集加载
- 缓存命中率
- 查询响应时间
- 内存使用

### 集成测试（15+ 个）

1. 历史系统集成测试
2. 补全系统集成测试
3. 多行输入集成测试
4. 搜索功能集成测试
5. 性能基准测试

### 性能基准

| 操作 | 数据规模 | 目标时间 |
|------|---------|---------|
| 会话列表加载 | 1000 会话 | < 100ms |
| 搜索查询 | 10000 消息 | < 200ms |
| 技能加载 | 100 技能 | < 50ms |
| 历史加载 | 1000 条 | < 100ms |
| 补全响应 | - | < 50ms |

---

## 📊 验收标准

### 功能完整性（100%）

- [ ] 命令历史功能完整
- [ ] 自动补全功能完整
- [ ] 多行输入功能完整
- [ ] 搜索功能完整
- [ ] 性能优化完成
- [ ] 用户体验增强完成

### 质量标准（100%）

- [ ] 单元测试覆盖率 ≥ 80%
- [ ] 所有集成测试通过
- [ ] 性能基准达标
- [ ] 0 编译警告
- [ ] 手动验收 100% 通过

### 文档完整性（100%）

- [ ] 更新 CLI_GUIDE.md
- [ ] 更新 CLI_REFERENCE.md
- [ ] 创建 Phase 5 完成报告
- [ ] 性能优化文档

---

## 📈 时间规划

| Chunk | 任务范围 | 时间 | 交付物 |
|-------|---------|------|--------|
| Chunk 1 | 命令历史系统 | Day 1-2 | 历史管理器 + 测试 |
| Chunk 2 | 自动补全系统 | Day 3-4 | 补全处理器 + 测试 |
| Chunk 3 | 多行输入支持 | Day 5-6 | 多行输入处理 + 测试 |
| Chunk 4 | 搜索功能 | Day 7-8 | 搜索服务 + 命令 |
| Chunk 5 | 性能优化 | Day 9-10 | 缓存 + 索引 + 测试 |
| Chunk 6 | 用户体验增强 | Day 11-12 | 别名 + 快捷键 + 文档 |

**总工期**: 12 天（1.5-2 周）

---

## ⚠️ 风险和应对

### 风险 1: ReadLine 库兼容性

**风险**: ReadLine 库在某些终端上可能不稳定

**应对**:
- 充分测试各平台（Windows/Linux/macOS）
- 提供降级方案（使用 Console.ReadLine）
- 允许用户禁用高级功能

### 风险 2: FTS5 性能问题

**风险**: 大量数据时 FTS5 性能可能不足

**应对**:
- 实现分片索引
- 添加结果数量限制
- 考虑异步索引更新

### 风险 3: 多行输入用户体验

**风险**: 多行输入的交互可能不够直观

**应对**:
- 提供清晰的提示和帮助
- 支持多种结束方式
- 允许用户配置行为

---

## 🎯 成功标准

Phase 5 完成的标志：

1. ✅ 所有 30 个任务完成
2. ✅ 80+ 测试全部通过
3. ✅ 性能基准全部达标
4. ✅ 手动验收 100% 通过
5. ✅ 文档完整清晰
6. ✅ 用户反馈积极
7. ✅ 0 编译警告

---

## 📚 参考资料

### NuGet 包

- **ReadLine**: https://github.com/tonerdo/readline
- **SQLite FTS5**: https://www.sqlite.org/fts5.html

### 技术文档

- [Spectre.Console 文档](https://spectreconsole.net/)
- [System.CommandLine 文档](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)
- [EF Core 性能优化](https://learn.microsoft.com/en-us/ef/core/performance/)

### 相关项目

- [Oh My Zsh](https://ohmyz.sh/) - 命令行增强参考
- [PowerShell PSReadLine](https://github.com/PowerShell/PSReadLine) - 历史和补全参考

---

**计划创建**: 2026-03-25
**创建者**: Claude Sonnet 4.5
**Phase 5 状态**: 📝 计划完成，准备执行
