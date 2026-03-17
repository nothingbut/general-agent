---
name: format
description: 格式化文本内容
parameters:
  - name: text
    type: string
    required: true
    description: 要格式化的文本
  - name: format_type
    type: string
    required: true
    description: 格式类型（uppercase/lowercase/title/sentence）
  - name: trim_whitespace
    type: bool
    required: false
    description: 是否去除首尾空格
    default_value: true
---

# 📝 文本格式化

**原始文本**:
```
{{ text }}
```

**格式化结果**:
```
{{ if trim_whitespace }}
{{ $trimmed = text | string.strip }}
{{ else }}
{{ $trimmed = text }}
{{ end }}

{{ if format_type == "uppercase" }}
{{ $trimmed | string.upcase }}
{{ else if format_type == "lowercase" }}
{{ $trimmed | string.downcase }}
{{ else if format_type == "title" }}
{{ $trimmed | string.capitalize }}
{{ else if format_type == "sentence" }}
{{ $trimmed | string.capitalizewords }}
{{ else }}
{{ $trimmed }}
{{ end }}
```

---

✅ 文本已按 **{{ format_type }}** 格式处理完成。
