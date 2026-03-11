#!/bin/bash
# 使用 Ollama 启动服务

echo "启动 general-agent 服务 (使用 Ollama qwen3.5:2b)..."
echo ""

export USE_OLLAMA=true
export OLLAMA_BASE_URL=http://localhost:11434
export OLLAMA_MODEL=qwen3.5:0.8b
export OLLAMA_TIMEOUT=120  # 2分钟超时，适应本地模型

echo "环境变量:"
echo "  USE_OLLAMA=$USE_OLLAMA"
echo "  OLLAMA_MODEL=$OLLAMA_MODEL"
echo "  OLLAMA_BASE_URL=$OLLAMA_BASE_URL"
echo ""

echo "启动服务..."
uvicorn src.main:app --reload --host 0.0.0.0 --port 8000
