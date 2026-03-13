# Ollama 配置指南

## 快速开始

### 1. 确保 Ollama 运行
```bash
# 检查 Ollama 是否运行
curl http://localhost:11434/api/tags

# 如果未运行，启动 Ollama
ollama serve
```

### 2. 下载模型（如果还没有）
```bash
# 推荐模型
ollama pull qwen2.5:7b        # 中文友好，7B 参数
ollama pull qwen2.5:14b       # 更强，14B 参数
ollama pull llama3.2          # 英文好
ollama pull mistral           # 快速轻量

# 查看已下载的模型
ollama list
```

### 3. 配置 General Agent
编辑 `config.yaml`:
```yaml
llm:
  provider: "ollama"
  ollama:
    base_url: "http://localhost:11434"
    model: "qwen2.5:7b"  # 改成你的模型名
    timeout: 120
```

### 4. 安装依赖
```bash
pip install httpx pyyaml
```

### 5. 启动 Agent
```bash
# 测试连接
python -c "
from src.core.ollama_client import OllamaClient
import asyncio
client = OllamaClient()
result = asyncio.run(client.chat([{'role':'user','content':'你好'}]))
print(result)
"

# 启动 TUI
python -m src.cli.app
```

## 配置说明

**config.yaml 参数：**
- `base_url`: Ollama 服务地址（默认 localhost:11434）
- `model`: 模型名称（必须是已下载的模型）
- `timeout`: 请求超时时间（秒）

**推荐模型选择：**
- **中文对话**: qwen2.5:7b 或 qwen2.5:14b
- **英文对话**: llama3.2
- **快速响应**: mistral
- **代码生成**: qwen2.5-coder 或 deepseek-coder

## 常见问题

### 1. 连接失败
```bash
# 检查 Ollama 是否运行
curl http://localhost:11434/api/tags

# 重启 Ollama
killall ollama
ollama serve
```

### 2. 模型未找到
```bash
# 查看可用模型
ollama list

# 下载模型
ollama pull <模型名>
```

### 3. 响应慢
- 减小模型大小（如用 7b 代替 14b）
- 增加 timeout 值
- 检查 CPU/GPU 资源

## 测试脚本

```bash
# 测试 Ollama 连接
python << 'EOF'
import asyncio
from src.core.ollama_client import OllamaClient

async def test():
    client = OllamaClient(
        base_url="http://localhost:11434",
        model="qwen2.5:7b"
    )
    response = await client.chat([
        {"role": "user", "content": "你好，请介绍一下你自己"}
    ])
    print(response)

asyncio.run(test())
EOF
```

## 切换回 Mock 模式

如果遇到问题，可以临时切回 Mock：
```yaml
llm:
  provider: "mock"  # 改成 mock
```
