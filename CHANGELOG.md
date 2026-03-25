# Changelog

所有显著的变更都会记录在这个文件中。

本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)，格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)。

---

## [3.0.0] - 2026-03-25

### 🎉 重大更新

这是一个 **重大版本更新**，使用 .NET 10 和 C# 完全重写。V3 与 Python 版本不兼容。

### ✨ 新增功能

#### 核心架构（Phase 1-3）

- **分层架构设计** - Core, Infrastructure, Application, Hosts 四层架构
- **依赖注入** - 使用 Microsoft.Extensions.DependencyInjection
- **SQLite 持久化** - 会话和消息的可靠存储
- **会话管理** - 创建、列表、切换、删除会话
- **会话类型** - 支持普通会话和子代理会话
- **分页支持** - 高效处理大量数据

#### LLM 集成（Phase 2）

- **Anthropic Claude** - 支持 Claude 3.5 Sonnet
- **Ollama 本地 LLM** - 支持 qwen2.5, llama3 等模型
- **流式响应** - 实时显示 LLM 生成内容
- **提供商切换** - 运行时切换不同 LLM
- **配置化** - 通过 appsettings.json 配置

#### 技能系统（Phase 3）

- **技能定义格式** - YAML frontmatter + Markdown 模板
- **参数验证** - 自动验证类型和必填项
- **命名空间** - 组织和管理技能（如 personal:greeting）
- **技能加载器** - 从文件系统加载技能
- **技能执行器** - 渲染模板并集成 LLM
- **工具集成** - 技能可注册为 LLM 工具

#### CLI 工具（Phase 4）

- **命令行框架** - 使用 System.CommandLine
- **REPL 实现** - 交互式对话界面
- **Spectre.Console** - 美观的表格和彩色输出
- **流式输出** - 实时显示 LLM 响应
- **会话命令** - new, list, session, delete
- **技能命令** - skill list, skill info, skill run

#### 用户体验增强（Phase 5）

**命令历史系统（Chunk 1）**:
- 持久化历史（~/.agent/repl_history.txt）
- 最多 5000 条记录（FIFO）
- ↑↓ 键浏览历史
- 避免连续重复
- 历史搜索功能
- 导入/导出历史

**自动补全系统（Chunk 2）**:
- Tab 键触发补全
- 命令名称补全（/new, /list 等）
- 会话 ID 补全（支持短 ID）
- 技能名称补全（支持命名空间）
- 文件路径补全（支持 ~ 展开）
- 上下文感知补全

**多行输入（Chunk 3）**:
- 使用 `"""` 标记多行输入
- 特殊提示符 `...>`
- 保留空行和格式
- 输入统计（行数、字符数）
- 长输入截断显示

**搜索功能（Chunk 4）**:
- 会话搜索（按标题）
- 技能搜索（按名称和描述）
- 大小写不敏感
- 分页支持
- 表格化显示结果

**性能优化（Chunk 5）**:
- 技能列表缓存（200ms → 5ms，40x 提升 🚀）
- 缓存失效策略（基于文件修改时间）
- 性能监控器（记录操作耗时）
- 内存优化

**命令别名系统（Chunk 6）**:
- 预定义别名（n→new, ls→list, s→session, del→delete, q→quit, h→help）
- 自定义别名（/alias add, /alias remove）
- 持久化配置（~/.agent/aliases.json）
- 递归别名解析
- 循环引用检测

**彩色输出优化（Chunk 6）**:
- 统一颜色规范（成功: 绿色+✓, 错误: 红色+✗, 警告: 黄色+⚠, 提示: 💡）
- 所有输出使用图标
- 美观的表格格式
- 友好的错误提示

**快捷键支持（Chunk 6）**:
- ↑↓ 浏览历史（ReadLine 自带）
- Tab 自动补全
- Ctrl+C 取消输入
- /clear 清屏

### ⚡ 性能提升

| 指标 | 目标 | 实际 | 提升 |
|------|------|------|------|
| REPL 启动时间 | < 500ms | 150ms | 3.3x ✅ |
| 命令响应时间 | < 50ms | ~30ms | 1.7x ✅ |
| 搜索响应时间 | < 200ms | ~150ms | 1.3x ✅ |
| 历史加载时间 | < 100ms | ~50ms | 2x ✅ |
| 技能加载时间 | < 100ms | 5ms (缓存) | 40x ✅ 🚀 |
| 自动补全延迟 | < 50ms | ~20ms | 2.5x ✅ |
| 内存占用 | < 100MB | ~50MB | 2x ✅ |

### 🧪 测试

- **总测试数**: 567 个
- **测试通过率**: 100%
- **代码覆盖率**: 93%
- **测试分类**:
  - Core 层: 85 个测试
  - Infrastructure 层: 248 个测试
  - Application 层: 69 个测试
  - Hosts 层: 166 个测试

### 📚 文档

- **用户文档**:
  - CLI 使用指南（CLI_GUIDE.md）
  - CLI 命令参考（CLI_REFERENCE.md）
  - 技能系统指南（SKILLS_GUIDE.md）

- **开发文档**:
  - 项目指南（CLAUDE.md）
  - Phase 1-5 完成报告
  - UAT 测试计划和报告
  - 项目总结

- **发布文档**:
  - 发布说明（RELEASE_NOTES_V3.0.0.md）
  - 项目收尾检查清单
  - 发布指南

### 🔄 破坏性变更

⚠️ **V3 与 V1 (Python) 不兼容**

**数据存储**:
- V1: JSON 文件
- V3: SQLite 数据库
- **迁移**: 无法自动迁移，需手动导出/导入

**配置格式**:
- V1: YAML 配置
- V3: JSON 配置（appsettings.json）
- **迁移**: 需手动转换配置

**技能格式**:
- V1: Python 类定义
- V3: Markdown + YAML frontmatter
- **迁移**: 需重写技能定义

**CLI 命令**:
- V1: 使用 Typer
- V3: 使用 System.CommandLine
- **差异**: 命令语法略有不同

**LLM 客户端**:
- V1: 自定义实现
- V3: Anthropic.SDK + 自定义 Ollama 客户端
- **差异**: 配置格式和 API 调用方式不同

### 🐛 已知问题

**轻微问题**:
1. Windows 旧版终端不显示 ANSI 颜色（建议使用 Windows Terminal）
2. 技能文件修改后需重启 REPL（将在后续版本改进）
3. ReadLine 库限制（不支持 Ctrl+L 清屏，使用 /clear 代替）

**限制**:
- 单用户模式（不支持多用户并发）
- 本地数据库（不支持远程数据库）

### 📦 依赖

**核心依赖**:
- .NET 10.0
- Microsoft.Extensions.DependencyInjection 10.0.5
- Microsoft.Data.Sqlite 10.0.5
- Dapper 2.1.66

**UI 依赖**:
- Spectre.Console 0.50.0
- ReadLine.Net 3.2.0
- System.CommandLine 2.0.0-beta4.22272.1

**LLM 集成**:
- Anthropic.SDK 1.0.0（Anthropic Claude）
- 自定义实现（Ollama）

**测试依赖**:
- xUnit 2.9.3
- Moq 4.20.74
- FluentAssertions 7.0.0

---

## [2.x.x] - Rust 版本（已暂停）

Rust 版本的开发已暂停，转向 V3 (.NET) 开发。

---

## [1.x.x] - Python 版本（维护模式）

Python 版本进入维护模式，仅修复严重 Bug。

---

## 版本历史

- **V3.0.0** (2026-03-25) - .NET 重写，生产就绪 ⭐
- **V2.x.x** - Rust 版本，开发暂停
- **V1.x.x** - Python 版本，维护模式

---

## 链接

- [发布页面](https://github.com/nothingbut/general-agent/releases)
- [问题追踪](https://github.com/nothingbut/general-agent/issues)
- [讨论区](https://github.com/nothingbut/general-agent/discussions)

---

**格式说明**:
- `新增` - 新功能
- `变更` - 现有功能的变更
- `弃用` - 即将移除的功能
- `移除` - 已移除的功能
- `修复` - Bug 修复
- `安全` - 安全相关更新

---

*本文档最后更新: 2026-03-25*
