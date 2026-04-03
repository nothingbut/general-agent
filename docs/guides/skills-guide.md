# 技能系统文档

General Agent 的技能系统允许您定义可复用的提示词模板，通过简单的语法调用，并支持参数化。

## 目录

- [快速开始](#快速开始)
- [技能文件格式](#技能文件格式)
- [调用技能](#调用技能)
- [命名空间](#命名空间)
- [参数系统](#参数系统)
- [创建自定义技能](#创建自定义技能)
- [.ignore 文件](#ignore-文件)
- [架构设计](#架构设计)
- [API 集成](#api-集成)

---

## 快速开始

### 查看可用技能

```bash
ls skills/personal/
# greeting.md  note.md  reminder.md

ls skills/productivity/
# brainstorm.md  task.md
```

### 调用技能

通过聊天接口使用 `@skill` 或 `/skill` 语法：

```bash
# 简单技能（无参数）
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@greeting", "session_id": "test"}'

# 带参数的技能
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@reminder task=\"Buy milk\" time=\"5pm\"", "session_id": "test"}'
```

---

## 技能文件格式

技能文件使用 Markdown 格式，带 YAML frontmatter：

```markdown
---
name: reminder
description: Create a reminder for a specific task
parameters:
  - name: task
    type: string
    required: true
    description: The task to be reminded about
  - name: time
    type: string
    required: true
    description: When to send the reminder
  - name: priority
    type: string
    required: false
    description: Priority level
    default: normal
---

# Reminder Skill

Create a reminder for the following task:

**Task:** {task}
**Time:** {time}
**Priority:** {priority}

Please confirm the reminder has been set.
```

### YAML Frontmatter 字段

| 字段 | 必需 | 说明 |
|------|------|------|
| `name` | ✅ | 技能的短名称（用于调用） |
| `description` | ✅ | 技能的简短描述 |
| `parameters` | ❌ | 参数定义列表 |

### 参数定义

| 字段 | 必需 | 说明 |
|------|------|------|
| `name` | ✅ | 参数名称 |
| `type` | ✅ | 参数类型（string, number, boolean, array, object） |
| `required` | ✅ | 是否必需（true/false） |
| `description` | ✅ | 参数描述 |
| `default` | ❌ | 默认值（仅用于可选参数） |

---

## 调用技能

### 语法

技能调用支持两种前缀：
- `@skill-name` - @ 前缀
- `/skill-name` - / 前缀

### 基本调用

**无参数技能：**
```
@greeting
/greeting
```

**带参数技能：**
```
@reminder task='Buy groceries' time='5pm'
@note content='Meeting notes' category='work'
/task title='Review PR' deadline='tomorrow' priority='high'
```

### 参数语法

参数使用 `key='value'` 或 `key="value"` 格式：

**单引号：**
```
@reminder task='Call dentist' time='3pm'
```

**双引号：**
```
@note content="Great idea for new feature" category="ideas"
```

**多参数：**
```
@task title='Finish report' deadline='Friday' priority='high' tags='urgent,review'
```

### 命名空间调用

当多个技能同名时，使用命名空间：

```
@personal:reminder task='Personal task' time='5pm'
@work:reminder task='Work task' time='2pm'
```

---

## 命名空间

技能自动按目录结构组织成命名空间：

```
skills/
├── personal/
│   ├── reminder.md      → personal:reminder
│   └── note.md          → personal:note
└── productivity/
    └── task.md          → productivity:task
```

### 命名空间规则

1. **短名称优先**：如果短名称唯一，可直接使用
   ```
   @note  # 如果只有一个 note 技能
   ```

2. **歧义检测**：如果短名称有多个匹配，会报错并提示使用完整名称
   ```
   Error: Skill 'reminder' is ambiguous.
   Use full name instead: personal:reminder, work:reminder
   ```

3. **完整名称**：始终可用，格式为 `namespace:skill-name`
   ```
   @personal:reminder
   @productivity:task
   ```

---

## 参数系统

### 参数类型

当前支持的类型：
- `string` - 字符串（最常用）
- `number` - 数字
- `boolean` - 布尔值
- `array` - 数组
- `object` - 对象

**注意**：所有参数值在执行时会转换为字符串。

### 必需参数

定义为 `required: true` 的参数必须提供，否则会报错：

```yaml
parameters:
  - name: task
    type: string
    required: true
    description: Task description
```

调用时缺少必需参数：
```
@reminder time='5pm'
# Error: Required parameter 'task' is missing
```

### 可选参数

定义为 `required: false` 的参数可以省略：

```yaml
parameters:
  - name: priority
    type: string
    required: false
    description: Task priority
    default: normal
```

调用时可以省略：
```
@reminder task='Buy milk' time='5pm'
# priority 自动使用默认值 'normal'
```

或覆盖默认值：
```
@reminder task='Buy milk' time='5pm' priority='high'
# priority 使用提供的值 'high'
```

### 参数验证

系统会自动验证：
1. ✅ **必需参数存在** - 缺少会报错
2. ✅ **参数值非空** - 空字符串会报错
3. ✅ **类型转换** - 自动转换为字符串
4. ✅ **默认值应用** - 自动填充默认值

---

## 创建自定义技能

### 步骤

1. **选择命名空间**
   ```bash
   mkdir -p skills/my-namespace
   ```

2. **创建技能文件**
   ```bash
   touch skills/my-namespace/my-skill.md
   ```

3. **编写技能定义**
   ```markdown
   ---
   name: my-skill
   description: What my skill does
   parameters:
     - name: input
       type: string
       required: true
       description: Input to process
   ---

   # My Skill

   Process the following input: {input}

   Please provide a detailed analysis.
   ```

4. **重启服务**
   ```bash
   # 服务会在启动时自动加载技能
   uvicorn src.main:app --reload
   ```

5. **测试技能**
   ```bash
   curl -X POST http://localhost:8000/api/chat \
     -H "Content-Type: application/json" \
     -d '{"message": "@my-skill input=\"test data\"", "session_id": "test"}'
   ```

### 最佳实践

**1. 清晰的提示词**
```markdown
# Good
Please analyze the following code and identify potential issues:

**Code:** {code}

Focus on:
1. Security vulnerabilities
2. Performance bottlenecks
3. Code maintainability

# Bad
Analyze this: {code}
```

**2. 有用的参数描述**
```yaml
# Good
- name: perspective
  type: string
  required: false
  description: Analysis perspective (security, performance, maintainability)
  default: security

# Bad
- name: type
  type: string
  required: false
  description: Type
```

**3. 提供使用示例**
```markdown
## Examples

- Simple greeting: `@greeting`
- With urgency: `@reminder task='Call doctor' time='today' priority='urgent'`
- Multi-step task: `@task title='Launch feature' deadline='next week' tags='release,marketing'`
```

**4. 使用有意义的默认值**
```yaml
# Good - 合理的默认值
- name: priority
  default: normal

- name: category
  default: general

# Bad - 无意义的默认值
- name: priority
  default: null
```

---

## .ignore 文件

`.ignore` 文件控制哪些文件在加载时被忽略，使用 gitignore 风格的模式。

### 位置

```
skills/.ignore
```

### 语法

```gitignore
# 注释以 # 开头

# 忽略特定文件
README.md
template.md

# 忽略模式（fnmatch）
draft-*.md          # draft-anything.md
*-draft.md          # anything-draft.md
*.bak               # 所有 .bak 文件

# 忽略子目录中的文件
**/README.md        # 任何子目录中的 README.md
**/test-*.md        # 任何子目录中的 test-*.md
```

### 常用模式

```gitignore
# Skill Ignore Patterns

# 文档文件
README.md
**/README.md
CHANGELOG.md

# 草稿和模板
draft-*.md
**/draft-*.md
*-draft.md
template.md
**/template.md

# 测试文件
test-*.md
**/test-*.md
example-*.md

# 备份文件
*.bak
*.backup
*~

# 隐藏文件
.*
**/.*
```

### 验证 .ignore

查看哪些文件被忽略：

```python
from pathlib import Path
from src.skills.loader import SkillLoader

loader = SkillLoader(Path('skills'))
loader._load_ignore_patterns()

# 测试文件
test_file = Path('skills/draft-test.md')
is_ignored = loader._should_ignore(test_file)
print(f"Ignored: {is_ignored}")  # True
```

---

## 架构设计

### 组件

技能系统由 5 个核心组件组成：

```
┌─────────────┐
│ SkillLoader │  加载 .md 文件
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ SkillParser │  解析 YAML + Markdown
└──────┬──────┘
       │
       ▼
┌──────────────┐
│ SkillRegistry│  存储和检索技能
└──────┬───────┘
       │
       ▼
┌──────────────┐
│SkillExecutor │  执行技能（参数验证 + LLM调用）
└──────────────┘
```

### 数据流

```
用户输入
  │
  ▼
SimpleRouter  ──────────────────┐
  │                             │
  │ 检测 @skill 或 /skill        │
  │                             │
  ▼                             ▼
ExecutionPlan               simple_query
type="skill"                   (LLM直接处理)
metadata={
  skill_name,
  parameters
}
  │
  ▼
AgentExecutor
  │
  │ 从 SkillRegistry 获取技能
  │
  ▼
SkillExecutor
  │
  │ 1. 验证参数
  │ 2. 构建提示词（替换 {param}）
  │ 3. 调用 LLM
  │
  ▼
SkillExecutionResult
  │
  ▼
响应给用户
```

### 模块说明

**1. SkillLoader** (`src/skills/loader.py`)
- 扫描 skills/ 目录
- 递归查找 *.md 文件
- 应用 .ignore 规则
- 提取命名空间

**2. SkillParser** (`src/skills/parser.py`)
- 解析 YAML frontmatter
- 验证必需字段（name, description）
- 解析参数定义
- 提取 Markdown 内容

**3. SkillRegistry** (`src/skills/registry.py`)
- 存储技能（Dict: full_name → SkillDefinition）
- 短名称索引（Dict: short_name → [full_names]）
- 命名空间查询
- 歧义检测

**4. SkillExecutor** (`src/skills/executor.py`)
- 参数验证（必需、类型、空值）
- 默认值应用
- 提示词构建（{param} 替换）
- LLM API 调用
- 错误处理

**5. 数据模型** (`src/skills/models.py`)
- `SkillParameter` - 参数定义
- `SkillDefinition` - 技能定义
- `SkillExecutionResult` - 执行结果

---

## API 集成

### Router 增强

`SimpleRouter` 检测技能调用：

```python
# 检测模式
SKILL_PATTERN = re.compile(r'^[@/](\S+)')
PARAM_PATTERN = re.compile(r"(\w+)=['\"]([^'\"]+)['\"]")

# 返回 ExecutionPlan
ExecutionPlan(
    type="skill",
    requires_llm=True,
    metadata={
        "skill_name": "reminder",
        "parameters": {"task": "Buy milk", "time": "5pm"}
    }
)
```

### Executor 集成

`AgentExecutor` 处理技能执行：

```python
async def execute(self, user_input: str, session_id: str):
    # 1. 路由
    plan = self.router.route(user_input)

    # 2. 执行技能
    if plan.type == "skill":
        skill = self.skill_registry.get(plan.metadata["skill_name"])
        result = await self.skill_executor.execute(
            skill,
            plan.metadata["parameters"]
        )
        response = result.output

    # 3. 返回结果
    return {"response": response, ...}
```

### 启动加载

`main.py` 在启动时加载技能：

```python
async def startup():
    # 加载技能
    skills_dir = Path("skills")
    if skills_dir.exists():
        loader = SkillLoader(skills_dir)
        skills = loader.load_all()

        # 注册
        registry = SkillRegistry()
        for skill in skills:
            registry.register(skill)

        # 创建执行器
        skill_executor = SkillExecutor(llm_client)

        # 传递给 AgentExecutor
        executor = AgentExecutor(
            db, router, llm_client,
            skill_registry=registry,
            skill_executor=skill_executor
        )
```

---

## 测试

### 测试覆盖率

技能系统有 67 个测试，覆盖率 95%+：

```bash
pytest tests/skills/ --cov=src.skills --cov-report=html
```

### 测试组织

```
tests/skills/
├── test_models.py         # 数据模型（10 tests）
├── test_parser.py         # YAML/Markdown 解析（11 tests）
├── test_loader.py         # 文件系统加载（12 tests）
├── test_registry.py       # 技能注册和查询（12 tests）
├── test_executor.py       # 技能执行（11 tests）
├── test_integration.py    # E2E 集成（11 tests）
└── conftest.py            # 共享 fixtures
```

### 运行测试

```bash
# 所有测试
pytest tests/skills/ -v

# 特定组件
pytest tests/skills/test_parser.py -v

# 集成测试
pytest tests/skills/test_integration.py -v

# 覆盖率报告
pytest tests/skills/ --cov=src.skills --cov-report=term-missing
```

---

## 故障排除

### 常见问题

**1. 技能未加载**

检查：
- skills/ 目录是否存在
- 文件扩展名是否为 .md
- YAML frontmatter 是否正确
- 文件是否被 .ignore 排除

查看日志：
```bash
# 启动时会输出加载信息
INFO: Loaded 5 skills from skills/
```

**2. 技能调用失败**

检查：
- 语法是否正确（`@skill` 或 `/skill`）
- 参数格式是否正确（`key='value'`）
- 必需参数是否提供
- 技能名称是否正确

错误示例：
```
# Wrong - 缺少引号
@reminder task=Buy milk time=5pm

# Right
@reminder task='Buy milk' time='5pm'
```

**3. 参数验证失败**

常见错误：
```
Error: Required parameter 'task' is missing
Error: Parameter 'task' cannot be empty
```

解决：
- 检查所有必需参数是否提供
- 检查参数值是否为空字符串

**4. 名称歧义**

错误：
```
Error: Skill 'reminder' is ambiguous.
Use full name instead: personal:reminder, work:reminder
```

解决：使用完整名称
```
@personal:reminder task='Buy milk' time='5pm'
```

---

## 下一步

- 📖 查看[示例技能](../skills/)了解更多用法
- 🚀 创建您自己的自定义技能
- 🔧 查看[架构文档](plans/2026-03-02-general-agent-design.md)
- 💬 参与[讨论和反馈](https://github.com/your-repo/issues)

---

## 附录

### 完整示例

**技能定义** (`skills/productivity/code-review.md`):
```markdown
---
name: code-review
description: Review code for quality and best practices
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
    description: Review focus area
    default: general
---

# Code Review Skill

Review the following {language} code:

```
{code}
```

Focus area: {focus}

Please analyze:
1. Code quality and readability
2. Potential bugs or issues
3. Performance considerations
4. Best practices adherence

Provide actionable feedback.
```

**调用示例**:
```bash
@code-review code='def foo(): return 42' language='python' focus='performance'
```

### 参考链接

- [技能系统设计文档](plans/2026-03-02-phase2-skill-system.md)
- [Phase 2 实现进度](.planning/phase2-progress-2026-03-04.md)
- [测试文档](../tests/skills/)
