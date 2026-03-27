# General Agent V3 - 高级功能规划

**创建日期**: 2026-03-26
**状态**: 📝 规划中
**优先级**: Phase 6-7 候选功能

---

## 📋 功能清单

根据讨论，以下 5 个高级功能被识别为重要的增强方向：

1. **长期记忆系统** - 持久化上下文和用户偏好
2. **上下文压缩** - 智能压缩对话历史以节省 token
3. **文件上传支持** - 支持文档、图片等文件处理
4. **对话中抽取 Skill** - 自动从对话中生成技能
5. **计划任务** - 定时执行任务和提醒

---

## 1️⃣ 长期记忆系统

### 概述
提供持久化的记忆能力，让 Agent 能够记住用户的偏好、历史交互和重要信息。

### 核心功能
- **用户档案**: 存储用户偏好、工作习惯、常用技能
- **知识库**: 持久化重要信息和事实
- **上下文恢复**: 跨会话恢复上下文
- **记忆检索**: 基于相似度的记忆检索

### 技术方案

#### 数据模型
```sql
-- 长期记忆表
CREATE TABLE long_term_memory (
    id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL,
    memory_type TEXT NOT NULL,  -- 'preference', 'fact', 'context'
    content TEXT NOT NULL,
    embedding BLOB,              -- 向量嵌入
    importance REAL DEFAULT 0.5, -- 重要性评分 (0-1)
    access_count INTEGER DEFAULT 0,
    last_accessed TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMP         -- 可选的过期时间
);

-- 记忆标签
CREATE TABLE memory_tags (
    memory_id TEXT NOT NULL,
    tag TEXT NOT NULL,
    PRIMARY KEY (memory_id, tag),
    FOREIGN KEY (memory_id) REFERENCES long_term_memory(id)
);

-- 记忆关联
CREATE TABLE memory_relations (
    from_memory_id TEXT NOT NULL,
    to_memory_id TEXT NOT NULL,
    relation_type TEXT NOT NULL, -- 'related', 'contradicts', 'updates'
    strength REAL DEFAULT 0.5,
    PRIMARY KEY (from_memory_id, to_memory_id)
);
```

#### 核心服务
```csharp
public class LongTermMemoryService
{
    // 存储记忆
    Task<string> StoreMemory(
        string content,
        MemoryType type,
        float importance = 0.5f,
        List<string>? tags = null
    );

    // 检索记忆
    Task<List<Memory>> RetrieveMemories(
        string query,
        int limit = 10,
        float? minImportance = null
    );

    // 更新记忆重要性
    Task UpdateImportance(string memoryId, float newImportance);

    // 遗忘（删除或标记过期）
    Task ForgetMemory(string memoryId);

    // 巩固记忆（周期性任务）
    Task ConsolidateMemories();
}
```

### CLI 命令
```bash
# 查看记忆
/memory list                      # 列出所有记忆
/memory search <query>            # 搜索记忆
/memory show <id>                 # 查看记忆详情

# 管理记忆
/memory add <content>             # 添加记忆
/memory update <id> <content>     # 更新记忆
/memory forget <id>               # 删除记忆
/memory tag <id> <tag>            # 添加标签

# 记忆统计
/memory stats                     # 记忆统计
/memory export <file>             # 导出记忆
```

### 实现优先级
- **P0**: 基础记忆存储和检索
- **P1**: 向量嵌入和相似度搜索
- **P2**: 记忆巩固和遗忘机制
- **P3**: 记忆关联和推理

---

## 2️⃣ 上下文压缩

### 概述
智能压缩对话历史，在保留关键信息的同时减少 token 使用。

### 核心功能
- **自动摘要**: 对长对话进行摘要
- **关键信息提取**: 提取并保留重要信息
- **分层压缩**: 近期消息详细，远期消息摘要
- **压缩策略**: 可配置的压缩策略

### 技术方案

#### 压缩策略
```csharp
public enum CompressionStrategy
{
    None,           // 不压缩
    Sliding,        // 滑动窗口（保留最近 N 条）
    Hierarchical,   // 分层压缩（近详远简）
    Semantic,       // 语义压缩（保留关键信息）
    Adaptive        // 自适应（根据 token 预算）
}

public class ContextCompressionService
{
    // 压缩上下文
    Task<CompressedContext> CompressContext(
        List<Message> messages,
        CompressionStrategy strategy,
        int targetTokens
    );

    // 估算 token 数
    int EstimateTokens(List<Message> messages);

    // 生成摘要
    Task<string> SummarizeMessages(List<Message> messages);

    // 提取关键信息
    Task<List<KeyPoint>> ExtractKeyPoints(List<Message> messages);
}
```

#### 压缩配置
```json
{
  "compression": {
    "enabled": true,
    "strategy": "hierarchical",
    "max_tokens": 4000,
    "preserve_recent": 10,      // 保留最近 10 条完整消息
    "summarize_threshold": 50,   // 超过 50 条开始摘要
    "key_points_limit": 20       // 最多保留 20 个关键点
  }
}
```

### CLI 命令
```bash
# 查看压缩状态
/context status                   # 当前上下文状态
/context tokens                   # Token 使用情况

# 手动压缩
/context compress                 # 压缩当前会话
/context summarize                # 生成摘要

# 配置压缩
/context config strategy <name>   # 设置压缩策略
/context config max-tokens <n>    # 设置 token 限制
```

### 实现优先级
- **P0**: 滑动窗口策略
- **P1**: 分层压缩
- **P2**: 语义压缩（使用 LLM）
- **P3**: 自适应压缩

---

## 3️⃣ 文件上传支持

### 概述
支持上传和处理各种文件类型（文档、图片、代码等）。

### 核心功能
- **文件上传**: 支持拖拽或命令上传
- **文件解析**: 自动解析常见格式
- **内容提取**: 提取文本、代码、图片等
- **文件管理**: 查看、删除、导出已上传文件

### 支持的文件类型

#### 文本类
- Markdown (.md)
- 代码文件 (.py, .cs, .js, .ts, .go, .rs, etc.)
- 纯文本 (.txt)
- JSON (.json)
- YAML (.yaml, .yml)

#### 文档类
- PDF (.pdf)
- Word (.docx)
- Excel (.xlsx)
- PowerPoint (.pptx)

#### 图片类
- PNG, JPEG, GIF, WebP
- SVG

### 技术方案

#### 文件存储
```sql
CREATE TABLE uploaded_files (
    id TEXT PRIMARY KEY,
    session_id TEXT NOT NULL,
    file_name TEXT NOT NULL,
    file_type TEXT NOT NULL,     -- 'text', 'document', 'image', 'code'
    mime_type TEXT NOT NULL,
    file_size INTEGER NOT NULL,
    content_hash TEXT NOT NULL,  -- SHA256
    storage_path TEXT NOT NULL,
    extracted_text TEXT,         -- 提取的文本内容
    metadata TEXT,               -- JSON 元数据
    uploaded_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (session_id) REFERENCES sessions(id)
);
```

#### 文件服务
```csharp
public class FileUploadService
{
    // 上传文件
    Task<UploadedFile> UploadFile(
        string sessionId,
        Stream fileStream,
        string fileName,
        string mimeType
    );

    // 解析文件
    Task<ParsedContent> ParseFile(string fileId);

    // 提取文本
    Task<string> ExtractText(string fileId);

    // 列出文件
    Task<List<UploadedFile>> ListFiles(string sessionId);

    // 删除文件
    Task DeleteFile(string fileId);
}
```

### CLI 命令
```bash
# 上传文件
/upload <file-path>               # 上传文件
/upload <file-path> --extract     # 上传并提取内容

# 管理文件
/files list                       # 列出已上传文件
/files show <id>                  # 查看文件详情
/files delete <id>                # 删除文件
/files export <id> <output>       # 导出文件

# 文件分析
/analyze <file-id>                # 分析文件内容
/summarize <file-id>              # 总结文件内容
```

### 实现优先级
- **P0**: 文本和代码文件支持
- **P1**: PDF 和 Markdown 支持
- **P2**: 图片支持（需要多模态模型）
- **P3**: Office 文档支持

---

## 4️⃣ 对话中抽取 Skill

### 概述
从用户与 Agent 的对话中自动识别和生成可复用的技能。

### 核心功能
- **模式识别**: 识别重复的任务模式
- **自动生成**: 生成技能定义和模板
- **参数提取**: 自动识别参数
- **技能建议**: 建议可复用的技能

### 技术方案

#### 模式检测
```csharp
public class SkillExtractionService
{
    // 分析对话，识别模式
    Task<List<SkillPattern>> AnalyzeConversation(
        List<Message> messages
    );

    // 生成技能定义
    Task<SkillDefinition> GenerateSkill(
        SkillPattern pattern,
        string skillName
    );

    // 提取参数
    List<SkillParameter> ExtractParameters(
        List<Message> examples
    );

    // 建议技能名称
    Task<List<string>> SuggestSkillNames(
        SkillPattern pattern
    );
}
```

#### 技能模式
```csharp
public class SkillPattern
{
    public string Description { get; set; }
    public List<Message> Examples { get; set; }
    public List<string> Keywords { get; set; }
    public float Confidence { get; set; }
    public int Frequency { get; set; }
}
```

### 工作流程

1. **检测阶段**: 
   - 监控对话，识别重复模式
   - 计算模式频率和置信度
   
2. **建议阶段**:
   - 当模式达到阈值时，建议创建技能
   - 显示示例和参数

3. **生成阶段**:
   - 用户确认后，生成技能定义
   - 自动创建 YAML + Markdown 文件

4. **测试阶段**:
   - 在当前会话中测试新技能
   - 收集反馈并优化

### CLI 命令
```bash
# 分析对话
/skill analyze                    # 分析当前会话
/skill patterns                   # 查看识别的模式

# 创建技能
/skill create <pattern-id>        # 从模式创建技能
/skill create custom              # 手动创建技能

# 管理建议
/skill suggestions                # 查看技能建议
/skill accept <id>                # 接受建议
/skill reject <id>                # 拒绝建议
```

### 实现优先级
- **P1**: 基础模式识别
- **P1**: 技能生成和保存
- **P2**: 参数自动提取
- **P3**: 持续学习和优化

---

## 5️⃣ 计划任务

### 概述
支持定时执行任务、设置提醒和自动化工作流。

### 核心功能
- **定时任务**: Cron 表达式支持
- **提醒系统**: 到期通知
- **循环任务**: 周期性执行
- **任务历史**: 执行记录和结果

### 技术方案

#### 数据模型
```sql
CREATE TABLE scheduled_tasks (
    id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL,
    task_type TEXT NOT NULL,     -- 'reminder', 'cron', 'once'
    description TEXT NOT NULL,
    command TEXT,                -- 要执行的命令或技能
    schedule TEXT NOT NULL,      -- Cron 表达式或时间戳
    enabled BOOLEAN DEFAULT TRUE,
    last_run TIMESTAMP,
    next_run TIMESTAMP,
    run_count INTEGER DEFAULT 0,
    max_runs INTEGER,            -- 最大执行次数
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE task_executions (
    id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    started_at TIMESTAMP NOT NULL,
    completed_at TIMESTAMP,
    status TEXT NOT NULL,        -- 'success', 'failed', 'cancelled'
    result TEXT,
    error TEXT,
    FOREIGN KEY (task_id) REFERENCES scheduled_tasks(id)
);
```

#### 任务调度器
```csharp
public class TaskSchedulerService
{
    // 创建任务
    Task<string> CreateTask(
        string description,
        string command,
        string schedule,
        TaskType type
    );

    // 列出任务
    Task<List<ScheduledTask>> ListTasks();

    // 启用/禁用任务
    Task EnableTask(string taskId, bool enabled);

    // 执行任务
    Task<TaskExecutionResult> ExecuteTask(string taskId);

    // 删除任务
    Task DeleteTask(string taskId);
}
```

### CLI 命令
```bash
# 创建任务
/schedule add "每天提醒备份" --cron "0 9 * * *"
/schedule add "一次性提醒" --at "2026-03-27 14:00"
/schedule add "周报提醒" --weekly monday 9:00

# 管理任务
/schedule list                    # 列出所有任务
/schedule show <id>               # 查看任务详情
/schedule enable <id>             # 启用任务
/schedule disable <id>            # 禁用任务
/schedule delete <id>             # 删除任务

# 执行历史
/schedule history <id>            # 查看执行历史
/schedule run <id>                # 立即执行任务
```

### 支持的调度类型

1. **一次性任务**: 指定时间执行一次
   ```bash
   /schedule add "会议提醒" --at "2026-03-27 14:00"
   ```

2. **Cron 任务**: 使用 Cron 表达式
   ```bash
   /schedule add "每日备份" --cron "0 9 * * *"
   ```

3. **简化表达式**: 自然语言
   ```bash
   /schedule add "周报" --every monday 9:00
   /schedule add "月报" --every "1st day" 9:00
   ```

### 实现优先级
- **P1**: 基础一次性提醒
- **P1**: Cron 表达式支持
- **P2**: 任务执行和历史
- **P3**: 高级调度和依赖

---

## 📈 实施计划

### Phase 6 候选 (3-4 周)
- ✅ 文件上传支持 (P0 - 文本和代码)
- ✅ 上下文压缩 (P0 - 滑动窗口)
- ⏸ 计划任务 (P1 - 基础提醒)

### Phase 7 候选 (4-5 周)
- ✅ 长期记忆系统 (P0 - 基础存储)
- ✅ 上下文压缩 (P1 - 分层压缩)
- ✅ 对话中抽取 Skill (P1 - 模式识别)

### Phase 8+ (长期)
- 完善所有 P2-P3 功能
- 集成和优化
- 性能调优

---

## 💡 讨论要点

### 优先级排序建议

**高优先级** (建议先实现):
1. **上下文压缩** - 解决 token 成本问题，实用性强
2. **文件上传支持** - 扩展使用场景，用户需求大

**中优先级**:
3. **计划任务** - 提升自动化能力
4. **长期记忆系统** - 改善用户体验

**低优先级** (可后续实现):
5. **对话中抽取 Skill** - 需要成熟的模式识别，复杂度高

### 技术难点

1. **上下文压缩**: 
   - 如何保留关键信息？
   - 语义压缩需要额外的 LLM 调用

2. **文件上传**:
   - 文件存储位置和大小限制
   - 多模态模型支持（图片）

3. **长期记忆**:
   - 向量嵌入和相似度搜索
   - 记忆重要性评估

4. **Skill 抽取**:
   - 模式识别准确性
   - 参数自动提取

5. **计划任务**:
   - 后台服务/守护进程
   - 跨平台通知

---

## 🤔 待讨论问题

1. **功能优先级**: 你认为哪些功能最重要？
2. **技术方案**: 对提出的技术方案有什么建议？
3. **用户体验**: 命令行界面设计是否合理？
4. **实施时间**: 预计的时间线是否合理？
5. **其他需求**: 是否有遗漏的重要功能？

---

**文档创建**: 2026-03-26
**创建者**: Claude Sonnet 4.5
**状态**: 📝 等待讨论和确认
