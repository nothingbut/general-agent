# V3 (C#) 开发 - 会话交接提示词

**使用此提示词**: 在新会话中直接粘贴以下内容开始 V3 开发

---

## 📍 快速上下文恢复

```
开始 General Agent V3 (C#) 版本开发。

【项目背景】
- 项目: General Agent - 通用 AI Agent 系统
- 当前版本: V2 (Rust) 已完成并合并到 main 分支
- 新任务: 开发 V3 (C#) 版本，用于 Windows 生态和企业场景
- 分支: main (最新代码)
- 最后提交: b5795b62 - V2 开发完成总结

【V2 完成情况】
✅ 9/9 核心模块全部完成
✅ TUI 性能监控集成完成 (2026-03-15)
✅ 所有测试通过，文档完整
✅ 代码质量优秀，生产就绪

【V3 目标】
- 语言: C# (.NET 8+)
- 平台: Windows 优先，跨平台支持
- 场景: 企业级应用、桌面集成
- UI: WPF/Avalonia 桌面应用

【关键参考】
- V2 架构: v2/docs/ARCHITECTURE.md
- V2 完成总结: V2_DEVELOPMENT_COMPLETE.md
- 项目 README: README.md
- CLAUDE.md: 项目指南

【建议第一步】
选项 1: 查看 V2 架构，规划 V3 架构
选项 2: 创建 V3 项目结构和初始化
选项 3: 讨论 V3 技术栈和设计决策
```

---

## 🎯 V3 开发目标

### 核心目标

**为什么开发 V3 (C#)?**
1. **Windows 生态集成**: 更好地与 Windows 系统集成
2. **企业场景**: 适合企业级应用开发
3. **桌面应用**: 提供图形界面（WPF/Avalonia）
4. **.NET 生态**: 利用丰富的 .NET 库和工具
5. **版本互补**: Python (快速原型) + Rust (高性能) + C# (企业/桌面)

### 技术栈建议

**核心框架**:
- .NET 8+ (LTS)
- C# 12+
- Entity Framework Core (数据持久化)

**UI 选项**:
- **WPF**: Windows 原生，性能好
- **Avalonia**: 跨平台，现代化
- **推荐**: Avalonia (更灵活)

**依赖管理**:
- NuGet 包管理
- 依赖注入 (Microsoft.Extensions.DependencyInjection)

**测试框架**:
- xUnit (单元测试)
- FluentAssertions (断言库)
- Moq (Mock 框架)

---

## 📊 V2 架构参考

### V2 模块映射到 V3

| V2 (Rust) | V3 (C#) 建议 | 说明 |
|-----------|--------------|------|
| `agent-core` | `GeneralAgent.Core` | 核心类型和接口 |
| `agent-storage` | `GeneralAgent.Storage` | EF Core + SQLite |
| `agent-llm` | `GeneralAgent.LLM` | LLM 客户端 |
| `agent-skills` | `GeneralAgent.Skills` | 技能系统 |
| `agent-mcp` | `GeneralAgent.MCP` | MCP 协议 |
| `agent-rag` | `GeneralAgent.RAG` | RAG 检索 |
| `agent-workflow` | `GeneralAgent.Workflow` | 工作流编排 |
| `agent-tui` | `GeneralAgent.Desktop` | WPF/Avalonia UI |
| `agent-cli` | `GeneralAgent.CLI` | 命令行工具 |

### V2 关键设计模式

1. **分层架构**:
   - Core: 核心类型和 Traits
   - Infrastructure: 存储、LLM、MCP
   - Domain: Skills、Workflow
   - Presentation: CLI、TUI

2. **依赖注入**: 所有组件通过接口解耦

3. **异步优先**: 所有 I/O 操作异步化

4. **强类型**: 类型安全，编译时检查

5. **测试驱动**: 单元测试 + 集成测试

---

## 🏗️ V3 项目结构建议

```
v3/
├── src/
│   ├── GeneralAgent.Core/              # 核心类型和接口
│   │   ├── Interfaces/                 # ILLMClient, IStorage, etc.
│   │   ├── Models/                     # Message, Session, etc.
│   │   └── Exceptions/                 # 自定义异常
│   │
│   ├── GeneralAgent.Storage/           # 数据持久化
│   │   ├── Context/                    # EF Core DbContext
│   │   ├── Repositories/               # Repository 模式
│   │   └── Migrations/                 # 数据库迁移
│   │
│   ├── GeneralAgent.LLM/               # LLM 客户端
│   │   ├── Anthropic/                  # Claude 集成
│   │   ├── OpenAI/                     # OpenAI 集成
│   │   └── Ollama/                     # Ollama 集成
│   │
│   ├── GeneralAgent.Skills/            # 技能系统
│   │   ├── Loader/                     # 技能加载器
│   │   ├── Executor/                   # 技能执行器
│   │   └── Templates/                  # 技能模板
│   │
│   ├── GeneralAgent.MCP/               # MCP 协议
│   │   ├── Protocol/                   # JSON-RPC 实现
│   │   ├── Transports/                 # Stdio, HTTP 传输
│   │   └── Security/                   # 安全管理
│   │
│   ├── GeneralAgent.RAG/               # RAG 系统
│   │   ├── Loaders/                    # 文档加载
│   │   ├── Chunkers/                   # 文本分块
│   │   ├── Embeddings/                 # 向量化
│   │   └── Retrieval/                  # 检索器
│   │
│   ├── GeneralAgent.Workflow/          # 工作流系统
│   │   ├── Orchestrator/               # 编排器
│   │   ├── Executor/                   # 执行器
│   │   ├── Performance/                # 性能监控
│   │   └── Subagent/                   # 子代理
│   │
│   ├── GeneralAgent.Desktop/           # 桌面应用 (Avalonia)
│   │   ├── Views/                      # 视图
│   │   ├── ViewModels/                 # MVVM
│   │   ├── Controls/                   # 自定义控件
│   │   └── Services/                   # UI 服务
│   │
│   └── GeneralAgent.CLI/               # 命令行工具
│       ├── Commands/                   # 命令实现
│       └── Program.cs                  # 入口
│
├── tests/
│   ├── GeneralAgent.Core.Tests/
│   ├── GeneralAgent.Storage.Tests/
│   ├── GeneralAgent.LLM.Tests/
│   ├── GeneralAgent.Skills.Tests/
│   ├── GeneralAgent.Workflow.Tests/
│   └── GeneralAgent.Integration.Tests/
│
├── docs/
│   ├── ARCHITECTURE.md                 # V3 架构文档
│   ├── API.md                          # API 设计
│   └── DEPLOYMENT.md                   # 部署指南
│
├── GeneralAgent.sln                    # 解决方案文件
├── Directory.Build.props               # 全局属性
├── Directory.Packages.props            # 中央包管理
└── README.md                           # V3 说明
```

---

## 🔧 技术决策

### 1. 数据持久化

**选择**: Entity Framework Core + SQLite

**理由**:
- EF Core 是 .NET 标准 ORM
- SQLite 轻量级，与 V2 一致
- 支持迁移和查询优化

**代码示例**:
```csharp
public class AgentDbContext : DbContext
{
    public DbSet<Session> Sessions { get; set; }
    public DbSet<Message> Messages { get; set; }
}
```

### 2. 依赖注入

**选择**: Microsoft.Extensions.DependencyInjection

**理由**:
- .NET 官方 DI 容器
- 与 ASP.NET Core 一致
- 支持生命周期管理

**代码示例**:
```csharp
services.AddScoped<ILLMClient, AnthropicClient>();
services.AddScoped<ISessionRepository, SessionRepository>();
```

### 3. 配置管理

**选择**: IConfiguration + appsettings.json

**理由**:
- .NET 标准配置系统
- 支持环境变量覆盖
- 类型安全的 Options 模式

### 4. 异步编程

**模式**: async/await + Task

**理由**:
- .NET 原生异步支持
- 非阻塞 I/O
- 与 V2 Rust 的 async 一致

### 5. UI 框架

**推荐**: Avalonia UI

**理由**:
- 跨平台（Windows, macOS, Linux）
- 现代化 XAML UI
- MVVM 架构
- 性能优秀

**备选**: WPF (仅 Windows，但更成熟)

---

## 📚 关键参考文档

### V2 文档（理解架构）
1. `v2/docs/ARCHITECTURE.md` - 系统架构
2. `V2_DEVELOPMENT_COMPLETE.md` - V2 总结
3. `v2/crates/agent-core/src/lib.rs` - 核心接口定义
4. `v2/crates/agent-workflow/src/workflow/performance.rs` - 性能监控实现

### 项目文档
1. `README.md` - 项目概述
2. `CLAUDE.md` - 开发指南
3. `docs/skills.md` - 技能系统
4. `docs/mcp.md` - MCP 集成

### V3 规划（待创建）
1. `v3/docs/ARCHITECTURE.md` - V3 架构设计
2. `v3/docs/ROADMAP.md` - V3 开发路线图
3. `v3/docs/API_DESIGN.md` - API 设计文档

---

## 🎯 第一阶段任务建议

### Week 1: 项目初始化和架构设计

**Day 1-2: 架构设计**
- [ ] 学习 V2 架构和设计模式
- [ ] 设计 V3 架构（类图、序列图）
- [ ] 确定技术栈和依赖
- [ ] 创建 `v3/docs/ARCHITECTURE.md`

**Day 3-4: 项目搭建**
- [ ] 创建 .NET 解决方案
- [ ] 配置项目结构
- [ ] 设置中央包管理
- [ ] 配置 CI/CD (GitHub Actions)

**Day 5: 核心模块**
- [ ] 实现 `GeneralAgent.Core` 项目
- [ ] 定义核心接口（ILLMClient, IStorage, etc.）
- [ ] 实现基础模型类（Message, Session, etc.）
- [ ] 编写单元测试

### Week 2: 数据层和 LLM 集成

**Day 1-2: 数据持久化**
- [ ] 实现 `GeneralAgent.Storage`
- [ ] 配置 EF Core + SQLite
- [ ] 实现 Repository 模式
- [ ] 数据库迁移

**Day 3-5: LLM 客户端**
- [ ] 实现 `GeneralAgent.LLM`
- [ ] Anthropic Claude 集成
- [ ] 流式响应支持
- [ ] 集成测试

---

## 🚀 开发流程建议

### 1. 创建新分支
```bash
git checkout -b feature/v3-initial-setup
```

### 2. 遵循 TDD
- 先写测试
- 实现功能
- 重构优化

### 3. 代码规范
- 使用 StyleCop 或 EditorConfig
- 遵循 C# 编码规范
- XML 文档注释

### 4. Git 提交格式
```
feat(core): 添加核心接口定义
test(storage): 添加 Repository 单元测试
docs(arch): 创建 V3 架构文档
```

---

## 💡 设计考虑

### 与 V2 保持一致

**保留的设计**:
1. 分层架构
2. 依赖注入
3. 异步优先
4. 强类型
5. 测试驱动

**适配 C# 特性**:
1. LINQ 查询
2. 属性（Property）语法
3. 事件（Event）机制
4. async/await 模式
5. IDisposable 模式

### C# 最佳实践

1. **命名约定**:
   - 类: PascalCase
   - 方法: PascalCase
   - 私有字段: _camelCase
   - 接口: IPascalCase

2. **异步方法**:
   - 方法名以 Async 结尾
   - 返回 Task 或 Task<T>
   - 使用 ConfigureAwait(false) (库代码)

3. **异常处理**:
   - 使用特定异常类型
   - 不要吞掉异常
   - 日志记录异常详情

4. **资源管理**:
   - 使用 using 语句
   - 实现 IDisposable
   - 避免内存泄漏

---

## 🎓 学习资源

### .NET 文档
- [.NET 8 文档](https://learn.microsoft.com/dotnet/)
- [EF Core 文档](https://learn.microsoft.com/ef/core/)
- [Avalonia 文档](https://docs.avaloniaui.net/)

### 设计模式
- Repository Pattern
- Unit of Work Pattern
- MVVM Pattern
- Factory Pattern
- Strategy Pattern

### C# 特性
- LINQ
- async/await
- IAsyncEnumerable
- Pattern Matching
- Records

---

## 📋 初始化检查清单

在开始开发前，确认以下事项：

**环境准备**:
- [ ] .NET 8 SDK 已安装
- [ ] Visual Studio 2022 或 Rider 已安装
- [ ] Git 配置正确
- [ ] SQLite 工具已安装

**项目准备**:
- [ ] 已阅读 V2 架构文档
- [ ] 已理解 V2 核心设计
- [ ] 已规划 V3 架构
- [ ] 已确定技术栈

**文档准备**:
- [ ] 创建 v3/docs 目录
- [ ] 准备 ARCHITECTURE.md 模板
- [ ] 准备 ROADMAP.md 模板

---

## ⚠️ 注意事项

### 避免的陷阱

1. **不要过度设计**: 从简单开始，逐步迭代
2. **不要重复 V2 的错误**: 学习 V2 的经验教训
3. **不要忽略测试**: 测试先行，TDD
4. **不要硬编码**: 使用配置文件
5. **不要阻塞 UI**: 所有 I/O 异步化

### 性能考虑

1. 避免过度分配（使用 Span<T>、ArrayPool）
2. 使用 ValueTask 优化热路径
3. 考虑使用 Source Generator
4. 内存池化和对象重用
5. 异步流（IAsyncEnumerable）

---

## 🎯 成功标准

### Week 1 完成标准
- ✅ V3 架构设计文档完成
- ✅ 项目结构创建完成
- ✅ 核心接口定义完成
- ✅ 基础单元测试通过

### Week 2 完成标准
- ✅ 数据层实现完成
- ✅ LLM 集成完成
- ✅ 集成测试通过
- ✅ 基本功能演示可运行

---

## 🤝 协作建议

### 与 V2 的关系
- V3 **不是** V2 的替代品
- V3 **是** V2 的补充（面向不同场景）
- 可以共享技能文件和配置
- API 设计保持一致性

### 版本选择建议
- **Python**: 快速原型、脚本自动化
- **Rust (V2)**: 高性能、系统编程、CLI/TUI
- **C# (V3)**: 企业应用、桌面应用、Windows 集成

---

## 📞 获取帮助

### 遇到问题时

1. **参考 V2 实现**: 查看 Rust 代码如何实现
2. **查阅文档**: .NET 文档、Avalonia 文档
3. **询问 Claude**: 描述问题和上下文
4. **提交 Issue**: 记录问题和解决方案

---

## 🎉 开始开发！

**准备好了吗？开始 V3 (C#) 开发之旅！**

建议的开场对话：

```
你好！我准备开始 General Agent V3 (C#) 版本的开发。

【当前状态】
- V2 (Rust) 已完成并合并到 main
- 我已阅读 V2 架构和 V3 交接文档
- 环境已准备就绪

【第一步计划】
我想先设计 V3 的架构，请帮我：
1. 分析 V2 的核心架构
2. 设计适合 C# 的架构方案
3. 创建初始项目结构

请问从哪里开始？
```

---

**交接文档版本**: 1.0
**创建日期**: 2026-03-15
**有效期**: 长期有效
**下一步**: 在新会话中使用此提示词开始 V3 开发

**祝开发顺利！** 🚀
