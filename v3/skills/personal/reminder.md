---
name: reminder
description: 创建个人提醒事项
parameters:
  - name: task
    type: string
    required: true
    description: 需要提醒的任务
  - name: time
    type: string
    required: true
    description: 提醒时间（如 5pm, 明天早上）
  - name: is_urgent
    type: bool
    required: false
    description: 是否紧急
    default_value: false
  - name: repeat
    type: string
    required: false
    description: 重复模式（daily/weekly/monthly）
---

{{ if is_urgent }}
⚠️ **紧急提醒** ⚠️
{{ else }}
📝 提醒事项
{{ end }}

**任务**: {{ task }}
**时间**: {{ time }}
{{ if repeat }}
**重复**: {{ repeat | string.capitalize }}
{{ end }}

{{ if is_urgent }}
这是一个紧急事项，请尽快处理！
{{ else }}
我会在 {{ time }} 提醒你完成这个任务。
{{ end }}
