# V2 Phase 2 验收测试

**日期**: 2026-03-13
**版本**: v0.2.0
**状态**: Phase 2 完成验收

---

## 前置条件

### 1. 安装 Rust
```bash
rustc --version  # 需要 1.75+
```

### 2. 构建项目
```bash
cd v2/
cargo build --release
```

### 3. 启动依赖服务

**Ollama（必需）**:
```bash
ollama pull qwen3.5:0.8b        # LLM 模型
ollama pull nomic-embed-text    # RAG Embedding
ollama serve                    # 启动服务
```

**Qdrant（RAG 测试需要）**:
```bash
docker run -d --name qdrant \
  -p 6333:6333 -p 6334:6334 \
  qdrant/qdrant
```

---

## 测试 1: 基本功能

### 1.1 创建会话
```bash
./target/release/agent new --title "测试会话"
# 预期: 输出新会话 ID
```

### 1.2 列出会话
```bash
./target/release/agent list
# 预期: 显示会话列表，包含刚创建的会话
```

### 1.3 简单对话
```bash
SESSION_ID=<上一步的ID>
./target/release/agent chat $SESSION_ID
# 输入: "你好，请介绍一下自己"
# 预期: LLM 正常回复
# 按 Ctrl+D 退出
```

### 1.4 流式响应
```bash
./target/release/agent --provider ollama chat $SESSION_ID
# 输入: "讲一个简短的故事"
# 预期: 逐字流式输出，无卡顿
# 按 Ctrl+D 退出
```

### 1.5 搜索会话
```bash
./target/release/agent search "测试"
# 预期: 找到包含"测试"的会话
```

### 1.6 删除会话
```bash
./target/release/agent delete $SESSION_ID
# 预期: 会话被删除，list 中不再显示
```

**✅ 通过标准**: 所有命令正常执行，无错误

---

## 测试 2: Skills 系统

### 2.1 准备测试技能
```bash
mkdir -p /tmp/test_skills
cat > /tmp/test_skills/greeting.md << 'EOF'
---
name: greeting
description: 向用户问候
parameters:
  - name: user_name
    type: string
    required: true
---

你好 {user_name}！欢迎使用 General Agent V2！
EOF
```

### 2.2 测试技能调用
```bash
./target/release/agent new --title "技能测试"
SESSION_ID=<新会话ID>

./target/release/agent --skills-dir /tmp/test_skills chat $SESSION_ID
# 输入: @greeting user_name='Alice'
# 预期: 输出 "你好 Alice！欢迎使用 General Agent V2！"
```

### 2.3 测试技能参数验证
```bash
# 在同一会话中输入: @greeting
# 预期: 提示缺少必需参数 user_name
```

**✅ 通过标准**: 技能正确加载和执行，参数验证生效

---

## 测试 3: MCP 集成

### 3.1 准备 MCP 配置
```bash
cat > /tmp/mcp-config.json << 'EOF'
{
  "servers": [
    {
      "name": "echo",
      "command": "python3",
      "args": ["-c", "import sys, json; [print(json.dumps({'jsonrpc':'2.0','id':msg.get('id'),'result':'Echo: '+str(msg.get('params',{}))})) for msg in [json.loads(line) for line in sys.stdin]]"]
    }
  ]
}
EOF
```

### 3.2 测试 MCP 连接
```bash
./target/release/agent new --title "MCP测试"
SESSION_ID=<新会话ID>

# 注意: 当前 CLI 可能不支持 --mcp 参数，需要通过代码测试
# 运行集成测试代替:
cd v2/
cargo test -p agent-mcp test_stdio_transport -- --ignored
```

**✅ 通过标准**: MCP 客户端能连接服务器，调用工具成功

---

## 测试 4: RAG 系统

### 4.1 准备测试文档
```bash
mkdir -p /tmp/test_docs
cat > /tmp/test_docs/rust_intro.md << 'EOF'
# Rust 编程语言

Rust 是一门系统编程语言，专注于安全性、速度和并发性。

## 特性
- 内存安全：无需垃圾回收
- 零成本抽象：高性能
- 并发安全：防止数据竞争
EOF
```

### 4.2 索引文档（通过测试代码）
```bash
cd v2/

# 运行 RAG 集成测试
cargo test -p agent-rag test_full_rag_pipeline -- --ignored --nocapture
```

### 4.3 验证检索功能
```bash
# 检查测试输出中的检索结果
# 预期: 能找到相关文档片段，相似度分数合理
```

**✅ 通过标准**: 文档索引成功，检索返回相关结果

---

## 测试 5: TUI 界面

### 5.1 启动 TUI Demo
```bash
cd v2/crates/agent-tui
RUST_LOG=info cargo run --example tui_demo
```

### 5.2 基本操作
```
1. 应该看到会话列表和聊天窗口的分栏布局
2. 按 'n' 创建新会话
3. 输入消息并按 Enter 发送
4. 观察流式响应实时显示
5. 按 'j'/'k' 导航会话列表
6. 按 Enter 切换到不同会话
7. 按 Ctrl+Q 退出
```

### 5.3 Subagent 监控
```
1. 在输入框输入: /subagent start "任务1" "任务2"
2. 按 Ctrl+S 打开 Subagent Overlay
3. 观察子代理状态（Pending/Running/Completed）
4. 按 Tab 切换视图（CurrentSession ↔ Global）
5. 按 Up/Down 浏览列表
6. 按 Esc 关闭 Overlay
```

**✅ 通过标准**:
- UI 渲染正常，无闪烁
- 快捷键全部响应
- 流式响应流畅
- Subagent Overlay 正常工作

---

## 测试 6: Workflow/Subagent

### 6.1 测试 Subagent 命令解析
```bash
cd v2/
cargo test -p agent-workflow test_parse_subagent_command -- --nocapture
```

### 6.2 测试 Subagent 创建
```bash
# 通过 TUI 测试（见测试 5.3）
# 或运行单元测试
cargo test -p agent-workflow subagent
```

**✅ 通过标准**: 子代理会话正确创建，状态正确记录

---

## 完整性检查

### 运行所有单元测试
```bash
cd v2/
cargo test
# 预期: 所有测试通过（约 53 个测试）
```

### 运行集成测试（需要服务）
```bash
# 确保 Ollama 和 Qdrant 运行
cargo test -- --ignored --nocapture
# 预期: RAG 和 MCP 集成测试通过
```

### 代码检查
```bash
cargo fmt --check    # 代码格式
cargo clippy         # Linter 检查
# 预期: 无错误和警告
```

---

## 性能检查

### 启动时间
```bash
time ./target/release/agent list
# 预期: < 100ms
```

### 内存占用
```bash
# 运行 TUI
./target/release/agent-tui &
PID=$!
ps aux | grep $PID
# 预期: < 50MB (空闲状态)
kill $PID
```

---

## 验收标准

### 必须通过（P0）
- [ ] 所有单元测试通过
- [ ] 基本功能（会话管理、对话）正常
- [ ] Skills 系统加载和执行正常
- [ ] TUI 界面启动并正常运行
- [ ] 流式响应无卡顿

### 应该通过（P1）
- [ ] MCP 集成测试通过（需要 MCP 服务器）
- [ ] RAG 集成测试通过（需要 Qdrant）
- [ ] Subagent 功能正常
- [ ] 无明显性能问题

### 建议通过（P2）
- [ ] Clippy 无警告
- [ ] 代码覆盖率 > 80%
- [ ] 文档完整

---

## 已知限制

1. **CLI 限制**: 当前 CLI 不支持直接测试 MCP 和 RAG（需通过集成测试）
2. **TUI 输入**: 暂不支持多行输入
3. **Subagent**: 只读监控，无取消/暂停功能

---

## 快速验收脚本

```bash
#!/bin/bash
set -e

echo "🚀 V2 Phase 2 快速验收测试"

# 1. 构建
echo "📦 构建项目..."
cd v2/
cargo build --release

# 2. 单元测试
echo "🧪 运行单元测试..."
cargo test

# 3. 基本功能
echo "✨ 测试基本功能..."
SESSION_ID=$(./target/release/agent new --title "验收测试" | grep -oE '[0-9a-f-]{36}')
echo "创建会话: $SESSION_ID"

./target/release/agent list | grep "$SESSION_ID" && echo "✅ 会话列表正常"

echo "你好" | ./target/release/agent chat $SESSION_ID && echo "✅ 对话功能正常"

./target/release/agent delete $SESSION_ID && echo "✅ 删除功能正常"

# 4. TUI 烟雾测试（需要手动）
echo "⚠️  请手动运行 TUI 测试:"
echo "   cd v2/crates/agent-tui && cargo run --example tui_demo"

echo ""
echo "🎉 快速验收测试完成！"
echo "📝 完整测试请参考 ACCEPTANCE_TEST.md"
```

保存为 `v2/quick_acceptance_test.sh` 并运行：
```bash
chmod +x v2/quick_acceptance_test.sh
./v2/quick_acceptance_test.sh
```

---

**验收结论**:
- 通过快速测试 → ✅ Phase 2 验收通过
- 发现问题 → 记录到 GitHub Issues，评估是否阻塞发布
