# General Agent V3 - 技能系统用户指南

## 快速开始

### 1. 创建技能文件

在 `skills/` 目录下创建 `.md` 文件：

```markdown
---
name: greeting
description: 向用户问候
parameters:
  - name: user_name
    type: string
    required: true
    description: 用户名称
  - name: time_of_day
    type: string
    required: false
    description: 时间段（morning/afternoon/evening）
    default_value: morning
---

{{ if time_of_day == "morning" }}
早上好，{{ user_name }}！
{{ else if time_of_day == "afternoon" }}
下午好，{{ user_name }}！
{{ else }}
晚上好，{{ user_name }}！
{{ end }}
```

### 2. 调用技能

**@ 语法（推荐）**:
```
@greeting user_name='张三'
@greeting user_name='李四' time_of_day='evening'
```

**/ 语法（命令风格）**:
```
/greeting user_name='张三'
```

**命名空间调用**:
```
@personal:greeting user_name='王五'
```

## 参数类型

| 类型 | 示例 | 说明 |
|------|------|------|
| string | `'Hello'`, `"World"` | 文本 |
| int | `42`, `100` | 整数，可不带引号 |
| bool | `true`, `false` | 布尔值，可不带引号 |
| array | `new[] { "a", "b" }` | 数组（代码传递） |

## Scriban 模板功能

### 条件判断
```scriban
{{ if condition }}
  内容
{{ else }}
  其他
{{ end }}
```

### 循环
```scriban
{{ for item in items }}
  {{ item }}
{{ end }}
```

### 过滤器
```scriban
{{ text | string.upcase }}
{{ text | string.capitalize }}
```

## 目录结构

```
skills/
├── .ignore              # 忽略规则
├── personal/           # 个人技能
├── productivity/       # 工作技能
└── utilities/          # 工具技能
```

## 完整文档

详见：
- Phase 3 完成报告: `V3_PHASE3_COMPLETION_REPORT.md`
- 示例技能: `skills/README.md`
- 验收清单: `V3_PHASE3_UAT_CHECKLIST.md`
