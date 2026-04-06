# V3 对话抽取 Skill 功能设计文档

**创建时间**: 2026-04-06
**优先级**: 高 ⭐⭐⭐⭐
**预计耗时**: 2-3 周
**复杂度**: 中-高

---

## 🎯 功能目标

实现从对话历史中自动识别重复性任务模式，并生成可复用的技能（Skill）定义。用户可以确认、编辑并立即使用生成的技能，逐步建立个性化的技能库。

---

## 💡 核心价值

### 1. 自动化重复任务

**场景示例**:
```
用户: 帮我查看这个 API 的文档，然后生成示例代码
Agent: [完成任务]

用户: 帮我查看那个 API 的文档，然后生成示例代码
Agent: [完成任务]

Agent: 💡 我注意到你经常需要"查API文档+生成示例"，
      是否要创建一个 api-helper 技能？
用户: 好的
Agent: ✓ 已创建技能 dev:api-helper
      使用方法: @dev:api-helper api="user-login"
```

### 2. 知识沉淀

- 将一次性任务转化为可复用的技能
- 形成个人/团队技能库
- 技能可以分享和传播

### 3. 降低认知负荷

- 不需要记忆复杂的提示词
- 简化重复性操作
- 标准化工作流程

---

## 📋 需求分析

### 功能需求

#### FR1: 模式识别

**需求**: 从对话历史中识别重复性任务模式

**输入**:
- 对话历史（最近 N 条消息）
- 用户消息和 Agent 回复

**输出**:
- 识别到的任务模式
- 任务步骤分解
- 任务参数提取
- 置信度评分（0.0-1.0）

**识别标准**:
1. **重复性**: 相似的任务至少出现 2-3 次
2. **结构化**: 任务有明确的输入和输出
3. **参数化**: 任务可以抽象为模板 + 参数
4. **价值**: 任务足够复杂，值得创建技能

#### FR2: 技能生成

**需求**: 自动生成技能定义文件（YAML + Markdown）

**输入**:
- 识别到的任务模式
- 用户确认和修改

**输出**:
- 技能名称（符合命名规范）
- 技能描述（清晰简洁）
- 参数定义（类型、必需性、默认值）
- 提示词模板（Scriban 语法）
- 建议的命名空间

**生成规则**:
1. **名称**: 小写字母、连字符，描述性（如 `api-helper`）
2. **命名空间**: 根据任务类型建议（如 `dev`, `productivity`）
3. **参数**: 自动提取变化的部分
4. **模板**: 基于实际对话生成，保留关键步骤

#### FR3: 用户交互

**需求**: 友好的用户交互流程

**场景 1: 自动建议**
```
[用户完成第 3 次类似任务后]
Agent: 💡 技能建议

我注意到你经常执行类似的任务：
  1. 查看 API 文档
  2. 生成示例代码
  3. 解释使用方法

建议创建技能: dev:api-helper
参数:
  - api (必需): API 名称
  - language (可选): 编程语言，默认 C#

是否创建此技能? [Y/n/e(编辑)]
```

**场景 2: 手动触发**
```
用户: /skill extract

Agent: 正在分析对话历史...

找到 2 个潜在的技能模式：
  1. API 文档助手 (置信度: 0.85) [推荐]
  2. 代码审查助手 (置信度: 0.72)

选择要创建的技能 [1/2/0(取消)]:
```

**场景 3: 交互式编辑**
```
用户: e (选择编辑)

Agent: 技能定义预览:

---
name: api-helper
description: 查看 API 文档并生成示例代码
parameters:
  - name: api
    type: string
    required: true
    description: API 名称
---

[查看 {{api}} 的文档，生成 C# 示例代码，并解释用法]

修改字段 [name/desc/params/template/save/cancel]:
```

#### FR4: 技能管理

**需求**: 查看、编辑、删除提取的技能

```bash
# 查看提取历史
/skill extraction-history

# 编辑已生成的技能
/skill edit dev:api-helper

# 删除技能
/skill delete dev:api-helper
```

### 非功能需求

#### NFR1: 性能

- 模式识别耗时 < 5 秒
- 技能生成耗时 < 3 秒
- 不影响正常对话流程

#### NFR2: 准确性

- 误报率 < 20%（不建议不合适的技能）
- 漏报率 < 30%（不遗漏明显的模式）
- 生成的技能模板可用性 > 80%

#### NFR3: 用户体验

- 建议时机恰当（不打扰用户）
- 交互流程简洁清晰
- 错误处理友好

---

## 🏗️ 技术方案

### 整体架构

```
用户对话
    ↓
对话历史分析 (后台/手动触发)
    ↓
模式识别 (SkillExtractionService)
    ↓
技能生成 (SkillGenerator)
    ↓
用户确认/编辑 (InteractiveEditor)
    ↓
技能保存 (SkillWriter)
    ↓
技能注册 (SkillRegistry)
    ↓
立即可用
```

### 核心组件

#### 1. SkillExtractionService

**职责**: 从对话历史中识别技能模式

**接口**:
```csharp
public interface ISkillExtractionService
{
    /// <summary>
    /// 从会话中提取技能建议
    /// </summary>
    Task<List<SkillSuggestion>> ExtractFromSessionAsync(
        string sessionId,
        int lookbackMessages = 50,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 从消息列表中提取技能建议
    /// </summary>
    Task<List<SkillSuggestion>> ExtractFromMessagesAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default);
}

public sealed record SkillSuggestion
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Namespace { get; init; }
    public required string Template { get; init; }
    public required List<SkillParameter> Parameters { get; init; }
    public required double Confidence { get; init; }
    public required string Rationale { get; init; }
    public int Occurrences { get; init; }
    public List<string> ExampleMessages { get; init; } = new();
}
```

**实现策略**:

1. **第一阶段（MVP）**: LLM 驱动的模式识别
   - 使用 LLM 分析对话历史
   - 提取任务模式和参数
   - 生成技能定义建议

2. **第二阶段（优化）**: 混合方法
   - 统计分析：识别重复的短语和结构
   - 语义分析：使用 Embedding 识别相似任务
   - LLM 精炼：生成最终的技能定义

**LLM 提示词设计**:
```
你是一个技能提取助手。分析对话历史，识别重复性任务模式。

识别标准：
1. 任务至少出现 2-3 次
2. 任务有明确的输入输出
3. 任务可以参数化
4. 任务足够复杂，值得创建技能

输出格式（JSON）：
{
  "suggestions": [
    {
      "name": "skill-name",
      "namespace": "category",
      "description": "简短描述",
      "template": "提示词模板（使用 {{param}} 占位符）",
      "parameters": [
        {
          "name": "param1",
          "type": "string",
          "required": true,
          "description": "参数说明"
        }
      ],
      "confidence": 0.85,
      "rationale": "识别原因",
      "occurrences": 3,
      "exampleMessages": ["示例1", "示例2"]
    }
  ]
}
```

#### 2. SkillGenerator

**职责**: 生成技能定义文件

**接口**:
```csharp
public interface ISkillGenerator
{
    /// <summary>
    /// 生成技能定义（YAML + Markdown）
    /// </summary>
    Task<string> GenerateSkillFileAsync(
        SkillSuggestion suggestion,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 验证技能定义
    /// </summary>
    Task<ValidationResult> ValidateSkillAsync(
        string skillContent,
        CancellationToken cancellationToken = default);
}
```

**生成逻辑**:
1. 构建 YAML frontmatter
2. 生成 Markdown 模板
3. 格式化和美化
4. 验证语法正确性

**生成示例**:
```markdown
---
name: api-helper
description: 查看 API 文档并生成示例代码
parameters:
  - name: api
    type: string
    required: true
    description: API 名称
  - name: language
    type: string
    required: false
    description: 编程语言
    default_value: C#
---

请帮我完成以下任务：

1. 查看 {{api}} 的 API 文档
2. 生成 {{language}} 的示例代码
3. 解释如何使用这个 API
4. 列出常见的注意事项

请提供完整、可运行的代码示例。
```

#### 3. InteractiveEditor

**职责**: 提供交互式编辑界面

**接口**:
```csharp
public interface IInteractiveEditor
{
    /// <summary>
    /// 显示技能建议并获取用户确认
    /// </summary>
    Task<EditResult> PromptUserAsync(
        SkillSuggestion suggestion,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 交互式编辑技能定义
    /// </summary>
    Task<string> EditSkillAsync(
        string initialContent,
        CancellationToken cancellationToken = default);
}

public enum EditAction
{
    Accept,      // 直接接受
    Edit,        // 编辑后接受
    Reject       // 拒绝
}

public sealed record EditResult
{
    public EditAction Action { get; init; }
    public string? EditedContent { get; init; }
    public string? RejectionReason { get; init; }
}
```

**交互流程**:
1. 显示建议摘要
2. 等待用户选择（Y/n/e）
3. 如果选择编辑，进入编辑模式
4. 实时验证修改
5. 确认保存

#### 4. SkillWriter

**职责**: 将技能保存到文件系统

**接口**:
```csharp
public interface ISkillWriter
{
    /// <summary>
    /// 保存技能到文件系统
    /// </summary>
    Task<string> SaveSkillAsync(
        string @namespace,
        string name,
        string content,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新现有技能
    /// </summary>
    Task UpdateSkillAsync(
        string skillPath,
        string content,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 删除技能
    /// </summary>
    Task<bool> DeleteSkillAsync(
        string skillPath,
        CancellationToken cancellationToken = default);
}
```

**文件路径规则**:
```
skills/
├── {namespace}/
│   └── {name}.md
```

例如：`skills/dev/api-helper.md`

#### 5. ExtractionHistoryService

**职责**: 记录提取历史

**接口**:
```csharp
public interface IExtractionHistoryService
{
    /// <summary>
    /// 记录提取事件
    /// </summary>
    Task RecordExtractionAsync(
        SkillSuggestion suggestion,
        EditAction action,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取提取历史
    /// </summary>
    Task<List<ExtractionRecord>> GetHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);
}

public sealed record ExtractionRecord
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string SkillName { get; init; }
    public EditAction Action { get; init; }
    public double Confidence { get; init; }
    public string? RejectionReason { get; init; }
}
```

**存储**: SQLite 表 `skill_extraction_history`

---

## 📊 数据模型

### 数据库表

```sql
CREATE TABLE skill_extraction_history (
    id TEXT PRIMARY KEY,                -- GUID
    timestamp TEXT NOT NULL,             -- ISO 8601
    session_id TEXT,                     -- 关联会话
    skill_name TEXT NOT NULL,            -- 技能名称
    skill_namespace TEXT NOT NULL,       -- 命名空间
    action TEXT NOT NULL,                -- Accept/Edit/Reject
    confidence REAL NOT NULL,            -- 置信度
    occurrences INTEGER NOT NULL,        -- 出现次数
    rejection_reason TEXT,               -- 拒绝原因
    metadata TEXT                        -- JSON 元数据
);

CREATE INDEX idx_skill_extraction_timestamp 
    ON skill_extraction_history(timestamp DESC);
CREATE INDEX idx_skill_extraction_action 
    ON skill_extraction_history(action);
```

---

## 🔄 工作流程

### 流程 1: 自动建议

```
1. 用户正常对话
   ↓
2. 后台分析最近 N 条消息
   ↓
3. 识别到重复模式（置信度 > 0.8）
   ↓
4. 在对话中插入建议
   "💡 我注意到你经常..."
   ↓
5. 等待用户响应
   ↓
6a. 接受 → 生成技能 → 保存 → 通知成功
6b. 编辑 → 交互式编辑 → 保存
6c. 拒绝 → 记录拒绝原因 → 继续对话
```

### 流程 2: 手动触发

```
1. 用户输入 /skill extract
   ↓
2. 分析当前会话历史
   ↓
3. 显示所有识别到的模式（按置信度排序）
   ↓
4. 用户选择要创建的技能
   ↓
5. 进入编辑确认流程
   ↓
6. 保存技能
```

### 流程 3: 查看历史

```
1. 用户输入 /skill extraction-history
   ↓
2. 显示最近的提取记录
   - 已接受的技能
   - 已编辑的技能
   - 已拒绝的建议
   ↓
3. 用户可以重新编辑或删除
```

---

## 🧪 测试策略

### 单元测试

- `SkillExtractionServiceTests` - 模式识别逻辑
- `SkillGeneratorTests` - 技能生成
- `InteractiveEditorTests` - 交互逻辑
- `SkillWriterTests` - 文件 I/O
- `ExtractionHistoryServiceTests` - 历史记录

### 集成测试

- 完整的提取-生成-保存流程
- LLM 集成测试
- 文件系统操作测试

### E2E 测试

- 自动建议场景
- 手动提取场景
- 编辑和拒绝场景

### 覆盖率目标

- 单元测试覆盖率 > 80%
- 集成测试覆盖主要流程
- 至少 3 个 E2E 场景

---

## 📝 REPL 命令设计

```bash
# 手动触发提取
/skill extract                           # 从当前会话提取
/skill extract --session <id>           # 从指定会话提取
/skill extract --messages 100           # 分析最近 100 条消息

# 查看提取历史
/skill extraction-history                # 最近 50 条
/skill extraction-history --all          # 全部历史
/skill extraction-history --accepted     # 只显示已接受的

# 编辑生成的技能
/skill edit <name>                       # 编辑技能
/skill show <name> --definition          # 查看技能定义

# 删除技能
/skill delete <name>                     # 删除技能
```

---

## 🚀 实施计划

### Phase 1: 基础架构（3-4 天）

1. 创建项目：`GeneralAgent.Infrastructure.SkillExtraction`
2. 定义核心接口和模型
3. 实现 `SkillExtractionService`（基础版）
4. 实现 `SkillGenerator`
5. 单元测试

### Phase 2: 技能生成和保存（2-3 天）

1. 实现 `SkillWriter`
2. 实现文件命名和路径逻辑
3. 集成到技能注册系统
4. 验证生成的技能可立即使用
5. 单元测试

### Phase 3: 用户交互（2-3 天）

1. 实现 `InteractiveEditor`
2. 设计交互流程和 UI
3. 实现 `/skill extract` 命令
4. 实现自动建议逻辑
5. E2E 测试

### Phase 4: 历史记录和管理（1-2 天）

1. 实现 `ExtractionHistoryService`
2. 创建数据库表
3. 实现 `/skill extraction-history` 命令
4. 实现编辑和删除命令
5. 集成测试

### Phase 5: 优化和文档（2-3 天）

1. 性能优化（缓存、并发）
2. 错误处理和边界情况
3. 编写用户指南
4. 编写验收测试指南
5. 更新 CLI 文档

---

## 🎯 成功标准

### 功能完整性

- [ ] 可以从对话中识别重复模式
- [ ] 可以生成有效的技能定义
- [ ] 用户可以接受、编辑、拒绝建议
- [ ] 生成的技能可以立即使用
- [ ] 支持查看和管理提取历史

### 质量标准

- [ ] 单元测试覆盖率 > 80%
- [ ] 至少 3 个 E2E 测试
- [ ] 误报率 < 20%
- [ ] 生成的技能可用性 > 80%

### 用户体验

- [ ] 建议时机恰当
- [ ] 交互流程简洁
- [ ] 错误提示清晰
- [ ] 文档完整

---

## 📚 相关资源

- [MemoryExtractionService](../../src/GeneralAgent.Infrastructure.Memory/Services/MemoryExtractionService.cs) - 可借鉴的设计模式
- [技能系统](../../src/GeneralAgent.Infrastructure.Skills/) - 现有技能架构
- [技能示例](../../skills/) - 技能定义格式

---

## 🔮 未来增强

以下功能延后到后续迭代：

1. ❌ 基于统计的模式识别（频率分析）
2. ❌ 基于 Embedding 的相似度检测
3. ❌ 技能推荐引擎（向用户推荐相关技能）
4. ❌ 技能版本管理
5. ❌ 技能分享和导入/导出
6. ❌ 技能市场（社区共享）

---

**创建者**: Claude Sonnet 4.5
**审核者**: 待审核
**批准者**: 待批准
