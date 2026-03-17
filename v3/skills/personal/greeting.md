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
