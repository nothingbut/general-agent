# TUI 终端界面使用指南

General Agent 提供了基于 Textual 的终端用户界面（TUI），支持命令行快速查询和交互式对话。

---

## 安装

```bash
pip install -e ".[cli]"
```

---

## 使用模式

### 1. 命令行模式（快速查询）

适用于快速问答，执行后立即返回结果并退出。

```bash
agent "今天天气如何？"
agent "帮我总结这段文字：..."
```

**特点：**
- 快速响应
- 自动创建临时会话
- 适合脚本调用

### 2. TUI 模式（交互式界面）

适用于多轮对话和长期会话。

```bash
agent --tui
```

**特点：**
- 持续对话
- 会话管理
- 历史记录
- 快捷键支持

---

## 界面说明

```
┌─────────────────────────────────────────────────────┐
│ General Agent - Session: session-abc123       [Help]│ <- 标题栏
├─────────────────────────────────────────────────────┤
│ 🧑 User: 今天天气如何？                              │
│                                                     │
│ 🤖 Agent: 我无法实时获取天气信息...                  │
│                                                     │ <- 消息区（可滚动）
│ ...                                                 │
├─────────────────────────────────────────────────────┤
│ > 输入消息... _                                      │ <- 输入框
├─────────────────────────────────────────────────────┤
│ Enter=发送 Ctrl+N=新会话 Ctrl+L=列表 Ctrl+Q=退出     │ <- 快捷键提示
└─────────────────────────────────────────────────────┘
```

---

## 快捷键

| 快捷键 | 功能 | 说明 |
|--------|------|------|
| `Enter` | 发送消息 | 输入框中按 Enter 发送 |
| `Ctrl+N` | 新建会话 | 创建新会话并清空屏幕 |
| `Ctrl+L` | 会话列表 | 显示所有会话，选择切换 |
| `Ctrl+K` | 清屏 | 清空当前消息列表 |
| `Ctrl+Q` | 退出 | 关闭 TUI 应用 |
| `Esc` | 关闭弹窗 | 关闭会话列表等弹窗 |

---

## 会话管理

### 创建新会话

**方法 1：启动时自动创建**
```bash
agent --tui
```

**方法 2：在 TUI 中创建**
按 `Ctrl+N`

### 切换会话

**按 `Ctrl+L` 打开会话列表：**

```
┌─────────────────────────────────────────────────────┐
│ 选择会话                                      [Esc 返回]│
├─────────────────────────────────────────────────────┤
│ ● session-abc123  "今天天气如何？"  (5分钟前)        │
│   session-def456  "帮我总结文档"   (1小时前)         │
│   session-ghi789  "创建提醒"       (昨天)            │
│                                                     │
│ [N] 新建会话                                         │
└─────────────────────────────────────────────────────┘
```

使用方向键选择，按 `Enter` 确认。

### 加载指定会话

```bash
agent --tui --session=session-abc123
```

---

## 与 Web 界面共享

TUI 和 Web 界面完全共享会话数据：

- **共享数据库** - 使用同一 SQLite 文件
- **会话互通** - TUI 创建的会话在 Web 端可见
- **消息同步** - 所有消息实时保存
- **无缝切换** - 可以在两个界面间自由切换

**示例：**
1. 在 TUI 中创建会话并发送消息
2. 打开 Web 界面 (http://localhost:8000)
3. 可以看到相同的会话和消息

---

## 配置

TUI 使用与 Web 相同的配置：

### .env 配置

```bash
# LLM 配置
USE_OLLAMA=true
OLLAMA_BASE_URL=http://localhost:11434
OLLAMA_MODEL=llama3.2:latest
OLLAMA_TEMPERATURE=0.7

# 数据库配置
DATABASE_PATH=data/general_agent.db

# 日志级别
LOG_LEVEL=INFO
```

### 启用详细日志

```bash
agent --tui --verbose
```

---

## 故障排除

### 无法连接到 Ollama

**错误：**
```
❌ 无法连接到 Ollama 服务 (http://localhost:11434)
```

**解决方法：**
1. 启动 Ollama：`ollama serve`
2. 检查 Ollama 是否在运行：`curl http://localhost:11434`
3. 或使用 Mock 模式：在 `.env` 中设置 `USE_OLLAMA=false`

### 数据库锁定

如果 TUI 和 Web 同时运行，SQLite 可能出现短暂锁定。系统会自动重试（最多 3 次）。

### 终端大小过小

TUI 需要至少 80x24 的终端大小。如果终端过小，调整窗口大小后重启。

### 依赖缺失

**错误：**
```
ModuleNotFoundError: No module named 'textual'
```

**解决方法：**
```bash
pip install -e ".[cli]"
```

---

## 高级用法

### 在脚本中使用

```bash
#!/bin/bash

# 快速查询模式，适合脚本
RESULT=$(agent "总结这个文件的内容" --session=my-session)
echo "Agent 回复: $RESULT"
```

### 持续会话

```bash
# 创建命名会话
SESSION_ID="work-$(date +%Y%m%d)"
agent --tui --session=$SESSION_ID
```

### 集成到工作流

```bash
# .zshrc 或 .bashrc
alias ask="agent"
alias chat="agent --tui"

# 使用
ask "今天天气"
chat  # 进入交互式界面
```

---

## 性能优化

### 使用更快的模型

```bash
# .env
OLLAMA_MODEL=llama3.2:1b  # 更小更快的模型
```

### 减少超时时间

如果需要调整超时设置，可以在代码中修改 `OLLAMA_TIMEOUT` 参数。

### 并发控制

TUI 会按顺序处理消息，避免并发问题。如果需要同时进行多个对话，可以开启多个 TUI 实例并使用不同的会话。

---

## 命令行参数

完整的命令行参数列表：

```bash
agent --help
```

### 主要参数

| 参数 | 说明 | 示例 |
|------|------|------|
| `query` | 查询内容（快速模式） | `agent "你好"` |
| `--tui` | 启动交互式 TUI | `agent --tui` |
| `--session=ID` | 指定会话 ID | `agent --tui --session=sess-123` |
| `--verbose` / `-v` | 显示详细日志 | `agent --tui -v` |
| `--version` | 显示版本信息 | `agent --version` |

---

## 最佳实践

### 1. 会话命名

虽然系统自动生成会话 ID，但你可以在会话中发送描述性消息作为"标题"，方便后续识别：

```bash
# 在 TUI 中第一条消息
"这个会话用于讨论项目 X 的需求"
```

### 2. 定期清理会话

使用 Web 界面查看和删除不需要的会话，保持数据库整洁。

### 3. 备份重要对话

会话数据存储在 SQLite 数据库中（默认 `data/general_agent.db`），定期备份该文件：

```bash
cp data/general_agent.db data/general_agent.db.backup
```

### 4. 多终端工作流

- 终端 1：运行 TUI 进行对话
- 终端 2：运行 Web 服务查看历史
- 终端 3：运行 Ollama 服务

---

## 技巧与窍门

### 快速访问

添加到你的 shell 配置：

```bash
# ~/.zshrc 或 ~/.bashrc
alias ask='agent'
alias chat='agent --tui'
alias asession='agent --tui --session'

# 使用
ask "今天的任务是什么？"
chat
asession my-work-session
```

### 搜索历史

使用 Web 界面的搜索功能查找历史消息，TUI 专注于当前对话。

### 快捷键提示

在 TUI 底部状态栏始终显示可用的快捷键，如果忘记了可以随时查看。

---

## 反馈与支持

遇到问题？

1. **查看日志：** `agent --tui --verbose`
2. **检查配置：** 确保 `.env` 正确
3. **重启服务：** 有时重启 Ollama 和 TUI 可以解决问题
4. **查看文档：** 参考本指南的故障排除部分

---

**文档更新时间：** 2026-03-06
