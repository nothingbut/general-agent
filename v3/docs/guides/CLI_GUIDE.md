# General Agent V3 - CLI 使用指南

## 📖 简介

General Agent V3 CLI 是一个功能强大的命令行工具，提供两种使用模式：

1. **命令行模式** - 直接执行单个命令（适合脚本和自动化）
2. **REPL 模式** - 交互式对话界面（适合日常使用）

---

## 🚀 快速开始

### 安装

```bash
# 克隆仓库
git clone https://github.com/your-org/general-agent.git
cd general-agent/v3

# 构建项目
dotnet build

# 运行 CLI
dotnet run --project src/GeneralAgent.Hosts.Console/
```

### 第一次使用

```bash
# 1. 启动 REPL（默认行为）
dotnet run --project src/GeneralAgent.Hosts.Console/

# 2. 或直接执行命令
dotnet run --project src/GeneralAgent.Hosts.Console/ -- new --title "我的第一个会话"
```

---

## 💻 命令行模式

命令行模式适合脚本化和自动化操作。

### 会话管理

#### 创建新会话

```bash
# 创建会话（自动标题）
agent new

# 创建会话（指定标题）
agent new --title "工作讨论"
agent new -t "代码审查"
```

#### 列出会话

```bash
# 列出所有会话（默认 20 条）
agent list

# 指定数量
agent list --limit 50

# 跳过前 10 条
agent list --offset 10
```

#### 切换会话

```bash
# 使用完整 ID
agent switch 12345678-1234-1234-1234-123456789abc

# 使用短 ID（前 8 位）
agent switch 12345678
```

#### 删除会话

```bash
# 删除指定会话（带确认）
agent delete 12345678

# 强制删除（无确认）
agent delete 12345678 --force
```

#### 发送消息

```bash
# 向当前会话发送消息
agent chat 12345678 "请帮我分析这段代码"

# 使用流式输出
agent chat 12345678 "解释一下量子计算" --stream
```

#### 导出会话

```bash
# 导出为 JSON
agent export 12345678 --format json

# 导出为 Markdown
agent export 12345678 --format markdown --output chat.md

# 导出到标准输出
agent export 12345678 --format markdown
```

### 技能管理

#### 列出技能

```bash
# 列出所有技能
agent skill list

# 按命名空间过滤
agent skill list personal
agent skill list productivity
```

#### 查看技能详情

```bash
# 显示技能信息
agent skill info personal:greeting

# 包含提示词模板
agent skill info personal:greeting --template
```

#### 执行技能

```bash
# 执行技能（键值对参数）
agent skill run personal:greeting user_name="张三" time_of_day="上午"

# 复杂参数使用引号
agent skill run productivity:task title="Review PR #123" priority="high" due_date="2026-03-25"
```

### 文件管理

#### 上传文件

```bash
# 上传文件到当前会话
agent file upload /path/to/document.txt

# 上传并自动提取到记忆
agent file upload config.json --to-memory
```

**支持的文件类型**：文本文件（.txt, .md）、代码文件（.cs, .py, .js 等）、配置文件（.json, .yaml）等

**文件限制**：最大 5 MB，内容长度最大 10,000 字符

#### 列出文件

```bash
# 表格视图（默认）
agent file list

# JSON 视图
agent file list --format json
```

#### 查看文件详情

```bash
# 使用文件 ID
agent file show abc12345-1234-1234-1234-123456789abc

# 支持短 ID
agent file show abc12345
```

#### 查看文件内容

```bash
# 查看完整内容
agent file content <file-id>

# 限制显示行数
agent file content <file-id> --lines 50
```

#### 删除文件

```bash
# 交互式确认
agent file delete <file-id>

# 跳过确认
agent file delete <file-id> --force
```

#### 在对话中引用文件

上传文件后，可以使用 `@file:` 语法在对话中引用：

```bash
# 按文件名引用
agent chat "请分析 @file:config.json 中的配置"

# 按文件 ID 引用
agent chat "请查看 @file:abc12345"

# 引用多个文件
agent chat "比较 @file:config.json 和 @file:config.prod.json 的差异"
```

更多信息请参考 [文件上传用户指南](FILE_UPLOAD_USER_GUIDE.md)。

### 配置管理

#### 查看配置

```bash
# 表格格式（默认）
agent config show

# JSON 格式
agent config show --format json
```

#### 设置配置

```bash
# 设置 LLM 提供商
agent config set DefaultProvider Ollama

# 设置模型
agent config set OllamaModel qwen2.5:latest

# 设置 API Key
agent config set AnthropicApiKey sk-ant-xxx
```

#### 重置配置

```bash
# 重置到默认值（带确认）
agent config reset

# 强制重置
agent config reset --force
```

---

## 🎮 REPL 模式

REPL（Read-Eval-Print Loop）模式提供交互式对话体验。

### 启动 REPL

```bash
# 方式 1：直接运行
agent

# 方式 2：显式指定
agent repl
```

### REPL 命令

#### 会话管理

| 命令 | 说明 | 示例 |
|------|------|------|
| `/new [title]` | 创建新会话 | `/new 工作讨论` |
| `/list` | 列出所有会话 | `/list` |
| `/session <id>` | 切换会话 | `/session 12345678` |
| `/delete [id]` | 删除会话 | `/delete` |
| `/history` | 查看当前会话历史 | `/history` |

#### 技能管理

| 命令 | 说明 | 示例 |
|------|------|------|
| `/skills [namespace]` | 列出技能 | `/skills personal` |
| `/skill <name>` | 显示技能详情 | `/skill personal:greeting` |

#### LLM 配置

| 命令 | 说明 | 示例 |
|------|------|------|
| `/switch <provider>` | 切换提供商 | `/switch Ollama` |
| `/provider` | 显示当前提供商 | `/provider` |

#### 别名管理

| 命令 | 说明 | 示例 |
|------|------|------|
| `/alias` | 列出所有别名 | `/alias` |
| `/alias add <别名> <命令>` | 添加别名 | `/alias add n new` |
| `/alias remove <别名>` | 移除别名 | `/alias remove n` |

#### 其他

| 命令 | 说明 | 示例 |
|------|------|------|
| `/clear` | 清屏 | `/clear` |
| `/help` | 显示帮助 | `/help` |
| `/exit` | 退出 REPL | `/exit` |

### 快捷键和高级功能

#### 键盘快捷键

| 快捷键 | 功能 | 说明 |
|--------|------|------|
| `↑/↓` | 浏览命令历史 | 上下箭头键浏览之前输入的命令 |
| `Tab` | 自动补全 | 补全命令、会话 ID、技能名称和文件路径 |
| `Ctrl+C` | 取消输入 | 取消当前正在输入的内容 |
| `Ctrl+D` | 退出 REPL | 当输入为空时，按 Ctrl+D 退出 REPL |

#### 命令历史

REPL 自动保存命令历史到 `~/.agent/repl_history.txt`，支持：

- 使用 `↑/↓` 浏览历史记录
- 历史记录持久化（最多 1000 条）
- Ctrl+R 搜索历史（由 ReadLine 库提供）

#### 多行输入

使用 `"""` 标记输入多行文本：

```bash
You> """
... 这是第一行
... 这是第二行
... 这是第三行
... """
→ 已接收多行输入: 3 行, 42 字符
```

#### 命令别名

自定义命令快捷方式，提升效率：

```bash
# 添加别名
You> /alias add n new
✓ 已添加别名: n -> new

# 使用别名
You> /n 快速会话
✓ 已创建新会话: 快速会话

# 列出所有别名
You> /alias
已配置 6 个别名：
┌────────┬─────────┐
│  别名  │  命令   │
├────────┼─────────┤
│   n    │  new    │
│   ls   │  list   │
│   s    │ session │
│   del  │ delete  │
│   q    │  quit   │
│   h    │  help   │
└────────┴─────────┘

# 移除别名
You> /alias remove n
✓ 已移除别名: n
```

预定义的别名（默认配置）：

- `n` → `new`
- `ls` → `list`
- `s` → `session`
- `del` → `delete`
- `q` → `quit`
- `h` → `help`

### REPL 使用示例

```bash
# 启动 REPL
$ agent

╔══════════════════════════════════════════════════╗
║  General Agent V3 - Console REPL                 ║
║                                                   ║
║  当前提供商: Ollama                              ║
║  输入 /help 查看可用命令                         ║
╚══════════════════════════════════════════════════╝

# 创建新会话
You> /new 工作讨论
✓ 已创建新会话: 工作讨论 (ID: 12345678...)

# 开始对话
You> 你好，请介绍一下自己
Assistant> 您好！我是 General Agent，一个基于 AI 的智能助手...

# 查看会话历史
You> /history
会话历史 (共 2 条消息):

You> 你好，请介绍一下自己
Assistant> 您好！我是 General Agent...

# 切换会话
You> /session 87654321
✓ 已切换到会话: 代码审查
  ID: 87654321...
  创建时间: 2026-03-24 14:30:00

# 列出技能
You> /skills
已加载 12 个技能：
┌────────────────────────┬─────────────────────┬────────────┐
│      完整名称          │       描述          │  参数数量  │
├────────────────────────┼─────────────────────┼────────────┤
│  personal              │                     │            │
│    personal:greeting   │ 向用户问候          │     2      │
...

# 退出
You> /exit

再见！
```

---

## 🔍 记忆管理

### 向量搜索（Vector Search）

Phase 2 引入了向量搜索功能，将语义搜索性能提升 1000-10000 倍（从 50-100秒 降至 10-50毫秒）。

#### 前置要求

1. 启动 Qdrant 向量数据库：
   ```bash
   docker run -d --name qdrant -p 6333:6333 qdrant/qdrant
   ```

2. 确保 Ollama 运行并下载 Embedding 模型：
   ```bash
   ollama pull nomic-embed-text
   ```

#### 使用向量搜索

```bash
# 语义搜索（自动使用向量搜索）
> /memory semantic-search "TDD测试"

✅ 找到 3 个相关记忆（向量搜索，耗时 ~15ms）

1. tdd_preference (相似度: 0.92)
   描述: 喜欢使用 TDD 方法
   类型: User

2. unit_testing (相似度: 0.85)
   描述: 单元测试最佳实践
   类型: Knowledge
```

#### 迁移现有记忆

如果你已经在 Phase 1 创建了记忆，需要迁移到向量数据库：

```bash
> /memory migrate-to-vectors

开始迁移现有记忆到向量数据库...
✓ Qdrant 健康检查通过
✓ 扫描到 50 个现有记忆
已迁移 10/50 (20%)...
已迁移 20/50 (40%)...
...
✅ 迁移完成！
  • 总计: 50 个记忆
  • 成功: 50 个
  • 失败: 0 个
```

#### 自动降级

如果 Qdrant 不可用，系统会自动降级到关键词搜索（较慢但仍可用）：

```bash
> /memory semantic-search "测试"

⚠️ 向量搜索不可用，使用关键词搜索（较慢）
提示：启动 Qdrant: docker run -p 6333:6333 qdrant/qdrant

⚠️ 找到 2 个相关记忆（关键词搜索，耗时 ~2s）
```

---

## 🔧 配置

### 配置文件

General Agent 使用两级配置系统：

1. **应用配置** - `v3/src/GeneralAgent.Hosts.Console/appsettings.json`
2. **用户配置** - `~/.agent/config.json`（优先级更高）

### 环境变量

支持通过环境变量覆盖配置：

```bash
# LLM 配置
export AGENT_PROVIDER=Ollama
export AGENT_OLLAMA_MODEL=qwen2.5:latest
export AGENT_ANTHROPIC_API_KEY=sk-ant-xxx

# 数据库配置
export AGENT_DB_PATH=~/.agent/agent.db
```

### LLM 提供商配置

#### Ollama（本地推理）

```bash
# 1. 安装 Ollama
brew install ollama  # macOS
# 或访问 https://ollama.ai

# 2. 拉取模型
ollama pull qwen2.5:latest

# 3. 配置 Agent
agent config set DefaultProvider Ollama
agent config set OllamaModel qwen2.5:latest
agent config set OllamaBaseUrl http://localhost:11434
```

#### Anthropic Claude

```bash
# 配置 API Key
agent config set DefaultProvider Anthropic
agent config set AnthropicApiKey sk-ant-xxx
agent config set AnthropicModel claude-3-5-sonnet-20241022
```

---

## 📂 数据存储

### 数据库位置

默认数据库位置：`~/.agent/agent.db` (SQLite)

### 备份和恢复

```bash
# 备份数据库
cp ~/.agent/agent.db ~/.agent/backups/agent-$(date +%Y%m%d).db

# 恢复数据库
cp ~/.agent/backups/agent-20260324.db ~/.agent/agent.db
```

---

## 🎯 常见使用场景

### 场景 1：代码审查助手

```bash
# 1. 创建专用会话
agent new --title "代码审查"

# 2. 在 REPL 中开始审查
agent
You> /session 12345678
You> 请审查以下代码：
...（粘贴代码）...
```

### 场景 2：批量会话处理

```bash
#!/bin/bash
# 创建多个会话并发送相同的问题

for topic in "Python" "Rust" "Go"; do
  session_id=$(agent new --title "$topic 教程" --json | jq -r '.id')
  agent chat $session_id "请介绍 $topic 的核心特性"
done
```

### 场景 3：技能自动化

```bash
# 使用技能生成日报
agent skill run productivity:daily_report date="2026-03-24" format="markdown" > report.md

# 批量发送提醒
cat tasks.csv | while IFS=, read task time; do
  agent skill run personal:reminder task="$task" time="$time"
done
```

---

## ⚠️ 故障排除

### 问题 1：数据库锁定错误

**症状**: `The database file is locked`

**解决方案**:
```bash
# 关闭所有 agent 实例
pkill -f "GeneralAgent.Hosts.Console"

# 检查数据库完整性
sqlite3 ~/.agent/agent.db "PRAGMA integrity_check;"
```

### 问题 2：Ollama 连接失败

**症状**: `Failed to connect to Ollama`

**解决方案**:
```bash
# 1. 检查 Ollama 是否运行
ollama list

# 2. 检查端口
curl http://localhost:11434/api/tags

# 3. 重启 Ollama
ollama serve
```

### 问题 3：技能加载失败

**症状**: `Skill not found`

**解决方案**:
```bash
# 1. 检查技能目录
ls -la skills/

# 2. 验证 YAML 格式
yamllint skills/**/*.md

# 3. 查看日志
agent --verbose
```

---

## 📚 相关文档

- [命令参考手册](./CLI_REFERENCE.md) - 完整的命令参数说明
- [使用示例](./CLI_EXAMPLES.md) - 更多实用示例
- [技能系统指南](./SKILLS.md) - 技能开发和使用
- [架构文档](./ARCHITECTURE.md) - 系统架构说明

---

## 💡 提示和技巧

### 提示 1：使用别名简化命令

```bash
# 添加到 ~/.bashrc 或 ~/.zshrc
alias agent='dotnet run --project ~/general-agent/v3/src/GeneralAgent.Hosts.Console/ --'
alias agent-repl='dotnet run --project ~/general-agent/v3/src/GeneralAgent.Hosts.Console/'
```

### 提示 2：使用短 ID

所有接受会话 ID 的命令都支持短格式（前 8 位）：

```bash
# 完整 ID（36 字符）
agent switch 12345678-1234-1234-1234-123456789abc

# 短 ID（8 字符）
agent switch 12345678
```

### 提示 3：组合命令

```bash
# 创建会话并立即发送消息
session_id=$(agent new --title "快速咨询" --json | jq -r '.id')
agent chat $session_id "什么是量子纠缠？"
```

---

**更新时间**: 2026-03-24
**版本**: V3 Phase 4
**维护者**: General Agent Team
