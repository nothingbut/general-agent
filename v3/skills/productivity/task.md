---
name: task
description: 创建和管理工作任务
parameters:
  - name: title
    type: string
    required: true
    description: 任务标题
  - name: priority
    type: string
    required: false
    description: 优先级（low/medium/high/critical）
    default_value: medium
  - name: assignee
    type: string
    required: false
    description: 任务负责人
  - name: tags
    type: array
    required: false
    description: 任务标签列表
  - name: estimated_hours
    type: int
    required: false
    description: 预计工作时长（小时）
---

# 📋 新任务：{{ title }}

## 基本信息
{{ if priority == "critical" }}
**优先级**: 🔴 {{ priority | string.upcase }}
{{ else if priority == "high" }}
**优先级**: 🟠 {{ priority | string.capitalize }}
{{ else if priority == "medium" }}
**优先级**: 🟡 {{ priority | string.capitalize }}
{{ else }}
**优先级**: 🟢 {{ priority | string.capitalize }}
{{ end }}

{{ if assignee }}
**负责人**: @{{ assignee }}
{{ end }}

{{ if estimated_hours }}
**预计时长**: {{ estimated_hours }} 小时
{{ end }}

{{ if tags && tags.size > 0 }}
**标签**: {{ for tag in tags }}#{{ tag }}{{ if !for.last }} {{ end }}{{ end }}
{{ end }}

---

{{ if priority == "critical" }}
⚠️ 这是一个关键任务，需要立即处理！
{{ else if priority == "high" }}
这个任务优先级较高，请尽快安排时间完成。
{{ else }}
任务已创建，请按计划推进。
{{ end }}
