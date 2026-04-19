# V2 手工验收测试指南

**版本**: Phase 3 + Phase 4 完成后
**测试数量**: 844 自动化测试 + 本指南的手工验收流程
**日期**: 2026-04-19

---

## 前置条件

### 1. 环境准备

```bash
# 确认 Rust 工具链
rustc --version    # >= 1.75
cargo --version

# 确认 Ollama（可选，LLM 功能需要）
ollama --version
ollama serve &     # 后台启动
ollama pull qwen2.5:0.5b   # 轻量模型用于测试

# 确认 Qdrant（可选，向量检索需要）
docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

### 2. 环境变量

```bash
export AGENT_DB=./test_agent.db
export AGENT_PROVIDER=ollama
export OLLAMA_MODEL=qwen2.5:0.5b
export OLLAMA_BASE_URL=http://localhost:11434
```

---

## 第一部分：编译验证

### 1.1 全量编译

```bash
cd v2
cargo build --workspace
```

**预期**: 编译成功，无 error（warning 可忽略）

### 1.2 Release 编译

```bash
cargo build --release --workspace
```

**预期**: 编译成功，产出二进制文件

### 1.3 代码质量检查

```bash
cargo fmt -- --check
cargo clippy --workspace -- -D warnings 2>&1 | tail -20
```

**预期**: 格式正确，clippy 无 error

---

## 第二部分：自动化测试

### 2.1 全量测试

```bash
cargo test --workspace
```

**预期**: 844+ passed, 0 failed

### 2.2 按 crate 运行（可选，用于定位问题）

```bash
# 核心层
cargo test --package agent-core
cargo test --package agent-storage

# 功能模块
cargo test --package agent-context-compression
cargo test --package agent-memory
cargo test --package agent-file-storage
cargo test --package agent-skill-extraction
cargo test --package agent-scheduled-tasks
cargo test --package agent-multi-agent

# 业务层
cargo test --package agent-workflow
cargo test --package agent-api
```

### 2.3 多 Agent 专项测试

```bash
cargo test --package agent-multi-agent -- --nocapture
```

**预期输出包含**:
- `test_parallel_execution ... ok`
- `test_sequential_with_chaining ... ok`
- `test_voting_majority_wins ... ok`
- `test_pipeline_data_flows_through ... ok`
- `test_register_and_get ... ok`
- `test_route_message ... ok`
- 共 23 个测试全部 ok

---

## 第三部分：CLI 功能验收

### 3.1 基础会话管理

```bash
# 查看帮助
cargo run --package agent-cli -- --help

# 创建会话
cargo run --package agent-cli -- new --title "验收测试会话"
# 记录输出的 session-id

# 列出会话
cargo run --package agent-cli -- list

# 查看会话详情
cargo run --package agent-cli -- show <session-id>

# 删除会话
cargo run --package agent-cli -- delete <session-id>
```

**预期**: 各命令正确输出，无 panic

### 3.2 记忆系统

```bash
# 添加记忆
cargo run --package agent-cli -- memory add user "我是一名 Rust 开发者"

# 列出记忆
cargo run --package agent-cli -- memory list

# 搜索记忆
cargo run --package agent-cli -- memory search "Rust"

# 查看统计
cargo run --package agent-cli -- memory stats
```

**预期**: 记忆正确创建、列出、搜索

### 3.3 文件存储

```bash
# 上传文件
echo "Hello World" > /tmp/test_upload.txt
cargo run --package agent-cli -- file upload /tmp/test_upload.txt

# 列出文件
cargo run --package agent-cli -- file list

# 查看文件详情
cargo run --package agent-cli -- file show <file-id>

# 查看存储统计
cargo run --package agent-cli -- file stats
```

**预期**: 文件正确上传、列出、访问

### 3.4 技能系统

```bash
# 列出技能
cargo run --package agent-cli -- skill list

# 查看技能详情（如果有内置技能）
cargo run --package agent-cli -- skill show <skill-name>
```

### 3.5 计划任务

```bash
# 创建计划任务
cargo run --package agent-cli -- task schedule "每日总结" \
  --schedule "0 9 * * *" \
  --type custom-command \
  --payload '{"command": "echo hello"}'

# 列出任务
cargo run --package agent-cli -- task list

# 查看任务详情
cargo run --package agent-cli -- task show <task-id>
```

**预期**: 任务正确创建和管理

---

## 第四部分：Web API 验收

### 4.1 启动 API 服务

```bash
cargo run --package agent-cli -- serve --port 3000
```

**预期**: 服务启动，监听 0.0.0.0:3000

### 4.2 健康检查

```bash
curl http://localhost:3000/health
```

**预期**: `{"status":"ok"}`

### 4.3 Swagger UI

打开浏览器访问：http://localhost:3000/swagger-ui/

**预期**: 显示 OpenAPI 文档界面，包含所有端点

### 4.4 会话管理 API

```bash
# 创建会话
curl -X POST http://localhost:3000/api/v1/sessions \
  -H "Content-Type: application/json" \
  -d '{"title": "API 测试会话"}'

# 列出会话
curl http://localhost:3000/api/v1/sessions

# 获取单个会话
curl http://localhost:3000/api/v1/sessions/<session-id>
```

### 4.5 记忆管理 API

```bash
# 创建记忆
curl -X POST http://localhost:3000/api/v1/memories \
  -H "Content-Type: application/json" \
  -d '{"memory_type": "user", "content": "API 测试记忆"}'

# 搜索记忆
curl -X POST http://localhost:3000/api/v1/memories/search \
  -H "Content-Type: application/json" \
  -d '{"query": "测试", "limit": 10}'

# 获取统计
curl http://localhost:3000/api/v1/memories/stats
```

### 4.6 文件管理 API

```bash
# 列出文件
curl http://localhost:3000/api/v1/files

# 存储统计
curl http://localhost:3000/api/v1/files/stats
```

### 4.7 技能 API

```bash
# 列出技能
curl http://localhost:3000/api/v1/skills
```

### 4.8 多 Agent 协作 API

```bash
# 列出已注册的 Agent
curl http://localhost:3000/api/v1/agents

# 执行并行协作（需要 Ollama 运行）
curl -X POST http://localhost:3000/api/v1/agents/collaborate \
  -H "Content-Type: application/json" \
  -d '{
    "title": "代码分析",
    "description": "多角度分析代码质量",
    "strategy": "parallel",
    "subtasks": [
      {
        "agent_id": "search-agent",
        "task": "搜索 Rust 最佳实践",
        "context": {}
      },
      {
        "agent_id": "analysis-agent",
        "task": "分析代码结构",
        "context": {"code": "fn main() { println!(\"hello\"); }"}
      }
    ]
  }'

# 执行投票协作
curl -X POST http://localhost:3000/api/v1/agents/collaborate \
  -H "Content-Type: application/json" \
  -d '{
    "title": "代码质量评估",
    "description": "多 Agent 投票评估代码质量",
    "strategy": "voting",
    "min_votes": 2,
    "subtasks": [
      {"agent_id": "search-agent", "task": "评估：优秀/良好/一般", "context": {}},
      {"agent_id": "analysis-agent", "task": "评估：优秀/良好/一般", "context": {}},
      {"agent_id": "summary-agent", "task": "评估：优秀/良好/一般", "context": {}}
    ]
  }'

# 执行管道协作
curl -X POST http://localhost:3000/api/v1/agents/collaborate \
  -H "Content-Type: application/json" \
  -d '{
    "title": "ETL 管道",
    "description": "搜索-分析-总结管道",
    "strategy": "pipeline",
    "subtasks": [
      {"agent_id": "search-agent", "task": "搜索 Rust 异步编程资料", "context": {}},
      {"agent_id": "analysis-agent", "task": "分析搜索结果", "context": {}},
      {"agent_id": "summary-agent", "task": "总结分析报告", "context": {}}
    ]
  }'
```

**预期**:
- `GET /agents` 返回 3 个内置 Agent（search-agent, analysis-agent, summary-agent）
- `POST /agents/collaborate` 返回协作结果（需要 Ollama 运行才能得到实际 LLM 输出）
- 不同策略（parallel/sequential/voting/pipeline）均正常执行

---

## 第五部分：架构验证

### 5.1 Crate 依赖图验证

```bash
# 确认 workspace 成员数量
grep -c "crates/" v2/Cargo.toml
```

**预期**: 16 个 crate

### 5.2 Feature Flag 验证

```bash
# 单独编译多 Agent 功能
cargo build --package agent-workflow --features multi-agent

# 不启用多 Agent（验证可选性）
cargo build --package agent-workflow --no-default-features
```

**预期**: 两种配置都能编译通过

### 5.3 测试覆盖统计

```bash
cargo test --workspace 2>&1 | grep "test result:" | \
  awk '{passed+=$4; failed+=$6} END {print "Total passed:", passed, "Failed:", failed}'
```

**预期**: 844+ passed, 0 failed

---

## 验收清单

| 类别 | 检查项 | 通过 |
|------|--------|------|
| **编译** | workspace 全量编译通过 | ☐ |
| **编译** | release 编译通过 | ☐ |
| **测试** | 844+ 测试全部通过 | ☐ |
| **测试** | multi-agent 23 个测试全部通过 | ☐ |
| **CLI** | 会话 CRUD 正常 | ☐ |
| **CLI** | 记忆系统正常 | ☐ |
| **CLI** | 文件存储正常 | ☐ |
| **CLI** | 计划任务正常 | ☐ |
| **API** | 服务启动正常 | ☐ |
| **API** | Swagger UI 可访问 | ☐ |
| **API** | 会话/记忆/文件端点正常 | ☐ |
| **API** | 多 Agent 列表端点正常 | ☐ |
| **API** | 多 Agent 协作端点正常 | ☐ |
| **架构** | 16 crate workspace 完整 | ☐ |
| **架构** | feature flag 可选性正确 | ☐ |

---

## 已知限制

1. **LLM 依赖**: 多 Agent 协作和对话功能需要 Ollama 运行
2. **向量检索**: 语义搜索需要 Qdrant 运行
3. **TUI 界面**: 需要终端环境，不适合 CI 自动化测试
4. **SSE 流式**: 需要支持 EventSource 的客户端测试

---

**最后更新**: 2026-04-19
