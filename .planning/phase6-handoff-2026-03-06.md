# Phase 6 TUI 实施 - 会话交接文档
**生成时间:** 2026-03-06
**当前进度:** Phase 6.3 完成 (69%)
**下一任务:** Task 6.4.1 添加启动检查

---

## 交接提示词 (复制使用)

```
继续 Phase 6 TUI 实施，当前在 worktree 中工作：

工作目录：
cd /Users/shichang/Workspace/program/python/general-agent/.worktrees/phase6-tui/python/general-agent

当前状态：
- Phase 6.1 基础框架：✅ 完成
- Phase 6.2 TUI 核心：✅ 完成
- Phase 6.3 会话管理：✅ 完成
- Phase 6.4 完善与测试：⏳ 待开始

下一步：Task 6.4.1 添加启动检查

方法：使用子代理驱动开发 (superpowers:subagent-driven-development)

计划文件：docs/plans/2026-03-05-phase6-tui-implementation.md
进度报告：.planning/phase6-progress-final.md

请按照以下流程执行：
1. 使用 Skill tool 调用 superpowers:subagent-driven-development
2. 从 Task 6.4.1 开始执行
3. 每个任务遵循：实现 → 规格审查 → 修复 → 代码质量审查 → 完成
4. 保持原有的高质量标准（规格符合性 + 代码质量审查）
```

---

## 当前状态详情

### 工作区位置
```bash
/Users/shichang/Workspace/program/python/general-agent/.worktrees/phase6-tui/python/general-agent
```

### Git 状态
- **分支:** feature/phase6-tui-implementation
- **最新提交:** 3455167 (fix: await session operations to prevent race conditions)
- **提交总数:** 15 个干净提交
- **未提交更改:** 无

### 已完成任务 (9/13)

#### Phase 6.1: 基础框架 ✅
- [x] Task 6.1.5: 实现快速查询模式
- [x] Task 6.1.6: 手动测试快速查询模式

#### Phase 6.2: TUI 核心 ✅
- [x] Task 6.2.1: 创建 MessageList 组件
- [x] Task 6.2.2: 实现基础 TUI 应用框架
- [x] Task 6.2.3: 手动测试 TUI 基础功能

#### Phase 6.3: 会话管理 ✅
- [x] Task 6.3.1: 实现 SessionList 组件
- [x] Task 6.3.2: 集成会话列表到 TUI
- [x] Task 6.3.3: 支持 --session 参数

### 待完成任务 (4/13)

#### Phase 6.4: 完善与测试 ⏳
- [ ] Task 6.4.1: 添加启动检查 ← **下一个任务**
- [ ] Task 6.4.2: 添加集成测试
- [ ] Task 6.4.3: 更新文档
- [ ] Task 6.4.4: 最终验收测试
- [ ] Task 6.4.5: 最终提交和标签

---

## 关键文件路径

### 源代码
```
src/cli/
├── __init__.py           # CLI 模块入口
├── __main__.py           # 命令行入口
├── app.py               # TUI 主应用 (282 lines)
├── app.css              # TUI 样式
├── quick.py             # 快速查询模式 (143 lines)
├── core_init.py         # 核心组件初始化
└── widgets/
    ├── __init__.py
    ├── message_list.py  # 消息列表组件 (129 lines)
    └── session_list.py  # 会话列表组件 (148 lines)
```

### 测试文件
```
tests/cli/
├── test_quick.py        # 快速查询测试 (7 tests)
├── test_app.py          # TUI 应用测试 (4 tests)
└── test_widgets.py      # 组件测试 (11 tests)
```

### 文档
```
docs/plans/2026-03-05-phase6-tui-implementation.md  # 详细实施计划
.planning/phase6-progress-final.md                  # 最终进度报告
.planning/phase6-handoff-2026-03-06.md             # 本交接文档
```

---

## Task 6.4.1 实施要点

### 任务概述
添加启动检查模块，确保 CLI 启动前验证系统环境。

### 需要创建的文件
- `src/cli/startup.py` - 启动检查模块

### 需要修改的文件
- `src/cli/app.py` - 在 on_mount 中调用启动检查

### 主要功能
1. **check_ollama_connection()**: 检查 Ollama 服务连接
2. **check_database()**: 验证数据库可访问
3. **check_dependencies()**: 检查必需依赖是否安装
4. **startup_checks()**: 统一的启动检查入口

### 预期行为
- 如果 USE_OLLAMA=true 但 Ollama 不可用，警告但不阻止启动
- 如果数据库不可访问，抛出错误并退出
- 如果关键依赖缺失，显示友好错误消息

### 参考计划
查看 `docs/plans/2026-03-05-phase6-tui-implementation.md` 第 1502-1548 行的详细规格。

---

## 测试覆盖率注意事项

### 当前覆盖率状态
- **CLI 模块整体:** ~60%
- **需要补充测试的组件:**
  - SessionList widget: 41% → 目标 80%+
  - 集成测试场景缺失

### Task 6.4.2 重点
在 Task 6.4.2 中需要添加：
1. SessionList 组件的完整测试
2. 端到端集成测试
3. 会话切换场景测试
4. 错误处理路径测试

目标：整体测试覆盖率达到 80%+

---

## 开发工作流程

### 子代理驱动开发流程
```
1. 派遣实现子代理 (general-purpose)
   → 实现功能，遵循 TDD

2. 派遣规格审查子代理 (general-purpose)
   → 验证是否符合规格
   → 如有偏差，派遣修复子代理

3. 派遣代码质量审查子代理 (everything-claude-code:code-reviewer)
   → 检查代码质量、测试覆盖率
   → 如有问题，派遣重构子代理

4. 更新任务状态 (TaskUpdate)
   → 标记任务为 completed

5. 继续下一个任务
```

### 质量标准
- ✅ 规格符合性: 100%
- ✅ 测试覆盖率: 80%+
- ✅ 函数大小: <50 lines
- ✅ 文件大小: <800 lines
- ✅ 嵌套深度: ≤4 levels
- ✅ 不可变模式
- ✅ 全面错误处理

---

## 环境配置

### 必需环境变量
```bash
# 测试时使用 Mock LLM
export USE_OLLAMA=false

# 如果测试真实 Ollama
export USE_OLLAMA=true
export OLLAMA_BASE_URL="http://localhost:11434"
export OLLAMA_MODEL="llama3.2:latest"
```

### 安装依赖
```bash
# 确保 CLI 依赖已安装
pip install -e ".[cli]"

# 验证安装
agent --version
python -c "import textual; import typer; import rich; print('✅ Dependencies OK')"
```

### 运行测试
```bash
# 所有 CLI 测试
pytest tests/cli/ -v

# 特定测试
pytest tests/cli/test_app.py -v
pytest tests/cli/test_widgets.py -v
pytest tests/cli/test_quick.py -v

# 带覆盖率
pytest tests/cli/ --cov=src/cli --cov-report=html
```

---

## 已知问题和注意事项

### 测试覆盖率
- SessionList widget 测试覆盖率仅 41%
- 需在 Task 6.4.2 中补充集成测试

### 代码质量建议
1. **list 构建模式:** 建议使用 list comprehension 替代 append（不可变模式）
2. **magic string:** 建议将 "__new__" 定义为类常量
3. **错误处理:** 部分异步回调缺少 try-except

这些是 LOW 优先级改进，不阻塞当前进度。

---

## 成功指标

### Phase 6.4 完成标准
- [ ] 所有单元测试通过 (目标: 80+ tests)
- [ ] 测试覆盖率 ≥ 80%
- [ ] 代码质量检查通过 (ruff, mypy)
- [ ] 手动测试所有功能通过
- [ ] 文档完整 (README + CLI 用户指南)
- [ ] Git 标签创建 (v0.2.0-phase6)

### 最终交付物
1. 完整的 TUI 应用 (快速查询 + 交互式界面)
2. 完善的会话管理功能
3. 80%+ 测试覆盖率
4. 完整的用户文档
5. 干净的 git 历史

---

## 快速参考

### 常用命令
```bash
# 切换到工作区
cd /Users/shichang/Workspace/program/python/general-agent/.worktrees/phase6-tui/python/general-agent

# 查看 git 状态
git status
git log --oneline -10

# 运行测试
pytest tests/cli/ -v

# 启动 TUI（测试模式）
export USE_OLLAMA=false
agent --tui

# 快速查询（测试模式）
export USE_OLLAMA=false
agent "你好"

# 查看进度
cat .planning/phase6-progress-final.md
```

### Task 工具命令
```bash
# 查看任务列表
/tasks

# 更新任务状态
TaskUpdate(taskId="1", status="in_progress")
TaskUpdate(taskId="1", status="completed")
```

---

## 联系信息

**项目:** General Agent TUI Implementation
**阶段:** Phase 6 (Terminal User Interface)
**进度:** 69% (9/13 tasks)
**预计剩余:** 6-8 小时

**关键文档:**
- 计划: docs/plans/2026-03-05-phase6-tui-implementation.md
- 进度: .planning/phase6-progress-final.md
- 交接: .planning/phase6-handoff-2026-03-06.md (本文件)

---

**准备就绪！** 使用上面的交接提示词即可无缝继续开发。
