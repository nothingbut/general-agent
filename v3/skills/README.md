# General Agent V3 - 技能系统示例

## 📚 技能目录结构

```
skills/
├── .ignore                  # 忽略模式配置
├── personal/                # 个人生产力技能
│   ├── greeting.md         # 个性化问候
│   └── reminder.md         # 提醒事项
├── productivity/            # 工作任务管理
│   ├── task.md             # 任务创建
│   └── meeting.md          # 会议安排
└── utilities/               # 实用工具
    ├── calculate.md        # 数学计算
    └── format.md           # 文本格式化
```

## 🎯 技能调用语法

### @ 语法（推荐）

```
@greeting user_name='张三' time_of_day='morning'
@personal:reminder task='买牛奶' time='5pm' is_urgent=true
@productivity:task title='Review PR' priority='high'
```

### / 语法（命令风格）

```
/greeting user_name='李四'
/task title='Fix bug' priority='critical' tags=['bug','urgent']
/meeting title='Sprint Planning' date='2026-03-20' time='10:00'
```

## 📝 技能文件格式

每个技能文件由两部分组成：

### 1. YAML Frontmatter（参数定义）

```yaml
---
name: skill_name
description: 技能描述
parameters:
  - name: param_name
    type: string|int|bool|array
    required: true|false
    description: 参数说明
    default_value: 默认值
---
```

### 2. Scriban 模板（提示词内容）

```scriban
{{ if condition }}
  条件内容
{{ else }}
  其他内容
{{ end }}

{{ for item in items }}
  - {{ item }}
{{ end }}

{{ variable | string.upcase }}  # 过滤器
```

## 🔧 参数类型说明

| 类型 | 示例 | 说明 |
|------|------|------|
| `string` | `'Hello'`, `"World"` | 文本字符串 |
| `int` | `42`, `100` | 整数 |
| `bool` | `true`, `false` | 布尔值 |
| `array` | `['a','b','c']` | 字符串数组 |

## 🎨 Scriban 功能展示

### 条件判断

```scriban
{{ if priority == "critical" }}
  🔴 紧急任务
{{ else if priority == "high" }}
  🟠 高优先级
{{ else }}
  🟢 普通任务
{{ end }}
```

### 循环遍历

```scriban
{{ for participant in participants }}
  {{ for.index + 1 }}. {{ participant }}
{{ end }}
```

### 字符串过滤器

```scriban
{{ text | string.upcase }}          # 转大写
{{ text | string.downcase }}        # 转小写
{{ text | string.capitalize }}      # 首字母大写
{{ text | string.strip }}           # 去除首尾空格
```

### 数组操作

```scriban
{{ tags.size }}                     # 数组长度
{{ if tags && tags.size > 0 }}      # 检查数组非空
{{ for tag in tags }}
  #{{ tag }}{{ if !for.last }}, {{ end }}
{{ end }}
{{ end }}
```

## 📋 示例技能说明

### 1. greeting (个性化问候)
- 根据时间段（morning/afternoon/evening）显示不同问候语
- 展示条件判断功能

### 2. reminder (提醒事项)
- 支持紧急标记和重复模式
- 展示布尔参数和条件格式化

### 3. task (任务创建)
- 支持优先级、标签、负责人等属性
- 展示数组参数和循环遍历

### 4. meeting (会议安排)
- 支持参会人员和议程列表
- 展示复杂的数组处理

### 5. calculate (数学计算)
- 数学表达式计算
- 展示布尔参数控制输出

### 6. format (文本格式化)
- 多种格式化类型（大写/小写/标题）
- 展示 Scriban 字符串过滤器

## 🚫 .ignore 文件

`.ignore` 文件使用类似 `.gitignore` 的语法来排除不需要加载的文件：

```
draft_*.md        # 草稿文件
_*.md             # 私有文件
*.tmp.md          # 临时文件
README.md         # 文档文件
test_*.md         # 测试文件
```

## 🔄 技能热加载

技能系统支持热加载，修改技能文件后：
1. 文件监视器会自动检测变化
2. 技能注册表自动重新加载
3. 无需重启应用即可使用新技能

## 🧪 测试技能

```bash
# 运行技能系统测试
dotnet test tests/GeneralAgent.Infrastructure.Skills.Tests/

# 运行集成测试
dotnet test tests/GeneralAgent.Application.Tests/Services/
```

## 📚 更多资源

- [Scriban 语法文档](https://github.com/scriban/scriban/tree/master/doc)
- [技能系统设计文档](../V3_PHASE3_PLAN.md)
- [Phase 3 交接文档](../V3_PHASE3_TASK6_HANDOFF.md)
