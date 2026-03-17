---
name: calculate
description: 执行数学计算
parameters:
  - name: expression
    type: string
    required: true
    description: 数学表达式（支持加减乘除）
  - name: show_steps
    type: bool
    required: false
    description: 是否显示计算步骤
    default_value: false
---

# 🧮 计算结果

{{ if show_steps }}
**计算表达式**: {{ expression }}

## 计算步骤
正在计算 `{{ expression }}`...

（注：实际计算需要调用 LLM 或外部计算服务）
{{ else }}
计算表达式：`{{ expression }}`
{{ end }}

---

💡 提示：设置 `show_steps=true` 可以查看详细的计算步骤。
