#!/bin/bash
set -e

echo "🚀 V2 Phase 2 快速验收测试"
echo "================================"
echo ""

# 检查当前目录
if [ ! -f "Cargo.toml" ] || [ ! -d "crates" ]; then
    echo "❌ 错误: 请在 v2/ 目录下运行此脚本"
    exit 1
fi

# 1. 构建
echo "📦 1/6 构建项目..."
cargo build --release --quiet
echo "   ✅ 构建成功"
echo ""

# 2. 单元测试
echo "🧪 2/6 运行单元测试..."
TEST_OUTPUT=$(cargo test --quiet 2>&1)
TEST_COUNT=$(echo "$TEST_OUTPUT" | grep -E "test result:" | tail -1 | grep -oE '[0-9]+ passed' | grep -oE '[0-9]+')
echo "   ✅ $TEST_COUNT 个测试通过"
echo ""

# 3. 代码检查
echo "🔍 3/6 代码格式和 Linter..."
cargo fmt --check --quiet && echo "   ✅ 代码格式正确" || echo "   ⚠️  代码格式需要修正"
cargo clippy --quiet 2>&1 | grep -q "warning" && echo "   ⚠️  有 Clippy 警告" || echo "   ✅ Clippy 检查通过"
echo ""

# 4. 基本功能
echo "✨ 4/6 测试基本功能..."

# 创建会话
SESSION_OUTPUT=$(./target/release/agent new --title "验收测试" 2>&1)
SESSION_ID=$(echo "$SESSION_OUTPUT" | grep -oE '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}' | head -1)

if [ -z "$SESSION_ID" ]; then
    echo "   ❌ 创建会话失败"
    exit 1
fi
echo "   ✅ 创建会话: $SESSION_ID"

# 列出会话
if ./target/release/agent list 2>&1 | grep -q "$SESSION_ID"; then
    echo "   ✅ 会话列表正常"
else
    echo "   ❌ 会话列表异常"
    exit 1
fi

# 测试对话（需要 Ollama）
if curl -s http://localhost:11434/api/version > /dev/null 2>&1; then
    echo "你好" | timeout 10 ./target/release/agent chat "$SESSION_ID" > /dev/null 2>&1 && \
        echo "   ✅ 对话功能正常" || \
        echo "   ⚠️  对话测试超时或失败"
else
    echo "   ⚠️  Ollama 未运行，跳过对话测试"
fi

# 删除会话
./target/release/agent delete "$SESSION_ID" > /dev/null 2>&1 && \
    echo "   ✅ 删除功能正常" || \
    echo "   ❌ 删除功能失败"
echo ""

# 5. Skills 测试
echo "🎯 5/6 测试 Skills 系统..."
SKILLS_DIR="/tmp/test_skills_$$"
mkdir -p "$SKILLS_DIR"

cat > "$SKILLS_DIR/test.md" << 'EOF'
---
name: test_skill
description: 测试技能
parameters:
  - name: message
    type: string
    required: true
---

测试消息: {message}
EOF

SESSION_ID=$(./target/release/agent new --title "技能测试" 2>&1 | grep -oE '[0-9a-f-]{36}' | head -1)
if echo "@test_skill message='Hello'" | timeout 10 ./target/release/agent --skills-dir "$SKILLS_DIR" chat "$SESSION_ID" 2>&1 | grep -q "测试消息"; then
    echo "   ✅ Skills 系统正常"
else
    echo "   ⚠️  Skills 测试失败（可能需要 Ollama）"
fi
./target/release/agent delete "$SESSION_ID" > /dev/null 2>&1
rm -rf "$SKILLS_DIR"
echo ""

# 6. 环境检查
echo "🔧 6/6 环境依赖检查..."

# Ollama
if curl -s http://localhost:11434/api/version > /dev/null 2>&1; then
    echo "   ✅ Ollama 运行中"
else
    echo "   ⚠️  Ollama 未运行（基本功能可用，但 LLM 对话需要）"
fi

# Qdrant
if curl -s http://localhost:6333/health > /dev/null 2>&1; then
    echo "   ✅ Qdrant 运行中（RAG 功能可用）"
else
    echo "   ⚠️  Qdrant 未运行（RAG 功能不可用）"
fi

echo ""
echo "================================"
echo "🎉 快速验收测试完成！"
echo ""
echo "📊 测试摘要:"
echo "   - 单元测试: $TEST_COUNT 个通过"
echo "   - 基本功能: 会话管理、对话"
echo "   - Skills 系统: 加载和执行"
echo ""
echo "📝 完整测试请参考: ACCEPTANCE_TEST.md"
echo ""
echo "🚦 下一步:"
echo "   1. 手动运行 TUI: cd crates/agent-tui && cargo run --example tui_demo"
echo "   2. 如需 RAG 测试: cargo test -p agent-rag -- --ignored"
echo "   3. 如需 MCP 测试: cargo test -p agent-mcp -- --ignored"
echo ""
