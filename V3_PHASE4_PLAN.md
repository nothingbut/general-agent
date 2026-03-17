# V3 Phase 4 实施计划 - CLI/TUI 增强

**项目**: General Agent V3 - CLI 工具
**Phase**: Phase 4 - CLI/TUI Enhancement
**开始日期**: 2026-03-17
**预计工期**: 2 周
**状态**: 📝 计划中

---

## 📋 Phase 概述

### 目标

将现有的 Console REPL 增强为完整的 CLI 工具，支持：
1. **命令行参数** - 脚本化和自动化
2. **子命令结构** - 更清晰的命令组织
3. **增强的 REPL** - 更多交互功能
4. **配置管理** - 用户配置和环境管理

### 当前状态分析

**已有功能** ✅:
- Spectre.Console 美化输出
- 流式对话显示
- 基础 REPL 命令（/new, /list, /switch, /provider, /history, /exit）
- 配置文件支持（appsettings.json）
- 技能系统集成

**待实现功能** 🔨:
- System.CommandLine 集成
- 命令行子命令
- 会话切换和删除
- 技能列表和独立调用
- 配置文件管理
- 导出/导入功能

---

## 🎯 Phase 目标

### 功能目标

1. **CLI 命令行模式**
   - `agent new` - 创建新会话
   - `agent chat <session-id>` - 在指定会话中发送消息
   - `agent list` - 列出会话
   - `agent switch <session-id>` - 切换会话
   - `agent delete <session-id>` - 删除会话
   - `agent export <session-id>` - 导出会话
   - `agent skill list` - 列出技能
   - `agent skill run <skill-name>` - 执行技能
   - `agent repl` - 启动交互式 REPL（默认行为）

2. **增强的 REPL**
   - 会话切换：`/switch <session-id>`
   - 会话删除：`/delete [session-id]`
   - 技能列表：`/skills`
   - 技能详情：`/skill <name>`
   - 清屏：`/clear`
   - 多行输入支持

3. **配置管理**
   - 显示配置：`agent config show`
   - 设置配置：`agent config set <key> <value>`
   - 重置配置：`agent config reset`

### 质量目标

- 测试覆盖率：≥ 80%
- 命令响应时间：< 100ms
- REPL 启动时间：< 2s
- 用户体验流畅

---

## 📐 架构设计

### 项目结构

保持现有 `GeneralAgent.Hosts.Console` 项目，增强为：

```
GeneralAgent.Hosts.Console/
├── Commands/                    # System.CommandLine 命令
│   ├── RootCommand.cs          # 根命令
│   ├── NewCommand.cs           # new 子命令
│   ├── ChatCommand.cs          # chat 子命令
│   ├── ListCommand.cs          # list 子命令
│   ├── SwitchCommand.cs        # switch 子命令
│   ├── DeleteCommand.cs        # delete 子命令
│   ├── SkillCommands.cs        # skill 子命令组
│   ├── ConfigCommands.cs       # config 子命令组
│   └── ReplCommand.cs          # repl 子命令（默认）
├── Repl/
│   ├── AgentRepl.cs            # REPL 实现（增强）
│   ├── ReplCommand.cs          # REPL 命令处理器
│   └── ReplExtensions.cs       # REPL 扩展方法
├── Utils/
│   ├── OutputFormatter.cs      # 输出格式化
│   ├── SessionSelector.cs      # 会话选择器
│   └── ExportHelper.cs         # 导出工具
├── Program.cs                  # 程序入口
└── appsettings.json           # 配置文件
```

### 技术选型

| 功能 | 技术方案 |
|------|---------|
| 命令行解析 | System.CommandLine 2.0 |
| 终端 UI | Spectre.Console 0.49+ |
| 配置管理 | Microsoft.Extensions.Configuration |
| 依赖注入 | Microsoft.Extensions.DependencyInjection |

---

## 📝 任务分解

### Chunk 1: System.CommandLine 集成（Day 1-2）

**任务**:
- [ ] Task 1: 添加 System.CommandLine 依赖
- [ ] Task 2: 创建 RootCommand 和基础命令结构
- [ ] Task 3: 实现 `agent new` 命令
- [ ] Task 4: 实现 `agent list` 命令
- [ ] Task 5: 实现 `agent chat` 命令

**交付物**:
- `Commands/` 目录结构
- 5 个命令类
- 10+ 单元测试

**验收标准**:
```bash
agent new --title "测试会话"
agent list --limit 10
agent chat <id> "你好"
```

---

### Chunk 2: 会话管理命令（Day 3-4）

**任务**:
- [ ] Task 6: 实现 `agent switch` 命令
- [ ] Task 7: 实现 `agent delete` 命令
- [ ] Task 8: 实现 `agent export` 命令
- [ ] Task 9: 添加会话选择器（交互式）
- [ ] Task 10: 添加导出格式支持（JSON, Markdown）

**交付物**:
- 3 个新命令类
- SessionSelector 工具类
- ExportHelper 工具类
- 10+ 单元测试

**验收标准**:
```bash
agent switch <id>
agent delete <id>
agent export <id> --format json
agent export <id> --format markdown --output session.md
```

---

### Chunk 3: 技能命令（Day 5-6）

**任务**:
- [ ] Task 11: 实现 `agent skill list` 命令
- [ ] Task 12: 实现 `agent skill run` 命令
- [ ] Task 13: 实现 `agent skill info <name>` 命令
- [ ] Task 14: 添加技能参数解析和验证
- [ ] Task 15: 美化技能输出

**交付物**:
- SkillCommands.cs
- 3 个子命令
- 8+ 单元测试

**验收标准**:
```bash
agent skill list
agent skill info personal:greeting
agent skill run personal:greeting --user_name "张三" --time_of_day "上午"
```

---

### Chunk 4: 配置管理（Day 7-8）

**任务**:
- [ ] Task 16: 实现 `agent config show` 命令
- [ ] Task 17: 实现 `agent config set` 命令
- [ ] Task 18: 实现 `agent config reset` 命令
- [ ] Task 19: 添加用户配置文件支持（~/.agent/config.json）
- [ ] Task 20: 添加环境变量支持

**交付物**:
- ConfigCommands.cs
- 配置管理逻辑
- 6+ 单元测试

**验收标准**:
```bash
agent config show
agent config set llm.provider Ollama
agent config set llm.model qwen2.5:0.5b
agent config reset
```

---

### Chunk 5: REPL 增强（Day 9-10）

**任务**:
- [ ] Task 21: 增强 REPL 命令（/switch, /delete, /skills, /skill）
- [ ] Task 22: 添加多行输入支持（Ctrl+D 结束）
- [ ] Task 23: 添加命令历史记录
- [ ] Task 24: 添加自动补全提示
- [ ] Task 25: 改进错误提示

**交付物**:
- 增强的 AgentRepl.cs
- ReplExtensions.cs
- 改进的用户体验

**验收标准**:
- 在 REPL 中测试所有新命令
- 多行输入正常工作
- 错误提示清晰友好

---

### Chunk 6: 集成测试和文档（Day 11-12）

**任务**:
- [ ] Task 26: 编写端到端集成测试
- [ ] Task 27: 编写 CLI 使用文档
- [ ] Task 28: 编写命令参考手册
- [ ] Task 29: 创建使用示例和教程
- [ ] Task 30: 手动验收测试

**交付物**:
- 集成测试套件（10+ 测试）
- README_CLI.md
- CLI_REFERENCE.md
- EXAMPLES.md
- V3_PHASE4_COMPLETION_REPORT.md

**验收标准**:
- 所有集成测试通过
- 文档完整清晰
- 手动验收清单 100% 通过

---

## 🧪 测试策略

### 单元测试（50+ 个）

**Commands 测试**:
- 参数解析正确性
- 命令执行逻辑
- 错误处理

**Utils 测试**:
- SessionSelector 选择逻辑
- ExportHelper 导出格式
- OutputFormatter 格式化输出

### 集成测试（10+ 个）

**端到端测试**:
1. 创建会话 → 发送消息 → 查看历史
2. 创建多个会话 → 切换 → 删除
3. 执行技能 → 验证输出
4. 配置管理 → 验证生效
5. REPL 交互流程

### 手动验收测试

完整的 CLI 命令验收清单（见 Task 30）

---

## 📊 验收标准

### 功能完整性（100%）

- [x] 所有 CLI 命令实现完成
- [x] REPL 增强功能完成
- [x] 配置管理功能完成
- [x] 技能命令功能完成

### 质量标准（100%）

- [x] 单元测试覆盖率 ≥ 80%
- [x] 所有集成测试通过
- [x] 无编译警告
- [x] 手动验收 100% 通过

### 文档完整性（100%）

- [x] CLI 使用文档
- [x] 命令参考手册
- [x] 使用示例
- [x] Phase 4 完成报告

---

## 🚀 命令示例

### CLI 模式

```bash
# 创建会话
agent new --title "工作讨论"

# 发送消息
agent chat abc123 "请帮我分析这段代码"

# 列出会话
agent list --limit 20

# 切换会话（设置为当前会话）
agent switch abc123

# 删除会话
agent delete abc123 --confirm

# 导出会话
agent export abc123 --format markdown --output chat.md

# 技能操作
agent skill list
agent skill info personal:greeting
agent skill run personal:greeting --user_name "张三"

# 配置管理
agent config show
agent config set llm.provider Ollama
```

### REPL 模式

```bash
# 启动 REPL
agent repl
# 或直接
agent

# 在 REPL 中
You> /help
You> /new 新会话
You> 你好
Assistant> 您好！有什么我可以帮您的吗？
You> /switch abc123
You> /skills
You> @personal:greeting user_name='李四'
You> /history
You> /exit
```

---

## 📁 输出和交付物

### 代码文件（20+）

1. Commands/ - 10 个命令类
2. Repl/ - 3 个 REPL 增强文件
3. Utils/ - 3 个工具类
4. Tests/ - 60+ 测试

### 配置文件（2）

1. appsettings.json
2. ~/.agent/config.json（用户配置）

### 文档文件（5）

1. README_CLI.md - CLI 使用指南
2. CLI_REFERENCE.md - 命令参考
3. EXAMPLES.md - 使用示例
4. V3_PHASE4_COMPLETION_REPORT.md - 完成报告
5. V3_PHASE4_UAT_CHECKLIST.md - 验收清单

---

## 🔧 依赖项

### NuGet 包（新增）

```xml
<PackageReference Include="System.CommandLine" Version="2.0.0-beta4.24324.3" />
```

### 现有依赖

- Spectre.Console 0.49+
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.DependencyInjection
- Entity Framework Core 9

---

## 📅 时间规划

| Chunk | 任务范围 | 时间 | 交付物 |
|-------|---------|------|--------|
| Chunk 1 | System.CommandLine 集成 | Day 1-2 | 5 个命令 |
| Chunk 2 | 会话管理命令 | Day 3-4 | 3 个命令 + 工具 |
| Chunk 3 | 技能命令 | Day 5-6 | 3 个子命令 |
| Chunk 4 | 配置管理 | Day 7-8 | 配置命令 |
| Chunk 5 | REPL 增强 | Day 9-10 | 增强功能 |
| Chunk 6 | 集成测试和文档 | Day 11-12 | 测试 + 文档 |

**总工期**: 12 天（2 周）

---

## ⚠️ 风险和应对

### 风险 1: System.CommandLine 版本兼容性

**风险**: System.CommandLine 2.0 仍在 beta，API 可能变化

**应对**:
- 使用稳定的 API 子集
- 做好版本锁定
- 准备回退到 1.x 版本

### 风险 2: REPL 多行输入复杂度

**风险**: 多行输入的交互体验难以实现

**应对**:
- 使用简单的标记结束（如 Ctrl+D）
- 参考 Python REPL 的实现
- 可选功能，不影响核心

### 风险 3: 用户配置文件权限问题

**风险**: 创建 ~/.agent/ 目录可能遇到权限问题

**应对**:
- 优雅降级到仅使用 appsettings.json
- 明确的错误提示
- 提供手动创建目录的文档

---

## 🎯 成功标准

Phase 4 完成的标志：

1. ✅ 所有 30 个任务完成
2. ✅ 60+ 测试全部通过
3. ✅ 手动验收 100% 通过
4. ✅ 文档完整清晰
5. ✅ CLI 工具可独立使用
6. ✅ REPL 体验流畅
7. ✅ 0 编译警告

---

**计划创建**: 2026-03-17
**创建者**: Claude Sonnet 4.5
**Phase 4 状态**: 📝 计划完成，等待执行
