# Skill 系统设计对比：V1 vs V2 vs V3

## 执行摘要

**关键发现**：V1 (Python) 的 skill 系统设计是**正确的**，而 V2 (Rust) 和 V3 (C#) 都存在**架构缺陷** —— 它们将 skill 渲染的结果直接作为最终响应返回，而不是作为提示词发送给 LLM。

## 三个版本的设计对比

### V1 (Python) - ✅ 正确设计

**文件位置**：`v1/src/skills/executor.py`

**执行流程**：
```python
async def execute(self, skill: SkillDefinition, parameters: Dict[str, Any]) -> SkillExecutionResult:
    # 1. 验证参数
    validated_params = self._validate_parameters(skill, parameters)

    # 2. 构建提示词（替换参数）
    prompt = self._build_prompt(skill, validated_params)

    # 3. 调用 LLM（关键步骤！）
    output = await self._call_llm(prompt)

    # 4. 返回 LLM 响应
    return SkillExecutionResult(
        skill_name=skill.full_name,
        success=True,
        output=output  # LLM 生成的内容
    )
```

**数据流**：
```
Skill 定义 + 参数 → 渲染提示词 → 调用 LLM → 返回 AI 响应
```

**优点**：
- ✅ Skill 作为结构化的提示词模板使用
- ✅ 充分利用 LLM 的理解和生成能力
- ✅ 用户获得智能的、上下文感知的响应
- ✅ 支持动态内容生成

**示例**：
```markdown
---
name: greeting
parameters:
  - name: user_name
    type: string
    required: true
---

你好 {user_name}！今天有什么我可以帮助你的吗？
```

调用 `@greeting user_name='Alice'` 的流程：
1. 渲染模板 → `"你好 Alice！今天有什么我可以帮助你的吗？"`
2. 发送给 LLM → LLM 可能生成：`"你好 Alice！很高兴见到你！我可以帮你处理各种任务..."`
3. 返回 LLM 的智能响应

---

### V2 (Rust) - ❌ 设计缺陷

**文件位置**：`v2/crates/agent-skills/src/executor.rs`

**执行流程**：
```rust
pub fn execute(
    &self,
    skill: &SkillDefinition,
    mut parameters: HashMap<String, String>,
) -> Result<String> {
    // 1. 处理位置参数
    // 2. 创建执行上下文
    let context = SkillExecutionContext::new(skill.clone(), parameters);

    // 3. 验证参数
    context.validate().map_err(ExecutorError::ValidationError)?;

    // 4. 构建提示词
    let prompt = context.build_prompt();

    // ❌ 直接返回渲染后的文本，没有调用 LLM！
    Ok(prompt)
}
```

**数据流**：
```
Skill 定义 + 参数 → 渲染文本 → 直接返回（不经过 LLM）
```

**问题**：
- ❌ Skill 渲染的结果直接作为最终响应
- ❌ 没有 LLM 参与，失去智能特性
- ❌ 只能返回静态的、模板化的文本
- ❌ 无法根据上下文动态生成内容

**示例**：
调用 `@greeting user_name='Alice'` 只会返回：
```
你好 Alice！今天有什么我可以帮助你的吗？
```
这是一个**静态的、死板的**响应，没有任何智能。

---

### V3 (C#) - ❌ 设计缺陷

**文件位置**：
- `v3/src/GeneralAgent.Infrastructure.Skills/Executors/SkillExecutor.cs`
- `v3/src/GeneralAgent.Application/Services/ConversationService.cs`

**Skill 执行器**：
```csharp
public Result<string> Execute(Skill skill, Dictionary<string, object> arguments)
{
    // 1. 验证参数
    var validationResult = ValidateAndPrepareArguments(skill, arguments);

    // 2. 解析 Scriban 模板
    var template = Template.Parse(skill.Template);

    // 3. 渲染模板
    string output = template.Render(context);

    // ❌ 直接返回渲染后的文本，没有调用 LLM！
    return Result<string>.Success(output);
}
```

**ConversationService 集成**：
```csharp
public async Task<string> SendMessageAsync(...)
{
    // 检查是否是技能调用
    if (_skillService != null && SkillCallParser.TryParse(userMessage, out var skillCall))
    {
        // ❌ 执行技能并直接返回结果（不调用 LLM）
        var skillResult = _skillService.ExecuteSkill(skillCall.SkillName, skillCall.Arguments);

        if (skillResult.IsSuccess)
        {
            responseContent = skillResult.Value!;  // 直接使用 skill 渲染的文本
        }
    }
    else
    {
        // 只有非 skill 调用才会走 LLM
        var response = await client.CompleteAsync(request, ct);
        responseContent = response.Content;
    }

    return responseContent;
}
```

**数据流**：
```
Skill 定义 + 参数 → Scriban 渲染 → 直接返回（绕过 LLM）
```

**问题**：
- ❌ Skill 调用和 LLM 调用是**互斥的**（要么走 skill，要么走 LLM）
- ❌ Skill 渲染的结果作为最终响应，没有 AI 生成
- ❌ 虽然使用了 Scriban 这样强大的模板引擎，但只是生成静态文本
- ❌ 失去了 LLM 的理解、推理和生成能力

**示例**：
调用 `@greeting user_name='Alice'` 只会返回：
```
你好 Alice！今天有什么我可以帮助你的吗？
```
同样是**静态的、没有智能的**响应。

---

## 核心问题分析

### V2 和 V3 的根本错误

**错误假设**：Skill 的输出应该直接作为最终响应返回给用户。

**为什么这是错误的**：

1. **Skill 的本质**：Skill 应该是**结构化的提示词模板**，用于指导 LLM 生成特定风格/格式的响应
2. **用户期望**：用户期望与 AI 助手对话，获得智能的、上下文感知的响应
3. **失去智能**：绕过 LLM 意味着失去了：
   - 上下文理解能力
   - 动态内容生成
   - 自然语言交互
   - 推理和解释能力

### 正确的架构应该是

```
┌─────────────────────────────────────────────────────────┐
│ 用户输入：@greeting user_name='Alice'                    │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│ SkillCallParser: 解析 skill 调用                        │
│ → skill_name: "greeting"                                │
│ → arguments: { user_name: "Alice" }                     │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│ SkillExecutor: 执行 skill                               │
│ 1. 加载 skill 定义                                      │
│ 2. 验证参数                                             │
│ 3. 渲染模板 → "你好 Alice！今天有什么我可以帮助你的吗？"   │
│ 4. **调用 LLM**（将渲染的提示词发送给 LLM）               │
│ 5. 返回 LLM 响应                                        │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│ LLM 响应（智能生成）：                                   │
│ "你好 Alice！很高兴见到你！我是你的 AI 助手，可以帮你处理  │
│ 各种任务，比如：日程管理、信息查询、文档编写等。           │
│ 请问今天有什么需要我帮忙的吗？"                          │
└─────────────────────────────────────────────────────────┘
```

---

## V3 的修复方案

### 方案 1：在 SkillExecutor 中集成 LLM（推荐）

**修改**：`SkillExecutor.cs`

```csharp
public class SkillExecutor : ISkillExecutor
{
    private readonly ILogger<SkillExecutor> _logger;
    private readonly ILLMClientFactory _llmClientFactory;  // ← 新增

    public SkillExecutor(
        ILogger<SkillExecutor> logger,
        ILLMClientFactory llmClientFactory)  // ← 新增
    {
        _logger = logger;
        _llmClientFactory = llmClientFactory;
    }

    public async Task<Result<string>> ExecuteAsync(
        Skill skill,
        Dictionary<string, object> arguments,
        string? providerName = null,
        CancellationToken ct = default)
    {
        try
        {
            // 1. 验证和准备参数
            var validationResult = ValidateAndPrepareArguments(skill, arguments);
            if (!validationResult.IsSuccess)
            {
                return Result<string>.Failure(validationResult.Error!);
            }

            // 2. 渲染模板生成提示词
            var prompt = RenderTemplate(skill, validationResult.Value!);
            if (!prompt.IsSuccess)
            {
                return prompt;
            }

            // 3. 调用 LLM（关键步骤！）
            var client = _llmClientFactory.GetClient(providerName);
            var request = new CompletionRequest
            {
                Model = "qwen3.5:0.8b",
                Messages = new List<ChatMessage>
                {
                    new() { Role = "user", Content = prompt.Value! }
                }
            };

            var response = await client.CompleteAsync(request, ct);

            _logger.LogDebug("技能执行成功: {SkillName}, LLM 响应长度: {Length}",
                skill.FullName, response.Content.Length);

            // 4. 返回 LLM 生成的响应
            return Result<string>.Success(response.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行技能失败: {SkillName}", skill.FullName);
            return Result<string>.Failure($"执行技能失败: {ex.Message}");
        }
    }

    private Result<string> RenderTemplate(
        Skill skill,
        Dictionary<string, object> arguments)
    {
        // 使用 Scriban 渲染模板
        var template = Template.Parse(skill.Template);
        if (template.HasErrors)
        {
            var errors = string.Join(", ", template.Messages.Select(m => m.Message));
            return Result<string>.Failure($"模板解析失败: {errors}");
        }

        var scriptObject = new ScriptObject();
        foreach (var (key, value) in arguments)
        {
            scriptObject.Add(key, value);
        }

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);

        try
        {
            var output = template.Render(context);
            return Result<string>.Success(output);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"模板渲染失败: {ex.Message}");
        }
    }
}
```

**修改**：`ISkillExecutor.cs`

```csharp
public interface ISkillExecutor
{
    Task<Result<string>> ExecuteAsync(
        Skill skill,
        Dictionary<string, object> arguments,
        string? providerName = null,
        CancellationToken ct = default);
}
```

**修改**：`ConversationService.cs`

```csharp
public async Task<string> SendMessageAsync(
    Guid sessionId,
    string userMessage,
    string? providerName = null,
    CancellationToken ct = default)
{
    // 验证会话存在
    var session = await _sessionRepository.GetByIdAsync(sessionId, ct)
        ?? throw new InvalidOperationException($"会话不存在: {sessionId}");

    // 保存用户消息
    var userMsg = Message.CreateUser(sessionId, userMessage);
    await _messageRepository.CreateAsync(userMsg, ct);

    string responseContent;

    // 检查是否是技能调用
    if (_skillService != null && SkillCallParser.TryParse(userMessage, out var skillCall) && skillCall != null)
    {
        // ✅ 执行技能（内部会调用 LLM）
        var skillResult = await _skillService.ExecuteSkillAsync(
            skillCall.SkillName,
            skillCall.Arguments,
            providerName,
            ct);

        if (skillResult.IsSuccess)
        {
            responseContent = skillResult.Value!;  // 这是 LLM 生成的响应
        }
        else
        {
            responseContent = $"❌ 技能执行失败: {skillResult.Error}";
        }
    }
    else
    {
        // 普通对话也调用 LLM
        var history = await _messageRepository.GetBySessionAsync(sessionId, ct);
        var chatMessages = ConvertToChatMessages(history);

        var client = _llmClientFactory.GetClient(providerName);
        var request = new CompletionRequest
        {
            Model = "qwen3.5:0.8b",
            Messages = chatMessages
        };
        var response = await client.CompleteAsync(request, ct);
        responseContent = response.Content;
    }

    // 保存助手响应
    var assistantMsg = Message.CreateAssistant(sessionId, responseContent);
    await _messageRepository.CreateAsync(assistantMsg, ct);

    return responseContent;
}
```

**修改**：`SkillService.cs`

```csharp
public async Task<Result<string>> ExecuteSkillAsync(
    string skillName,
    Dictionary<string, object> arguments,
    string? providerName = null,
    CancellationToken ct = default)
{
    if (!_initialized)
    {
        return Result<string>.Failure("技能系统未初始化，请先调用 LoadSkillsAsync");
    }

    try
    {
        _logger.LogDebug("执行技能: {SkillName}", skillName);

        // 查找技能
        var skill = FindSkill(skillName);
        if (skill == null)
        {
            _logger.LogWarning("技能不存在: {SkillName}", skillName);
            return Result<string>.Failure($"技能不存在: {skillName}");
        }

        // ✅ 执行技能（会调用 LLM）
        var result = await _executor.ExecuteAsync(skill, arguments, providerName, ct);

        if (result.IsSuccess)
        {
            _logger.LogDebug("技能执行成功: {SkillName}", skillName);
        }

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "执行技能失败: {SkillName}", skillName);
        return Result<string>.Failure($"执行技能失败: {ex.Message}");
    }
}
```

### 方案 2：在 ConversationService 中调用 LLM（不推荐）

这种方案会导致职责不清晰，因为 skill 的渲染和 LLM 调用会分散在不同的层。

---

## V2 的修复方案

**修改**：`v2/crates/agent-skills/src/executor.rs`

需要将 executor 改为异步，并集成 LLM 客户端：

```rust
use agent_llm::LLMClient;  // 假设存在 LLM 客户端

pub struct SkillExecutor {
    skill_pattern: Regex,
    param_pattern: Regex,
    llm_client: Arc<dyn LLMClient>,  // 新增
}

impl SkillExecutor {
    pub fn new(llm_client: Arc<dyn LLMClient>) -> Self {
        Self {
            skill_pattern: Regex::new(r"^[@/](\S+)").unwrap(),
            param_pattern: Regex::new(r#"(\w+)=['"]([^'"]+)['"]"#).unwrap(),
            llm_client,
        }
    }

    // 修改为异步方法，并调用 LLM
    pub async fn execute(
        &self,
        skill: &SkillDefinition,
        mut parameters: HashMap<String, String>,
    ) -> Result<String> {
        // 1. 处理位置参数
        if let Some(value) = parameters.remove("__positional_0") {
            if let Some(first_required_param) = skill.parameters.iter().find(|p| p.required) {
                parameters.insert(first_required_param.name.clone(), value);
            } else {
                return Err(ExecutorError::InvalidSyntax(
                    "Cannot use positional argument: skill has no required parameters".to_string()
                ));
            }
        }

        // 2. 创建执行上下文并验证
        let context = SkillExecutionContext::new(skill.clone(), parameters);
        context.validate().map_err(ExecutorError::ValidationError)?;

        // 3. 构建提示词
        let prompt = context.build_prompt();

        // 4. 调用 LLM（关键步骤！）
        let response = self.llm_client
            .complete(&prompt)
            .await
            .map_err(|e| ExecutorError::LLMError(e.to_string()))?;

        // 5. 返回 LLM 响应
        Ok(response.content)
    }
}
```

同时需要在 `ExecutorError` 中添加 LLM 错误类型：

```rust
#[derive(Debug, Error)]
pub enum ExecutorError {
    #[error("Invalid invocation syntax: {0}")]
    InvalidSyntax(String),

    #[error("Validation error: {0}")]
    ValidationError(String),

    #[error("LLM error: {0}")]
    LLMError(String),  // 新增
}
```

---

## 总结

| 版本 | 设计状态 | Skill 执行流程 | 是否调用 LLM | 响应类型 |
|------|---------|---------------|-------------|---------|
| **V1 (Python)** | ✅ **正确** | 渲染模板 → 调用 LLM → 返回 AI 响应 | ✅ 是 | 智能、动态 |
| **V2 (Rust)** | ❌ **错误** | 渲染模板 → 直接返回 | ❌ 否 | 静态、模板化 |
| **V3 (C#)** | ❌ **错误** | 渲染模板 → 直接返回 | ❌ 否 | 静态、模板化 |

### 推荐行动

1. **V3 优先修复**：按照方案 1 修改 `SkillExecutor`，集成 LLM 客户端
2. **V2 后续修复**：重构 executor 为异步，集成 LLM 客户端
3. **保持 V1 设计**：作为参考实现，确保新版本遵循相同的架构原则

### 核心原则

> **Skill 是提示词模板，不是响应模板**
>
> Skill 的作用是帮助用户快速构建结构化的提示词，最终的响应应该由 LLM 生成，而不是直接返回模板渲染的结果。
