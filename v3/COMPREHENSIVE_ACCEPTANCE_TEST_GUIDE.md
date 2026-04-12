# General Agent V3 - 完整验收测试指南

**版本**: V3.2.0  
**创建时间**: 2026-04-10  
**测试目标**: 验证 General Agent V3 的**所有功能**，包括核心系统和5个优先功能

---

## 📋 测试概览

本文档提供**完整的功能验收测试**，覆盖：

### 测试范围

1. **核心系统功能** （基础架构）
2. **LLM 集成功能** （对话能力）
3. **技能系统功能** （技能定义和调用）
4. **5个用户优先功能**：
   - 长期记忆系统
   - 上下文压缩
   - 文件上传系统
   - 技能抽取功能
   - 计划任务系统
5. **V3.1 增强功能** （智能搜索和标签）
6. **CLI/REPL 功能** （用户界面）

### 测试环境要求

- **.NET SDK**: 10.0 或更高
- **操作系统**: macOS 14+, Ubuntu 22.04+, Windows 11+
- **可选服务**:
  - Ollama (本地LLM测试)
  - Qdrant (向量搜索测试)

---

## 🚀 第一部分：快速验证

### 方法 1: 使用快速测试脚本（推荐）

```bash
cd v3
./quick-test.sh
```

**预期结果**:
```
==================================================
General Agent V3 快速验证测试
==================================================

1. 编译项目...
✓ 编译成功

2. 运行核心测试...
✓ 所有核心测试通过

3. 测试计划任务功能...
✓ 任务创建成功
✓ 任务列表正常
✓ 任务删除成功

4. 测试基本命令...
✓ 帮助命令正常

==================================================
✓ 所有测试通过！
==================================================
```

### 方法 2: 运行完整测试套件

```bash
cd v3
dotnet test --logger "console;verbosity=normal"
```

**预期结果**:
- ✅ **总计**: 866 个测试
- ✅ **通过**: 865 个
- ℹ️ **跳过**: 1 个 (Ollama 集成测试)
- ❌ **失败**: 0 个
- ✅ **覆盖率**: 85%+

---

## 📦 第二部分：核心系统功能测试

### 测试 1: 编译和构建

#### 1.1 清理和重新构建

```bash
cd v3

# 清理所有构建产物
find . -type d -name "bin" -o -name "obj" | xargs rm -rf

# 恢复依赖
dotnet restore

# 编译项目
dotnet build --configuration Release
```

**验收标准**:
- [ ] ✅ 无编译错误
- [ ] ✅ 无编译警告
- [ ] ✅ 所有项目成功构建
- [ ] ✅ 输出: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

### 测试 2: 数据库初始化

#### 2.1 数据库自动迁移

```bash
cd src/GeneralAgent.Hosts.Console

# 删除旧数据库（如果存在）
rm -f ~/.agent/agent.db

# 启动应用（会自动创建数据库）
dotnet run -- --help
```

**验收标准**:
- [ ] ✅ 数据库文件创建成功: `~/.agent/agent.db`
- [ ] ✅ 所有表创建成功（15+ 张表）
- [ ] ✅ 无数据库错误
- [ ] ✅ 帮助信息正常显示

#### 2.2 验证数据库结构

```bash
# 使用 sqlite3 查看
sqlite3 ~/.agent/agent.db ".tables"
```

**预期表**:
```
Sessions              Messages              Skills
Memories              UploadedFiles         FileVersions
FilePermissions       ScheduledTasks        TaskExecutions
ExtractionRecords     SearchQueries         Tags
SessionTags           __EFMigrationsHistory
```

**验收标准**:
- [ ] ✅ 所有核心表存在
- [ ] ✅ 迁移历史记录正常

---

### 测试 3: 应用启动和性能

#### 3.1 启动时间测试

```bash
cd src/GeneralAgent.Hosts.Console

# 测试 CLI 模式启动
time dotnet run -- --help

# 测试 REPL 模式启动
time dotnet run
```

**验收标准**:
- [ ] ✅ CLI 启动时间 < 3 秒
- [ ] ✅ REPL 启动时间 < 200ms（从显示提示符开始计算）
- [ ] ✅ 无启动错误
- [ ] ✅ 日志输出清晰

#### 3.2 REPL 基本功能

在 REPL 中测试：

```bash
You> /help           # 查看帮助
You> /version        # 查看版本
You> /exit           # 退出
```

**验收标准**:
- [ ] ✅ 提示符显示正常
- [ ] ✅ 命令响应时间 < 50ms
- [ ] ✅ 帮助信息完整
- [ ] ✅ 退出正常

---

## 🗄️ 第三部分：会话和消息管理测试

### 测试 4: 会话管理

#### 4.1 创建会话

```bash
cd src/GeneralAgent.Hosts.Console

# 创建新会话
dotnet run -- new --title "测试会话1"
dotnet run -- new --title "测试会话2"
dotnet run -- new --title "测试会话3"
```

**验收标准**:
- [ ] ✅ 返回会话 ID
- [ ] ✅ 会话标题正确
- [ ] ✅ 创建时间自动设置
- [ ] ✅ 无错误信息

#### 4.2 列出会话

```bash
# 列出所有会话
dotnet run -- list

# 分页列表
dotnet run -- list --page 1 --size 10
```

**验收标准**:
- [ ] ✅ 显示所有已创建的会话
- [ ] ✅ 包含会话 ID、标题、创建时间
- [ ] ✅ 分页功能正常
- [ ] ✅ 表格格式美观

#### 4.3 查看会话详情

```bash
# 获取会话 ID（从列表中）
SESSION_ID="<your-session-id>"

# 查看详情
dotnet run -- show $SESSION_ID
```

**验收标准**:
- [ ] ✅ 显示会话基本信息
- [ ] ✅ 显示消息数量
- [ ] ✅ 显示创建和更新时间
- [ ] ✅ 格式清晰易读

#### 4.4 删除会话

```bash
# 删除会话
dotnet run -- delete $SESSION_ID
```

**验收标准**:
- [ ] ✅ 删除成功确认
- [ ] ✅ 列表中不再显示
- [ ] ✅ 关联消息也被删除（级联删除）

---

### 测试 5: 消息管理

#### 5.1 发送消息

```bash
# 创建新会话
SESSION_ID=$(dotnet run -- new --title "消息测试" | grep "ID:" | awk '{print $2}')

# 发送消息
dotnet run -- chat $SESSION_ID "你好，请介绍一下自己"
dotnet run -- chat $SESSION_ID "你能帮我做什么？"
```

**验收标准**:
- [ ] ✅ LLM 响应正常
- [ ] ✅ 消息保存到数据库
- [ ] ✅ 对话历史可访问
- [ ] ✅ 响应时间合理（< 30秒）

#### 5.2 查看对话历史

```bash
# 查看会话详情（包含历史）
dotnet run -- show $SESSION_ID
```

**验收标准**:
- [ ] ✅ 显示所有历史消息
- [ ] ✅ 区分用户和助手消息
- [ ] ✅ 时间戳正确
- [ ] ✅ 内容完整

---

## 🤖 第四部分：LLM 集成功能测试

### 测试 6: LLM 提供商配置

#### 6.1 Anthropic Claude 配置

编辑 `appsettings.json`:
```json
{
  "LLM": {
    "DefaultProvider": "Anthropic",
    "Providers": {
      "Anthropic": {
        "ApiKey": "sk-ant-your-key",
        "Model": "claude-3-5-sonnet-20241022"
      }
    }
  }
}
```

测试对话:
```bash
dotnet run -- chat <session-id> "请用一句话介绍 .NET 10"
```

**验收标准**:
- [ ] ✅ API 调用成功
- [ ] ✅ 响应内容合理
- [ ] ✅ 无 API 错误
- [ ] ✅ Token 使用统计正常

#### 6.2 Ollama 配置

编辑 `appsettings.json`:
```json
{
  "LLM": {
    "DefaultProvider": "Ollama",
    "Providers": {
      "Ollama": {
        "BaseUrl": "http://localhost:11434",
        "Model": "qwen2.5:7b"
      }
    }
  }
}
```

启动 Ollama:
```bash
ollama pull qwen2.5:7b
ollama serve
```

测试对话:
```bash
dotnet run -- chat <session-id> "介绍一下 Ollama"
```

**验收标准**:
- [ ] ✅ 连接 Ollama 成功
- [ ] ✅ 响应流式输出正常
- [ ] ✅ 本地推理工作
- [ ] ✅ 无连接错误

#### 6.3 流式响应测试

在 REPL 中测试:
```bash
You> 请详细解释什么是 .NET 10，包括它的主要特性
```

**验收标准**:
- [ ] ✅ 响应逐字显示（流式输出）
- [ ] ✅ 无明显延迟
- [ ] ✅ 可以中断（Ctrl+C）
- [ ] ✅ 完整响应保存到历史

---

## 📚 第五部分：技能系统功能测试

### 测试 7: 技能定义和加载

#### 7.1 创建测试技能

创建文件 `v3/skills/test/hello.md`:
```markdown
---
name: hello
description: 向用户打招呼
parameters:
  - name: user_name
    type: string
    required: true
    description: 用户名称
  - name: greeting
    type: string
    required: false
    default: "你好"
    description: 问候语
---

{{ greeting }}，{{ user_name }}！欢迎使用 General Agent V3。

今天有什么我可以帮助你的吗？
```

#### 7.2 验证技能加载

```bash
# 启动 REPL
dotnet run

# 在 REPL 中
You> /skills                    # 列出所有技能
You> /skills test               # 列出 test 命名空间的技能
You> /skill test:hello          # 查看技能详情
You> /skill test:hello --template  # 查看模板
```

**验收标准**:
- [ ] ✅ 技能自动加载
- [ ] ✅ 命名空间解析正确
- [ ] ✅ 参数定义清晰
- [ ] ✅ 模板显示正确

#### 7.3 调用技能

```bash
# @ 语法（对话中调用）
You> @test:hello user_name="张三"
You> @test:hello user_name="李四" greeting="早上好"

# / 命令语法
You> /skill test:hello --user-name "王五"
```

**验收标准**:
- [ ] ✅ 参数替换正确
- [ ] ✅ 必填参数验证
- [ ] ✅ 默认值生效
- [ ] ✅ LLM 收到正确的提示词

#### 7.4 技能命名空间管理

创建多个命名空间:
```bash
# 创建技能文件
mkdir -p v3/skills/personal
mkdir -p v3/skills/work
mkdir -p v3/skills/productivity

# 创建不同的技能文件
touch v3/skills/personal/greeting.md
touch v3/skills/work/meeting.md
touch v3/skills/productivity/todo.md
```

测试:
```bash
You> /skills                    # 列出所有
You> /skills personal           # 仅个人技能
You> /skills work               # 仅工作技能
```

**验收标准**:
- [ ] ✅ 命名空间隔离正确
- [ ] ✅ 技能名称无冲突
- [ ] ✅ 列表过滤功能正常

---

## 🧠 第六部分：长期记忆系统测试

### 测试 8: 记忆基本操作

#### 8.1 创建记忆

```bash
# 在 REPL 中
You> /memory add user john_preferences

# LLM 会引导你输入记忆内容
# 输入: John 喜欢使用 Python 和 Rust 编程，偏好函数式编程风格
```

**验收标准**:
- [ ] ✅ 记忆创建成功
- [ ] ✅ 返回记忆 ID
- [ ] ✅ 类型设置正确
- [ ] ✅ 时间戳自动生成

#### 8.2 列出记忆

```bash
You> /memory list                   # 所有记忆
You> /memory list user              # 仅用户记忆
You> /memory list feedback          # 仅反馈记忆
```

**验收标准**:
- [ ] ✅ 显示所有记忆
- [ ] ✅ 类型过滤正常
- [ ] ✅ 列表格式清晰

#### 8.3 查看记忆详情

```bash
You> /memory show john_preferences
```

**验收标准**:
- [ ] ✅ 显示完整内容
- [ ] ✅ 显示元数据（类型、来源、时间）
- [ ] ✅ 格式易读

#### 8.4 更新记忆

```bash
You> /memory update john_preferences

# 更新内容: John 现在也开始学习 Go 语言
```

**验收标准**:
- [ ] ✅ 更新成功
- [ ] ✅ 更新时间自动更新
- [ ] ✅ 内容替换正确

#### 8.5 删除记忆

```bash
You> /memory delete john_preferences
```

**验收标准**:
- [ ] ✅ 删除成功
- [ ] ✅ 列表中不再显示

---

### 测试 9: 记忆搜索功能

#### 9.1 关键词搜索

```bash
# 创建测试记忆
You> /memory add knowledge python_best_practices
# 内容: Python 最佳实践包括使用类型提示、遵循 PEP 8 规范、编写单元测试

You> /memory add knowledge rust_ownership
# 内容: Rust 的所有权系统是其内存安全的核心，包括借用检查器和生命周期

# 搜索
You> /memory search "Python"
You> /memory search "类型"
You> /memory search "内存安全"
```

**验收标准**:
- [ ] ✅ 搜索结果相关
- [ ] ✅ 高亮匹配关键词
- [ ] ✅ 响应时间 < 100ms

#### 9.2 语义搜索（需要 Qdrant）

启动 Qdrant:
```bash
docker run -d --name qdrant -p 6333:6333 qdrant/qdrant
```

配置 Embedding:
```bash
# 使用 Ollama
ollama pull nomic-embed-text
ollama serve
```

测试语义搜索:
```bash
You> /memory semantic-search "如何写出高质量的代码"
You> /memory semantic-search "并发编程的安全性"
```

**验收标准**:
- [ ] ✅ 返回语义相关的记忆
- [ ] ✅ 相似度评分合理
- [ ] ✅ 响应时间 10-50ms
- [ ] ✅ 结果按相似度排序

#### 9.3 混合搜索

```bash
You> /memory hybrid-search "Python 编程最佳实践"
```

**验收标准**:
- [ ] ✅ 结合关键词和语义结果
- [ ] ✅ 综合排序合理
- [ ] ✅ 性能良好

---

### 测试 10: LLM 驱动的记忆提取

#### 10.1 从对话提取

```bash
# 创建一个对话
You> 我最喜欢的编程语言是 Rust，因为它的性能很好
Assistant> 好的，我记住了...

# 提取记忆
You> /memory extract

# 系统会自动分析对话并生成记忆
```

**验收标准**:
- [ ] ✅ 自动识别关键信息
- [ ] ✅ 生成合适的记忆类型
- [ ] ✅ 内容提取准确
- [ ] ✅ 可选择接受或拒绝

#### 10.2 记忆降级测试

```bash
# 停止 Qdrant
docker stop qdrant

# 测试搜索（应降级到关键词搜索）
You> /memory semantic-search "Python 编程"
```

**验收标准**:
- [ ] ✅ 自动降级到关键词搜索
- [ ] ✅ 显示降级提示
- [ ] ✅ 搜索仍然可用
- [ ] ✅ 无错误抛出

---

## 🗜️ 第七部分：上下文压缩测试

### 测试 11: 压缩策略

#### 11.1 触发自动压缩

```bash
# 创建长对话（15+ 条消息）
You> 消息1
Assistant> 回复1
You> 消息2
Assistant> 回复2
...
# 继续到15条以上
```

**验收标准**:
- [ ] ✅ 自动触发压缩（消息数 >= 15）
- [ ] ✅ 选择合适的压缩策略
- [ ] ✅ 压缩后 Token 数量减少
- [ ] ✅ 关键信息保留

#### 11.2 测试 Sliding Window 策略

```bash
# 短对话（10-15 条消息）
# 系统应选择 Sliding Window 策略
```

**验收标准**:
- [ ] ✅ 保留最近 N 条消息
- [ ] ✅ 旧消息被移除
- [ ] ✅ 压缩速度快（< 100ms）

#### 11.3 测试 Semantic 策略

```bash
# 中等长度对话（15-30 条消息）
# 系统应选择 Semantic 策略
```

**验收标准**:
- [ ] ✅ LLM 生成摘要
- [ ] ✅ 关键信息提取准确
- [ ] ✅ 压缩率高（50%+）
- [ ] ✅ 摘要质量好

#### 11.4 压缩统计

```bash
# 查看压缩统计
# （需要在代码中添加命令）
```

**验收标准**:
- [ ] ✅ Token 使用统计准确
- [ ] ✅ 压缩率计算正确
- [ ] ✅ 历史记录可查询

---

## 📁 第八部分：文件上传系统测试

### 测试 12: 基本文件操作

#### 12.1 上传文件

```bash
# 创建测试文件
echo "这是一个测试文件" > test.txt
echo "console.log('Hello');" > test.js
cat > test.json <<EOF
{
  "name": "test",
  "version": "1.0.0"
}
EOF

# 上传文件
You> /file upload test.txt
You> /file upload test.js
You> /file upload test.json
```

**验收标准**:
- [ ] ✅ 上传成功返回文件 ID
- [ ] ✅ 文件类型自动识别
- [ ] ✅ 文件大小记录正确
- [ ] ✅ 所有者设置正确

#### 12.2 列出文件

```bash
You> /file list
```

**验收标准**:
- [ ] ✅ 显示所有上传的文件
- [ ] ✅ 包含文件名、大小、类型、上传时间
- [ ] ✅ 表格格式清晰

#### 12.3 查看文件详情

```bash
FILE_ID="<your-file-id>"

You> /file show $FILE_ID
```

**验收标准**:
- [ ] ✅ 显示完整元数据
- [ ] ✅ 显示访问级别
- [ ] ✅ 显示版本信息

#### 12.4 查看文件内容

```bash
You> /file content $FILE_ID
```

**验收标准**:
- [ ] ✅ 内容显示正确
- [ ] ✅ 格式保留
- [ ] ✅ 大文件截断合理

#### 12.5 删除文件

```bash
You> /file delete $FILE_ID
```

**验收标准**:
- [ ] ✅ 删除成功
- [ ] ✅ 列表中不再显示
- [ ] ✅ 物理文件也被删除

---

### 测试 13: 文件引用功能

#### 13.1 在对话中引用文件

```bash
# 上传一个代码文件
cat > example.cs <<EOF
using System;

public class Example
{
    public void SayHello()
    {
        Console.WriteLine("Hello, World!");
    }
}
EOF

You> /file upload example.cs

# 在对话中引用
You> @file:example.cs 请分析这段代码
```

**验收标准**:
- [ ] ✅ 文件内容自动读取
- [ ] ✅ 注入到 LLM 上下文
- [ ] ✅ LLM 能分析文件内容
- [ ] ✅ 引用语法解析正确

#### 13.2 引用多个文件

```bash
You> @file:test.txt @file:test.json 比较这两个文件
```

**验收标准**:
- [ ] ✅ 两个文件都被读取
- [ ] ✅ LLM 能同时分析
- [ ] ✅ 上下文组织合理

---

### 测试 14: 跨会话文件访问

#### 14.1 设置访问级别

```bash
# 上传 Private 文件（默认）
You> /file upload private.txt

# 上传 Public 文件
You> /file upload public.txt --access-level public

# 修改访问级别
You> /file access <file-id> --level shared
```

**验收标准**:
- [ ] ✅ Private 文件仅所有者访问
- [ ] ✅ Public 文件所有人访问
- [ ] ✅ Shared 文件需要权限
- [ ] ✅ 访问级别修改成功

#### 14.2 权限管理

```bash
# 授予权限
You> /file share <file-id> --user "user2" --permission read

# 查看权限
You> /file permissions <file-id>

# 撤销权限
You> /file revoke <file-id> --user "user2"
```

**验收标准**:
- [ ] ✅ 权限授予成功
- [ ] ✅ 权限列表显示正确
- [ ] ✅ 权限撤销生效
- [ ] ✅ 读写权限分别控制

#### 14.3 全局文件库

```bash
# 查看所有可访问文件
You> /file library list

# 按级别过滤
You> /file library list --level public

# 搜索文件
You> /file library search "test"

# 查看我拥有的文件
You> /file library owned

# 查看共享给我的文件
You> /file library shared
```

**验收标准**:
- [ ] ✅ 列表包含所有可访问文件
- [ ] ✅ 过滤功能正常
- [ ] ✅ 搜索准确
- [ ] ✅ 权限检查正确

---

### 测试 15: 文件版本控制

#### 15.1 创建新版本

```bash
# 上传同名文件（创建新版本）
echo "Version 1" > version-test.txt
You> /file upload version-test.txt

# 修改并重新上传
echo "Version 2" > version-test.txt
You> /file upload version-test.txt

echo "Version 3" > version-test.txt
You> /file upload version-test.txt
```

**验收标准**:
- [ ] ✅ 每次上传创建新版本
- [ ] ✅ 版本号自动递增
- [ ] ✅ 当前版本更新

#### 15.2 查看版本历史

```bash
You> /file versions <file-id>
```

**验收标准**:
- [ ] ✅ 显示所有版本
- [ ] ✅ 包含版本号、时间、大小
- [ ] ✅ 按时间倒序排列

#### 15.3 查看特定版本内容

```bash
You> /file content <file-id> --version 1
You> /file content <file-id> --version 2
```

**验收标准**:
- [ ] ✅ 显示指定版本内容
- [ ] ✅ 内容与上传时一致
- [ ] ✅ 不同版本内容不同

#### 15.4 恢复到旧版本

```bash
You> /file restore <file-id> --version 2
```

**验收标准**:
- [ ] ✅ 恢复成功
- [ ] ✅ 当前版本变为指定版本
- [ ] ✅ 版本号继续递增（不覆盖）

---

## 🤖 第九部分：技能抽取功能测试

### 测试 16: 从对话抽取技能

#### 16.1 创建包含可抽取模式的对话

```bash
# 创建新会话
SESSION_ID=$(dotnet run -- new --title "技能抽取测试" | grep "ID:" | awk '{print $2}')

# 进行对话
dotnet run -- chat $SESSION_ID "请帮我写一封感谢信，收件人是 Alice"
dotnet run -- chat $SESSION_ID "再写一封给 Bob"
dotnet run -- chat $SESSION_ID "再写一封给 Carol"
```

**验收标准**:
- [ ] ✅ 对话包含重复模式
- [ ] ✅ 对话保存完整

#### 16.2 抽取技能

```bash
You> /skill extract $SESSION_ID
```

**LLM 会分析对话并生成技能定义，例如**:
```yaml
---
name: thank-you-letter
description: 生成感谢信
parameters:
  - name: recipient
    type: string
    required: true
    description: 收件人姓名
---

亲爱的 {{ recipient }}：

感谢您一直以来的支持...

此致
敬礼
```

**验收标准**:
- [ ] ✅ 识别重复模式
- [ ] ✅ 生成合理的参数
- [ ] ✅ 模板格式正确
- [ ] ✅ 可选择接受或编辑

#### 16.3 技能编辑和确认

```bash
# 系统会询问是否接受
# 如果选择编辑，可以修改：
# - 技能名称
# - 技能描述
# - 参数定义
# - 模板内容

# 确认后保存
```

**验收标准**:
- [ ] ✅ 可以编辑所有字段
- [ ] ✅ 验证通过后保存
- [ ] ✅ 技能立即可用
- [ ] ✅ 保存到正确的命名空间

#### 16.4 验证抽取的技能

```bash
# 列出技能
You> /skills

# 查看抽取的技能
You> /skill thank-you-letter

# 使用抽取的技能
You> @thank-you-letter recipient="David"
```

**验收标准**:
- [ ] ✅ 技能出现在列表中
- [ ] ✅ 详情显示正确
- [ ] ✅ 可以正常调用
- [ ] ✅ 参数替换正确

---

### 测试 17: 技能抽取历史和统计

#### 17.1 查看抽取历史

```bash
You> /skill history
```

**验收标准**:
- [ ] ✅ 显示所有抽取记录
- [ ] ✅ 包含时间、会话、状态
- [ ] ✅ 按时间倒序排列

#### 17.2 过滤历史记录

```bash
You> /skill history --status success
You> /skill history --status failed
You> /skill history --from "2024-01-01" --to "2024-12-31"
```

**验收标准**:
- [ ] ✅ 状态过滤正常
- [ ] ✅ 时间范围过滤正常
- [ ] ✅ 结果准确

#### 17.3 查看统计信息

```bash
You> /skill stats
```

**验收标准**:
- [ ] ✅ 显示总抽取次数
- [ ] ✅ 显示成功/失败率
- [ ] ✅ 显示最常用的技能类型
- [ ] ✅ 统计数据准确

---

## ⏰ 第十部分：计划任务系统测试

### 测试 18: 任务创建和调度

#### 18.1 使用 Cron 表达式创建任务

```bash
# 创建任务（每 5 分钟执行）
dotnet run -- task schedule "测试任务-Cron" \
  --schedule "*/5 * * * *" \
  --type reminder \
  --payload '{"message":"5分钟提醒"}' \
  --description "每5分钟执行一次"

# 创建任务（每天 9:00）
dotnet run -- task schedule "每日任务" \
  --schedule "0 9 * * *" \
  --type reminder \
  --payload '{"message":"早安"}' \
  --description "每天早上9点"
```

**验收标准**:
- [ ] ✅ Cron 表达式解析正确
- [ ] ✅ 任务创建成功
- [ ] ✅ 下次执行时间计算正确
- [ ] ✅ 返回任务 ID

#### 18.2 使用自然语言创建任务

```bash
# 中文自然语言
dotnet run -- task schedule "中文任务" \
  --schedule "每天9:00" \
  --type reminder \
  --payload '{"message":"起床了"}' \
  --description "每天早上9点提醒"

dotnet run -- task schedule "周一任务" \
  --schedule "每周一9:00" \
  --type reminder \
  --payload '{"message":"周会提醒"}' \
  --description "每周一早上9点"

dotnet run -- task schedule "月度任务" \
  --schedule "每月1号9:00" \
  --type reminder \
  --payload '{"message":"月度报告"}' \
  --description "每月1号"
```

**验收标准**:
- [ ] ✅ 自然语言解析正确
- [ ] ✅ 转换为 Cron 表达式
- [ ] ✅ 任务创建成功
- [ ] ✅ 执行时间准确

#### 18.3 创建不同类型的任务

```bash
# 技能调用任务
dotnet run -- task schedule "技能任务" \
  --schedule "每小时" \
  --type skill \
  --payload '{"skill":"test:hello","args":{"user_name":"Auto"}}'

# 记忆提醒任务
dotnet run -- task schedule "记忆提醒" \
  --schedule "每天9:00" \
  --type reminder \
  --payload '{"message":"查看今日待办"}'

# 自定义命令任务
dotnet run -- task schedule "自定义任务" \
  --schedule "0 2 * * *" \
  --type custom \
  --payload '{"command":"backup.sh"}'
```

**验收标准**:
- [ ] ✅ 所有任务类型创建成功
- [ ] ✅ Payload 格式正确
- [ ] ✅ 类型字段设置正确

---

### 测试 19: 任务管理操作

#### 19.1 列出任务

```bash
# 列出所有任务
dotnet run -- task list

# 按状态过滤
dotnet run -- task list --status pending
dotnet run -- task list --status paused

# 按类型过滤
dotnet run -- task list --type reminder
dotnet run -- task list --type skill

# JSON 格式输出
dotnet run -- task list --format json
```

**验收标准**:
- [ ] ✅ 显示所有任务
- [ ] ✅ 包含关键信息（名称、状态、下次执行时间）
- [ ] ✅ 过滤功能正常
- [ ] ✅ 格式选项生效

#### 19.2 查看任务详情

```bash
TASK_ID="<your-task-id>"

dotnet run -- task show $TASK_ID
dotnet run -- task show $TASK_ID --format json
```

**验收标准**:
- [ ] ✅ 显示完整任务信息
- [ ] ✅ 包含调度配置
- [ ] ✅ 包含执行统计
- [ ] ✅ 格式清晰

#### 19.3 更新任务

```bash
# 更新调度
dotnet run -- task update $TASK_ID --schedule "0 10 * * *"

# 更新描述
dotnet run -- task update $TASK_ID --description "新的描述"

# 更新多个字段
dotnet run -- task update $TASK_ID \
  --schedule "每天10:00" \
  --description "更新后的任务"
```

**验收标准**:
- [ ] ✅ 更新成功
- [ ] ✅ 下次执行时间重新计算
- [ ] ✅ UpdatedAt 时间更新

#### 19.4 暂停和恢复任务

```bash
# 暂停任务
dotnet run -- task pause $TASK_ID

# 查看状态（应为 Paused）
dotnet run -- task show $TASK_ID

# 恢复任务
dotnet run -- task resume $TASK_ID

# 查看状态（应为 Pending）
dotnet run -- task show $TASK_ID
```

**验收标准**:
- [ ] ✅ 暂停成功
- [ ] ✅ 状态正确切换
- [ ] ✅ 暂停期间不执行
- [ ] ✅ 恢复后正常调度

#### 19.5 手动执行任务

```bash
# 立即执行任务
dotnet run -- task run $TASK_ID

# 查看执行历史
dotnet run -- task history $TASK_ID
```

**验收标准**:
- [ ] ✅ 立即执行成功
- [ ] ✅ 不影响正常调度
- [ ] ✅ 执行历史记录正确

#### 19.6 删除任务

```bash
# 删除任务
dotnet run -- task delete $TASK_ID --force
```

**验收标准**:
- [ ] ✅ 删除成功
- [ ] ✅ 列表中不再显示
- [ ] ✅ 执行历史保留（可选）

---

### 测试 20: 任务执行和历史

#### 20.1 查看执行历史

```bash
# 查看历史（默认最近 10 条）
dotnet run -- task history $TASK_ID

# 查看更多历史
dotnet run -- task history $TASK_ID --limit 50

# JSON 格式
dotnet run -- task history $TASK_ID --format json
```

**验收标准**:
- [ ] ✅ 显示所有执行记录
- [ ] ✅ 包含执行时间、状态、结果
- [ ] ✅ 按时间倒序排列
- [ ] ✅ 限制参数生效

#### 20.2 验证重试机制

```bash
# 创建一个会失败的任务
dotnet run -- task schedule "失败任务" \
  --schedule "每分钟" \
  --type custom \
  --payload '{"command":"nonexistent-command"}' \
  --max-retries 3

# 等待执行并查看历史
sleep 70
dotnet run -- task history <task-id>
```

**验收标准**:
- [ ] ✅ 任务执行失败
- [ ] ✅ 自动重试（最多 3 次）
- [ ] ✅ 重试次数记录正确
- [ ] ✅ 指数退避生效

#### 20.3 验证超时控制

```bash
# 创建一个长时间运行的任务
dotnet run -- task schedule "长任务" \
  --schedule "每分钟" \
  --type custom \
  --payload '{"command":"sleep","args":["60"]}' \
  --timeout 10

# 等待执行并查看历史
sleep 70
dotnet run -- task history <task-id>
```

**验收标准**:
- [ ] ✅ 任务超时被终止
- [ ] ✅ 状态为 Timeout
- [ ] ✅ 执行时间接近超时限制

---

### 测试 21: 后台服务和调度

#### 21.1 验证后台服务启动

```bash
# 启动应用
dotnet run

# 查看日志（应有调度器启动信息）
```

**验收标准**:
- [ ] ✅ 后台服务自动启动
- [ ] ✅ 调度器初始化成功
- [ ] ✅ 任务队列加载

#### 21.2 验证自动调度

```bash
# 创建一个即将执行的任务
dotnet run -- task schedule "即时任务" \
  --schedule "*/2 * * * *" \
  --type reminder \
  --payload '{"message":"2分钟测试"}'

# 等待执行
sleep 150

# 查看历史
dotnet run -- task history <task-id>
```

**验收标准**:
- [ ] ✅ 任务按时执行
- [ ] ✅ 执行历史正常记录
- [ ] ✅ 下次执行时间更新

#### 21.3 验证优雅关闭

```bash
# 启动应用
dotnet run

# 创建任务并观察
# 然后按 Ctrl+C 退出
```

**验收标准**:
- [ ] ✅ 接收到关闭信号
- [ ] ✅ 停止调度新任务
- [ ] ✅ 等待当前任务完成
- [ ] ✅ 清理资源

---

## 🔍 第十一部分：智能搜索和标签测试（V3.1功能）

### 测试 22: 智能搜索

#### 22.1 自然语言搜索

```bash
# 在 REPL 中
You> /search "上周关于 Python 的讨论"
You> /search "最近的技能抽取会话"
You> /search "包含文件上传的对话"
```

**验收标准**:
- [ ] ✅ 理解自然语言查询
- [ ] ✅ 返回相关会话
- [ ] ✅ 响应时间 < 100ms
- [ ] ✅ 结果按相关性排序

#### 22.2 时间范围搜索

```bash
You> /search "Python" --from "2024-01-01" --to "2024-12-31"
You> /search "技能" --days 7
```

**验收标准**:
- [ ] ✅ 时间过滤正确
- [ ] ✅ 相对时间解析正确

#### 22.3 搜索缓存

```bash
# 执行相同的搜索多次
You> /search "Python 编程"
You> /search "Python 编程"
You> /search "Python 编程"
```

**验收标准**:
- [ ] ✅ 第一次查询正常速度
- [ ] ✅ 后续查询 < 50ms（缓存命中）
- [ ] ✅ 缓存命中率 > 70%

---

### 测试 23: 智能标签

#### 23.1 添加标签

```bash
# 创建会话
SESSION_ID=$(dotnet run -- new --title "标签测试" | grep "ID:" | awk '{print $2}')

# 添加标签
You> /tag add Python
You> /tag add "Python" --emoji 🐍 --color "#FFD43B"
You> /tag add "Machine Learning" --emoji 🤖
```

**验收标准**:
- [ ] ✅ 标签创建成功
- [ ] ✅ Emoji 和颜色设置正确
- [ ] ✅ 标签与会话关联

#### 23.2 LLM 标签建议

```bash
# 在会话中进行对话
You> 我正在学习 Rust 和 WebAssembly
Assistant> ...

# 请求标签建议
You> /tag suggest
```

**验收标准**:
- [ ] ✅ LLM 分析对话内容
- [ ] ✅ 建议相关标签
- [ ] ✅ 可选择接受或拒绝
- [ ] ✅ 建议时间 < 5s

#### 23.3 标签管理

```bash
# 列出当前会话标签
You> /tag list

# 移除标签
You> /tag remove "Python"

# 查看全局标签统计
You> /tag list --all
```

**验收标准**:
- [ ] ✅ 列表显示正确
- [ ] ✅ 移除功能正常
- [ ] ✅ 全局统计准确

---

## 🖥️ 第十二部分：CLI/REPL 功能测试

### 测试 24: REPL 交互功能

#### 24.1 命令历史

```bash
# 在 REPL 中输入多个命令
You> /help
You> /list
You> /skills

# 使用上下箭头浏览历史
# 按 ↑ 键应显示 /skills
# 再按 ↑ 应显示 /list
# 按 ↓ 键应向前浏览
```

**验收标准**:
- [ ] ✅ 历史记录保存（5000 条）
- [ ] ✅ ↑↓ 键浏览正常
- [ ] ✅ 历史持久化
- [ ] ✅ 跨会话保留

#### 24.2 自动补全

```bash
# 输入部分命令后按 Tab
You> /li<Tab>      # 应补全为 /list
You> /sk<Tab>      # 应补全为 /skills
You> /mem<Tab>     # 应显示 /memory 相关命令
```

**验收标准**:
- [ ] ✅ 命令补全正常
- [ ] ✅ 参数补全正常
- [ ] ✅ 会话 ID 补全
- [ ] ✅ 技能名称补全

#### 24.3 多行输入

```bash
You> """
这是多行输入
第二行
第三行
"""
```

**验收标准**:
- [ ] ✅ 多行输入正常
- [ ] ✅ 格式保留
- [ ] ✅ 提交后处理正确

#### 24.4 命令别名

```bash
# 使用预定义别名
You> /n 新会话       # /new 的别名
You> /ls            # /list 的别名
You> /q             # /quit 的别名

# 自定义别名
You> /alias add c chat
You> /c "测试消息"
```

**验收标准**:
- [ ] ✅ 预定义别名工作
- [ ] ✅ 可以添加自定义别名
- [ ] ✅ 别名解析正确
- [ ] ✅ 别名持久化

---

### 测试 25: CLI 命令模式

#### 25.1 基本 CLI 命令

```bash
# 非交互模式命令
dotnet run -- --version
dotnet run -- --help
dotnet run -- new --title "CLI 测试"
dotnet run -- list
```

**验收标准**:
- [ ] ✅ 所有命令正常工作
- [ ] ✅ 输出格式正确
- [ ] ✅ 退出码正确（0=成功）

#### 25.2 管道和脚本

```bash
# 管道使用
dotnet run -- list | grep "测试"

# 脚本使用
cat > test-script.sh <<'EOF'
#!/bin/bash
SESSION=$(dotnet run -- new --title "脚本会话" | grep "ID:" | awk '{print $2}')
dotnet run -- chat $SESSION "Hello from script"
dotnet run -- show $SESSION
EOF

chmod +x test-script.sh
./test-script.sh
```

**验收标准**:
- [ ] ✅ 管道输出正常
- [ ] ✅ 脚本自动化工作
- [ ] ✅ 退出码可靠

---

## 📊 第十三部分：性能和压力测试

### 测试 26: 性能基准

#### 26.1 启动时间

```bash
# 测试 10 次取平均
for i in {1..10}; do
  time dotnet run -- --help > /dev/null
done
```

**验收标准**:
- [ ] ✅ 平均启动时间 < 3 秒
- [ ] ✅ 标准差小

#### 26.2 命令响应时间

```bash
# 测试各种命令
time dotnet run -- list
time dotnet run -- task list
time dotnet run -- skills
```

**验收标准**:
- [ ] ✅ list: < 1 秒
- [ ] ✅ task list: < 1 秒
- [ ] ✅ skills: < 500ms

#### 26.3 大量数据测试

```bash
# 创建大量会话
for i in {1..100}; do
  dotnet run -- new --title "测试会话 $i"
done

# 测试列表性能
time dotnet run -- list
```

**验收标准**:
- [ ] ✅ 列表时间 < 2 秒
- [ ] ✅ 分页功能正常
- [ ] ✅ 内存使用合理

#### 26.4 并发测试

```bash
# 并发创建会话
for i in {1..10}; do
  dotnet run -- new --title "并发测试 $i" &
done
wait

# 检查结果
dotnet run -- list | grep "并发测试" | wc -l
```

**验收标准**:
- [ ] ✅ 所有会话创建成功
- [ ] ✅ 数据一致性
- [ ] ✅ 无竞态条件

---

## 🧪 第十四部分：集成测试

### 测试 27: 端到端场景

#### 场景 1: 完整的文件处理工作流

```bash
# 1. 创建会话
SESSION_ID=$(dotnet run -- new --title "文件处理工作流" | grep "ID:" | awk '{print $2}')

# 2. 上传文件
echo "function hello() { console.log('hello'); }" > code.js
FILE_ID=$(dotnet run -- chat $SESSION_ID "/file upload code.js" | grep "ID:" | awk '{print $2}')

# 3. 在对话中引用并分析
dotnet run -- chat $SESSION_ID "@file:code.js 请分析这段代码并提供改进建议"

# 4. 根据分析结果抽取技能
dotnet run -- chat $SESSION_ID "/skill extract $SESSION_ID"

# 5. 创建计划任务定期检查代码
dotnet run -- task schedule "代码检查" \
  --schedule "每天9:00" \
  --type skill \
  --payload "{\"skill\":\"code-review\",\"args\":{\"file\":\"$FILE_ID\"}}"

# 6. 保存到记忆
dotnet run -- chat $SESSION_ID "/memory add project code_review_workflow"
```

**验收标准**:
- [ ] ✅ 所有步骤无错误
- [ ] ✅ 数据流转正确
- [ ] ✅ 功能集成良好

#### 场景 2: 跨会话协作

```bash
# 用户 A: 创建并共享文件
SESSION_A=$(dotnet run -- new --title "用户A会话" | grep "ID:" | awk '{print $2}')
FILE_ID=$(dotnet run -- chat $SESSION_A "/file upload shared-doc.txt --access-level shared" | grep "ID:" | awk '{print $2}')
dotnet run -- chat $SESSION_A "/file share $FILE_ID --user user_b --permission read"

# 用户 B: 访问共享文件
SESSION_B=$(dotnet run -- new --title "用户B会话" | grep "ID:" | awk '{print $2}')
dotnet run -- chat $SESSION_B "@file:$FILE_ID 请总结这个文档"
```

**验收标准**:
- [ ] ✅ 文件共享成功
- [ ] ✅ 权限验证正确
- [ ] ✅ 用户 B 可以访问
- [ ] ✅ 内容引用正常

---

## ✅ 第十五部分：验收清单

### 核心系统 (15/15)

- [ ] 编译和构建无错误
- [ ] 数据库自动迁移
- [ ] 应用启动时间 < 3 秒
- [ ] REPL 启动时间 < 200ms
- [ ] 会话创建和管理
- [ ] 消息发送和历史
- [ ] 命令历史记录
- [ ] 自动补全功能
- [ ] 多行输入
- [ ] 命令别名
- [ ] LLM 集成（Anthropic）
- [ ] LLM 集成（Ollama）
- [ ] 流式响应
- [ ] 错误处理
- [ ] 日志输出

### 技能系统 (8/8)

- [ ] 技能定义（YAML + Markdown）
- [ ] 技能加载和解析
- [ ] 命名空间管理
- [ ] 参数验证
- [ ] @ 语法调用
- [ ] / 命令调用
- [ ] LLM 工具调用集成
- [ ] 技能列表和详情

### 长期记忆系统 (12/12)

- [ ] 记忆 CRUD 操作
- [ ] 五种记忆类型
- [ ] 关键词搜索
- [ ] 语义搜索（Qdrant）
- [ ] 混合搜索
- [ ] LLM 驱动提取
- [ ] 记忆相关性评分
- [ ] 自动降级策略
- [ ] Embedding 向量化
- [ ] 向量数据库集成
- [ ] 批量操作性能
- [ ] 缓存优化

### 上下文压缩系统 (6/6)

- [ ] Sliding Window 策略
- [ ] Semantic 策略
- [ ] Hierarchical 策略
- [ ] 自动策略选择
- [ ] Token 统计
- [ ] 自动触发机制

### 文件上传系统 (16/16)

- [ ] 文件上传和存储
- [ ] 文件列表和详情
- [ ] 文件内容查看
- [ ] 文件删除
- [ ] 20+ 文件类型支持
- [ ] 对话中引用（@file:）
- [ ] 多文件引用
- [ ] 三级访问控制
- [ ] 权限管理（授予/撤销）
- [ ] 版本控制
- [ ] 版本历史
- [ ] 版本恢复
- [ ] 全局文件库
- [ ] 文件搜索
- [ ] 跨会话访问
- [ ] CLI 命令集成

### 技能抽取系统 (9/9)

- [ ] 对话模式识别
- [ ] LLM 驱动生成
- [ ] 参数自动提取
- [ ] 交互式编辑
- [ ] 技能验证
- [ ] 命名空间管理
- [ ] 抽取历史记录
- [ ] 统计信息
- [ ] 缓存优化

### 计划任务系统 (15/15)

- [ ] Cron 表达式解析
- [ ] 自然语言解析
- [ ] 三种任务类型
- [ ] 任务创建和列表
- [ ] 任务详情和更新
- [ ] 任务暂停/恢复
- [ ] 任务手动执行
- [ ] 任务删除
- [ ] 执行历史记录
- [ ] 重试机制
- [ ] 超时控制
- [ ] 后台服务调度
- [ ] 优雅关闭
- [ ] 时间范围限制
- [ ] CLI 命令集成

### 智能搜索和标签 (V3.1) (8/8)

- [ ] 自然语言搜索
- [ ] 多字段检索
- [ ] 时间范围过滤
- [ ] LRU 查询缓存
- [ ] 添加标签
- [ ] Emoji 和颜色支持
- [ ] LLM 标签建议
- [ ] 全局标签统计

---

## 📝 验收报告模板

```markdown
# General Agent V3 验收测试报告

**测试日期**: YYYY-MM-DD
**测试人员**: [姓名]
**版本**: V3.2.0

## 测试环境

- 操作系统: [macOS 14.x / Ubuntu 22.04 / Windows 11]
- .NET SDK: [版本号]
- 数据库: SQLite
- 可选服务: [Ollama / Qdrant]

## 测试结果总览

- 总测试项: 109
- 通过项: [数量]
- 失败项: [数量]
- 跳过项: [数量]
- 通过率: [百分比]%

## 自动化测试

- 单元测试: 586/586 ✅
- 文件存储测试: 111/111 ✅
- 技能抽取测试: 56/56 ✅
- 集成测试: 111/111 ✅
- 总计: 864/864 ✅

## 功能测试详情

### 核心系统: [15/15] ✅
### 技能系统: [8/8] ✅
### 长期记忆: [12/12] ✅
### 上下文压缩: [6/6] ✅
### 文件上传: [16/16] ✅
### 技能抽取: [9/9] ✅
### 计划任务: [15/15] ✅
### 智能搜索和标签: [8/8] ✅

## 性能测试

- 启动时间: [X]ms (目标: <200ms)
- 命令响应: [X]ms (目标: <50ms)
- 语义搜索: [X]ms (目标: <100ms)
- 任务调度延迟: [X]ms (目标: <1s)

## 已知问题

[列出发现的问题]

## 结论

- [ ] ✅ 全部功能正常，可以发布
- [ ] ⚠️ 有小问题但不影响使用
- [ ] ❌ 有严重问题需要修复

## 签字

测试人员: ________________
日期: ________________
```

---

## 📞 获取帮助

如果验收测试遇到问题：

1. **查看日志**: 检查控制台输出和错误信息
2. **查看文档**: 
   - [用户指南](docs/guides/CLI_GUIDE.md)
   - [命令参考](docs/guides/CLI_REFERENCE.md)
   - [故障排除](../CLAUDE.md#常见问题)
3. **运行快速测试**: `./quick-test.sh`
4. **提交 Issue**: [GitHub Issues](https://github.com/nothingbut/general-agent/issues)
5. **邮件联系**: shi.chang@163.com

---

**验收测试完成！** 🎉

如果所有测试通过，恭喜您，General Agent V3 的**全部功能**已验证就绪，可以投入使用。
