# V2 文档索引 (Rust 版本)

General Agent V2 是使用 Rust 重写的高性能版本，具有类型安全、内存安全和高并发性能。

## 📚 核心文档

- [架构设计](ARCHITECTURE.md) - V2 系统架构和模块设计
- [部署指南](DEPLOYMENT.md) - 生产环境部署说明
- [技能系统](SKILLS.md) - 技能开发和集成指南

## 🔌 API 文档

- [API 参考](api/api-reference.md) - RESTful API 接口规范

## 🎯 功能特性

- [Workflow 集成](features/workflow-integration.md) - 工作流编排系统集成

## 📐 设计计划

### MCP & RAG
- [MCP-RAG 集成设计](plans/2026-03-10-mcp-rag-integration-design.md)
- [MCP-RAG 实现计划](plans/2026-03-10-mcp-rag-implementation-plan.md)

### TUI 终端界面
- [TUI 设计](plans/2026-03-10-tui-design.md)
- [TUI 实现计划](plans/2026-03-10-tui-implementation-plan.md)

### 技能系统
- [技能系统设计](plans/skills-system-design.md)
- [技能集成计划](plans/skills-integration-plan.md)

### 其他
- [集成测试计划](plans/integration-tests-plan.md)
- [Phase 8 设计总结](plans/phase8-design-summary.md)
- [V2 Phase 2 路线图](plans/v2-phase2-roadmap.md)

## 🧪 测试文档

- [Phase 2 测试指南](testing/phase2-testing-guide.md)
- [测试结果](testing/test-results.md)
- [UUID 修复](testing/uuid-fix.md)

## 🔄 Workflow 文档

- [Phase 7: Agent Workflow](workflow/2026-03-06-phase7-agent-workflow.md)
- [监控面板](workflow/2026-03-09-monitoring-dashboard.md)

## 📊 进度总结

- [Week 4 完成总结](WEEK4_COMPLETE_SUMMARY.md)
- [Week 4 Day 3 总结](WEEK4_DAY3_SUMMARY.md)

## 📦 归档文档

V2 的历史文档已归档至公共文档目录：

- [交接文档](../../docs/archives/v2/handoffs/) - 4 个交接文档
- [进度报告](../../docs/archives/v2/progress/) - 32 个周/日进度文档
- [迁移文档](../../docs/archives/v2/migrations/) - 2 个迁移计划

---

## 🏗️ 技术栈

- **语言**: Rust (Edition 2021)
- **异步运行时**: Tokio
- **数据库**: SQLite (SQLx)
- **LLM 集成**: Anthropic + Ollama
- **向量数据库**: Qdrant
- **终端 UI**: Ratatui
- **序列化**: Serde

## 📋 项目结构

```
v2/
├── crates/
│   ├── agent-core         # 核心模型和 Traits
│   ├── agent-storage      # SQLite 持久化
│   ├── agent-llm          # LLM 客户端
│   ├── agent-skills       # 技能系统
│   ├── agent-mcp          # MCP 协议
│   ├── agent-rag          # RAG 检索
│   ├── agent-workflow     # 业务逻辑
│   ├── agent-cli          # 命令行工具
│   └── agent-tui          # 终端 UI
├── docs/                  # 文档目录
├── examples/              # 示例代码
└── tests/                 # 集成测试
```

## 🚀 快速开始

```bash
# 构建项目
cargo build --release

# 运行 CLI
./target/release/agent new --title "会话标题"
./target/release/agent chat <session-id>

# 运行 TUI
cargo run -p agent-tui --example tui_demo

# 运行测试
cargo test
```

## 📌 相关资源

- [V2 README](../README.md) - V2 项目总览
- [根目录文档](../../docs/README.md) - 公共文档
- [V3 文档](../../v3/docs/README.md) - C# 版本文档
- [CLAUDE.md](../../CLAUDE.md) - AI 辅助开发指南

## 🔗 外部资源

- [Rust 官方文档](https://doc.rust-lang.org/)
- [Tokio 文档](https://tokio.rs/)
- [SQLx 文档](https://github.com/launchbadge/sqlx)
- [Ratatui 文档](https://ratatui.rs/)
