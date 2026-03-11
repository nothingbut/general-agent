#!/bin/bash

# 技能系统集成测试脚本

echo "=== 技能系统集成测试 ==="
echo ""

cd /Users/shichang/Workspace/program/python/general-agent/v2

# 1. 创建测试会话
echo "1. 创建测试会话..."
SESSION_ID=$(./target/release/agent new --title "技能测试" | grep "ID:" | awk '{print $2}')
echo "   会话 ID: $SESSION_ID"
echo ""

# 2. 显示测试说明
echo "2. 测试技能调用"
echo "   运行以下命令进入对话模式："
echo "   ./target/release/agent --skills-dir ./crates/agent-skills/examples/test_skills chat $SESSION_ID"
echo ""
echo "   在对话中尝试以下命令："
echo "   - @greeting user_name='Alice'    # 测试技能调用"
echo "   - /greeting user_name='Bob'      # 测试 / 语法"
echo "   - Hello, how are you?            # 测试普通消息"
echo "   - exit                            # 退出"
echo ""
echo "=== 注意：--skills-dir 必须放在子命令之前 ==="
