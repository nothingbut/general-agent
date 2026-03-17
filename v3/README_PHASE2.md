# General Agent V3 - Phase 2: LLM 集成

**版本**: v3.0.0-phase2
**状态**: ✅ 完成
**日期**: 2026-03-17

---

## 📖 概述

General Agent V3 Phase 2 实现了与本地 LLM 平台的完整集成，提供交互式 Console REPL 界面。

### 核心功能

- ✅ 支持多个 LLM 提供商（Ollama, LMStudio, llama.cpp, OMLX）
- ✅ OpenAI 兼容 API 客户端
- ✅ 流式和非流式对话
- ✅ 会话管理和持久化
- ✅ 交互式 REPL 界面
- ✅ 运行时提供商切换

---

## 🚀 快速开始

### 前置要求

- .NET 10.0 SDK
- (可选) Ollama 或其他 LLM 提供商

### 安装和运行

```bash
# 1. 克隆仓库
git clone <repo-url>
cd general-agent/v3

# 2. 构建项目
dotnet build

# 3. 运行测试
dotnet test

# 4. 启动 Console REPL
cd src/GeneralAgent.Hosts.Console
dotnet run
```

### 使用 Ollama（推荐）

```bash
# 安装 Ollama
brew install ollama  # macOS
# 或访问 https://ollama.ai/download

# 启动服务
ollama serve

# 拉取模型
ollama pull qwen2.5:0.5b
```

---

## 🎮 使用指南

### REPL 命令

| 命令 | 说明 | 示例 |
|------|------|------|
| `/help` | 显示帮助信息 | `/help` |
| `/new [title]` | 创建新会话 | `/new 测试会话` |
| `/list` | 列出所有会话 | `/list` |
| `/switch <provider>` | 切换 LLM 提供商 | `/switch LMStudio` |
| `/provider` | 显示当前提供商 | `/provider` |
| `/history` | 显示当前会话历史 | `/history` |
| `/exit` | 退出 REPL | `/exit` |

### 示例对话

```
You> 你好
Assistant> 你好！我是一个 AI 助手。有什么我可以帮助你的吗？

You> 介绍一下你自己
Assistant> 我是一个基于大语言模型的 AI 助手...

You> /history
会话历史 (共 4 条消息):

You> 你好
Assistant> 你好！我是一个 AI 助手...

You> 介绍一下你自己
Assistant> 我是一个基于大语言模型的 AI 助手...

You> /exit
再见！
```

---

## ⚙️ 配置

### appsettings.json

```json
{
  "ConnectionStrings": {
    "AgentDb": "Data Source=agent.db"
  },
  "LLM": {
    "DefaultProvider": "Ollama",
    "Providers": {
      "Ollama": {
        "Name": "Ollama",
        "BaseUrl": "http://localhost:11434",
        "DefaultModel": "qwen2.5:0.5b",
        "TimeoutSeconds": 120
      },
      "LMStudio": {
        "Name": "LMStudio",
        "BaseUrl": "http://localhost:1234",
        "DefaultModel": "local-model",
        "TimeoutSeconds": 120
      }
    }
  }
}
```

### 添加新的 LLM 提供商

1. 在 `appsettings.json` 的 `LLM.Providers` 中添加配置
2. 确保提供商实现 OpenAI 兼容 API
3. 重启应用

示例：

```json
"MyProvider": {
  "Name": "MyProvider",
  "BaseUrl": "http://localhost:8080",
  "DefaultModel": "my-model",
  "TimeoutSeconds": 120
}
```

---

## 🏗️ 架构

### 项目结构

```
v3/
├── src/
│   ├── GeneralAgent.Core/              # 核心接口和模型
│   ├── GeneralAgent.Infrastructure/     # 数据持久化
│   ├── GeneralAgent.Infrastructure.LLM/ # LLM 客户端实现
│   ├── GeneralAgent.Application/        # 业务逻辑层
│   └── GeneralAgent.Hosts.Console/      # Console REPL
└── tests/
    ├── GeneralAgent.Core.Tests/
    ├── GeneralAgent.Infrastructure.Tests/
    ├── GeneralAgent.Infrastructure.LLM.Tests/
    ├── GeneralAgent.Application.Tests/
    └── GeneralAgent.Integration.Tests/
```

### 分层设计

```
Console REPL (Presentation)
    ↓
Application (Business Logic)
    ↓
Infrastructure.LLM + Infrastructure (Data Access)
    ↓
Core (Domain Models & Interfaces)
```

### 关键组件

**Core 层**:
- `ILLMClient` - LLM 客户端接口
- `ILLMClientFactory` - 客户端工厂
- `CompletionRequest/Response` - API 模型
- `Session`, `Message` - 领域模型

**Infrastructure.LLM 层**:
- `OpenAICompatibleClient` - OpenAI 兼容客户端
- `LLMClientFactory` - 多提供商管理
- `LLMOptions` - 配置模型

**Application 层**:
- `SessionService` - 会话 CRUD
- `ConversationService` - 对话编排
- `MockLLMClient` - 测试 Mock

**Console 层**:
- `AgentRepl` - REPL 主循环
- `Program.cs` - 应用入口

---

## 🧪 测试

### 运行测试

```bash
# 所有测试
dotnet test

# 特定测试项目
dotnet test tests/GeneralAgent.Application.Tests/

# 集成测试（需要数据库）
dotnet test tests/GeneralAgent.Integration.Tests/

# 带覆盖率
dotnet test --collect:"XPlat Code Coverage"
```

### 测试统计

- **单元测试**: 217 tests
  - Core: 73 tests
  - Infrastructure: 14 tests
  - Infrastructure.LLM: 76 tests (1 skipped)
  - Application: 54 tests
- **集成测试**: 5 tests
- **总计**: 222 tests
- **覆盖率**: 80%+

---

## 🔧 开发

### 构建

```bash
# 完整构建
dotnet build

# Release 构建
dotnet build -c Release

# 清理
dotnet clean
```

### 添加新功能

1. 在 Core 层定义接口
2. 在 Infrastructure 层实现
3. 在 Application 层编排
4. 在 Console 层集成
5. 编写单元测试和集成测试

### 代码风格

- 使用中文注释和文档
- 遵循 C# 命名约定
- 保持不可变性（使用 `with` 表达式）
- 单一职责原则
- 依赖注入

---

## 🐛 故障排查

### 常见问题

#### 1. "Connection refused" 错误

**原因**: LLM 提供商未运行

**解决**:
```bash
# Ollama
ollama serve

# LMStudio
# 启动 LMStudio 并加载模型
```

#### 2. "未找到数据库连接字符串"

**原因**: `appsettings.json` 配置缺失

**解决**: 确保 `appsettings.json` 在输出目录，并检查连接字符串配置

#### 3. 数据库迁移错误

**解决**:
```bash
# 删除旧数据库
rm agent.db

# 重新启动应用（自动创建数据库）
dotnet run
```

#### 4. "未知提供商" 错误

**原因**: 提供商名称不在配置中

**解决**: 检查 `appsettings.json` 中的 `LLM.Providers` 配置

---

## 📚 API 文档

### ILLMClient

```csharp
public interface ILLMClient
{
    // 非流式补全
    Task<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken ct = default);

    // 流式补全
    IAsyncEnumerable<StreamChunk> StreamAsync(
        CompletionRequest request,
        CancellationToken ct = default);
}
```

### SessionService

```csharp
// 创建会话
var session = await sessionService.CreateSessionAsync("标题");

// 获取会话
var session = await sessionService.GetSessionAsync(sessionId);

// 列出会话
var pagedResult = await sessionService.ListSessionsAsync(limit: 10);

// 更新标题
var updated = await sessionService.UpdateSessionTitleAsync(sessionId, "新标题");

// 删除会话
await sessionService.DeleteSessionAsync(sessionId);
```

### ConversationService

```csharp
// 非流式对话
var response = await conversationService.SendMessageAsync(
    sessionId,
    "你好",
    providerName: "Ollama");

// 流式对话
await foreach (var chunk in conversationService.SendMessageStreamAsync(
    sessionId,
    "你好",
    providerName: "Ollama"))
{
    Console.Write(chunk);
}
```

---

## 🔐 安全性

### 数据隐私

- 所有数据存储在本地 SQLite 数据库
- 不上传任何数据到云端
- LLM 提供商可以是本地服务（Ollama, llama.cpp）

### 配置安全

- API 密钥（如需要）应存储在环境变量或安全配置中
- 不要将敏感信息提交到版本控制

---

## 📈 性能

### 优化建议

1. **流式输出**: 优先使用流式 API 以获得更好的响应体验
2. **连接池**: HttpClient 使用连接池，避免频繁创建连接
3. **数据库索引**: Session 和 Message 表已建立索引
4. **超时设置**: 根据模型大小调整 `TimeoutSeconds`

### 基准测试

- 小型模型（0.5B-7B）: 响应时间 < 5s
- 中型模型（13B-30B）: 响应时间 5-15s
- 大型模型（70B+）: 响应时间 15s+

---

## 🛣️ 路线图

### Phase 3（计划中）

- [ ] Web API 接口
- [ ] Subagent 系统集成
- [ ] 技能系统（Skills）
- [ ] RAG 检索增强

### Phase 4（计划中）

- [ ] MCP 协议支持
- [ ] Workflow 编排
- [ ] TUI 界面（Textual）
- [ ] 插件系统

---

## 🤝 贡献

### 如何贡献

1. Fork 仓库
2. 创建特性分支 (`git checkout -b feature/amazing-feature`)
3. 提交变更 (`git commit -m 'feat: add amazing feature'`)
4. 推送分支 (`git push origin feature/amazing-feature`)
5. 创建 Pull Request

### 开发规范

- 所有代码必须有单元测试
- 保持测试覆盖率 80%+
- 遵循 TDD 流程
- 使用中文编写文档和注释

---

## 📝 许可证

[添加许可证信息]

---

## 📞 联系方式

- **项目地址**: [添加 GitHub 仓库地址]
- **问题反馈**: [添加 Issues 链接]
- **文档**: 查看 `docs/` 目录

---

## 🙏 致谢

- [Ollama](https://ollama.ai/) - 本地 LLM 运行时
- [LM Studio](https://lmstudio.ai/) - LLM 管理工具
- [Spectre.Console](https://spectreconsole.net/) - 美观的 CLI 界面库

---

**最后更新**: 2026-03-17
**版本**: Phase 2 完成
