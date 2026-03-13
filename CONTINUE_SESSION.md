# 快速恢复会话 - 提示词

**项目**: General Agent
**日期**: 2026-03-13
**分支**: main (Week 4 已合并)

---

## 📋 简要状态

**已完成**: Week 4 Workflow 高级功能（~3100 行代码，54 个测试，100% 通过）
- 重试机制、状态持久化、错误分类、性能监控

**当前任务**: 短期计划 → v3 C# TUI 开发

---

## 🎯 下次会话提示词

```
继续 General Agent 项目开发。

【当前状态】
- Week 4 完成并合并到 main (提交: 8b768b4d)
- Workflow 核心功能完整：重试、持久化、错误分类、性能监控
- 分支: main，代码已推送

【下一步计划】
短期任务（1.5-2 周）：
1. Subagent 系统完善
   - 文件: v2/crates/agent-workflow/src/subagent/
   - 任务: 并行执行优化、状态管理、Workflow 集成

2. TUI 性能监控集成
   - 文件: v2/crates/agent-tui/
   - 任务: 性能面板、实时数据绑定、交互功能

完成后切换到：v3 C# TUI 开发（4-5 周）
- 技术栈: .NET 8 + Terminal.Gui + SQLite
- 目录: v3/（已创建）

【关键文件】
- SESSION_HANDOFF.md: 详细交接文档
- v2/crates/agent-workflow/src/workflow/: Workflow 实现
- v2/crates/agent-workflow/src/subagent/: 待完善
- v2/docs/WEEK4_COMPLETE_SUMMARY.md: Week 4 总结

【快速验证】
cd v2/
cargo test -p agent-workflow  # 应该 270 passed

【建议第一步】
从 Subagent 系统优化开始：
cd v2/crates/agent-workflow/src/subagent/
# 优化 orchestrator.rs 的并行执行逻辑
```

---

## 💡 使用方法

直接复制上面的提示词给 Claude，即可快速恢复上下文。

---

**创建时间**: 2026-03-13
**有效期**: 长期有效（除非项目状态重大变更）
