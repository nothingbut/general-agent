# Subagent 系统设计 - 会话交接文档

**日期：** 2026-03-11
**会话类型：** 设计阶段（Brainstorming）
**状态：** 设计文档已完成，等待审查
**下次启动目录：** `general-agent/`（注意：不是根目录 `program/`）

---

## 📋 当前进度

### ✅ 已完成

1. **需求分析和设计决策**
   - 通过 brainstorming skill 完成完整的需求分析
   - 确认了 7 个关键决策（详见下方）
   - 使用可视化工具展示了 TUI 布局方案

2. **设计文档编写**
   - 完成详细的设计文档（1139 行）
   - 位置：`docs/superpowers/specs/2026-03-11-subagent-system-design.md`
   - 已提交到 git（commit: 1375936）

3. **架构确定**
   - 混合架构：消息队列（tokio::mpsc）+ 共享状态（DashMap）
   - 核心组件：SubagentOrchestrator + SubagentTask + SessionCardWidget
   - TUI 方案：卡片式展示 + 进度条 + 实时更新

### ⏳ 待完成

1. **Spec 文档审查**
   - 使用 spec-document-reviewer subagent 审查设计文档
   - 修复发现的问题
   - 最多 5 轮迭代

2. **用户审查**
   - 用户审阅设计文档
   - 根据反馈调整

3. **实施计划**
   - 调用 writing-plans skill
   - 生成详细的实施计划

---

## 🎯 关键决策回顾

### 1. Subagent 触发方式
**决策：C. 混合模式**
- LLM 可以提议启动 subagent
- 需要用户确认
- 用户也可以直接触发

### 2. Subagent 生命周期
**决策：C. 智能管理**
- 默认完成后自动关闭
- 支持标记为"保持活跃"
- 可唤醒已关闭的 subagent

### 3. 主会话编排能力
**决策：C. 分阶段编排**
- 任务分为多个 Stage
- 每个 Stage 可并发执行多个 subagent
- 阶段间评估结果，决定下一步

### 4. TUI 界面呈现
**决策：D. 卡片式 + 状态概览**
- 信息丰富（进度、状态、预估时间）
- 支持展开/折叠
- 适合复杂编排场景
- 预计实现时间：10-13 小时

### 5. 数据隔离
**决策：C. 有限共享**
- 启动时指定共享内容（最近 N 条消息、变量）
- Subagent 在此基础上独立工作

### 6. 结果汇总方式
**决策：C. 智能编排**
- 主会话评估各 subagent 结果
- 决定是否需要进一步处理
- 生成最终回复

### 7. LLM 配置
**决策：C. 智能选择**
- 根据任务类型自动选择模型
- 简单任务用快速模型（qwen2.5:0.5b）
- 复杂任务用强大模型（claude-3-5-sonnet）
- 支持手动覆盖

### 8. 架构方案
**决策：C. 混合架构**
- 消息队列（tokio::mpsc）传递命令/结果
- DashMap 共享状态（无锁并发）
- SubagentOrchestrator 集中管理

---

## 📁 重要文件位置

**注意：所有路径相对于 `general-agent/` 目录**

### 设计文档
```
docs/superpowers/specs/2026-03-11-subagent-system-design.md
```
- 1139 行完整设计文档
- 包含架构、数据模型、工作流、测试策略

### 视觉伴侣文件（可选参考）
```
.superpowers/brainstorm/94656-1773213438/
├── session-tree-layout.html          # TUI 布局方案对比
├── implementation-comparison.html     # B vs D 方案对比
└── architecture-overview.html         # 架构概览图
```

### 现有 V2 代码
```
v2/
├── crates/
│   ├── agent-core/src/models/session.rs      # 需要扩展
│   ├── agent-workflow/src/
│   │   ├── session_manager.rs                # 现有会话管理
│   │   └── conversation_flow.rs              # 需要集成
│   └── agent-tui/src/ui/                     # 需要添加 session_card.rs
└── docs/
    ├── ARCHITECTURE.md                        # V2 架构文档
    └── plans/v2-phase2-roadmap.md            # Phase 2 路线图
```

---

## 🔄 下一步工作流程

### Step 1: Spec 审查循环（约 30-60 分钟）

```bash
# 1. 使用 spec-document-reviewer subagent
# 位置：skills/brainstorming/spec-document-reviewer-prompt.md

# 2. 审查要点：
- 架构设计是否合理
- 数据模型是否完整
- 工作流是否清晰
- 错误处理是否充分
- 实施计划是否可行

# 3. 修复发现的问题
# 4. 重复直到审查通过（最多 5 轮）
```

### Step 2: 用户审查（约 15-30 分钟）

```bash
# 1. 用户阅读设计文档
# 2. 提出修改意见
# 3. 修改文档
# 4. 用户确认
```

### Step 3: 生成实施计划（约 30-45 分钟）

```bash
# 调用 writing-plans skill
# 生成详细的任务分解和实施步骤
```

---

## 📝 预期工作量

### 总体估算
- **代码量：** ~2000-2500 行
- **开发时间：** 2-3 周（全职）
- **测试覆盖：** > 80%

### 分阶段
- **Phase 1：** 核心功能（Week 1，40-50h）
- **Phase 2：** TUI 集成（Week 2，30-40h）
- **Phase 3：** 高级特性（Week 2-3，20-30h）
- **Phase 4：** 文档验收（Week 3，10-15h）

---

## 🚨 注意事项

### 1. 上下文管理
- 本次会话上下文使用 93%，已接近极限
- 建议下次会话从审查开始，避免加载过多历史

### 2. 路径问题
- **关键：** 下次会话在 `general-agent/` 启动
- 所有路径都相对于这个目录
- 例如：设计文档路径是 `docs/superpowers/specs/...`（不是 `claude/general-agent/docs/...`）

### 3. Git 状态
- 设计文档已提交（commit: 1375936）
- 当前分支：main
- 无未提交的更改

### 4. 视觉伴侣
- 服务器可能仍在运行（http://localhost:58216）
- 如需要，可以重新启动查看架构图
- 但通常不需要，设计文档已足够详细

---

## 💬 交接提示词

**用户下次会话开始时，复制以下内容：**

```
继续 Subagent 系统设计。上次会话完成了设计文档编写（docs/superpowers/specs/2026-03-11-subagent-system-design.md），现在需要：

1. 使用 spec-document-reviewer subagent 审查设计文档
2. 根据审查结果修复问题（最多 5 轮）
3. 审查通过后，我会阅读文档并提供反馈
4. 确认后，调用 writing-plans skill 生成实施计划

注意：当前目录是 general-agent/，所有路径都相对于此目录。

请开始 spec 审查。
```

---

## 📊 会话统计

- **会话时长：** 约 2 小时
- **关键决策：** 8 个
- **生成文档：** 1139 行
- **可视化图表：** 3 个
- **Git 提交：** 1 个
- **上下文使用：** 93%

---

**交接完成时间：** 2026-03-11 下午
**下次接续：** Spec 审查循环
**预计下次会话时长：** 1-2 小时（审查 + 用户反馈 + 实施计划）

---

## ✅ 交接检查清单

- [x] 设计文档已完成并提交
- [x] 关键决策已记录
- [x] 文件位置已说明
- [x] 下一步工作已明确
- [x] 交接提示词已提供
- [x] 注意事项已列出
- [x] 路径问题已强调（下次在 general-agent/ 启动）

**交接准备完毕！** 🚀
