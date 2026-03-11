# Ollama 本地模型配置指南

**更新日期：** 2026-03-04
**适用版本：** General Agent v0.2+

---

## 📋 概述

General Agent 已集成 Ollama 支持，可以使用本地 LLM 模型，无需 OpenAI API。

**优势：**
- ✅ 完全本地运行，无需 API 密钥
- ✅ 数据隐私保护
- ✅ 无使用成本
- ✅ 支持离线使用
- ✅ 多种开源模型可选

---

## 1️⃣ 安装 Ollama

### macOS

```bash
# 方法1：使用官方安装程序
# 访问 https://ollama.ai/download
# 下载 macOS 安装包并安装

# 方法2：使用 Homebrew
brew install ollama
```

### Linux

```bash
# 一键安装脚本
curl -fsSL https://ollama.ai/install.sh | sh
```

### Windows

```bash
# 访问 https://ollama.ai/download
# 下载 Windows 安装包并安装
```

### 验证安装

```bash
# 检查 Ollama 是否安装成功
ollama --version

# 输出示例: ollama version is 0.1.17
```

---

## 2️⃣ 启动 Ollama 服务

### 启动服务

```bash
# 启动 Ollama 服务（会在后台运行）
ollama serve
```

**注意：** Ollama 服务默认监听在 `http://localhost:11434`

### 验证服务运行

```bash
# 检查服务状态
curl http://localhost:11434

# 预期输出: "Ollama is running"
```

---

## 3️⃣ 下载模型

### 推荐模型

根据你的硬件选择合适的模型：

| 模型 | 大小 | 内存需求 | 适用场景 | 下载命令 |
|------|------|----------|---------|---------|
| **llama3.2:1b** | ~1GB | 2GB+ | 低配置、快速响应 | `ollama pull llama3.2:1b` |
| **llama3.2:3b** ⭐ | ~2GB | 4GB+ | **推荐**，平衡性能 | `ollama pull llama3.2:3b` |
| **llama3.2:latest** | ~2GB | 4GB+ | 默认选项 | `ollama pull llama3.2:latest` |
| **llama3.1:8b** | ~4.7GB | 8GB+ | 高质量回答 | `ollama pull llama3.1:8b` |
| **qwen2.5:latest** | ~4.7GB | 8GB+ | 中文优化 | `ollama pull qwen2.5:latest` |
| **gemma2:2b** | ~1.6GB | 3GB+ | Google开源 | `ollama pull gemma2:2b` |

### 下载模型

```bash
# 下载推荐模型（llama3.2 3B版本）
ollama pull llama3.2:3b

# 或下载默认版本
ollama pull llama3.2:latest

# 下载中文优化模型
ollama pull qwen2.5:latest
```

### 查看已下载模型

```bash
# 列出本地所有模型
ollama list

# 输出示例:
# NAME                    ID              SIZE    MODIFIED
# llama3.2:3b            a80c4f17acd5    2.0 GB  2 hours ago
# qwen2.5:latest         f6db76e38559    4.7 GB  1 day ago
```

### 测试模型

```bash
# 交互式测试模型
ollama run llama3.2:3b

# 输入测试消息
>>> Hello, how are you?
```

按 `Ctrl+D` 或输入 `/bye` 退出。

---

## 4️⃣ 配置 General Agent

### 步骤1：创建环境配置文件

```bash
# 复制示例配置文件
cp .env.example .env

# 编辑配置文件
# macOS/Linux
nano .env

# 或使用你喜欢的编辑器
code .env
```

### 步骤2：编辑 .env 文件

```bash
# ================================
# LLM 配置
# ================================

# 启用 Ollama
USE_OLLAMA=true

# Ollama 服务器地址（默认）
OLLAMA_BASE_URL=http://localhost:11434

# 选择模型（使用你已下载的模型）
OLLAMA_MODEL=llama3.2:3b

# 温度参数（0.0-1.0，推荐0.7）
OLLAMA_TEMPERATURE=0.7
```

### 步骤3：启动 General Agent

```bash
# 确保 Ollama 服务正在运行
# 然后启动 General Agent
uvicorn src.main:app --reload --port 8000
```

### 步骤4：验证配置

检查启动日志，应该看到：

```
INFO:     Using Ollama client with model: llama3.2:3b
INFO:     Application startup complete.
```

---

## 5️⃣ 测试集成

### 使用 Web 界面

1. 访问 http://localhost:8000
2. 输入消息："Hello, tell me about yourself"
3. 等待响应（首次可能较慢）

### 使用 API

```bash
# 发送测试请求
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello, what is your name?", "session_id": "test"}' \
  | jq '.response'
```

### 预期结果

应该收到 Ollama 模型生成的实际回复（而不是 Mock 客户端的"我收到了你的消息..."）。

---

## 6️⃣ 高级配置

### 6.1 切换模型

**实时切换模型：**

```bash
# 停止服务器（Ctrl+C）

# 修改 .env 文件
OLLAMA_MODEL=qwen2.5:latest

# 重启服务器
uvicorn src.main:app --reload --port 8000
```

### 6.2 调整温度参数

**温度参数说明：**
- `0.0-0.3`：非常确定性，适合事实性问答
- `0.4-0.6`：平衡，适合大多数场景
- `0.7-0.9`：更有创造性，适合写作和头脑风暴
- `0.9-1.0`：高度随机，适合创意生成

**示例：**

```bash
# 用于严谨的技术问答
OLLAMA_TEMPERATURE=0.3

# 用于创意写作
OLLAMA_TEMPERATURE=0.8
```

### 6.3 远程 Ollama 服务器

如果 Ollama 运行在其他机器：

```bash
# .env 配置
OLLAMA_BASE_URL=http://192.168.1.100:11434
```

### 6.4 性能优化

**GPU 加速（如果有 NVIDIA GPU）：**

Ollama 会自动检测并使用 GPU。检查日志：

```bash
# 查看 Ollama 日志
journalctl -u ollama -f  # Linux
```

**内存不足优化：**

```bash
# 使用更小的模型
OLLAMA_MODEL=llama3.2:1b

# 或量化版本（如果可用）
OLLAMA_MODEL=llama3.2:3b-q4_0
```

---

## 7️⃣ 常见问题排查

### 问题1：连接 Ollama 失败

**错误信息：**
```
Failed to connect to Ollama at http://localhost:11434
```

**解决方案：**
```bash
# 1. 检查 Ollama 服务是否运行
curl http://localhost:11434

# 2. 如果未运行，启动服务
ollama serve

# 3. 检查端口占用
lsof -i :11434
```

---

### 问题2：模型未找到

**错误信息：**
```
model 'llama3.2:3b' not found
```

**解决方案：**
```bash
# 1. 列出本地模型
ollama list

# 2. 下载缺失的模型
ollama pull llama3.2:3b

# 3. 验证模型可用
ollama run llama3.2:3b
```

---

### 问题3：响应速度慢

**原因：**
- 首次运行需要加载模型
- 模型太大，硬件性能不足
- CPU 运行（无 GPU 加速）

**解决方案：**
```bash
# 1. 使用更小的模型
OLLAMA_MODEL=llama3.2:1b

# 2. 首次加载后，后续响应会快很多（模型缓存在内存中）

# 3. 考虑升级硬件或使用云端 GPU 实例
```

---

### 问题4：内存不足

**错误信息：**
```
Out of memory
```

**解决方案：**
```bash
# 1. 使用更小的模型
ollama pull llama3.2:1b
OLLAMA_MODEL=llama3.2:1b

# 2. 关闭其他占用内存的程序

# 3. 考虑使用量化模型（更小但质量略降）
```

---

### 问题5：Mac M1/M2 性能问题

**优化建议：**
```bash
# M1/M2 芯片对 Ollama 有很好的支持
# 确保使用最新版本的 Ollama
brew upgrade ollama

# 推荐模型（针对 Apple Silicon 优化）
OLLAMA_MODEL=llama3.2:3b  # 良好性能
OLLAMA_MODEL=llama3.1:8b  # 16GB+ 内存
```

---

## 8️⃣ 切换回 Mock 模式

如果需要暂时禁用 Ollama（用于测试或调试）：

```bash
# 修改 .env
USE_OLLAMA=false

# 重启服务器
# 系统会使用 Mock 客户端
```

---

## 9️⃣ 模型对比和选择

### 性能对比

| 模型 | 响应速度 | 回答质量 | 中文能力 | 推荐场景 |
|------|---------|---------|---------|---------|
| llama3.2:1b | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | 低配置、快速原型 |
| llama3.2:3b | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | **推荐**，日常使用 |
| llama3.1:8b | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | 高质量回答 |
| qwen2.5:latest | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | 中文场景 |
| gemma2:2b | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | Google 技术栈 |

### 选择建议

**开发测试：**
- llama3.2:1b（最快，适合频繁测试）

**日常使用：**
- llama3.2:3b（平衡性能和质量）

**高质量需求：**
- llama3.1:8b（英文）
- qwen2.5:latest（中文）

**有限硬件：**
- llama3.2:1b（< 4GB 内存）
- gemma2:2b（< 4GB 内存）

---

## 🔟 完整示例

### 完整配置流程

```bash
# 1. 安装 Ollama
brew install ollama  # macOS

# 2. 启动 Ollama 服务
ollama serve &

# 3. 下载模型
ollama pull llama3.2:3b

# 4. 配置 General Agent
cd /path/to/general-agent
cp .env.example .env
nano .env  # 设置 USE_OLLAMA=true, OLLAMA_MODEL=llama3.2:3b

# 5. 启动 General Agent
uvicorn src.main:app --reload --port 8000

# 6. 测试（新终端）
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello!", "session_id": "test"}' \
  | jq '.response'
```

---

## 📚 参考资源

- **Ollama 官网**：https://ollama.ai/
- **Ollama GitHub**：https://github.com/ollama/ollama
- **模型库**：https://ollama.ai/library
- **Llama 模型**：https://ai.meta.com/llama/
- **通义千问**：https://github.com/QwenLM/Qwen

---

## 🆘 获取帮助

如有问题：
1. 查看 Ollama 日志：`ollama logs`
2. 查看 General Agent 日志（终端输出）
3. 提交 Issue 到项目仓库

---

**配置完成后，享受完全本地化的 AI 助手体验！** 🎉
