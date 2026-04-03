# General Agent 文档索引

欢迎查阅 General Agent 项目文档。本目录包含公共文档、V1 历史文档和归档资料。

## 📚 快速开始

- [Ollama 安装指南](getting-started/ollama-setup.md) - 本地 LLM 部署指南

## 📖 用户指南

- [MCP 集成指南](guides/mcp-guide.md) - Model Context Protocol 使用说明
- [RAG 系统指南](guides/rag-guide.md) - 检索增强生成功能
- [技能系统指南](guides/skills-guide.md) - 自定义技能开发
- [TUI 界面指南](guides/tui-guide.md) - 终端用户界面使用

## 🔌 API 参考

- [API 参考文档](api/api-reference.md) - RESTful API 接口说明

## 🎯 功能特性

- [Subagent 系统](features/subagent-system.md) - 并行子任务执行系统

## 📐 设计与计划

### 核心设计
- [General Agent 总体设计](plans/2026-03-02-general-agent-design.md)
- [Phase 1: 基础架构](plans/2026-03-02-phase1-foundation.md)
- [MCP 集成设计](plans/2026-03-04-mcp-integration-design.md)
- [Phase 6: TUI 设计](plans/2026-03-05-phase6-tui-design.md)
- [Phase 7: Agent Workflow](plans/2026-03-06-phase7-agent-workflow.md)

### 监控与优化
- [监控面板设计](plans/2026-03-08-monitoring-dashboard-design.md)
- [性能优化设计](plans/2026-03-08-performance-optimization-design.md)
- [监控面板实现](plans/2026-03-09-monitoring-dashboard.md)

### 路线图
- [V3 下一步路线图](plans/V3_NEXT_STEPS_ROADMAP.md)
- [V3.2 路线图](plans/V3.2_ROADMAP.md)
- [V3.3 核心 Agent 功能](plans/V3.3_CORE_AGENT_FEATURES.md)

## 📊 项目管理

- [V3 项目清单](projects/V3_PROJECT_CHECKLIST.md)
- [V3 项目总结](projects/V3_PROJECT_SUMMARY.md)

## 🚀 发布管理

- [V3 发布指南](releases/V3_RELEASE_GUIDE.md)
- [V3.0.0 发布说明](releases/v3.0.0/RELEASE_NOTES_V3.0.0.md)

## 🔬 研究分析

- [E2E 测试方案研究](research/e2e-testing-solutions.md)
- [TUI 方案研究](research/tui-solutions-research.md)
- [技能系统对比分析](analysis/skill-system-comparison.md)

## 🧪 用户验收测试

- [V3 UAT 计划](uat/V3_UAT_PLAN.md)
- [V3 UAT 报告](uat/V3_UAT_REPORT.md)

## 🔄 Workflow 文档

- [审批 UI](workflow/approval-ui.md)
- [集成测试](workflow/integration-tests.md)
- [通知系统](workflow/notification-system.md)
- [Week 2 高级功能](workflow/week2-advanced-features.md)

## 🎨 Superpowers (高级功能)

### 设计规范
- [Subagent 系统设计](superpowers/specs/2026-03-11-subagent-system-design.md)
- [V3 C# 架构设计](superpowers/specs/2026-03-16-v3-csharp-architecture-design.md)
- [V3 技能系统重设计](superpowers/specs/2026-03-18-v3-skill-system-redesign.md)
- [V3.1 搜索标签设计](superpowers/specs/2026-03-25-v3.1-search-tags-design.md)
- [V3 Phase 2 Embedding 向量数据库设计](superpowers/specs/2026-03-27-v3-phase2-embedding-vector-db-design.md)

### 实现计划
- [Subagent 系统实现](superpowers/plans/2026-03-11-subagent-system-implementation.md)
- [Subagent 集成计划](superpowers/plans/2026-03-12-subagent-integration-plan.md)
- [V3 Phase 1 核心存储](superpowers/plans/2026-03-16-v3-phase1-core-storage.md)
- [V3 Phase 2 LLM 集成](superpowers/plans/2026-03-16-v3-phase2-llm-integration.md)
- [V3.1 搜索标签实现](superpowers/plans/2026-03-25-v3.1-search-tags-implementation.md)

## 📦 归档文档

### V1 归档
- [V1 验收测试](archives/v1/ACCEPTANCE_TEST.md)
- [MCP Phase 3 完成](archives/v1/mcp-phase-3-complete.md)
- [V1 功能列表](archives/v1/v1-features.md)
- [V1 路线图](archives/v1/v1-roadmap.md)

### V2 归档
- [交接文档](archives/v2/handoffs/) - 4 个交接文档
- [进度报告](archives/v2/progress/) - 32 个进度文档
- [迁移文档](archives/v2/migrations/) - 2 个迁移计划

### V3 归档
- [交接文档](archives/v3/handoffs/) - 5 个交接文档
- [阶段文档](archives/v3/phases/) - 17 个阶段文档

### 其他归档
- [文档清理计划](archive/DOCS_CLEANUP_PLAN.md)
- [Git 清理相关](archive/git-cleanup/) - 仓库清理记录
- [会话交接](archive/handoffs/) - 历史会话交接
- [过时文档](archive/obsolete/) - 已废弃文档
- [TUI 性能优化](archive/tui-performance/) - 性能优化记录

## 📋 各阶段文档

### Phase 1 - 基础架构
- [执行交接](phases/phase1/V3_PHASE1_EXECUTION_HANDOFF.md)
- [计划交接](phases/phase1/V3_PHASE1_PLAN_HANDOFF.md)

### Phase 2 - LLM 集成
- [完成报告](phases/phase2/V3_PHASE2_COMPLETION_REPORT.md)
- [执行交接](phases/phase2/V3_PHASE2_EXECUTION_HANDOFF.md)
- [分块进度](phases/phase2/) - Chunk 2-4 交接文档

### Phase 3 - 技能系统
- [完成报告](phases/phase3/V3_PHASE3_COMPLETION_REPORT.md)
- [实施计划](phases/phase3/V3_PHASE3_PLAN.md)
- [任务完成](phases/phase3/) - Task 6-9 完成文档
- [UAT 检查清单](phases/phase3/V3_PHASE3_UAT_CHECKLIST.md)

### Phase 4 - MCP 集成
- [完成报告](phases/phase4/V3_PHASE4_COMPLETION_REPORT.md)
- [实施计划](phases/phase4/V3_PHASE4_PLAN.md)
- [下一步计划](phases/phase4/V3_PHASE4_NEXT_STEPS.md)
- [分块进度](phases/phase4/) - Chunk 2-5 完成文档

### Phase 5 - RAG 系统
- [完成报告](phases/phase5/V3_PHASE5_COMPLETION_REPORT.md)
- [实施计划](phases/phase5/V3_PHASE5_PLAN.md)
- [进度总结](phases/phase5/V3_PHASE5_PROGRESS_SUMMARY.md)
- [分块进度](phases/phase5/) - Chunk 1-6 完成文档

---

## 📌 相关资源

- [根目录 README](../README.md) - 项目总览
- [路线图](../ROADMAP.md) - 项目发展规划
- [变更日志](../CHANGELOG.md) - 版本变更记录
- [Claude Code 指南](../CLAUDE.md) - AI 辅助开发指南

## 🔗 版本特定文档

- [V2 文档](../v2/docs/README.md) - Rust 版本文档
- [V3 文档](../v3/docs/README.md) - C# 版本文档
