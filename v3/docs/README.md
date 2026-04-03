# V3 文档索引 (C# 版本)

General Agent V3 是使用 C# 和 .NET 8 重写的企业级版本，采用清晰架构和领域驱动设计。

## 📚 快速参考

- [手动验收检查清单](MANUAL_ACCEPTANCE_CHECKLIST.md) - 手动测试检查项
- [快速修复指南](QUICK_FIX_GUIDE.md) - 常见问题快速解决方案

## 📖 用户指南

- [CLI 使用指南](guides/CLI_GUIDE.md) - 命令行工具使用说明
- [CLI 命令参考](guides/CLI_REFERENCE.md) - 完整命令列表和参数
- [技能系统指南](guides/SKILLS_GUIDE.md) - 自定义技能开发指南
- [文件上传用户指南](guides/FILE_UPLOAD_USER_GUIDE.md) - 文件上传功能使用说明

## 🎯 功能特性

- [工具调用](features/tool-calling.md) - 工具调用机制说明
- [优先功能路线图](features/priority-features.md) - 功能优先级和规划
- [文件上传计划](features/file-upload-plan.md) - 文件上传功能实现计划

## 🚀 发布管理

### V3.1 发布
- [V3.1 新功能](releases/v3.1/V3.1_FEATURES.md) - 新功能列表
- [V3.1 发布检查清单](releases/v3.1/V3.1_RELEASE_CHECKLIST.md) - 发布前检查项
- [V3.1 UAT 计划](releases/v3.1/V3.1_UAT_PLAN.md) - 用户验收测试计划
- [V3.1 UAT 报告](releases/v3.1/V3.1_UAT_REPORT.md) - 用户验收测试报告

## 📦 归档文档

V3 的历史文档已归档至公共文档目录：

- [交接文档](../../docs/archives/v3/handoffs/) - 5 个阶段交接文档
- [阶段文档](../../docs/archives/v3/phases/) - 17 个阶段实施和完成文档

---

## 🏗️ 技术栈

- **语言**: C# 13
- **框架**: .NET 8.0
- **架构**: 清晰架构 (Clean Architecture)
- **数据库**: SQLite (Entity Framework Core 9.0)
- **LLM 集成**: Anthropic Claude API
- **向量数据库**: Qdrant
- **测试框架**: xUnit + FluentAssertions + NSubstitute
- **日志**: Microsoft.Extensions.Logging
- **依赖注入**: Microsoft.Extensions.DependencyInjection

## 📋 项目结构

```
v3/
├── src/
│   ├── GeneralAgent.Core/                    # 核心领域模型
│   ├── GeneralAgent.Application/             # 应用服务层
│   ├── GeneralAgent.Infrastructure.Storage/  # 数据持久化
│   ├── GeneralAgent.Infrastructure.LLM/      # LLM 集成
│   ├── GeneralAgent.Infrastructure.Skills/   # 技能系统
│   ├── GeneralAgent.Infrastructure.MCP/      # MCP 协议
│   ├── GeneralAgent.Infrastructure.Memory/   # 记忆系统
│   ├── GeneralAgent.Infrastructure.FileStorage/ # 文件存储
│   └── GeneralAgent.CLI/                     # 命令行工具
├── tests/                                     # 测试项目
├── docs/                                      # 文档目录
└── examples/                                  # 示例代码
```

## 🚀 快速开始

### 构建和运行

```bash
# 还原依赖
dotnet restore

# 构建项目
dotnet build

# 运行 CLI
dotnet run --project src/GeneralAgent.CLI/GeneralAgent.CLI.csproj

# 运行测试
dotnet test

# 测试覆盖率
dotnet test --collect:"XPlat Code Coverage"
```

### 使用示例

```bash
# 创建新会话
dotnet run --project src/GeneralAgent.CLI -- new --title "我的会话"

# 开始对话
dotnet run --project src/GeneralAgent.CLI -- chat <session-id>

# 列出所有会话
dotnet run --project src/GeneralAgent.CLI -- list

# 使用技能
@greeting user_name='Alice'

# 上传文件
/upload /path/to/file.txt

# 引用文件
请分析 @file:config.json 的内容
```

## 🧪 测试策略

### 测试类型
- **单元测试**: 独立功能单元测试
- **集成测试**: 跨模块集成测试
- **端到端测试**: 完整用户场景测试

### 运行测试

```bash
# 所有测试
dotnet test

# 特定项目
dotnet test tests/GeneralAgent.Infrastructure.FileStorage.Tests/

# 特定测试
dotnet test --filter "FullyQualifiedName~FileStorageServiceTests"

# 带覆盖率
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# 生成覆盖率报告
reportgenerator -reports:"coverage/**/*.xml" -targetdir:"coverage/html" -reporttypes:Html
```

## 🔧 配置管理

### 环境变量

```bash
# LLM 配置
export ANTHROPIC_API_KEY=sk-ant-xxx
export ANTHROPIC_MODEL=claude-sonnet-4-5

# 数据库配置
export DATABASE_PATH=/path/to/database.db

# 向量数据库配置
export QDRANT_URL=http://localhost:6333
export QDRANT_COLLECTION=general-agent

# 文件存储配置
export FILE_STORAGE_ROOT=/path/to/storage
export FILE_STORAGE_MAX_SIZE=10485760  # 10MB
```

### 配置文件

配置文件位于 `src/GeneralAgent.CLI/appsettings.json`：

```json
{
  "Anthropic": {
    "ApiKey": "",
    "Model": "claude-sonnet-4-5"
  },
  "Database": {
    "ConnectionString": "Data Source=data/general-agent.db"
  },
  "FileStorage": {
    "RootDirectory": "data/files",
    "MaxFileSize": 10485760,
    "SupportedExtensions": [".txt", ".md", ".json", ".cs", ".py"]
  }
}
```

## 📊 性能指标

- **启动时间**: < 2 秒
- **消息响应**: < 500ms (不含 LLM 调用)
- **数据库查询**: < 100ms
- **文件处理**: < 1s (10MB 文件)
- **向量检索**: < 200ms (10k 向量)

## 🔒 安全考虑

- **API 密钥**: 使用环境变量或密钥管理服务
- **文件上传**: 验证文件类型和大小限制
- **路径遍历**: 防止目录遍历攻击
- **SQL 注入**: 使用参数化查询
- **XSS 防护**: 对用户输入进行清理

## 📌 相关资源

- [V3 README](../README.md) - V3 项目总览
- [根目录文档](../../docs/README.md) - 公共文档
- [V2 文档](../../v2/docs/README.md) - Rust 版本文档
- [CLAUDE.md](../../CLAUDE.md) - AI 辅助开发指南

## 🔗 外部资源

- [.NET 文档](https://docs.microsoft.com/dotnet/)
- [C# 编程指南](https://docs.microsoft.com/dotnet/csharp/)
- [Entity Framework Core](https://docs.microsoft.com/ef/core/)
- [xUnit 文档](https://xunit.net/)
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)

## 🆘 常见问题

### 构建失败

```bash
# 清理并重新构建
dotnet clean
dotnet restore
dotnet build
```

### 测试失败

```bash
# 查看详细错误信息
dotnet test --logger "console;verbosity=detailed"
```

### 数据库问题

```bash
# 重置数据库
rm data/general-agent.db
dotnet run --project src/GeneralAgent.CLI -- migrate
```

### 文件上传问题

检查配置：
- 文件大小是否超过限制
- 文件类型是否受支持
- 存储目录权限是否正确

---

**最后更新**: 2026-04-03
