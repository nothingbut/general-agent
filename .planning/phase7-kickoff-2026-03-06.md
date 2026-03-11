# Phase 7.1 开发启动 - 交接文档

**创建时间:** 2026-03-06 22:14
**分支:** feature/phase7-agent-workflow
**状态:** 已初始化，准备开始实施

---

## 当前状态

### ✅ 已完成
1. ✅ Phase 7.1 详细规划文档创建
   - 位置: `docs/plans/2026-03-06-phase7-agent-workflow.md`
   - 包含完整的 4-6 周实施计划

2. ✅ 创建开发分支
   - 分支: `feature/phase7-agent-workflow`
   - 基于: main 分支（已包含 Phase 6）

3. ✅ 创建目录结构
   ```
   src/workflow/          # 核心模块
   tests/workflow/        # 测试
   docs/workflow/         # 文档
   ```

4. ✅ 创建第一个任务
   - Task #6: 设计数据库表结构
   - 状态: in_progress

### 📋 待办事项

#### 第一阶段：核心架构（Week 1-2）

**Task 7.1.1: 设计数据库表结构** ← 下一步从这里开始
- [ ] 创建 `src/storage/migrations/007_workflow_tables.sql`
- [ ] 定义 3 张表：workflows, task_executions, workflow_approvals
- [ ] 更新 `src/storage/database.py` 添加 workflow 相关方法
- [ ] 编写数据库迁移测试

**Task 7.1.2: 实现 Workflow/Task 模型**
- [ ] 创建 `src/workflow/models.py`
- [ ] 定义 Workflow, Task, TaskStatus, WorkflowStatus 等类
- [ ] 使用 dataclass 和类型注解
- [ ] 编写模型单元测试

**Task 7.1.3: 实现工具注册表**
- [ ] 创建 `src/workflow/orchestrator.py`
- [ ] 实现工具发现和注册
- [ ] 支持 MCP/Skills/RAG/LLM 四种工具类型
- [ ] 编写工具调用接口

**Task 7.1.4: 实现任务规划器**
- [ ] 创建 `src/workflow/planner.py`
- [ ] 设计 LLM Prompt 模板
- [ ] 实现计划生成和验证
- [ ] 测试不同场景的规划质量

**Task 7.1.5: Week 1 单元测试**
- [ ] 测试覆盖率 ≥ 80%
- [ ] 所有测试通过

---

## 开发环境

### 分支信息
```bash
# 当前分支
git branch --show-current
# 输出: feature/phase7-agent-workflow

# 查看当前状态
git status
```

### 目录结构
```
/Users/shichang/Workspace/program/python/general-agent/
├── src/
│   ├── workflow/          # ← 新模块
│   │   ├── __init__.py
│   │   ├── models.py      # 数据模型
│   │   ├── planner.py     # 任务规划器
│   │   ├── executor.py    # 执行引擎
│   │   ├── orchestrator.py # 工具编排
│   │   ├── state.py       # 状态管理
│   │   └── approval.py    # 审批机制
│   └── storage/
│       └── migrations/
│           └── 007_workflow_tables.sql  # ← 第一个任务
│
├── tests/
│   └── workflow/          # ← 新测试目录
│
├── docs/
│   ├── plans/
│   │   └── 2026-03-06-phase7-agent-workflow.md  # 详细规划
│   └── workflow/          # ← 新文档目录
│
└── .planning/
    └── phase7-kickoff-2026-03-06.md  # 本文件
```

---

## 下一步操作指南

### 继续开发（推荐）

```bash
# 1. 确认在正确分支
cd /Users/shichang/Workspace/program/python/general-agent
git branch --show-current  # 应该显示 feature/phase7-agent-workflow

# 2. 查看任务列表
# 告诉 Claude: "继续 Task 7.1.1: 设计数据库表结构"

# 3. Claude 会帮你：
#    - 创建数据库迁移脚本
#    - 更新 database.py
#    - 编写测试
#    - 提交代码
```

### 或者查看规划

```bash
# 查看完整规划文档
cat docs/plans/2026-03-06-phase7-agent-workflow.md

# 查看数据库表设计（规划文档 7.4 节）
```

---

## 关键设计决策

### 1. 数据模型
**3 张核心表：**
- `workflows` - 工作流主表（存储目标、计划、状态）
- `task_executions` - 任务执行记录（存储每个任务的执行结果）
- `workflow_approvals` - 审批记录（存储用户审批决策）

### 2. 工具类型
**支持 4 种工具：**
- `mcp:*` - MCP 工具调用
- `skill:*` - Skills 技能执行
- `rag:*` - RAG 知识库操作
- `llm:*` - LLM 直接调用

### 3. 执行模式
- 使用 DAG（有向无环图）表示任务依赖
- 支持并行执行（无依赖任务）
- 异步执行（asyncio）
- 状态持久化（支持崩溃恢复）

### 4. 审批机制
- 危险操作需用户审批
- Web 和 TUI 双界面支持
- 支持批准/拒绝/暂停

---

## 参考资料

### 规划文档位置
```
docs/plans/2026-03-06-phase7-agent-workflow.md
```

**关键章节：**
- 第 7.4 节：状态管理器（包含完整 SQL 表结构）
- 第 7.1 节：任务规划器
- 第 7.2 节：执行引擎
- 第 7.3 节：工具编排器

### 现有代码参考
```python
# MCP 集成参考
src/mcp/executor.py          # 工具调用模式
src/mcp/connection.py        # 连接管理

# Skills 参考
src/skills/executor.py       # 技能执行

# RAG 参考
src/rag/retrieval.py         # 检索接口

# 数据库参考
src/storage/database.py      # 数据库操作模式
src/storage/models.py        # 模型定义模式
```

---

## 实施检查清单

### Task 7.1.1 完成标准
- [ ] SQL 迁移脚本创建
- [ ] 3 张表定义完整
- [ ] 必要的索引已添加
- [ ] database.py 方法实现
- [ ] 单元测试编写并通过
- [ ] 代码提交并推送

### 每个 Task 的标准流程
1. **创建文件** - 按规划创建相应文件
2. **编写代码** - 实现核心功能
3. **编写测试** - 单元测试覆盖
4. **运行测试** - 确保通过
5. **代码审查** - 使用 code-reviewer 子代理
6. **提交代码** - 清晰的 commit message
7. **更新任务** - 标记为 completed

---

## Phase 7 整体进度

```
Phase 7.1: Agent 工作流与任务编排
├─ Week 1-2: 核心架构
│  ├─ [●○○○○] Task 7.1.1: 数据库表结构  ← 当前
│  ├─ [○○○○○] Task 7.1.2: 数据模型
│  ├─ [○○○○○] Task 7.1.3: 工具注册表
│  ├─ [○○○○○] Task 7.1.4: 任务规划器
│  └─ [○○○○○] Task 7.1.5: 单元测试
│
├─ Week 3-4: 人机协同
├─ Week 4-5: 容错优化
└─ Week 6: 文档测试
```

**当前进度:** 0% (0/20+ tasks)
**预计完成:** 4-6 周后

---

## 联系上下文

**Phase 1-6 回顾：**
- Phase 1: 基础聊天 ✅
- Phase 2: Skill System ✅
- Phase 3-4: MCP Integration ✅
- Phase 5: RAG Engine ✅
- Phase 6: TUI Terminal ✅
- **Phase 7: Agent Workflow** ← 当前

**Git 历史：**
```bash
# 查看 Phase 6 提交
git log --oneline main --grep="phase6" | head -5

# 查看当前分支
git log --oneline -5
```

---

## 快速启动命令

```bash
# 启动开发
cd /Users/shichang/Workspace/program/python/general-agent
git checkout feature/phase7-agent-workflow

# 新会话中告诉 Claude:
"继续 Phase 7.1 Task 7.1.1: 设计数据库表结构"

# 或查看规划:
"查看 Phase 7.1 规划文档"
```

---

**准备好了！下次对话直接说"继续 Task 7.1.1"即可开始实施。** 🚀

**记住：**
- 当前分支: `feature/phase7-agent-workflow`
- 第一个任务: Task 7.1.1 设计数据库表结构
- 规划文档: `docs/plans/2026-03-06-phase7-agent-workflow.md`
