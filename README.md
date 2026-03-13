# General Agent

通用 AI Agent 系统，支持技能系统、MCP 集成、RAG 和工作流编排。

## 📁 项目结构

```
general-agent/
├── v1/                # Python 版本（第一版，参考实现）
│   ├── src/          # Python 源代码
│   ├── tests/        # Python 测试
│   └── README.md     # V1 说明文档
│
├── v2/                # Rust 版本（生产版本，活跃开发）
│   ├── crates/       # Rust crates
│   ├── docs/         # 文档
│   └── README.md     # V2 说明文档
│
├── docs/              # 通用文档
└── CLAUDE.md          # Claude Code 项目指南
```

## 🚀 推荐使用 v2 (Rust)

**高性能、类型安全、生产就绪**

- 快速入门：[v2/README.md](v2/README.md)
- 开发指南：[v2/docs/DEVELOPMENT.md](v2/docs/DEVELOPMENT.md)
- 架构文档：[v2/docs/ARCHITECTURE.md](v2/docs/ARCHITECTURE.md)

### 构建和运行

```bash
cd v2/
cargo build --release
cargo test
```

## 📚 V1 (Python) 参考

V1 Python 版本保留用于功能对比和快速原型开发。

详见：[v1/README.md](v1/README.md)

## 🔧 当前开发

- **活跃分支**: `feature/workflow-migration`
- **当前进度**: Week 4 - 容错优化和性能监控
- **最新功能**: 重试机制、状态持久化

## 📝 关键文档

- [CLAUDE.md](CLAUDE.md) - 项目开发指南
- [MIGRATION_GAP_ANALYSIS.md](MIGRATION_GAP_ANALYSIS.md) - Python 到 Rust 迁移分析
- [ROADMAP.md](ROADMAP.md) - 项目路线图

## 📄 License

MIT License
