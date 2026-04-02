# 项目根目录文档整理计划

**创建时间**: 2026-04-02
**当前状态**: 根目录有 83 个 Markdown 文档，需要整理

---

## 📊 当前问题

1. **文档过多**: 83 个 Markdown 文件在根目录
2. **分类混乱**: 历史文档、临时文档、活跃文档混在一起
3. **重复内容**: 多个相似功能的文档（CONTINUE_*, HANDOFF_*, 等）
4. **过时信息**: 许多旧的会话总结和临时状态文件

---

## 🎯 整理目标

**根目录应该保留的核心文档**（5-8 个）：
- ✅ README.md - 项目主要说明
- ✅ CLAUDE.md - Claude Code 配置
- ✅ CHANGELOG.md - 更新日志
- ✅ ROADMAP.md - 主路线图（聚合所有版本）
- ✅ LICENSE（如果需要）
- ✅ CONTRIBUTING.md（如果需要）

**其他所有文档应该分类归档到 docs/ 目录**

---

## 📁 目标目录结构

```
/
├── README.md
├── CLAUDE.md
├── CHANGELOG.md
├── ROADMAP.md
├── docs/
│   ├── archive/           # 历史文档归档
│   │   ├── sessions/      # 会话总结
│   │   ├── handoffs/      # 交接文档
│   │   ├── git-cleanup/   # Git 清理相关
│   │   └── tui-performance/  # TUI 性能优化历史
│   ├── releases/          # 发布文档
│   │   ├── v3.0.0/
│   │   ├── v3.1.0/
│   │   └── v3.2.0/
│   ├── plans/             # 规划文档（活跃）
│   │   ├── v3-roadmap.md
│   │   └── priority-features.md
│   ├── phases/            # 阶段性文档
│   │   ├── phase1/
│   │   ├── phase2/
│   │   ├── phase3/
│   │   ├── phase4/
│   │   └── phase5/
│   ├── uat/               # 验收测试文档
│   │   ├── v3.0-uat.md
│   │   └── v3.1-uat.md
│   └── projects/          # 项目总结
│       └── v3-summary.md
└── v3/
    └── docs/              # V3 特定文档（技术文档）
        ├── CLI_GUIDE.md
        ├── V3_PRIORITY_FEATURES_ROADMAP.md
        └── ...
```

---

## 🗂️ 文档分类和移动计划

### 类别 1: 保留在根目录（4 个）

| 文件 | 状态 | 操作 |
|------|------|------|
| README.md | ✅ 活跃 | 保留，可能需要更新 |
| CLAUDE.md | ✅ 活跃 | 保留 |
| CHANGELOG.md | ✅ 活跃 | 保留，更新到 Phase 2 |
| ROADMAP.md | ✅ 活跃 | 保留，整合各版本路线图 |

---

### 类别 2: 移动到 docs/archive/sessions/ （4 个）

所有会话总结文档：

| 源文件 | 目标位置 |
|--------|----------|
| SESSION_2026-03-24_SUMMARY.md | docs/archive/sessions/ |
| SESSION_2026-03-26_V3.1_RELEASE.md | docs/archive/sessions/ |
| SESSION_HANDOFF.md | docs/archive/sessions/ |
| HANDOFF-2026-03-11-SESSION2.md | docs/archive/sessions/ |

---

### 类别 3: 移动到 docs/archive/handoffs/ （7 个）

所有交接和继续提示文档：

| 源文件 | 目标位置 |
|--------|----------|
| CONTINUE_PHASE3_PROMPT.md | docs/archive/handoffs/ |
| CONTINUE_PHASE3_TASK8_PROMPT.md | docs/archive/handoffs/ |
| CONTINUE_PHASE3_TASK9_PROMPT.md | docs/archive/handoffs/ |
| CONTINUE_PROMPT.md | docs/archive/handoffs/ |
| CONTINUE_SESSION.md | docs/archive/handoffs/ |
| CONTINUE_TUI_PERFORMANCE.md | docs/archive/handoffs/ |
| CONTINUE_V3_PHASE7.md | docs/archive/handoffs/ |
| HANDOFF_PHASE3_COMPLETE.md | docs/archive/handoffs/ |
| HANDOFF_V3_PHASE34_MERGE.md | docs/archive/handoffs/ |
| V3_HANDOFF_PROMPT.md | docs/archive/handoffs/ |

---

### 类别 4: 移动到 docs/archive/git-cleanup/ （8 个）

Git 和仓库管理相关：

| 源文件 | 目标位置 |
|--------|----------|
| BRANCH_STATUS_REPORT.md | docs/archive/git-cleanup/ |
| CLEANUP_COMPLETE.md | docs/archive/git-cleanup/ |
| GIT_CLEANUP_SUMMARY.md | docs/archive/git-cleanup/ |
| GIT_REPOSITORY_STATUS.md | docs/archive/git-cleanup/ |
| MERGE_COMPLETE.md | docs/archive/git-cleanup/ |
| MIGRATION_GAP_ANALYSIS.md | docs/archive/git-cleanup/ |
| PR-DESCRIPTION.md | docs/archive/git-cleanup/ |
| PUSH_AND_CLEANUP_COMPLETE.md | docs/archive/git-cleanup/ |
| REPOSITORY_SIZE_ANALYSIS.md | docs/archive/git-cleanup/ |
| V3_PHASE34_MERGE_COMPLETE.md | docs/archive/git-cleanup/ |

---

### 类别 5: 移动到 docs/archive/tui-performance/ （7 个）

TUI 性能优化历史：

| 源文件 | 目标位置 |
|--------|----------|
| TUI_PERFORMANCE_ACCEPTANCE_RESULT.md | docs/archive/tui-performance/ |
| TUI_PERFORMANCE_DAY2_COMPLETE.md | docs/archive/tui-performance/ |
| TUI_PERFORMANCE_DAY3_COMPLETE.md | docs/archive/tui-performance/ |
| TUI_PERFORMANCE_DAY4_COMPLETE.md | docs/archive/tui-performance/ |
| TUI_PERFORMANCE_MONITOR_ACCEPTANCE.md | docs/archive/tui-performance/ |
| TUI_PERFORMANCE_MONITOR_PLAN.md | docs/archive/tui-performance/ |

---

### 类别 6: 移动到 docs/phases/phase1/ （2 个）

| 源文件 | 目标位置 |
|--------|----------|
| V3_PHASE1_EXECUTION_HANDOFF.md | docs/phases/phase1/ |
| V3_PHASE1_PLAN_HANDOFF.md | docs/phases/phase1/ |

---

### 类别 7: 移动到 docs/phases/phase2/ （9 个）

| 源文件 | 目标位置 |
|--------|----------|
| V3_PHASE2_CHUNK2_HANDOFF.md | docs/phases/phase2/ |
| V3_PHASE2_CHUNK3_HANDOFF.md | docs/phases/phase2/ |
| V3_PHASE2_CHUNK4_HANDOFF.md | docs/phases/phase2/ |
| V3_PHASE2_COMPLETION_REPORT.md | docs/phases/phase2/ |
| V3_PHASE2_EXECUTION_HANDOFF.md | docs/phases/phase2/ |
| V3_PHASE2_HANDOFF.md | docs/phases/phase2/ |
| V3_PHASE2_PROGRESS_HANDOFF.md | docs/phases/phase2/ |

**保留在根目录**（最近创建，仍然活跃）：
- V3_PHASE2_ACCEPTANCE_TEST_GUIDE.md → **移到 v3/docs/**

---

### 类别 8: 移动到 docs/phases/phase3/ （9 个）

| 源文件 | 目标位置 |
|--------|----------|
| V3_PHASE3_COMPLETION_REPORT.md | docs/phases/phase3/ |
| V3_PHASE3_PLAN.md | docs/phases/phase3/ |
| V3_PHASE3_TASK6_HANDOFF.md | docs/phases/phase3/ |
| V3_PHASE3_TASK7_COMPLETION.md | docs/phases/phase3/ |
| V3_PHASE3_TASK8_COMPLETION.md | docs/phases/phase3/ |
| V3_PHASE3_TASK9_COMPLETION.md | docs/phases/phase3/ |
| V3_PHASE3_UAT_CHECKLIST.md | docs/phases/phase3/ |

---

### 类别 9: 移动到 docs/phases/phase4/ （9 个）

| 源文件 | 目标位置 |
|--------|----------|
| V3_PHASE4_CHUNK2_COMPLETE.md | docs/phases/phase4/ |
| V3_PHASE4_CHUNK3_COMPLETE.md | docs/phases/phase4/ |
| V3_PHASE4_CHUNK4_COMPLETE.md | docs/phases/phase4/ |
| V3_PHASE4_CHUNK5_COMPLETE.md | docs/phases/phase4/ |
| V3_PHASE4_COMPLETION_REPORT.md | docs/phases/phase4/ |
| V3_PHASE4_HANDOFF.md | docs/phases/phase4/ |
| V3_PHASE4_NEXT_STEPS.md | docs/phases/phase4/ |
| V3_PHASE4_PLAN.md | docs/phases/phase4/ |

---

### 类别 10: 移动到 docs/phases/phase5/ （10 个）

| 源文件 | 目标位置 |
|--------|----------|
| V3_PHASE5_CHUNK1_COMPLETE.md | docs/phases/phase5/ |
| V3_PHASE5_CHUNK2_COMPLETE.md | docs/phases/phase5/ |
| V3_PHASE5_CHUNK3_COMPLETE.md | docs/phases/phase5/ |
| V3_PHASE5_CHUNK4_COMPLETE.md | docs/phases/phase5/ |
| V3_PHASE5_CHUNK5_COMPLETE.md | docs/phases/phase5/ |
| V3_PHASE5_CHUNK6_PREP.md | docs/phases/phase5/ |
| V3_PHASE5_COMPLETION_REPORT.md | docs/phases/phase5/ |
| V3_PHASE5_PLAN.md | docs/phases/phase5/ |
| V3_PHASE5_PROGRESS_SUMMARY.md | docs/phases/phase5/ |
| V3.1_PHASE5_COMPLETE.md | docs/phases/phase5/ |

---

### 类别 11: 移动到 docs/releases/ （2 个）

| 源文件 | 目标位置 |
|--------|----------|
| RELEASE_NOTES_V3.0.0.md | docs/releases/v3.0.0/ |
| V3_RELEASE_GUIDE.md | docs/releases/ |

---

### 类别 12: 移动到 docs/uat/ （2 个）

| 源文件 | 目标位置 |
|--------|----------|
| V3_UAT_PLAN.md | docs/uat/ |
| V3_UAT_REPORT.md | docs/uat/ |

---

### 类别 13: 移动到 docs/plans/ （3 个）

**活跃的规划文档**：

| 源文件 | 目标位置 |
|--------|----------|
| V3.2_ROADMAP.md | docs/plans/ |
| V3.3_CORE_AGENT_FEATURES.md | docs/plans/ |
| V3_NEXT_STEPS_ROADMAP.md | docs/plans/ |

---

### 类别 14: 移动到 docs/projects/ （3 个）

项目总结文档：

| 源文件 | 目标位置 |
|--------|----------|
| V3_PROJECT_CHECKLIST.md | docs/projects/ |
| V3_PROJECT_SUMMARY.md | docs/projects/ |

---

### 类别 15: 删除或移到 docs/archive/obsolete/ （3 个）

过时文档：

| 源文件 | 操作 | 原因 |
|--------|------|------|
| NEXT_STEPS.md | 移到 archive/obsolete/ | 已有新的路线图 |
| STATUS.md | 移到 archive/obsolete/ | 旧的状态文件 |
| V2_DEVELOPMENT_COMPLETE.md | 移到 archive/obsolete/ | V2 不再是重点 |

---

### 类别 16: 移动到 v3/docs/ （1 个）

V3 特定的技术文档：

| 源文件 | 目标位置 |
|--------|----------|
| V3_PHASE2_ACCEPTANCE_TEST_GUIDE.md | v3/docs/ |

**注意**: v3/docs/ 中还应该添加刚创建的 V3_PRIORITY_FEATURES_ROADMAP.md（已经在正确位置）

---

## 📋 执行步骤

### 步骤 1: 创建目录结构
```bash
mkdir -p docs/archive/{sessions,handoffs,git-cleanup,tui-performance,obsolete}
mkdir -p docs/releases/v3.0.0
mkdir -p docs/plans
mkdir -p docs/phases/{phase1,phase2,phase3,phase4,phase5}
mkdir -p docs/uat
mkdir -p docs/projects
```

### 步骤 2: 移动文件（按类别执行）
使用 git mv 保留历史记录

### 步骤 3: 更新引用
检查并更新 README.md 和其他文档中的链接

### 步骤 4: 提交更改
```bash
git add .
git commit -m "docs: 整理项目根目录文档结构

- 创建分类目录结构
- 将 79 个历史/临时文档移到 docs/ 归档
- 根目录保留 4 个核心文档
- 更新文档引用和链接
"
```

---

## ✅ 预期结果

**清理前**（根目录）:
- 83 个 Markdown 文档
- 混乱无序

**清理后**（根目录）:
- 4 个核心文档：README.md, CLAUDE.md, CHANGELOG.md, ROADMAP.md
- 1 个临时清理计划：DOCS_CLEANUP_PLAN.md（执行后删除）

**清理后**（docs/ 目录）:
- 79 个归档文档，按功能分类
- 结构清晰，易于查找

---

## 🚀 开始执行？

准备好后，我可以帮你：
1. 执行所有文件移动命令
2. 更新相关链接
3. 提交到 Git

**是否开始执行整理？**
