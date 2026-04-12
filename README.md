# General Agent

<div align="center">

**通用 AI Agent 系统，支持技能系统、LLM 集成和工作流编排**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/nothingbut/general-agent)](https://github.com/nothingbut/general-agent/releases)

[功能特性](#-功能特性) • [快速开始](#-快速开始) • [文档](#-文档) • [版本说明](#-版本说明)

</div>

---

## 🎉 最新版本: V3.2.0 - 完整 Agent 能力 🎊

**V3.2 完成所有 5 个用户优先功能，系统现已具备完整的通用 Agent 核心能力！**

<div align="center">

| 🧠 长期记忆 | 📦 文件上传 | 🤖 技能抽取 | ⏰ 计划任务 |
|------------|-------------|-------------|-------------|
| 向量检索 | 跨会话访问 | 对话自动生成 | Cron + 自然语言 |
| 语义搜索 | 权限管理 | LLM 驱动 | 三种任务类型 |
| 混合搜索 | 版本控制 | 历史记录 | 重试和超时 |

[📖 功能清单](v3/docs/features/priority-features.md) • [📝 更新日志](CHANGELOG.md)

</div>

### ✨ V3.2 - 5 个优先功能全部完成

1. **✅ 长期记忆系统** - 向量化、语义搜索、混合检索
2. **✅ 上下文压缩** - 三种策略、智能选择
3. **✅ 文件上传** - 跨会话访问、权限管理、版本控制
4. **✅ 对话抽取 Skill** - LLM 驱动、自动生成、历史记录
5. **✅ 计划任务** - Cron 表达式、自然语言、后台调度 **NEW!**

### 🆕 V3.1 新增亮点

#### 🔍 智能搜索系统
- **自然语言查询** - 使用 `/search "上周关于Python的讨论"` 进行语义搜索
- **多字段检索** - 同时搜索标题、内容、标签，支持时间范围过滤
- **LLM 增强** - 智能理解查询意图，自动扩展同义词
- **LRU 查询缓存** - 常用查询响应时间 <50ms，缓存命中率 >70%

#### 🏷️ 智能标签系统
- **自动标签建议** - LLM 分析会话内容，智能推荐相关标签
- **自定义样式** - 支持 Emoji 和颜色标记（如 `🐍 Python #FF6347`）
- **批量管理** - 添加、移除、查看标签，支持全局统计
- **多来源支持** - User、LLM、System 三种标签来源

**详细功能文档**: [V3.1_FEATURES.md](v3/docs/V3.1_FEATURES.md)

---

## ✨ 功能特性

### 🎨 现代化 CLI 工具

- **美观的界面** - 使用 Spectre.Console，支持彩色输出和表格
- **交互式 REPL** - 流畅的对话体验，实时响应
- **命令历史** - 5000 条历史记录，↑↓ 键浏览
- **自动补全** - Tab 键补全命令、会话 ID、技能名称
- **多行输入** - 支持 `"""` 标记的多行输入
- **命令别名** - 自定义快捷命令（如 `/n` → `/new`）

### 📚 灵活的技能系统

```markdown
---
name: greeting
description: 向用户问候
parameters:
  - name: user_name
    type: string
    required: true
---

你好 {user_name}！今天有什么我可以帮助你的吗？
```

- **简单定义** - YAML + Markdown 格式
- **参数验证** - 自动验证和类型转换
- **命名空间** - 组织和管理技能
- **热加载** - 修改即生效（开发模式）

### 🤖 LLM 集成

- **Anthropic Claude** - 支持 Claude 3.5 Sonnet
- **Ollama** - 本地 LLM 部署（qwen2.5, llama3 等）
- **流式响应** - 实时显示生成内容
- **提供商切换** - 轻松切换不同 LLM

### 💾 会话管理

- **SQLite 持久化** - 可靠的数据存储
- **会话历史** - 完整的对话记录
- **分页支持** - 高效处理大量数据

### 🔍 智能搜索 (V3.1+)

- **自然语言查询** - 使用自然语言描述搜索需求
- **多字段检索** - 同时搜索标题、内容、标签
- **时间范围过滤** - 支持相对时间（如 "上周"）和绝对时间
- **LLM 增强** - 智能理解查询意图，自动扩展关键词
- **查询缓存** - LRU 缓存加速常用查询（命中率 >70%）

### 🏷️ 智能标签 (V3.1+)

- **自动标签建议** - LLM 分析会话内容推荐标签
- **自定义样式** - 支持 Emoji 🎨 和颜色 #FF6347
- **批量管理** - 添加、移除、列出标签
- **多来源追踪** - User、LLM、System 三种来源
- **全局统计** - 查看所有会话的标签使用情况

### ⏰ 计划任务 (V3.2+)

- **灵活调度** - 支持 Cron 表达式和中文自然语言（"每天9点"）
- **三种任务类型** - 技能调用、记忆提醒、自定义命令
- **完整生命周期** - 创建、暂停、恢复、删除、手动执行
- **重试机制** - 指数退避，可配置最大重试次数
- **执行历史** - 完整记录每次执行的结果和日志
- **后台服务** - 自动调度，优雅关闭

### 🧠 长期记忆 (V3.0+)

- **向量化存储** - Qdrant 向量数据库，语义搜索
- **混合检索** - 关键词 + 语义，双重保障
- **五种记忆类型** - User、Feedback、Project、Reference、Knowledge
- **LLM 驱动提取** - 自动从对话中提取记忆
- **自动降级** - Qdrant 不可用时降级到关键词搜索

### 📦 文件上传 (V3.0+)

- **跨会话访问** - Private、Shared、Public 三级权限
- **版本控制** - 历史版本、恢复到指定版本
- **20+ 文件类型** - 文本、代码、配置文件
- **对话引用** - `@file:filename.txt` 语法
- **权限管理** - 授予、撤销读写权限

### 🤖 对话抽取 Skill (V3.0+)

- **LLM 驱动** - 自动从对话识别可复用模式
- **交互式编辑** - 生成后可编辑和确认
- **命名空间管理** - 组织和管理技能
- **抽取历史** - 查看历史记录和统计
- **缓存优化** - 重复请求 <10ms

### ⚡ 卓越性能

| 指标 | V3.1 性能 | V3.0 性能 | 提升 |
|------|----------|----------|------|
| REPL 启动 | 150ms | 150ms | - |
| 命令响应 | 30ms | 30ms | - |
| 技能加载 | 5ms | 5ms | - |
| **搜索查询** | **<100ms** | N/A | **新增** 🚀 |
| **缓存命中** | **<50ms** | N/A | **新增** 🚀 |
| **标签建议** | **<5s** | N/A | **新增** 🚀 |
| 内存占用 | 55MB | 50MB | +10% |

---

## 🚀 快速开始

### 系统要求

- **.NET SDK**: 10.0 或更高
- **操作系统**: macOS 14+, Ubuntu 22.04+, Windows 11+
- **可选**: Ollama（用于本地 LLM）

### 安装

#### 方式 1: 从源码构建

```bash
# 1. 克隆仓库
git clone https://github.com/nothingbut/general-agent.git
cd general-agent

# 2. 构建 V3
cd v3
dotnet build --configuration Release

# 3. 运行
cd src/GeneralAgent.Hosts.Console
dotnet run
```

#### 方式 2: 下载预编译版本

从 [Releases](https://github.com/nothingbut/general-agent/releases/tag/v3.1.0) 页面下载对应平台的版本。

### 基础使用

#### CLI 命令模式

```bash
# 创建会话
dotnet run -- new --title "我的会话"

# 列出会话
dotnet run -- list

# 发送消息
dotnet run -- chat <session-id> "你好"

# 查看技能
dotnet run -- skill list
dotnet run -- skill info personal:greeting
```

#### REPL 交互模式

```bash
# 启动 REPL
dotnet run

# 在 REPL 中
You> /help                     # 查看帮助
You> /new 测试会话             # 创建会话
You> 你好，请介绍一下自己      # 对话
You> /skills                   # 列出技能
You> @personal:greeting user_name='Alice'  # 调用技能

# V3.1 新增命令
You> /search "上周关于Python的讨论"    # 自然语言搜索
You> /tag add Python --emoji 🐍 --color #FFD43B  # 添加标签
You> /tag suggest              # 智能标签建议
You> /tag list                 # 列出当前会话标签
You> /tag list --all           # 查看全局标签统计

# V3.2 新增命令
You> agent task schedule "每日提醒" --schedule "每天9:00" --type reminder --payload '{"message":"开始工作"}'
You> agent task list           # 列出所有任务
You> agent task show <task-id> # 查看任务详情
You> agent task pause <task-id> # 暂停任务
You> agent task run <task-id>  # 立即执行任务

You> /exit                     # 退出
```

#### 使用别名和快捷键

```bash
# 预定义别名
You> /n 新会话        # 等同于 /new
You> /ls              # 等同于 /list
You> /q               # 等同于 /quit

# 自定义别名
You> /alias add c chat
You> /c "你好"

# 快捷键
↑/↓     # 浏览历史
Tab     # 自动补全
"""     # 多行输入
```

### 配置

编辑 `v3/src/GeneralAgent.Hosts.Console/appsettings.json`:

```json
{
  "LLM": {
    "DefaultProvider": "Anthropic",
    "Providers": {
      "Anthropic": {
        "ApiKey": "YOUR_API_KEY",
        "Model": "claude-3-5-sonnet-20241022",
        "MaxTokens": 8096
      },
      "Ollama": {
        "Model": "qwen2.5:7b",
        "BaseUrl": "http://localhost:11434"
      }
    }
  }
}
```

---

## 📚 文档

### 用户文档

- **[CLI 使用指南](v3/docs/guides/CLI_GUIDE.md)** - 详细的使用说明
- **[CLI 命令参考](v3/docs/guides/CLI_REFERENCE.md)** - 所有命令的完整参考
- **[技能系统指南](v3/docs/guides/SKILLS_GUIDE.md)** - 如何创建和使用技能
- **[计划任务指南](v3/docs/features/scheduled-tasks-user-guide.md)** - 计划任务完整使用指南 **NEW!**
- **[文件上传指南](v3/docs/guides/FILE_UPLOAD_USER_GUIDE.md)** - 文件上传和管理
- **[优先功能清单](v3/docs/features/priority-features.md)** - 5 个优先功能完成情况
- **[发布说明](RELEASE_NOTES_V3.0.0.md)** - V3.0.0 完整发布说明

### 开发文档

- **[项目指南 (CLAUDE.md)](CLAUDE.md)** - 项目概述和开发指南
- **[UAT 测试计划](V3_UAT_PLAN.md)** - 用户验收测试计划
- **[UAT 测试报告](V3_UAT_REPORT.md)** - 测试结果和质量报告
- **[项目总结](V3_PROJECT_SUMMARY.md)** - 项目统计和成就

### Phase 文档

- Phase 1: 核心架构
- Phase 2: LLM 集成
- Phase 3: 技能系统
- Phase 4: CLI 工具
- Phase 5: 用户体验增强

完整的 Phase 文档请查看根目录的 `V3_PHASE*_COMPLETION_REPORT.md` 文件。

---

## 📁 项目结构

```
general-agent/
├── v3/                         ⭐ V3 (.NET 10) - 最新版本
│   ├── src/
│   │   ├── GeneralAgent.Core/              # 核心模型和接口
│   │   ├── GeneralAgent.Infrastructure/    # 数据访问和基础设施
│   │   ├── GeneralAgent.Application/       # 业务逻辑
│   │   └── GeneralAgent.Hosts.Console/     # CLI 和 REPL
│   ├── tests/                              # 单元测试和集成测试
│   └── docs/                               # V3 文档
│
├── v2/                         # Rust 版本（已暂停）
│   └── crates/                 # Rust 实现
│
├── v1/                         # Python 版本（维护模式）
│   └── src/                    # Python 实现
│
├── docs/                       # 通用文档
├── CLAUDE.md                   # 项目开发指南
├── README.md                   # 本文件
└── RELEASE_NOTES_V3.0.0.md    # 发布说明
```

---

## 🔧 版本说明

### V3.0.0 (.NET) - 当前版本 ⭐

**状态**: ✅ 生产就绪
**技术栈**: .NET 10, C# 13, SQLite
**发布日期**: 2026-03-25

**特点**:
- 🚀 高性能（启动 150ms，响应 30ms）
- 🎨 现代化 CLI 和 REPL
- 📚 完整的技能系统
- 🧪 567 个测试（100% 通过）
- 📖 完整的文档

[查看详细发布说明 →](RELEASE_NOTES_V3.0.0.md)

### V2 (Rust) - 已暂停

**状态**: 🚧 开发暂停
**原因**: 转向 V3 (.NET) 开发

保留代码供参考，未来可能恢复开发。

### V1 (Python) - 维护模式

**状态**: 🔒 维护模式
**用途**: 功能参考，快速原型

基础功能完整，仅修复严重 Bug。

---

## 🎯 开发统计

```
项目周期: 38 天（2026-03-17 至 2026-04-09）
完成功能: 5 个优先功能 + 核心系统
总任务数: 150+ 个
代码行数: ~16,000 行
测试代码: ~11,000 行
测试数量: 864 个（586 + 111 + 56 + 111）
测试通过率: 100%
代码覆盖率: 85%+
编译警告: 0
```

### 功能完成度

✅ **5/5 用户优先功能** (100%)
- ✅ 长期记忆系统
- ✅ 上下文压缩
- ✅ 文件上传
- ✅ 对话抽取 Skill
- ✅ 计划任务

---

## 🛠️ 开发指南

### 构建和测试

```bash
# 构建项目
cd v3
dotnet build

# 运行所有测试
dotnet test

# 运行特定测试
dotnet test --filter "FullyQualifiedName~AliasManager"

# Release 构建
dotnet build --configuration Release

# 发布
dotnet publish -c Release -r osx-x64 --self-contained
```

### 代码质量

```bash
# 格式化代码
dotnet format

# 运行 linter
dotnet build /p:TreatWarningsAsErrors=true
```

### 添加新技能

1. 在 `v3/skills/<namespace>/` 创建 `.md` 文件
2. 添加 YAML frontmatter 和 Markdown 模板
3. 重启 REPL 或使用 `/skills` 验证

示例参考: `v3/skills/personal/greeting.md`

---

## 🤝 贡献

欢迎贡献！请查看 [CONTRIBUTING.md](CONTRIBUTING.md)（即将添加）了解详情。

### 报告问题

发现 Bug 或有功能建议？请创建 [Issue](https://github.com/nothingbut/general-agent/issues)。

### 开发流程

1. Fork 项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

---

## 📈 路线图

### 已完成功能 ✅

- ✅ **V3.0** - 核心架构、LLM 集成、技能系统、CLI/REPL
- ✅ **V3.1** - 智能搜索、标签系统、查询缓存
- ✅ **V3.2** - 长期记忆、上下文压缩、文件上传、技能抽取、计划任务

### 潜在增强功能（未来）

#### 系统增强
- [ ] 分布式任务调度（多节点支持）
- [ ] WebSocket 实时通知
- [ ] 图形化任务配置界面
- [ ] Prometheus 指标导出

#### 用户体验
- [ ] TUI 模式增强（分屏布局）
- [ ] 配置文件管理
- [ ] 主题系统
- [ ] 移动端支持

#### 协作功能
- [ ] 会话分享
- [ ] 团队技能库
- [ ] 远程数据库支持
- [ ] 使用统计和分析

详见 [优先功能路线图](v3/docs/features/priority-features.md)。

---

## 📜 许可证

本项目采用 MIT 许可证。详见 [LICENSE](LICENSE) 文件。

---

## 🙏 致谢

### 开发

感谢 Claude Sonnet 4.5 在整个 V3 开发过程中的贡献。

### 技术栈

- [.NET](https://dotnet.microsoft.com/) - 应用平台
- [Spectre.Console](https://spectreconsole.net/) - CLI UI 框架
- [Anthropic](https://www.anthropic.com/) - Claude API
- [Ollama](https://ollama.ai/) - 本地 LLM 运行时
- [xUnit](https://xunit.net/) - 测试框架

---

## 📞 联系方式

- **GitHub Issues**: [问题追踪](https://github.com/nothingbut/general-agent/issues)
- **GitHub Discussions**: [讨论区](https://github.com/nothingbut/general-agent/discussions)
- **Email**: shi.chang@163.com

---

<div align="center">

**[⬆ 回到顶部](#general-agent)**

Made with ❤️ by Claude Sonnet 4.5

</div>
