# Tool Calling 使用指南

## 概述

Tool Calling 是 General Agent V3 的核心特性，允许 LLM（大语言模型）自动调用工具（如技能、API、命令等）来完成复杂任务。系统支持两种调用方式：

- **显式调用**：用户使用 `@skill` 或 `/skill` 语法直接调用特定技能
- **隐式调用**：用户使用自然语言提问，LLM 自动选择并调用合适的工具

Tool Calling 系统会自动管理多轮对话，处理工具执行结果，并在必要时向用户请求确认。

## 配置

Tool Calling 通过 `appsettings.json` 文件配置：

```json
{
  "ToolCalling": {
    "Enabled": true,              // 是否启用 Tool Calling
    "MaxRounds": 3,               // 默认最大调用轮数
    "InteractiveMode": true,      // 达到限制时是否询问用户
    "AutoExtendBy": 5,            // 自动模式下延长的轮数
    "AbsoluteMaxRounds": 20       // 绝对最大轮数（防止无限循环）
  }
}
```

### 配置说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Enabled` | bool | `true` | 是否启用 Tool Calling 功能 |
| `MaxRounds` | int | `3` | LLM 可以连续调用工具的最大轮数 |
| `InteractiveMode` | bool | `true` | 达到最大轮数时是否询问用户是否继续 |
| `AutoExtendBy` | int | `5` | 用户同意继续时，额外增加的轮数 |
| `AbsoluteMaxRounds` | int | `20` | 绝对最大轮数，超过此值对话将强制终止 |

### 配置示例

**开发环境**（启用交互确认）：
```json
{
  "ToolCalling": {
    "Enabled": true,
    "MaxRounds": 3,
    "InteractiveMode": true,
    "AutoExtendBy": 5,
    "AbsoluteMaxRounds": 20
  }
}
```

**自动化环境**（无交互）：
```json
{
  "ToolCalling": {
    "Enabled": true,
    "MaxRounds": 10,
    "InteractiveMode": false,
    "AbsoluteMaxRounds": 15
  }
}
```

**调试模式**（禁用 Tool Calling）：
```json
{
  "ToolCalling": {
    "Enabled": false
  }
}
```

## 使用方式

### 显式调用

显式调用允许你直接指定要使用的工具和参数。

#### @ 语法（推荐）

```
@greeting user_name='张三'
@personal:reminder task='买牛奶' time='5pm' is_urgent=true
@productivity:task title='完成报告' priority='high'
```

**语法规则**：
- 使用 `@` 前缀表示工具调用
- 支持命名空间：`@namespace:skill_name`
- 参数使用键值对：`key='value'` 或 `key=value`（无空格时可省略引号）
- 布尔值和数字可不带引号：`is_urgent=true`, `count=5`

#### / 语法（命令风格）

```
/greeting user_name='李四'
/reminder task='开会' time='明天下午3点'
```

**语法规则**：
- 使用 `/` 前缀表示命令
- 参数规则与 `@` 语法相同
- 更接近传统命令行风格

### 隐式调用

隐式调用让 LLM 根据对话内容自动选择和调用工具。

#### 示例 1：自动问候

```
用户：向张三问好
```

LLM 会自动调用：
```
@greeting user_name='张三'
```

#### 示例 2：多步骤任务

```
用户：帮我安排今天的工作：早上9点开会，下午2点提交报告，晚上6点买牛奶
```

LLM 可能会自动调用多个工具：
```
@reminder task='开会' time='9am'
@reminder task='提交报告' time='2pm'
@reminder task='买牛奶' time='6pm'
```

#### 示例 3：上下文感知

```
用户：现在几点了？
Agent：现在是下午3点。
用户：给我发个下午的问候吧
```

LLM 会利用上下文调用：
```
@greeting user_name='用户' time_of_day='afternoon'
```

## 上下文感知 Skill

技能可以通过 Scriban 模板语法实现上下文感知功能。

### 基础模板

```yaml
---
name: greeting
description: 向用户发送个性化问候
parameters:
  - name: user_name
    type: string
    required: true
    description: 用户的名字
  - name: time_of_day
    type: string
    required: false
    description: 时间段（morning/afternoon/evening）
    default_value: morning
---

{{ if time_of_day == "morning" }}
早上好，{{ user_name }}！新的一天开始了，今天有什么计划吗？
{{ else if time_of_day == "afternoon" }}
下午好，{{ user_name }}！希望你今天过得愉快。
{{ else if time_of_day == "evening" }}
晚上好，{{ user_name }}！辛苦了一天，该放松一下了。
{{ else }}
你好，{{ user_name }}！今天过得怎么样？
{{ end }}
```

### 高级功能

#### 条件渲染

```scriban
{{ if is_urgent }}
⚠️ **紧急提醒** ⚠️
{{ else }}
📝 提醒事项
{{ end }}
```

#### 循环

```scriban
{{ for item in items }}
- {{ item.name }}: {{ item.description }}
{{ end }}
```

#### 过滤器

```scriban
{{ text | string.upcase }}          # 转大写
{{ text | string.capitalize }}      # 首字母大写
{{ date | date.to_string '%Y-%m-%d' }}  # 日期格式化
```

#### 默认值

```yaml
parameters:
  - name: priority
    type: string
    required: false
    default_value: medium  # 参数级默认值
```

```scriban
{{ priority ?? "medium" }}  # 模板级默认值
```

## 用户确认机制

当 Tool Calling 达到最大轮数限制时，系统会向用户请求确认。

### 确认流程

1. **达到限制**：LLM 的工具调用达到 `MaxRounds` 设置的轮数
2. **显示信息**：系统显示当前状态和待执行的工具
3. **用户选择**：
   - **继续**：延长 `AutoExtendBy` 轮数，继续执行
   - **停止**：终止对话循环，返回当前结果

### 示例输出

```
⚠️ 已达到最大轮数限制（3 轮）

当前待执行工具：
  1. reminder - 创建提醒（买牛奶）
  2. greeting - 发送问候（张三）

是否继续执行？(y/n) [默认: n]:
```

### 自动模式

当 `InteractiveMode` 设置为 `false` 时：
- 达到 `MaxRounds` 后自动终止
- 不会显示确认提示
- 适用于自动化脚本和批处理任务

### 安全保护

- **绝对最大轮数**：即使用户持续选择继续，也不会超过 `AbsoluteMaxRounds`
- **无限循环保护**：防止 LLM 陷入无限调用循环
- **资源限制**：避免过度消耗 API 配额和计算资源

## 最佳实践

### 1. 合理设置最大轮数

- **简单任务**：`MaxRounds = 3`，适合单步或两步工具调用
- **复杂任务**：`MaxRounds = 5-10`，适合多步骤工作流
- **自动化任务**：设置较高的 `MaxRounds`，并禁用 `InteractiveMode`

### 2. 使用命名空间组织技能

```
skills/
├── personal/          # 个人生产力
│   ├── greeting.md
│   └── reminder.md
├── productivity/      # 工作任务
│   ├── task.md
│   └── meeting.md
└── utilities/         # 工具类
    ├── time.md
    └── calculator.md
```

调用时使用完整路径：
```
@personal:greeting user_name='张三'
@productivity:task title='完成报告'
```

### 3. 提供清晰的技能描述

**好的描述**：
```yaml
description: 向用户发送个性化问候，支持不同时间段的问候语
```

**不好的描述**：
```yaml
description: 问候  # 太简短，LLM 难以理解用途
```

### 4. 善用默认值

为非必需参数提供合理的默认值，减少 LLM 的调用复杂度：

```yaml
parameters:
  - name: priority
    type: string
    required: false
    default_value: medium
  - name: is_urgent
    type: bool
    required: false
    default_value: false
```

### 5. 监控和日志

- 定期查看日志，了解 Tool Calling 的使用情况
- 注意是否频繁达到最大轮数限制
- 根据实际使用调整配置参数

### 6. 测试和验证

**单元测试**：测试单个技能的执行
```bash
dotnet test --filter "TestCategory=Skills"
```

**集成测试**：测试 Tool Calling 循环
```bash
dotnet test --filter "TestCategory=ToolCalling"
```

**手动验证**：使用不同场景测试
```
# 简单调用
@greeting user_name='测试用户'

# 复杂任务
帮我安排明天的行程：早上9点开会，下午2点提交报告
```

## 故障排除

### 工具未被调用

**可能原因**：
- `Enabled` 设置为 `false`
- 技能描述不清晰，LLM 无法理解
- 参数定义错误

**解决方法**：
1. 检查 `appsettings.json` 中的 `ToolCalling.Enabled`
2. 改进技能的 `description` 和参数说明
3. 查看日志，确认技能是否成功加载

### 达到最大轮数

**可能原因**：
- 任务过于复杂，需要更多轮次
- LLM 陷入重复调用循环
- `MaxRounds` 设置过低

**解决方法**：
1. 增加 `MaxRounds` 的值
2. 检查是否有工具返回了错误，导致 LLM 重试
3. 优化技能设计，减少依赖步骤

### 调用参数错误

**可能原因**：
- 参数类型不匹配
- 必需参数缺失
- 参数值格式错误

**解决方法**：
1. 检查参数定义中的 `type` 和 `required` 字段
2. 提供清晰的 `description` 帮助 LLM 理解参数用途
3. 使用默认值避免缺失参数

## 相关文档

- [技能系统指南](./SKILLS_GUIDE.md) - 如何创建和管理技能
- [配置说明](../README.md#配置) - 完整配置选项
- [API 参考](../README.md#api-参考) - 编程接口文档

## 示例和模板

更多示例和技能模板，请参考：
- [技能示例目录](../skills/) - 预定义技能集合
- [集成测试](../tests/GeneralAgent.Integration.Tests/) - 端到端测试用例
