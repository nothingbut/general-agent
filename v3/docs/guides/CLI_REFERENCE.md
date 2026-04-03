# General Agent V3 - CLI 命令参考手册

## 📋 目录

- [全局选项](#全局选项)
- [会话管理命令](#会话管理命令)
- [技能命令](#技能命令)
- [配置命令](#配置命令)
- [REPL 命令](#repl-命令)
- [退出码](#退出码)

---

## 全局选项

所有命令都支持以下全局选项：

| 选项 | 简写 | 说明 | 默认值 |
|------|------|------|--------|
| `--verbose` | `-v` | 显示详细输出 | `false` |
| `--help` | `-h` | 显示帮助信息 | - |
| `--version` | | 显示版本信息 | - |

### 示例

```bash
# 显示详细输出
agent new --verbose

# 显示帮助
agent --help
agent new --help

# 显示版本
agent --version
```

---

## 会话管理命令

### `agent new` - 创建新会话

创建一个新的对话会话。

#### 语法

```bash
agent new [options]
```

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--title` | `-t` | string | 会话标题 | 自动生成 |
| `--json` | | flag | JSON 格式输出 | `false` |

#### 示例

```bash
# 创建会话（自动标题）
agent new

# 指定标题
agent new --title "工作讨论"
agent new -t "代码审查"

# JSON 输出
agent new --title "测试" --json
# 输出: {"id":"12345678-...","title":"测试","createdAt":"2026-03-24T14:30:00Z"}
```

#### 返回值

- 成功：会话 ID 和创建时间
- 失败：错误消息

#### 退出码

- `0` - 成功
- `1` - 失败

---

### `agent list` - 列出会话

列出所有会话，支持分页。

#### 语法

```bash
agent list [options]
```

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--limit` | `-l` | number | 返回数量 | `20` |
| `--offset` | `-o` | number | 跳过数量 | `0` |
| `--format` | `-f` | string | 输出格式（table/json） | `table` |

#### 示例

```bash
# 列出前 20 个会话
agent list

# 列出前 50 个
agent list --limit 50

# 跳过前 10 个
agent list --offset 10

# JSON 输出
agent list --format json
```

#### 输出格式

**表格格式** (默认):
```
┌──────────┬────────────┬────────┬──────────────────┐
│    ID    │    标题    │ 消息数 │    创建时间      │
├──────────┼────────────┼────────┼──────────────────┤
│ 12345... │ 工作讨论   │   15   │ 2026-03-24 14:30 │
└──────────┴────────────┴────────┴──────────────────┘
```

**JSON 格式**:
```json
{
  "total": 42,
  "items": [
    {
      "id": "12345678-1234-1234-1234-123456789abc",
      "title": "工作讨论",
      "messageCount": 15,
      "createdAt": "2026-03-24T14:30:00Z"
    }
  ]
}
```

#### 退出码

- `0` - 成功
- `1` - 失败

---

### `agent switch` - 切换会话

将指定会话设置为当前会话。

#### 语法

```bash
agent switch <session-id>
```

#### 参数

| 参数 | 类型 | 说明 |
|------|------|------|
| `session-id` | string | 会话 ID（支持短格式） |

#### 示例

```bash
# 使用完整 ID
agent switch 12345678-1234-1234-1234-123456789abc

# 使用短 ID（前 8 位）
agent switch 12345678

# 如果有多个匹配
agent switch 123
# 输出: 找到多个匹配的会话，请使用更长的 ID
```

#### 输出

```
✓ 已切换到会话
  ID: 12345678-1234-1234-1234-123456789abc
  标题: 工作讨论
  类型: Normal
  创建时间: 2026-03-24 14:30:00
```

#### 退出码

- `0` - 成功
- `1` - 会话不存在或 ID 不明确

---

### `agent delete` - 删除会话

删除指定会话及其所有消息。

#### 语法

```bash
agent delete <session-id> [options]
```

#### 参数

| 参数 | 类型 | 说明 |
|------|------|------|
| `session-id` | string | 会话 ID（支持短格式） |

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--force` | `-f` | flag | 跳过确认提示 | `false` |

#### 示例

```bash
# 删除会话（带确认）
agent delete 12345678
# 提示: 确定要删除会话 "工作讨论" (12345678...) 吗？ (y/n)

# 强制删除（无确认）
agent delete 12345678 --force
```

#### 退出码

- `0` - 成功删除
- `1` - 会话不存在或用户取消

---

### `agent chat` - 发送消息

向指定会话发送消息并获取响应。

#### 语法

```bash
agent chat <session-id> <message> [options]
```

#### 参数

| 参数 | 类型 | 说明 |
|------|------|------|
| `session-id` | string | 会话 ID（支持短格式） |
| `message` | string | 消息内容 |

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--stream` | `-s` | flag | 流式输出 | `true` |
| `--provider` | `-p` | string | LLM 提供商 | 配置的默认值 |

#### 示例

```bash
# 发送消息
agent chat 12345678 "请解释一下量子计算"

# 使用特定提供商
agent chat 12345678 "写一首诗" --provider Anthropic

# 非流式输出
agent chat 12345678 "快速问题" --no-stream
```

#### 输出

```
Assistant> 量子计算是一种基于量子力学原理的计算方式...
```

#### 退出码

- `0` - 成功
- `1` - 会话不存在或发送失败

---

### `agent export` - 导出会话

将会话导出为指定格式的文件。

#### 语法

```bash
agent export <session-id> [options]
```

#### 参数

| 参数 | 类型 | 说明 |
|------|------|------|
| `session-id` | string | 会话 ID（支持短格式） |

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--format` | `-f` | string | 导出格式（json/markdown） | `json` |
| `--output` | `-o` | string | 输出文件路径 | 标准输出 |
| `--include-metadata` | | flag | 包含元数据 | `false` |

#### 示例

```bash
# 导出为 JSON（标准输出）
agent export 12345678 --format json

# 导出为 Markdown 文件
agent export 12345678 --format markdown --output chat.md

# 包含元数据
agent export 12345678 --format json --include-metadata > full.json
```

#### 输出格式

**JSON 格式**:
```json
{
  "session": {
    "id": "12345678-...",
    "title": "工作讨论",
    "createdAt": "2026-03-24T14:30:00Z"
  },
  "messages": [
    {
      "role": "user",
      "content": "你好",
      "timestamp": "2026-03-24T14:30:05Z"
    },
    {
      "role": "assistant",
      "content": "您好！有什么我可以帮您的吗？",
      "timestamp": "2026-03-24T14:30:06Z"
    }
  ]
}
```

**Markdown 格式**:
```markdown
# 工作讨论

创建时间: 2026-03-24 14:30:00

---

## You
你好

## Assistant
您好！有什么我可以帮您的吗？
```

#### 退出码

- `0` - 成功
- `1` - 会话不存在或导出失败

---

## 技能命令

### `agent skill list` - 列出技能

列出所有已加载的技能。

#### 语法

```bash
agent skill list [namespace] [options]
```

#### 参数

| 参数 | 类型 | 说明 |
|------|------|------|
| `namespace` | string | 命名空间过滤（可选） |

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--format` | `-f` | string | 输出格式（table/json） | `table` |

#### 示例

```bash
# 列出所有技能
agent skill list

# 按命名空间过滤
agent skill list personal
agent skill list productivity

# JSON 输出
agent skill list --format json
```

#### 输出

```
已加载 12 个技能：
┌────────────────────────┬─────────────────────┬────────────┐
│      完整名称          │       描述          │  参数数量  │
├────────────────────────┼─────────────────────┼────────────┤
│  personal              │                     │            │
│    personal:greeting   │ 向用户问候          │     2      │
│    personal:reminder   │ 创建提醒事项        │     3      │
└────────────────────────┴─────────────────────┴────────────┘
```

#### 退出码

- `0` - 成功
- `1` - 失败

---

### `agent skill info` - 显示技能详情

显示指定技能的详细信息。

#### 语法

```bash
agent skill info <skill-name> [options]
```

#### 参数

| 参数 | 类型 | 说明 |
|------|------|------|
| `skill-name` | string | 技能名称（完整名或简短名） |

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--template` | `-t` | flag | 显示提示词模板 | `false` |
| `--format` | `-f` | string | 输出格式（table/json） | `table` |

#### 示例

```bash
# 显示技能信息
agent skill info personal:greeting

# 包含模板
agent skill info personal:greeting --template

# JSON 输出
agent skill info personal:greeting --format json
```

#### 输出

```
╔══════════════════════════════════════════════════════════╗
║                     技能详情                              ║
╠══════════════════════════════════════════════════════════╣
║  personal:greeting                                       ║
║                                                           ║
║  描述：                                                   ║
║  向用户问候，根据时段和用户名生成友好的问候语              ║
║                                                           ║
║  命名空间： personal                                      ║
║  需要上下文： 否                                          ║
║  返回给 LLM： 是                                          ║
╚══════════════════════════════════════════════════════════╝

参数：
┌────────────────┬──────────┬──────────┬─────────────────────┐
│    参数名      │   类型   │   必需   │        描述         │
├────────────────┼──────────┼──────────┼─────────────────────┤
│  user_name     │  string  │    是    │ 用户名称            │
│  time_of_day   │  string  │    否    │ 时段（如：上午）    │
└────────────────┴──────────┴──────────┴─────────────────────┘
```

#### 退出码

- `0` - 成功
- `1` - 技能不存在

---

### `agent skill run` - 执行技能

执行指定的技能。

#### 语法

```bash
agent skill run <skill-name> [arguments...] [options]
```

#### 参数

| 参数 | 类型 | 说明 |
|------|------|------|
| `skill-name` | string | 技能名称 |
| `arguments` | key=value | 技能参数（键值对） |

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--session` | `-s` | string | 会话 ID | 当前会话 |
| `--provider` | `-p` | string | LLM 提供商 | 配置的默认值 |
| `--stream` | | flag | 流式输出 | `true` |

#### 示例

```bash
# 简单参数
agent skill run personal:greeting user_name="张三"

# 多个参数
agent skill run personal:greeting user_name="张三" time_of_day="上午"

# 复杂参数（使用引号）
agent skill run productivity:task \
  title="Review PR #123" \
  priority="high" \
  due_date="2026-03-25"

# 指定会话
agent skill run personal:greeting user_name="李四" --session 12345678
```

#### 输出

```
上午好，张三！有什么我可以帮您的吗？
```

#### 退出码

- `0` - 成功
- `1` - 技能不存在或参数错误
- `2` - 执行失败

---

## 文件管理命令

### `agent file upload` - 上传文件

上传文件到当前会话，支持文本、代码、配置等多种文件类型。

#### 语法

```bash
agent file upload <file-path> [options]
```

#### 参数

| 参数 | 类型 | 是否必需 | 说明 |
|------|------|----------|------|
| `file-path` | string | 是 | 要上传的文件路径 |

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--to-memory` | `-m` | flag | 自动提取文件内容到记忆 | false |

#### 示例

```bash
# 基本上传
agent file upload /path/to/document.txt

# 上传并提取到记忆
agent file upload config.json --to-memory

# 上传代码文件
agent file upload src/UserService.cs
```

#### 输出

```
✓ 文件已上传
文件 ID: abc12345-6789-1234-5678-123456789abc
文件名: document.txt
文件类型: .txt
文件大小: 1.2 KB
使用 '@file:document.txt' 或 '@file:abc12345' 引用此文件
```

#### 退出码

- `0` - 成功
- `1` - 文件不存在
- `2` - 文件验证失败（类型不支持或超过大小限制）

---

### `agent file list` - 列出文件

列出当前会话中上传的所有文件。

#### 语法

```bash
agent file list [options]
```

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--format` | `-f` | string | 输出格式（table/json） | `table` |

#### 示例

```bash
# 表格视图
agent file list

# JSON 视图
agent file list --format json
```

#### 输出格式

**表格格式**:

```
会话文件列表:
  文件 ID                                  | 文件名          | 类型  | 大小   | 上传时间
  ----------------------------------------|----------------|-------|--------|------------------
  abc12345-6789-...                       | document.txt   | .txt  | 1.2 KB | 2026-04-03 10:30
  def67890-abcd-...                       | config.json    | .json | 342 B  | 2026-04-03 10:35
```

**JSON 格式**:

```json
{
  "sessionId": "sess-123",
  "files": [
    {
      "id": "abc12345-6789-1234-5678-123456789abc",
      "fileName": "document.txt",
      "fileType": ".txt",
      "fileSize": 1234,
      "uploadedAt": "2026-04-03T10:30:00Z"
    }
  ],
  "total": 2
}
```

#### 退出码

- `0` - 成功

---

### `agent file show` - 查看文件详情

显示文件的详细信息，包括元数据和统计信息。

#### 语法

```bash
agent file show <file-id>
```

#### 参数

| 参数 | 类型 | 是否必需 | 说明 |
|------|------|----------|------|
| `file-id` | string | 是 | 文件 ID（支持短 ID） |

#### 示例

```bash
# 使用完整 ID
agent file show abc12345-6789-1234-5678-123456789abc

# 使用短 ID
agent file show abc12345
```

#### 输出

```
文件详情:
  ID: abc12345-6789-1234-5678-123456789abc
  文件名: document.txt
  类型: .txt
  大小: 1.2 KB
  上传时间: 2026-04-03 10:30:00
  会话 ID: sess-123
  
文件统计:
  行数: 42
  字符数: 1234
  是否截断: 否
```

#### 退出码

- `0` - 成功
- `1` - 文件不存在

---

### `agent file content` - 查看文件内容

显示文件的实际内容。

#### 语法

```bash
agent file content <file-id> [options]
```

#### 参数

| 参数 | 类型 | 是否必需 | 说明 |
|------|------|----------|------|
| `file-id` | string | 是 | 文件 ID（支持短 ID） |

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--lines` | `-n` | int | 限制显示行数 | 无限制 |

#### 示例

```bash
# 显示完整内容
agent file content abc12345

# 只显示前 50 行
agent file content abc12345 --lines 50
```

#### 输出

```
文件内容 (document.txt):
----------------------------------------
第一行内容
第二行内容
...
----------------------------------------
```

#### 退出码

- `0` - 成功
- `1` - 文件不存在

---

### `agent file delete` - 删除文件

从会话中删除文件。

#### 语法

```bash
agent file delete <file-id> [options]
```

#### 参数

| 参数 | 类型 | 是否必需 | 说明 |
|------|------|----------|------|
| `file-id` | string | 是 | 文件 ID（支持短 ID） |

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--force` | `-f` | flag | 跳过确认 | false |

#### 示例

```bash
# 交互式确认
agent file delete abc12345

# 跳过确认
agent file delete abc12345 --force
```

#### 输出

```
确认删除文件 'document.txt' (abc12345)? [y/N]: y
✓ 文件已删除
```

#### 退出码

- `0` - 成功
- `1` - 文件不存在
- `2` - 用户取消操作

---

### 在对话中引用文件

上传文件后，可以在对话中使用 `@file:` 语法引用文件内容：

#### 语法

```bash
@file:<filename>    # 按文件名引用
@file:<file-id>     # 按 ID 引用
```

#### 示例

```bash
# 按文件名引用
agent chat "请分析 @file:config.json 中的配置"

# 按 ID 引用（推荐，避免文件名冲突）
agent chat "请查看 @file:abc12345"

# 引用多个文件
agent chat "比较 @file:config.json 和 @file:config.prod.json 的差异"
```

#### 工作原理

1. 系统自动识别消息中的 `@file:` 引用
2. 从数据库和文件系统加载文件内容
3. 将文件内容替换为结构化格式（XML）
4. 发送给 LLM 进行处理

#### 注意事项

- 单个文件内容最大 10,000 字符（超过将自动截断）
- 如果文件名存在歧义，系统会提示使用 ID 引用
- 文件引用仅在上传的会话中有效

---

## 配置命令

### `agent config show` - 显示配置

显示当前配置。

#### 语法

```bash
agent config show [options]
```

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--format` | `-f` | string | 输出格式（table/json） | `table` |

#### 示例

```bash
# 表格格式
agent config show

# JSON 格式
agent config show --format json
```

#### 输出

```
┌──────────────────────┬────────────────────────┐
│        配置项        │          值            │
├──────────────────────┼────────────────────────┤
│  DefaultProvider     │  Ollama                │
│  OllamaModel         │  qwen2.5:latest        │
│  OllamaBaseUrl       │  http://localhost:11434│
│  EnableStreaming     │  true                  │
└──────────────────────┴────────────────────────┘
```

#### 退出码

- `0` - 成功

---

### `agent config set` - 设置配置

设置配置项的值。

#### 语法

```bash
agent config set <key> <value>
```

#### 参数

| 参数 | 类型 | 说明 |
|------|------|------|
| `key` | string | 配置项名称 |
| `value` | string | 配置项值 |

#### 支持的配置项

| 配置项 | 类型 | 说明 |
|--------|------|------|
| `DefaultProvider` | string | LLM 提供商（Ollama/Anthropic） |
| `OllamaModel` | string | Ollama 模型名称 |
| `OllamaBaseUrl` | string | Ollama 服务地址 |
| `AnthropicApiKey` | string | Anthropic API Key |
| `AnthropicModel` | string | Anthropic 模型名称 |
| `EnableStreaming` | boolean | 启用流式输出 |

#### 示例

```bash
# 设置提供商
agent config set DefaultProvider Ollama

# 设置模型
agent config set OllamaModel qwen2.5:latest

# 设置 API Key
agent config set AnthropicApiKey sk-ant-xxx

# 设置布尔值
agent config set EnableStreaming true
```

#### 退出码

- `0` - 成功
- `1` - 配置项不存在或值无效

---

### `agent config reset` - 重置配置

重置配置到默认值。

#### 语法

```bash
agent config reset [options]
```

#### 选项

| 选项 | 简写 | 类型 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--force` | `-f` | flag | 跳过确认提示 | `false` |

#### 示例

```bash
# 重置配置（带确认）
agent config reset

# 强制重置
agent config reset --force
```

#### 退出码

- `0` - 成功
- `1` - 用户取消

---

## REPL 命令

在 REPL 模式下可用的命令。

### 会话管理

| 命令 | 参数 | 说明 |
|------|------|------|
| `/new` | `[title]` | 创建新会话 |
| `/list` | | 列出所有会话 |
| `/session` | `<id>` | 切换会话 |
| `/delete` | `[id]` | 删除会话 |
| `/history` | | 显示当前会话历史 |

### 技能管理

| 命令 | 参数 | 说明 |
|------|------|------|
| `/skills` | `[namespace]` | 列出技能 |
| `/skill` | `<name>` | 显示技能详情 |

### 搜索功能 🆕 (V3.1)

| 命令 | 参数 | 说明 |
|------|------|------|
| `/search` | `<查询>` | 使用自然语言搜索消息内容 |

#### 示例

```bash
# 简单关键词搜索
/search Python

# 复合查询
/search bug fix

# 多词查询
/search 关于数据库的讨论
```

**注意**: 当前实现为简化版本，返回空结果。完整的 FTS5 全文搜索功能将在后续阶段实现。

---

### Memory 命令 🆕 (Phase 2)

#### `/memory migrate-to-vectors`

迁移现有记忆到向量数据库。

**用法**：
```bash
/memory migrate-to-vectors
```

**前置要求**：
- Qdrant 服务运行在 http://localhost:6333
- Ollama 服务运行在 http://localhost:11434
- nomic-embed-text 模型已下载

**行为**：
1. 验证 Qdrant 健康状态
2. 扫描所有现有记忆文件
3. 分批生成 Embedding 向量（每批 10 个）
4. 批量插入 Qdrant
5. 显示迁移进度和结果

**注意事项**：
- 迁移过程不会修改文件系统中的记忆
- 向量数据库仅用于加速搜索
- 迁移失败不会影响现有记忆

#### `/memory semantic-search`

使用向量搜索进行语义检索记忆。

**用法**：
```bash
/memory semantic-search "<查询文本>"
```

**示例**：
```bash
# 语义搜索
/memory semantic-search "TDD测试"

# 搜索编程概念
/memory semantic-search "函数式编程"
```

**性能**：
- 向量搜索：10-50ms
- 关键词搜索（降级）：1-5s

**自动降级**：
如果 Qdrant 不可用，系统会自动降级到关键词搜索。

---

### 别名管理 🆕 (V3 Phase 5)

| 命令 | 参数 | 说明 |
|------|------|------|
| `/alias` | | 列出所有已配置的别名 |
| `/alias add` | `<别名> <命令>` | 添加新别名 |
| `/alias remove` | `<别名>` | 移除别名 |

#### 示例

```bash
# 列出所有别名
/alias

# 添加别名
/alias add n new
/alias add ls list
/alias add s session

# 使用别名
/n 快速会话      # 等同于 /new 快速会话
/ls              # 等同于 /list
/s 12345678      # 等同于 /session 12345678

# 移除别名
/alias remove n
```

#### 预定义别名

默认配置包含以下别名：

- `n` → `new`
- `ls` → `list`
- `s` → `session`
- `del` → `delete`
- `q` → `quit`
- `h` → `help`

#### 别名配置文件

别名保存在 `~/.agent/aliases.json`：

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

### 标签管理 🆕 (V3.1)

| 命令 | 参数 | 说明 |
|------|------|------|
| `/tag add` | `<标签> [--emoji 🐍] [--color #FF0000]` | 为当前会话添加标签 |
| `/tag remove` | `<标签>` | 从当前会话移除标签 |
| `/tag list` | | 列出当前会话的标签 |
| `/tag list --all` | | 列出所有标签及使用统计 |
| `/tag suggest` | | 基于会话标题生成智能标签建议 |

#### 添加标签示例

```bash
# 简单标签
/tag add python

# 带 Emoji 和颜色
/tag add python --emoji 🐍 --color #3776AB

# 只带 Emoji
/tag add bug --emoji 🐛

# 只带颜色
/tag add feature --color #00FF00
```

#### 移除标签示例

```bash
/tag remove python
```

#### 列出标签示例

```bash
# 列出当前会话标签
/tag list

# 列出所有标签统计
/tag list --all
```

#### 智能标签建议示例

```bash
# 生成建议
/tag suggest

# 输出示例:
# 💡 生成标签建议中...
#
# ┌──────────┬───────┬─────────┐
# │ 标签     │ Emoji │ 颜色    │
# ├──────────┼───────┼─────────┤
# │ python   │ 🐍    │ #3776AB │
# │ tutorial │ 📚    │ #FFA500 │
# │ beginner │ 🌱    │ #90EE90 │
# └──────────┴───────┴─────────┘
#
# 是否应用这些标签? (y/n)
```

#### 标签管理限制

- 每个会话最多 5 个标签
- 标签名称最长 50 字符
- 标签名称自动转换为小写
- 自动去重（重复标签会被忽略）
- 智能建议响应时间 < 5秒（取决于 LLM 速度）

#### 标签来源

- **User**: 手动添加的标签
- **LLM**: 通过 `/tag suggest` 生成的标签

---

### LLM 配置

| 命令 | 参数 | 说明 |
|------|------|------|
| `/switch` | `<provider>` | 切换提供商 |
| `/provider` | | 显示当前提供商 |

### 其他

| 命令 | 参数 | 说明 |
|------|------|------|
| `/clear` | | 清屏 |
| `/help` | | 显示帮助 |
| `/exit` | | 退出 REPL |

---

## 退出码

| 退出码 | 说明 |
|--------|------|
| `0` | 成功 |
| `1` | 通用错误（参数错误、资源不存在等） |
| `2` | 执行失败（技能执行、LLM 调用等） |
| `3` | 配置错误 |
| `4` | 数据库错误 |

---

## 性能指标 (V3.1)

| 命令 | 平均响应时间 | 备注 |
|------|--------------|------|
| `/search` | < 1秒 | 简化实现，当前返回空结果 |
| `/tag add` | < 100ms | 本地数据库操作 |
| `/tag remove` | < 100ms | 本地数据库操作 |
| `/tag list` | < 200ms | 本地数据库查询 |
| `/tag suggest` | < 5秒 | LLM 调用，取决于网络和模型速度 |

---

**更新时间**: 2026-03-26
**版本**: V3.1 (智能搜索和标签系统)
**维护者**: General Agent Team
