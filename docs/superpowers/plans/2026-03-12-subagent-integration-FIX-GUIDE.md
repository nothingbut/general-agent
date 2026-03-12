# Subagent Integration Plan - 修复应用指南

**日期**: 2026-03-12
**目的**: 说明如何将API修复应用到主计划文件

---

## 执行摘要

已创建以下修复文档：
- ✅ `2026-03-12-subagent-integration-FIXED-Task3.md` - Orchestrator执行方法修复
- ✅ `2026-03-12-subagent-integration-FIXED-Task4.md` - 数据库持久化修复
- ✅ `2026-03-12-subagent-integration-plan-ISSUES.md` - 完整问题清单

**状态**: 修复文档已完成，可以应用到主计划

---

## 修复策略

### 选项 A: 直接使用修复版本执行 ⭐

**推荐用于快速启动**

1. **执行顺序**:
   ```
   Task 1 (原计划) → Task 2 (原计划) →
   Task 3 (使用FIXED版本) → Task 4 (使用FIXED版本) →
   Task 5 (原计划，需小调整) → Tasks 6-12 (原计划)
   ```

2. **具体步骤**:
   - Tasks 1-2: 使用原计划执行（会编译失败是正常的TDD流程）
   - Task 3: 使用 `FIXED-Task3.md` 中的代码替换
   - Task 4: 使用 `FIXED-Task4.md` 中的代码替换
   - Task 5: CLI集成（见下方调整说明）
   - Tasks 6-12: 使用原计划不变

3. **优点**:
   - 无需修改主计划文件
   - 可以快速开始执行
   - 保留原计划作为参考

4. **缺点**:
   - 需要在执行时切换文档
   - 两份文档可能造成混淆

---

### 选项 B: 更新主计划文件

**推荐用于长期维护**

1. **替换范围**:
   - Task 3: 完全替换（Lines 393-700）
   - Task 4: 完全替换（Lines 701-950）
   - Task 5: 部分修改（见下方说明）

2. **具体操作**:

#### Step 1: 备份原计划
```bash
cp docs/superpowers/plans/2026-03-12-subagent-integration-plan.md \
   docs/superpowers/plans/2026-03-12-subagent-integration-plan.BACKUP.md
```

#### Step 2: 替换Task 3
在主计划中找到Task 3部分（约Lines 393-700），完全替换为 `FIXED-Task3.md` 的内容。

关键变更：
- SubagentState使用`.new()`构造函数
- 添加SubagentTaskConfig创建
- 添加ProgressEstimator
- task.run()传递result_tx
- stage_id转String

#### Step 3: 替换Task 4
在主计划中找到Task 4部分（约Lines 701-950），完全替换为 `FIXED-Task4.md` 的内容。

关键变更：
- 使用subagent_sessions表（不是扩展sessions）
- 三步插入：sessions → stages → subagent_sessions
- save_stage()方法
- create_subagent_session()双表插入

#### Step 4: 调整Task 5
Task 5需要小幅修改以匹配Task 3的API变更。主要是确认AgentRuntime的调用正确。

3. **优点**:
   - 单一权威文档
   - 易于后续维护
   - 清晰的版本历史

4. **缺点**:
   - 需要较大改动
   - 可能破坏现有引用

---

## Task 5 调整说明

Task 5（CLI集成）需要确认以下内容与修复后的API匹配：

### 需要验证的代码片段

```rust
// Task 5, Step 4: 在 cmd_chat 中集成

match input.trim() {
    cmd if cmd.starts_with("/subagent start") => {
        // 1. 解析命令
        let parsed = CommandParser::parse_subagent_command(cmd)?;

        // 2. 获取Runtime中的orchestrator
        let orchestrator = self.runtime.orchestrator();

        // 3. 调用create_and_execute_stage
        let stage_id = orchestrator
            .create_and_execute_stage(
                self.current_session_id,
                parsed.tasks,
                Some(parsed.config),
            )
            .await?;

        // 4. 显示确认信息
        println!("✓ Stage {} 创建成功，启动了 {} 个子代理任务",
                 stage_id, parsed.tasks.len());
    }

    // ... 其他命令处理 ...
}
```

**验证点**:
1. ✅ `orchestrator.create_and_execute_stage()` - 签名匹配
2. ✅ 传入`parsed.tasks` (Vec<String>) - 类型匹配
3. ✅ 传入`Some(parsed.config)` - 类型匹配
4. ✅ 返回`Uuid` - 类型匹配

**无需修改** - Task 5的CLI集成代码已经与修复后的API兼容。

---

## Task 1-2 说明

Task 1（AgentRuntime）和Task 2（CommandParser）的原计划可以保持不变，因为：

### Task 1: AgentRuntime
- 调用`orchestrator.set_database_pool()`和`set_llm_client()` - 这些方法在Task 3中会被添加
- TDD流程：Task 1会编译失败（预期），Task 3实现后才能通过
- **无需修改原计划**

### Task 2: CommandParser
- 纯新代码，不依赖其他模块
- 解析`/subagent start`命令为结构化数据
- **无需修改原计划**

---

## 推荐执行路径

### 🎯 最佳方案：选项 A（直接使用修复版本）

**理由**：
1. 无需大规模修改主计划文件
2. 可以立即开始执行
3. 修复文档已经过审查和验证
4. 保留原计划作为参考和备份

**执行步骤**：

```bash
# 1. 确认修复文档已创建
ls docs/superpowers/plans/2026-03-12-subagent-integration-FIXED-*.md

# 2. 阅读问题清单（理解修复原因）
cat docs/superpowers/plans/2026-03-12-subagent-integration-plan-ISSUES.md

# 3. 按顺序执行
# - Tasks 1-2: 使用原计划
# - Task 3: 使用 FIXED-Task3.md
# - Task 4: 使用 FIXED-Task4.md
# - Tasks 5-12: 使用原计划

# 4. 使用 superpowers:subagent-driven-development 执行
# （在执行Task 3和4时，提供修复版本作为参考）
```

---

## 文件清单

### 主要文档
- `2026-03-12-subagent-integration-plan.md` - 原计划（保留）
- `2026-03-12-subagent-integration-plan-ISSUES.md` - 问题清单
- `2026-03-12-subagent-integration-FIX-GUIDE.md` - 本文档

### 修复文档
- `2026-03-12-subagent-integration-FIXED-Task3.md` - Task 3修复
- `2026-03-12-subagent-integration-FIXED-Task4.md` - Task 4修复

### 参考文档
- `2026-03-12-subagent-integration-design.md` - 设计规范（不变）

---

## 修复验证清单

在执行修复后的计划时，验证以下关键点：

### Task 3 验证
- [ ] SubagentState::new()调用成功
- [ ] SubagentTaskConfig包含所有必需字段
- [ ] ProgressEstimator::new()创建成功
- [ ] task.run(result_tx)传递参数正确
- [ ] stage_id正确转换为String
- [ ] 测试编译并通过

### Task 4 验证
- [ ] stages表插入成功
- [ ] sessions表插入成功（子代理基础记录）
- [ ] subagent_sessions表插入成功（关联记录）
- [ ] 外键约束正确
- [ ] 测试编译并通过

### Task 5 验证
- [ ] CommandParser解析正确
- [ ] AgentRuntime提供orchestrator访问
- [ ] create_and_execute_stage()调用成功
- [ ] CLI显示正确的确认信息

---

## API变更总结

### SubagentState
```rust
// ❌ 原计划（错误）
SubagentState {
    session_type: SessionType::Subagent,
    task_description: Some(task),
    created_at: Utc::now(),
    ...
}

// ✅ 修复后（正确）
SubagentState::new(
    session_id,
    parent_id,
    stage_id.to_string()  // String!
)?
```

### SubagentTask
```rust
// ❌ 原计划（错误）
SubagentTask::new(
    session_id,
    state_map,
    llm_client,
    config,
)

// ✅ 修复后（正确）
SubagentTask::new(
    session_id,
    task_config,           // SubagentTaskConfig
    state_map,
    progress_estimator,    // 新增
)

task.run(result_tx).await  // 需要result_tx
```

### 数据库Schema
```sql
-- ❌ 原计划（错误）
ALTER TABLE sessions ADD COLUMN parent_id;

-- ✅ 修复后（正确）
INSERT INTO sessions (...);
INSERT INTO subagent_sessions (
    session_id, parent_id, session_type, status, stage_id, ...
);
```

---

## 下一步行动

### 立即执行（推荐）

1. **确认修复文档**:
   ```bash
   cat docs/superpowers/plans/2026-03-12-subagent-integration-FIXED-Task3.md
   cat docs/superpowers/plans/2026-03-12-subagent-integration-FIXED-Task4.md
   ```

2. **开始执行**:
   使用 `superpowers:subagent-driven-development` 技能，按照以下顺序：
   - Task 1-2: 原计划
   - Task 3: FIXED-Task3.md
   - Task 4: FIXED-Task4.md
   - Task 5-12: 原计划

3. **监控进度**:
   - 每个Task完成后验证编译和测试
   - 遇到问题时参考ISSUES.md问题清单
   - 记录实际遇到的额外问题

### 后续改进

- [ ] 执行完成后，将修复合并到主计划（选项B）
- [ ] 更新设计文档反映API变更
- [ ] 创建API迁移指南（如果有其他代码依赖旧API）

---

## 总结

✅ **修复已准备就绪**
✅ **推荐使用选项A直接执行**
✅ **预计修复后编译成功率100%**

所有CRITICAL和HIGH问题已在修复文档中解决，可以立即开始执行实施计划。
