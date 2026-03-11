# API文档

## 基础信息

- Base URL: `http://localhost:8000`
- Content-Type: `application/json`

## 端点

### 1. 健康检查

```
GET /health
```

响应：
```json
{
  "status": "ok"
}
```

### 2. 聊天接口

```
POST /api/chat
```

请求体：
```json
{
  "message": "用户输入（必需）",
  "session_id": "会话ID（可选）"
}
```

响应：
```json
{
  "response": "助手回复",
  "session_id": "会话ID",
  "plan_type": "simple_query"
}
```

**说明：**
- 如果不提供`session_id`，系统会自动生成一个新会话
- 相同`session_id`的请求会共享上下文
- `plan_type`表示执行计划类型（当前仅支持`simple_query`）

## 错误处理

所有错误响应格式：
```json
{
  "detail": "错误描述"
}
```

HTTP状态码：
- `200` - 成功
- `400` - 请求参数错误
- `500` - 服务器内部错误

## 示例

### Python

```python
import requests

response = requests.post(
    "http://localhost:8000/api/chat",
    json={
        "message": "Hello",
        "session_id": "my-session"
    }
)
print(response.json())
```

### JavaScript

```javascript
fetch('http://localhost:8000/api/chat', {
    method: 'POST',
    headers: {
        'Content-Type': 'application/json'
    },
    body: JSON.stringify({
        message: 'Hello',
        session_id: 'my-session'
    })
})
.then(res => res.json())
.then(data => console.log(data));
```

### cURL

```bash
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello", "session_id": "my-session"}'
```
