# 技能系统使用指南

**版本:** 0.1.0
**更新日期:** 2026-03-10

---

## 📋 目录

- [什么是技能](#什么是技能)
- [快速开始](#快速开始)
- [技能定义](#技能定义)
- [技能调用](#技能调用)
- [高级特性](#高级特性)
- [最佳实践](#最佳实践)
- [示例技能](#示例技能)

---

## 什么是技能

技能是**可复用的提示词模板**，让你能够：

- ✅ 标准化常用提示词
- ✅ 参数化提示词内容
- ✅ 组织和管理提示词库
- ✅ 在对话中快速调用
- ✅ 团队共享最佳实践

**技能 vs 普通提示词:**

| 特性 | 普通提示词 | 技能 |
|------|----------|------|
| 复用性 | 每次手动输入 | 一次定义，多次使用 |
| 参数化 | 不支持 | 支持参数和默认值 |
| 可维护性 | 分散在聊天记录中 | 集中管理 |
| 版本控制 | 困难 | 易于版本控制 |
| 团队协作 | 难以共享 | 易于共享 |

---

## 快速开始

### 1. 创建技能文件

创建 `greeting.md`:

```markdown
---
name: greeting
description: Greet the user warmly
parameters:
  - name: user_name
    type: string
    required: true
    description: User's name
---

Hello {user_name}! How can I help you today?
```

### 2. 启动 CLI 并加载技能

```bash
./target/release/agent --skills-dir ./my-skills chat <session-id>
```

### 3. 使用技能

在对话中输入：

```
@greeting user_name='Alice'
```

LLM 将收到：
```
Hello Alice! How can I help you today?
```

---

## 技能定义

### 文件格式

技能文件使用 **Markdown + YAML frontmatter** 格式：

```markdown
---
# 技能元数据（YAML）
name: skill_name
description: Skill description
namespace: optional_namespace
parameters:
  - name: param_name
    type: string
    required: true
    description: Parameter description
    default: optional_default_value
---

# 技能内容（Markdown）
This is the skill template content.

You can use {param_name} to insert parameters.
```

### 必需字段

- `name`: 技能名称（必须唯一）
- `description`: 技能描述

### 可选字段

- `namespace`: 命名空间（避免冲突）
- `parameters`: 参数列表

### 参数定义

每个参数包含：

```yaml
- name: param_name           # 参数名称
  type: string               # 参数类型（string/number/boolean）
  required: true             # 是否必需
  description: Description   # 参数说明
  default: default_value     # 默认值（可选）
```

---

## 技能调用

### 两种语法

#### 1. @ 语法（推荐）

```
@skill_name param1='value1' param2='value2'
```

#### 2. / 语法

```
/skill_name param1='value1' param2='value2'
```

### 参数传递

**基本语法:**
```
@skill param='value'
```

**多个参数:**
```
@skill param1='value1' param2='value2'
```

**带引号的值:**
```
@skill text='Hello, world!' number='42'
```

**带空格的值:**
```
@skill name='John Doe' title='Senior Engineer'
```

### 命名空间调用

如果技能有命名空间，使用 `:` 分隔：

```
@namespace:skill_name param='value'
```

### 省略可选参数

如果参数有默认值，可以省略：

```yaml
---
name: greeting
parameters:
  - name: user_name
    required: true
  - name: tone
    required: false
    default: friendly
---

Hello {user_name}! Tone: {tone}
```

调用：
```
@greeting user_name='Alice'         # tone 使用默认值 'friendly'
@greeting user_name='Bob' tone='formal'  # 覆盖默认值
```

---

## 高级特性

### 1. 条件渲染

**简单条件:**

```markdown
---
name: personalized_greeting
parameters:
  - name: user_name
    required: true
  - name: is_vip
    type: boolean
    default: false
---

Hello {user_name}!

{#if is_vip}
Welcome back, valued customer!
{else}
Nice to see you!
{/if}
```

### 2. 循环渲染

```markdown
---
name: task_list
parameters:
  - name: tasks
    type: array
    required: true
---

Your tasks:

{#for task in tasks}
- {task}
{/for}
```

### 3. 嵌套参数

```markdown
---
name: user_profile
parameters:
  - name: user
    type: object
    required: true
---

User: {user.name}
Email: {user.email}
Role: {user.role}
```

### 4. 多行内容

```markdown
---
name: code_review
parameters:
  - name: code
    type: string
    required: true
  - name: language
    required: true
---

Please review the following {language} code:

```{language}
{code}
```

Focus on:
1. Code quality
2. Best practices
3. Potential bugs
```

---

## 最佳实践

### 1. 命名规范

**技能名称:**
- 使用小写字母和下划线
- 描述性命名
- 避免缩写

```
✅ code_review
✅ generate_test_cases
❌ cr
❌ GenTest
```

**参数名称:**
- 使用 snake_case
- 清晰明确

```
✅ user_name
✅ max_tokens
❌ UN
❌ MaxTokens
```

### 2. 文档清晰

**好的描述:**
```yaml
name: summarize
description: Summarize a long text into key points
parameters:
  - name: text
    description: The text to summarize (can be up to 10,000 words)
  - name: max_points
    description: Maximum number of key points to generate (1-10)
    default: 5
```

**差的描述:**
```yaml
name: sum
description: Summarize
parameters:
  - name: t
    description: Text
```

### 3. 合理默认值

为常用参数提供合理默认值：

```yaml
parameters:
  - name: language
    default: English
  - name: max_length
    default: 500
  - name: format
    default: markdown
```

### 4. 参数验证

使用类型和 required 确保输入正确：

```yaml
parameters:
  - name: email
    type: string
    required: true
  - name: age
    type: number
    required: false
  - name: accept_terms
    type: boolean
    required: true
```

### 5. 模块化组织

**按功能分类:**
```
skills/
├── coding/
│   ├── code_review.md
│   ├── generate_tests.md
│   └── refactor.md
├── writing/
│   ├── summarize.md
│   ├── translate.md
│   └── proofread.md
└── productivity/
    ├── meeting_notes.md
    ├── email_template.md
    └── task_breakdown.md
```

**使用命名空间:**
```yaml
# coding/code_review.md
---
name: code_review
namespace: coding
---

# writing/code_review.md
---
name: code_review
namespace: writing
---
```

调用：
```
@coding:code_review code='...'
@writing:code_review text='...'
```

### 6. 版本控制

```yaml
---
name: api_design
version: 2.0.0
description: Design RESTful API endpoints
---
```

---

## 示例技能

### 1. 代码审查

```markdown
---
name: code_review
namespace: coding
description: Comprehensive code review
parameters:
  - name: code
    type: string
    required: true
    description: The code to review
  - name: language
    type: string
    required: true
    description: Programming language
  - name: focus
    type: string
    required: false
    default: general
    description: Focus area (general/security/performance)
---

Please review the following {language} code:

```{language}
{code}
```

Review focus: {focus}

Please check:
1. Code quality and readability
2. Best practices adherence
3. Potential bugs or issues
4. Performance considerations
5. Security concerns

Provide specific suggestions for improvement.
```

### 2. 文档生成

```markdown
---
name: generate_docs
namespace: coding
description: Generate API documentation
parameters:
  - name: function_signature
    required: true
  - name: language
    required: true
  - name: doc_style
    required: false
    default: google
---

Generate documentation for this {language} function:

```{language}
{function_signature}
```

Documentation style: {doc_style}

Include:
- Brief description
- Parameters explanation
- Return value
- Usage example
- Edge cases
```

### 3. 会议纪要

```markdown
---
name: meeting_notes
namespace: productivity
description: Structure meeting notes
parameters:
  - name: raw_notes
    required: true
  - name: meeting_type
    required: false
    default: general
---

Please structure these meeting notes:

{raw_notes}

Format as:
## {meeting_type} Meeting

### Attendees
[List participants]

### Agenda
[Key topics discussed]

### Action Items
[Tasks with owners]

### Next Steps
[Follow-up actions]

### Next Meeting
[Date and topics]
```

### 4. 邮件模板

```markdown
---
name: email_template
namespace: productivity
description: Generate professional email
parameters:
  - name: purpose
    required: true
    description: Email purpose (inquiry/follow-up/introduction)
  - name: recipient_name
    required: true
  - name: key_points
    required: true
    description: Main points to cover
  - name: tone
    required: false
    default: professional
---

Generate a {tone} email for {purpose}.

Recipient: {recipient_name}

Key points to cover:
{key_points}

Include:
- Appropriate greeting
- Clear subject line
- Well-structured body
- Professional closing
```

### 5. 测试用例生成

```markdown
---
name: generate_tests
namespace: coding
description: Generate unit tests
parameters:
  - name: function_code
    required: true
  - name: language
    required: true
  - name: test_framework
    required: false
    default: pytest
---

Generate unit tests for this {language} function:

```{language}
{function_code}
```

Test framework: {test_framework}

Generate tests for:
1. Happy path scenarios
2. Edge cases
3. Error conditions
4. Boundary values

Use descriptive test names and include comments.
```

---

## 故障排除

### 技能未找到

**问题:** `Skill not found: greeting`

**原因:**
- 技能名称拼写错误
- 技能文件未加载
- 命名空间冲突

**解决:**
```bash
# 检查技能目录
ls -la ./my-skills/

# 确保文件名与 name 字段一致
# greeting.md 应该包含 name: greeting

# 使用完整名称（带命名空间）
@namespace:skill_name
```

### 参数验证失败

**问题:** `Validation error: Missing required parameter 'user_name'`

**解决:**
```
# 确保提供所有必需参数
@greeting user_name='Alice'

# 检查参数名拼写
# user_name 不等于 username
```

### 技能未启用

**问题:** `Skills not enabled`

**解决:**
```bash
# 确保使用 --skills-dir 参数
./target/release/agent --skills-dir ./skills chat <id>
```

---

## 性能建议

### 1. 技能数量

- 建议：< 100 个技能
- 加载时间：~10ms per skill
- 内存占用：~1KB per skill

### 2. 技能大小

- 建议：< 10KB per skill
- 模板复杂度：适中
- 避免：嵌套过深的条件/循环

### 3. 参数数量

- 建议：< 10 个参数 per skill
- 保持简单：更易使用和维护

---

## 下一步

1. **查看示例技能** - `crates/agent-skills/examples/test_skills/`
2. **创建自己的技能** - 从简单的开始
3. **组织技能库** - 按功能分类
4. **分享技能** - 与团队共享最佳实践
5. **持续改进** - 根据使用情况优化技能

---

**更新日期:** 2026-03-10
**版本:** 0.1.0
