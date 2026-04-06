# V3 对话抽取 Skill 功能实施计划

**创建时间**: 2026-04-06
**优先级**: 高 ⭐⭐⭐⭐
**预计耗时**: 2-3 周（10-15 个工作日）
**状态**: 📋 待开始

---

## 🎯 目标

实现从对话历史中自动识别重复性任务模式并生成可复用技能的完整功能。

---

## 📊 总览

| Phase | 任务 | 预计耗时 | 状态 |
|-------|------|----------|------|
| Phase 1 | 基础架构 | 3-4 天 | ⬜ 待开始 |
| Phase 2 | 技能生成和保存 | 2-3 天 | ⬜ 待开始 |
| Phase 3 | 用户交互 | 2-3 天 | ⬜ 待开始 |
| Phase 4 | 历史记录和管理 | 1-2 天 | ⬜ 待开始 |
| Phase 5 | 优化和文档 | 2-3 天 | ⬜ 待开始 |

**总计**: 10-15 天

---

## Phase 1: 基础架构（3-4 天）

### 目标

搭建技能提取的核心基础设施，实现基本的模式识别功能。

### 任务清单

#### 1.1 创建项目结构（0.5 天）

```bash
# 创建项目
dotnet new classlib -n GeneralAgent.Infrastructure.SkillExtraction
cd src/GeneralAgent.Infrastructure.SkillExtraction

# 创建目录结构
mkdir Models
mkdir Services
mkdir Repositories
mkdir Extensions
```

**文件结构**:
```
GeneralAgent.Infrastructure.SkillExtraction/
├── Models/
│   ├── SkillSuggestion.cs
│   ├── SkillParameter.cs
│   ├── EditAction.cs
│   ├── EditResult.cs
│   └── ExtractionRecord.cs
├── Services/
│   ├── ISkillExtractionService.cs
│   ├── SkillExtractionService.cs
│   ├── ISkillGenerator.cs
│   └── SkillGenerator.cs
├── Repositories/
│   ├── IExtractionHistoryRepository.cs
│   └── ExtractionHistoryRepository.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

**验收**:
- [ ] 项目结构清晰
- [ ] 引用依赖正确
- [ ] 可以编译通过

#### 1.2 定义核心模型（0.5 天）

**Models/SkillSuggestion.cs**:
```csharp
public sealed record SkillSuggestion
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Namespace { get; init; }
    public required string Template { get; init; }
    public required List<SkillParameterDefinition> Parameters { get; init; }
    public required double Confidence { get; init; }
    public required string Rationale { get; init; }
    public int Occurrences { get; init; }
    public List<string> ExampleMessages { get; init; } = new();
}

public sealed record SkillParameterDefinition
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required bool Required { get; init; }
    public required string Description { get; init; }
    public string? DefaultValue { get; init; }
}
```

**Models/EditAction.cs**:
```csharp
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

**Models/ExtractionRecord.cs**:
```csharp
public sealed record ExtractionRecord
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string? SessionId { get; init; }
    public string SkillName { get; init; }
    public string SkillNamespace { get; init; }
    public EditAction Action { get; init; }
    public double Confidence { get; init; }
    public int Occurrences { get; init; }
    public string? RejectionReason { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
```

**验收**:
- [ ] 所有模型定义完整
- [ ] XML 注释完整
- [ ] 使用 record 类型（不可变）

#### 1.3 实现 SkillExtractionService（2-2.5 天）

**Services/ISkillExtractionService.cs**:
```csharp
public interface ISkillExtractionService
{
    Task<List<SkillSuggestion>> ExtractFromSessionAsync(
        string sessionId,
        int lookbackMessages = 50,
        CancellationToken cancellationToken = default);
    
    Task<List<SkillSuggestion>> ExtractFromMessagesAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default);
}
```

**Services/SkillExtractionService.cs**:

**核心逻辑**:
1. 构建分析提示词
2. 调用 LLM 分析对话历史
3. 解析 JSON 响应
4. 验证和评分
5. 返回建议列表

**LLM 提示词模板**:
```csharp
private const string ExtractionSystemPrompt = """
    你是一个技能提取助手。分析对话历史，识别重复性任务模式并生成技能建议。

    识别标准：
    1. 任务至少出现 2-3 次
    2. 任务有明确的步骤和输入输出
    3. 任务可以参数化（有变化的部分）
    4. 任务足够复杂，值得创建技能

    技能命名规则：
    - 使用小写字母和连字符（如 api-helper）
    - 名称清晰描述功能
    - 长度 10-30 字符

    命名空间建议：
    - dev: 开发相关
    - productivity: 生产力工具
    - personal: 个人助手
    - analysis: 数据分析
    - writing: 写作辅助

    输出格式（JSON）：
    {
      "suggestions": [
        {
          "name": "skill-name",
          "namespace": "category",
          "description": "简短描述（一句话）",
          "template": "提示词模板（使用 {{param}} 占位符）",
          "parameters": [
            {
              "name": "param1",
              "type": "string|number|boolean",
              "required": true,
              "description": "参数说明"
            }
          ],
          "confidence": 0.0-1.0,
          "rationale": "为什么建议这个技能（1-2句话）",
          "occurrences": 出现次数,
          "exampleMessages": ["示例消息1", "示例消息2"]
        }
      ]
    }

    如果没有识别到合适的模式，返回 {"suggestions": []}
    """;
```

**测试用例**:
- 识别简单的问候模式
- 识别 API 文档查询模式
- 识别代码审查模式
- 没有重复模式时返回空
- 置信度过低时过滤

**验收**:
- [ ] 可以从会话提取建议
- [ ] LLM 调用正常
- [ ] JSON 解析正确
- [ ] 单元测试覆盖率 > 80%

#### 1.4 单元测试（0.5 天）

**测试文件**:
```
tests/GeneralAgent.Infrastructure.SkillExtraction.Tests/
├── Models/
│   └── SkillSuggestionTests.cs
├── Services/
│   └── SkillExtractionServiceTests.cs
└── Fixtures/
    └── SkillExtractionFixture.cs
```

**测试场景**:
- 提取简单模式
- 提取带参数的模式
- 过滤低置信度建议
- 处理空对话历史
- LLM 响应解析错误处理

**验收**:
- [ ] 所有测试通过
- [ ] 覆盖率 > 80%

---

## Phase 2: 技能生成和保存（2-3 天）

### 目标

实现技能定义文件的生成和保存功能。

### 任务清单

#### 2.1 实现 SkillGenerator（1-1.5 天）

**Services/ISkillGenerator.cs**:
```csharp
public interface ISkillGenerator
{
    Task<string> GenerateSkillFileAsync(
        SkillSuggestion suggestion,
        CancellationToken cancellationToken = default);
    
    Task<ValidationResult> ValidateSkillAsync(
        string skillContent,
        CancellationToken cancellationToken = default);
}
```

**Services/SkillGenerator.cs**:

**生成逻辑**:
1. 构建 YAML frontmatter
   ```yaml
   ---
   name: api-helper
   description: 查看 API 文档并生成示例代码
   parameters:
     - name: api
       type: string
       required: true
       description: API 名称
   ---
   ```

2. 生成 Markdown 模板
   ```markdown
   请帮我完成以下任务：
   
   1. 查看 {{api}} 的文档
   2. 生成示例代码
   3. 解释使用方法
   ```

3. 合并和格式化

**使用库**:
- YamlDotNet - YAML 序列化
- Scriban - 模板验证

**验收**:
- [ ] 生成的 YAML 语法正确
- [ ] 生成的模板可用
- [ ] 验证逻辑完善

#### 2.2 实现 SkillWriter（0.5-1 天）

**Services/ISkillWriter.cs**:
```csharp
public interface ISkillWriter
{
    Task<string> SaveSkillAsync(
        string @namespace,
        string name,
        string content,
        CancellationToken cancellationToken = default);
    
    Task UpdateSkillAsync(
        string skillPath,
        string content,
        CancellationToken cancellationToken = default);
    
    Task<bool> DeleteSkillAsync(
        string skillPath,
        CancellationToken cancellationToken = default);
}
```

**实现要点**:
- 文件路径：`skills/{namespace}/{name}.md`
- 创建目录（如果不存在）
- 文件名冲突处理
- 权限检查

**验收**:
- [ ] 可以保存到正确路径
- [ ] 自动创建命名空间目录
- [ ] 文件名冲突有提示

#### 2.3 集成到技能系统（0.5 天）

**目标**: 生成的技能立即可用

**步骤**:
1. 保存技能文件后
2. 触发技能重新加载
3. 验证技能可以被发现
4. 验证技能可以被执行

**集成点**:
- `SkillRegistry.ReloadAsync()`
- 或监听文件系统变化（FileSystemWatcher）

**验收**:
- [ ] 生成的技能立即可用
- [ ] `/skills` 命令显示新技能
- [ ] 可以执行新技能

#### 2.4 单元测试（0.5 天）

**测试场景**:
- 生成简单技能
- 生成带多个参数的技能
- 验证 YAML 语法
- 保存到文件系统
- 文件名冲突处理

**验收**:
- [ ] 所有测试通过
- [ ] 覆盖率 > 80%

---

## Phase 3: 用户交互（2-3 天）

### 目标

实现友好的用户交互流程，包括自动建议和手动触发。

### 任务清单

#### 3.1 实现 InteractiveEditor（1-1.5 天）

**Services/IInteractiveEditor.cs**:
```csharp
public interface IInteractiveEditor
{
    Task<EditResult> PromptUserAsync(
        SkillSuggestion suggestion,
        CancellationToken cancellationToken = default);
    
    Task<string> EditSkillAsync(
        string initialContent,
        CancellationToken cancellationToken = default);
}
```

**交互流程**:
1. 显示建议摘要
   ```
   💡 技能建议
   
   名称: dev:api-helper
   描述: 查看 API 文档并生成示例代码
   置信度: 0.85
   出现次数: 3
   
   参数:
     - api (必需): API 名称
     - language (可选): 编程语言，默认 C#
   
   是否创建此技能? [Y/n/e(编辑)]
   ```

2. 等待用户输入
3. 处理响应（Y/n/e）

**编辑模式**:
- 显示完整定义
- 逐字段编辑
- 实时验证
- 确认保存

**验收**:
- [ ] 交互流程清晰
- [ ] 输入验证完善
- [ ] 编辑模式可用

#### 3.2 实现 /skill extract 命令（0.5-1 天）

**命令定义**:
```bash
/skill extract                           # 从当前会话提取
/skill extract --session <id>           # 从指定会话提取
/skill extract --messages 100           # 分析最近 100 条消息
```

**执行流程**:
1. 解析参数
2. 加载消息历史
3. 调用提取服务
4. 显示建议列表
5. 用户选择
6. 进入编辑/保存流程

**输出格式**:
```
正在分析对话历史...

找到 2 个潜在的技能模式：
  1. API 文档助手 (置信度: 0.85) [推荐]
  2. 代码审查助手 (置信度: 0.72)

选择要创建的技能 [1/2/0(取消)]:
```

**验收**:
- [ ] 命令执行正常
- [ ] 参数解析正确
- [ ] 输出清晰友好

#### 3.3 实现自动建议逻辑（0.5-1 天）

**触发条件**:
- 用户完成第 N 次类似任务后（N=3）
- 置信度 > 0.8
- 最近没有建议过同类技能

**实现方式**:
- 选项 A: 定期后台分析（每 10 条消息）
- 选项 B: 在特定事件后触发（会话结束时）

**输出示例**:
```
💡 我注意到你经常执行类似的任务：
  1. 查看 API 文档
  2. 生成示例代码
  3. 解释使用方法

建议创建技能: dev:api-helper
是否创建? [Y/n/later]
```

**验收**:
- [ ] 建议时机恰当
- [ ] 不过度打扰用户
- [ ] 可以选择"稍后"

#### 3.4 E2E 测试（0.5 天）

**测试场景**:
- 手动触发提取
- 自动建议触发
- 接受建议
- 编辑建议
- 拒绝建议

**验收**:
- [ ] 所有场景通过
- [ ] 用户体验流畅

---

## Phase 4: 历史记录和管理（1-2 天）

### 目标

记录提取历史，支持查看和管理已生成的技能。

### 任务清单

#### 4.1 实现 ExtractionHistoryRepository（0.5 天）

**创建数据库表**:
```sql
CREATE TABLE skill_extraction_history (
    id TEXT PRIMARY KEY,
    timestamp TEXT NOT NULL,
    session_id TEXT,
    skill_name TEXT NOT NULL,
    skill_namespace TEXT NOT NULL,
    action TEXT NOT NULL,
    confidence REAL NOT NULL,
    occurrences INTEGER NOT NULL,
    rejection_reason TEXT,
    metadata TEXT
);

CREATE INDEX idx_skill_extraction_timestamp 
    ON skill_extraction_history(timestamp DESC);
```

**Repositories/IExtractionHistoryRepository.cs**:
```csharp
public interface IExtractionHistoryRepository
{
    Task<Guid> SaveAsync(
        ExtractionRecord record,
        CancellationToken cancellationToken = default);
    
    Task<List<ExtractionRecord>> GetHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);
    
    Task<List<ExtractionRecord>> GetByActionAsync(
        EditAction action,
        CancellationToken cancellationToken = default);
}
```

**验收**:
- [ ] 数据库表创建成功
- [ ] CRUD 操作正常
- [ ] 索引生效

#### 4.2 实现 ExtractionHistoryService（0.5 天）

**Services/IExtractionHistoryService.cs**:
```csharp
public interface IExtractionHistoryService
{
    Task RecordExtractionAsync(
        SkillSuggestion suggestion,
        EditAction action,
        string? rejectionReason = null,
        CancellationToken cancellationToken = default);
    
    Task<List<ExtractionRecord>> GetHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);
}
```

**验收**:
- [ ] 可以记录事件
- [ ] 可以查询历史

#### 4.3 实现 /skill extraction-history 命令（0.5 天）

**命令定义**:
```bash
/skill extraction-history                # 最近 50 条
/skill extraction-history --all          # 全部历史
/skill extraction-history --accepted     # 只显示已接受的
```

**输出格式**:
```
技能提取历史:

  时间                   | 技能名称              | 动作    | 置信度
  -----------------------|----------------------|---------|-------
  2026-04-06 10:30       | dev:api-helper       | Accept  | 0.85
  2026-04-06 09:15       | productivity:task    | Edit    | 0.78
  2026-04-05 16:45       | personal:reminder    | Reject  | 0.65
```

**验收**:
- [ ] 命令执行正常
- [ ] 格式清晰易读

#### 4.4 集成测试（0.5 天）

**测试场景**:
- 记录提取事件
- 查询历史
- 按动作过滤

**验收**:
- [ ] 所有测试通过

---

## Phase 5: 优化和文档（2-3 天）

### 目标

优化性能，完善错误处理，编写文档。

### 任务清单

#### 5.1 性能优化（0.5-1 天）

**优化点**:
1. LLM 调用缓存（相同对话不重复分析）
2. 并发处理（分析多个会话）
3. 数据库查询优化
4. 减少不必要的文件 I/O

**验收**:
- [ ] 提取耗时 < 5 秒
- [ ] 生成耗时 < 3 秒

#### 5.2 错误处理（0.5 天）

**场景**:
- LLM 调用失败
- JSON 解析错误
- 文件保存失败
- 权限不足
- 技能名称冲突

**策略**:
- 友好的错误提示
- 自动重试（LLM 调用）
- 降级处理

**验收**:
- [ ] 所有错误场景有处理
- [ ] 错误提示清晰

#### 5.3 编写用户指南（0.5-1 天）

**文档**:
- `SKILL_EXTRACTION_USER_GUIDE.md`
- 功能介绍
- 使用示例
- 常见问题
- 故障排除

**验收**:
- [ ] 文档完整清晰

#### 5.4 更新 CLI 文档（0.5 天）

**更新文件**:
- `CLI_GUIDE.md` - 添加技能提取章节
- `CLI_REFERENCE.md` - 添加新命令文档

**验收**:
- [ ] CLI 文档更新完整

#### 5.5 编写验收测试指南（0.5 天）

**文档**:
- `SKILL_EXTRACTION_ACCEPTANCE_TEST.md`
- 10+ 测试场景
- 验收标准
- 测试报告模板

**验收**:
- [ ] 验收测试指南完整

---

## 🎯 总体验收标准

### 功能完整性

- [ ] 可以从对话中识别重复模式
- [ ] 可以生成有效的技能定义
- [ ] 用户可以接受、编辑、拒绝建议
- [ ] 生成的技能可以立即使用
- [ ] 支持查看和管理提取历史

### 质量标准

- [ ] 单元测试覆盖率 > 80%
- [ ] 至少 3 个 E2E 测试通过
- [ ] 所有集成测试通过
- [ ] 误报率 < 20%（不建议不合适的技能）
- [ ] 生成的技能可用性 > 80%

### 用户体验

- [ ] 建议时机恰当（不过度打扰）
- [ ] 交互流程简洁清晰
- [ ] 错误提示友好具体
- [ ] 文档完整易懂

### 性能

- [ ] 模式识别耗时 < 5 秒
- [ ] 技能生成耗时 < 3 秒
- [ ] 不影响正常对话流程

---

## 📊 进度跟踪

| 日期 | Phase | 完成任务 | 遇到问题 | 下一步 |
|------|-------|----------|----------|--------|
| 2026-04-06 | - | 设计文档完成 | 无 | 开始 Phase 1 |
| ... | ... | ... | ... | ... |

---

## 📝 风险和缓解

### 风险 1: LLM 识别准确率不足

**影响**: 高误报率，用户体验差

**缓解**:
- 提高置信度阈值
- 使用更好的提示词
- 添加统计分析辅助

### 风险 2: 生成的技能不可用

**影响**: 用户需要大量手工修改

**缓解**:
- 提供交互式编辑
- 实时验证语法
- 提供多个模板示例

### 风险 3: 性能问题

**影响**: 分析耗时过长

**缓解**:
- 后台异步处理
- 缓存分析结果
- 限制分析的消息数量

---

## 🔗 相关文档

- [设计文档](./skill-extraction-design.md)
- [优先级路线图](./priority-features.md)
- [技能系统指南](../guides/SKILLS_GUIDE.md)

---

**创建者**: Claude Sonnet 4.5
**维护者**: 开发团队
**版本**: 1.0
