#!/bin/bash
set -e

echo "========================================="
echo "Subagent Integration 验收测试"
echo "========================================="

echo ""
echo "Step 1: 构建项目"
cargo build --release

echo ""
echo "Step 2: 运行单元测试"
cargo test

echo ""
echo "Step 3: 启动 TUI（需要手工验证）"
echo ""
echo "请按照以下步骤手工验证："
echo "1. 启动 TUI: cargo run --bin agent-tui"
echo "2. 创建会话并执行命令: /subagent start \"测试任务1\" \"测试任务2\""
echo "3. 按 Ctrl+S 打开 Subagent Monitor"
echo "4. 验证显示两个子代理任务"
echo "5. 按 Tab 切换到全局视图"
echo "6. 按 Up/Down 导航列表"
echo "7. 按 Esc 关闭 Monitor"
echo ""
echo "验收标准："
echo "✅ 命令解析正确"
echo "✅ 子代理在后台执行"
echo "✅ Ctrl+S 可以切换 overlay 可见性"
echo "✅ Tab 可以切换视图模式"
echo "✅ Up/Down 可以导航列表"
echo "✅ 状态颜色正确显示"
echo "✅ Esc 可以关闭 overlay"
echo ""

echo ""
echo "========================================="
echo "验收测试完成！"
echo "========================================="
