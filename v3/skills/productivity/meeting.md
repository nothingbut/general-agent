---
name: meeting
description: 安排团队会议
parameters:
  - name: title
    type: string
    required: true
    description: 会议主题
  - name: date
    type: string
    required: true
    description: 会议日期
  - name: time
    type: string
    required: true
    description: 会议时间
  - name: duration
    type: int
    required: false
    description: 会议时长（分钟）
    default_value: 60
  - name: participants
    type: array
    required: false
    description: 参会人员列表
  - name: agenda
    type: array
    required: false
    description: 会议议程列表
  - name: location
    type: string
    required: false
    description: 会议地点或在线链接
---

# 📅 会议安排：{{ title }}

## 会议信息
**日期**: {{ date }}
**时间**: {{ time }}
**时长**: {{ duration }} 分钟

{{ if location }}
**地点**: {{ location }}
{{ end }}

{{ if participants && participants.size > 0 }}
## 参会人员
{{ for participant in participants }}
- {{ participant }}
{{ end }}
{{ end }}

{{ if agenda && agenda.size > 0 }}
## 会议议程
{{ for item in agenda }}
{{ for.index + 1 }}. {{ item }}
{{ end }}
{{ end }}

---

{{ if participants }}
会议邀请已发送给 {{ participants.size }} 位参会人员。
{{ else }}
会议已创建，请添加参会人员。
{{ end }}
