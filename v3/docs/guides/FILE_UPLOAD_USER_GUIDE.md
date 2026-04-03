# 文件上传功能使用指南

## 📖 概述

文件上传功能允许你在对话中上传文件，Agent 可以读取和分析文件内容，并通过 `@file:` 语法在对话中引用文件。

## 🚀 快速开始

### 1. 上传文件

```bash
# 上传文件到当前会话
agent file upload /path/to/file.txt

# 上传文件并自动提取到记忆
agent file upload config.json --to-memory
```

**支持的文件类型**：
- 文本文件：`.txt`, `.md`, `.markdown`, `.log`
- 代码文件：`.cs`, `.py`, `.js`, `.ts`, `.rs`, `.go`, `.java`, `.cpp`, 等
- 配置文件：`.json`, `.yaml`, `.yml`, `.xml`, `.toml`, `.ini`
- Web 文件：`.html`, `.css`, `.scss`
- Shell 脚本：`.sh`, `.bash`, `.zsh`, `.ps1`, `.bat`

**文件限制**：
- 最大文件大小：5 MB
- 文件内容最大长度：10,000 字符（超过将自动截断）

### 2. 列出已上传的文件

```bash
# 表格视图
agent file list

# JSON 视图
agent file list --format json
```

### 3. 查看文件详情

```bash
# 使用文件 ID
agent file show abc12345-1234-1234-1234-123456789abc

# 支持短 ID
agent file show abc12345
```

### 4. 查看文件内容

```bash
# 查看完整内容
agent file content <file-id>

# 限制显示行数
agent file content <file-id> --lines 50
```

### 5. 删除文件

```bash
# 交互式确认
agent file delete <file-id>

# 跳过确认
agent file delete <file-id> --force
```

## 💬 在对话中引用文件

上传文件后，可以使用 `@file:` 语法在对话中引用文件内容：

```bash
# 按文件名引用
agent chat "请分析 @file:config.json 中的配置"

# 按文件 ID 引用
agent chat "请查看 @file:abc12345-1234-1234-1234-123456789abc"

# 引用多个文件
agent chat "比较 @file:config.json 和 @file:config.prod.json 的差异"
```

**引用解析流程**：
1. 系统自动识别 `@file:` 引用
2. 从数据库和文件系统加载文件内容
3. 将文件内容替换为结构化格式
4. 发送给 LLM 进行处理

**替换后的格式**：
```xml
<file name="config.json" type=".json" size="1234">
{
  "api_url": "https://api.example.com",
  "timeout": 30
}
</file>
```

## 🧠 记忆集成

使用 `--to-memory` 选项可以自动将文件内容提取到记忆系统：

```bash
agent file upload important-doc.md --to-memory
```

**提取流程**：
1. 读取文件内容
2. 构建提取上下文（包含文件元数据）
3. 调用 LLM 提取记忆建议
4. 自动保存高置信度记忆（> 0.7）
5. 显示保存结果

**适用场景**：
- 项目文档：自动提取关键信息和决策
- 配置文件：记录重要配置和参数
- 代码文件：提取架构和最佳实践
- 笔记文件：捕获个人偏好和反馈

## 📝 使用示例

### 示例 1：上传并分析代码文件

```bash
# 1. 上传代码文件
agent file upload src/UserService.cs

# 输出：
# ✓ 文件已上传
# 文件 ID: abc12345-6789-...
# 文件名: UserService.cs
# 文件类型: .cs
# 文件大小: 3.5 KB
# 使用 '@file:UserService.cs' 或 '@file:abc12345-6789-...' 引用此文件

# 2. 在对话中引用
agent chat "请审查 @file:UserService.cs 中的代码，找出潜在的安全问题"
```

### 示例 2：上传配置文件并提取到记忆

```bash
# 上传并自动提取记忆
agent file upload appsettings.json --to-memory

# 输出：
# ✓ 文件已上传
# 正在提取文件内容到记忆...
# ✓ 已保存记忆: database_connection (Reference)
# ✓ 已保存记忆: api_endpoints (Knowledge)
# ✓ 成功保存 2 条记忆
```

### 示例 3：比较多个文件

```bash
# 上传两个版本的配置
agent file upload config.dev.json
agent file upload config.prod.json

# 比较差异
agent chat "比较 @file:config.dev.json 和 @file:config.prod.json，列出主要差异"
```

## 🔍 文件处理细节

### 文本文件处理

- 统计行数
- 检测编码（默认 UTF-8）
- 超长内容自动截断

### 代码文件处理

- 自动语言检测（20+ 种语言）
- 统计代码行数和非空行数
- 保留完整代码结构

### 配置文件处理

- JSON 格式验证
- 检测根节点类型
- 解析错误提示

## ⚠️ 注意事项

1. **文件大小限制**：单个文件最大 5 MB
2. **内容截断**：超过 10,000 字符的文件将被截断
3. **会话隔离**：文件只能在上传的会话中访问
4. **文件名冲突**：同名文件会自动添加时间戳前缀
5. **重名引用**：按文件名引用时，如有多个同名文件，使用最新上传的

## 🛠️ 故障排除

### 文件上传失败

**问题**：`✗ 文件验证失败: 不支持的文件类型`

**解决**：检查文件扩展名是否在支持列表中，或联系管理员添加新类型。

### 文件引用无法解析

**问题**：对话中 `@file:xxx` 没有被替换

**解决**：
1. 确认文件已成功上传（使用 `agent file list` 检查）
2. 检查文件名拼写是否正确
3. 如果使用 ID 引用，确认 ID 完整且正确

### 记忆提取无反应

**问题**：使用 `--to-memory` 但没有提取到记忆

**解决**：
1. 确认记忆服务已启用
2. 检查文件内容是否包含可提取的信息
3. 查看日志了解详细错误信息

## 📚 更多信息

- 技术实现文档：[V3_PHASE_FILE_UPLOAD_PLAN.md](./V3_PHASE_FILE_UPLOAD_PLAN.md)
- API 参考：查看源代码注释
- 问题反馈：提交 GitHub Issue
