#!/bin/bash
# General Agent V2 TUI 启动脚本

set -e

echo "🚀 General Agent V2 TUI 启动中..."
echo ""

# 检查可执行文件
if [ ! -f "../../target/release/examples/tui_demo" ]; then
    echo "❌ 错误: 找不到可执行文件"
    echo "请先运行: cargo build -p agent-tui --example tui_demo --release"
    exit 1
fi

# 检查 Ollama 是否运行
if ! curl -s http://localhost:11434/api/tags > /dev/null 2>&1; then
    echo "⚠️  警告: Ollama 似乎未运行"
    echo "请先启动 Ollama: ollama serve"
    echo "并拉取模型: ollama pull qwen3.5:0.8b"
    echo ""
    read -p "是否继续？(y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# 检查模型是否存在
if ! curl -s http://localhost:11434/api/tags | grep -q "qwen3.5:0.8b"; then
    echo "⚠️  警告: 未找到 qwen3.5:0.8b 模型"
    echo "请运行: ollama pull qwen3.5:0.8b"
    echo ""
    read -p "是否继续？(y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

echo "✅ 环境检查通过"
echo ""
echo "📖 快捷键说明:"
echo "  Ctrl+C / Ctrl+Q  - 退出应用"
echo "  Tab              - 切换焦点"
echo "  j/k 或 ↓/↑      - 导航会话"
echo "  Enter            - 选择会话"
echo "  n                - 新建会话"
echo "  d                - 删除会话"
echo ""
echo "🎯 启动 TUI..."
echo ""

# 运行 TUI
exec ../../target/release/examples/tui_demo
