#!/usr/bin/env python3
"""快速诊断配置脚本"""
import os
import sys
from dotenv import load_dotenv

print("=" * 60)
print("General Agent 配置诊断")
print("=" * 60)
print()

# 加载 .env
load_dotenv()

# 检查 .env 文件
if os.path.exists('.env'):
    print("✅ .env 文件存在")
else:
    print("❌ .env 文件不存在")
    sys.exit(1)

print()
print("环境变量读取:")
print("-" * 60)

# 读取配置
use_ollama = os.getenv("USE_OLLAMA", "false")
ollama_url = os.getenv("OLLAMA_BASE_URL", "http://localhost:11434")
ollama_model = os.getenv("OLLAMA_MODEL", "llama3.2:latest")
ollama_temp = os.getenv("OLLAMA_TEMPERATURE", "0.7")

print(f"USE_OLLAMA       = {use_ollama}")
print(f"OLLAMA_BASE_URL  = {ollama_url}")
print(f"OLLAMA_MODEL     = {ollama_model}")
print(f"OLLAMA_TEMPERATURE = {ollama_temp}")

print()
print("配置解析:")
print("-" * 60)

use_ollama_bool = use_ollama.lower() == "true"
print(f"使用 Ollama?     = {use_ollama_bool}")

if use_ollama_bool:
    print("✅ 系统将使用 Ollama 本地模型")
else:
    print("⚠️  系统将使用 Mock 客户端（测试模式）")

print()
print("Ollama 服务检查:")
print("-" * 60)

# 检查 Ollama 服务
import urllib.request
try:
    response = urllib.request.urlopen(ollama_url, timeout=2)
    print(f"✅ Ollama 服务运行中: {ollama_url}")
except Exception as e:
    print(f"❌ 无法连接到 Ollama: {e}")
    if use_ollama_bool:
        print()
        print("请启动 Ollama 服务: ollama serve")
        sys.exit(1)

print()
print("模型检查:")
print("-" * 60)

# 检查模型
import subprocess
try:
    result = subprocess.run(
        ["ollama", "list"],
        capture_output=True,
        text=True,
        timeout=5
    )

    if ollama_model in result.stdout:
        print(f"✅ 模型已安装: {ollama_model}")
    else:
        print(f"❌ 模型未找到: {ollama_model}")
        print()
        print(f"请下载模型: ollama pull {ollama_model}")
        print()
        print("已安装的模型:")
        print(result.stdout)
        sys.exit(1)

except FileNotFoundError:
    print("❌ ollama 命令不可用，请安装 Ollama")
    sys.exit(1)
except Exception as e:
    print(f"⚠️  无法检查模型: {e}")

print()
print("=" * 60)
print("✅ 所有配置检查通过！")
print("=" * 60)
print()
print("启动命令:")
print("  uvicorn src.main:app --reload --port 8000 --log-level info")
print()
