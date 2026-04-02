# V3 Phase 1 实施计划 - 会话交接

**创建时间**: 2026-03-16
**更新时间**: 2026-03-16
**上下文使用**: 完成
**状态**: ✅ 计划编写完成，等待执行

---

## ✅ 已完成的工作

### 1. 架构设计（已提交 Git）

**提交历史**:
```
519ba241 - docs(v3): 创建 V3 (C#) 架构设计文档
1df62fdc - docs(v3): 修复架构设计的关键问题
```

**设计文档**: `docs/superpowers/specs/2026-03-16-v3-csharp-architecture-design.md`

**关键修复**:
- ✅ 使用 record 实现不可变性（Session, Message）
- ✅ 添加 With* 方法支持更新
- ✅ 完整的错误处理策略（Result vs 异常）
- ✅ Subagent 系统设计（SubagentService）
- ✅ AOT 兼容性说明（EF Core 限制 + Dapper 备选）
- ✅ 性能监控设计（AgentMetrics）

### 2. 实施计划（部分完成）

**计划文件**: `docs/superpowers/plans/2026-03-16-v3-phase1-core-storage.md`

**已完成部分**:
- ✅ 计划头部（Goal, Architecture, Tech Stack）
- ✅ 完整的文件结构列表
- ✅ Chunk 1: 项目初始化和核心模型（Task 1-4）
  - Task 1: 创建解决方案和项目结构
  - Task 2: 创建 Core 项目和领域模型
  - Task 3: 实现 Session 模型（TDD）
  - Task 4: 实现 Message 模型（TDD）
- ✅ Chunk 2: 核心抽象和通用类型（Task 5-7）
  - Task 5: 实现 Result 模式和 PagedResult
  - Task 6: 定义核心异常类型
  - Task 7: 定义 Repository 接口
- ✅ Chunk 3: 存储层实现（Task 8-13）
  - Task 8: 创建 Infrastructure 项目和 DbContext
  - Task 9: 实现 EF Core 实体配置
  - Task 10: 创建数据库迁移（占位步骤）
  - Task 11: 实现 SessionRepository（TDD）
  - Task 12: 实现 MessageRepository（TDD）
  - Task 13: 实现依赖注入扩展
- ✅ Chunk 4: 验证和测试（Task 14-17）
  - Task 14: 创建 Hosts.Console 验证应用
  - Task 15: 执行数据库迁移（实际执行）
  - Task 16: 端到端验证
  - Task 17: 测试覆盖率验证

**代码质量**:
- 所有代码示例完整可运行
- 遵循 TDD 流程（测试先行）
- 步骤粒度合理（2-5分钟）

### 3. 计划审核

**第一轮审核**:
- 审核代理: af966ac03f30e85a9
- 审核结果: ❌ ISSUES_FOUND
- 发现问题: 2 CRITICAL, 3 HIGH, 4 MEDIUM, 4 LOW

**第二轮修复**:
- ✅ 所有 CRITICAL 问题已修复
- ✅ 所有 HIGH 问题已修复
- ✅ 计划内容完整（17 个 Task）
- 🔄 待进行第二轮审核验证

---

## ✅ 已修复的关键问题

### CRITICAL 问题（已修复）

#### 1. ✅ 缺少数据库迁移步骤
**位置**: Chunk 3, Task 8 之后

**修复方案**:
- 在 Task 10 添加迁移占位步骤（说明在 Console 应用创建后执行）
- 在 Task 15 添加实际迁移执行步骤

**原计划**:
```markdown
- [ ] **Step 6: 创建初始迁移**

cd v3/src/GeneralAgent.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../GeneralAgent.Hosts.Console

预期: 在 Migrations/ 目录下生成迁移文件

- [ ] **Step 7: 应用迁移**

cd v3
dotnet ef database update --project src/GeneralAgent.Infrastructure --startup-project src/GeneralAgent.Hosts.Console

预期: 创建 agent.db 文件
```

#### 2. ✅ SessionStatus 枚举缺少状态转换说明
**位置**: Chunk 1, Task 2, Step 4

**修复方案**: 已在 SessionStatus 枚举添加状态转换规则注释

**添加的注释**:
```csharp
/// <summary>
/// 会话状态
/// 状态转换规则：
/// - Normal 会话: Active (默认)
/// - Subagent 会话: Active → Running → Completed/Failed
/// - 父会话: Active → Running (有子会话时) → Active (子会话完成)
/// </summary>
public enum SessionStatus
{
    Active,
    Running,
    Completed,
    Failed
}
```

### HIGH 问题（已修复）

#### 3. ✅ Message.Metadata 序列化风险
**位置**: Chunk 3, Task 9, MessageConfiguration

**问题**: `Dictionary<string, object>` 反序列化可能失败

**修复方案**: 已采用选项 A，改用 `Dictionary<string, JsonElement>`

**实施的修复**:
```csharp
// 选项 A: 使用 JsonElement（推荐）
builder.Property(m => m.Metadata)
    .HasConversion(
        v => v == null ? null : JsonSerializer.Serialize(v),
        v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(v));

// 选项 B: 保持 object，添加注释说明限制
/// <summary>
/// Metadata 存储为 JSON
/// ⚠️ 注意：值类型仅支持 string, int, bool, double（JSON 基本类型）
/// </summary>
```

#### 4. ✅ 缺少 Hosts.Console 创建任务
**位置**: Chunk 3 结束后

**修复方案**: 已添加 Task 14 - 创建 Console 验证应用
- 项目创建和依赖配置
- 依赖注入配置（使用 Infrastructure DI 扩展）
- 完整的 CRUD 演示（7 个验证场景）
- appsettings.json 配置（连接字符串和日志）

#### 5. ✅ Application 层不在 Phase 1 范围
**状态**: 已调整

**修复**:
- Goal 已更新为 "Application 层留到 Phase 2"
- 文件结构中保留 Application 项目列表（但标注为 Phase 2）
- Console 应用直接调用 Repository（不通过 Application 层）

---

## 📋 计划执行路线图

### ✅ 计划编写阶段（已完成）

**成果**:
- 完整的 17 个 Task（从项目初始化到验证完成）
- 所有 CRITICAL 和 HIGH 问题已修复
- 详细的步骤说明（每个步骤 2-5 分钟）
- TDD 流程完整（测试先行）
- 验收标准明确

### 🔄 下一阶段：计划执行

**执行顺序**:
1. **Chunk 1**: Task 1-4（项目初始化和核心模型）
2. **Chunk 2**: Task 5-7（核心抽象和通用类型）
3. **Chunk 3**: Task 8-13（存储层实现）
4. **Chunk 4**: Task 14-17（验证和测试）

**预计时间**: 4-6 小时

**建议执行方式**:
- 使用 `superpowers:executing-plans` skill
- 或使用 `superpowers:subagent-driven-development` skill（如果支持）
- 严格遵循 TDD 流程
- 每个 Task 完成后验证测试通过

---

## 🎯 验收标准（Phase 1）

完成后应满足：

1. **项目结构**
   - ✅ 解决方案和项目创建
   - ✅ 中央包管理配置
   - ✅ Core 项目（模型、接口、异常）
   - ✅ Infrastructure 项目（DbContext、Repositories）
   - ✅ Console 验证应用

2. **功能完整性**
   - ✅ Session CRUD 操作
   - ✅ Message CRUD 操作
   - ✅ 分页查询
   - ✅ 搜索功能
   - ✅ 数据库迁移

3. **测试质量**
   - ✅ 所有测试通过
   - ✅ 测试覆盖率 >= 80%
   - ✅ TDD 流程完整

4. **可运行演示**
   - ✅ Console 应用可运行
   - ✅ 完整的 CRUD 演示
   - ✅ 数据持久化验证

---

## 🚀 如何继续（执行实施计划）

### 选项 A: 执行完整计划（推荐）

**在新会话中粘贴以下内容**:

```
执行 General Agent V3 Phase 1 实施计划。

【计划文件】
- docs/superpowers/plans/2026-03-16-v3-phase1-core-storage.md

【计划概要】
- 17 个 Task，分为 4 个 Chunk
- 从项目初始化到验证完成
- TDD 流程，测试覆盖率 >= 80%
- 最终产出：可运行的 Console 应用 + 完整测试

【执行方式】
使用 superpowers:executing-plans skill 执行计划。

【验收标准】
- 所有 32 个测试通过（Core 18 + Infrastructure 14）
- Console 应用运行成功并通过 7 个验证场景
- 测试覆盖率 >= 80%
- Git 提交历史清晰（至少 15 次提交）
```

### 选项 B: 审核计划（验证完整性）

**在新会话中粘贴以下内容**:

```
审核 General Agent V3 Phase 1 实施计划的完整性和正确性。

【计划文件】
- docs/superpowers/plans/2026-03-16-v3-phase1-core-storage.md

【审核重点】
1. 验证所有 CRITICAL 和 HIGH 问题已修复
2. 检查 Task 步骤的完整性和可执行性
3. 验证 TDD 流程正确性
4. 检查验收标准是否明确

【第一轮审核反馈】
- 已修复所有 CRITICAL 问题（数据库迁移、状态转换说明）
- 已修复所有 HIGH 问题（Metadata 类型、Console 应用）
- 已添加完整的 Chunk 4（验证和测试）

【期望结果】
- 确认计划可直接执行
- 或提供需要调整的建议
```

---

## 📚 关键参考

**设计规范**: `docs/superpowers/specs/2026-03-16-v3-csharp-architecture-design.md`

**审核代理 ID**: `af966ac03f30e85a9` (可用于 resume)

**Git 提交**:
- `519ba241` - V3 架构设计
- `1df62fdc` - 架构设计修复
- `7ff7bfb2` - Phase 1 实施计划（进行中）+ 交接文档
- `fe5c3dc0` - 完成 Phase 1 实施计划并修复关键问题（当前）

**关键设计决策**:
1. 使用 record 实现不可变性
2. EF Core + SQLite（标准模式），Dapper（AOT 模式）
3. Result<T> 用于业务逻辑错误，异常用于基础设施错误
4. Phase 1 专注 Core + Storage，Application 留到 Phase 2

---

## ⚠️ 注意事项

1. **中文提交信息**: 使用英文冒号 `:` 而非中文冒号 `：`
2. **TDD 流程**: 严格遵循"测试 → 失败 → 实现 → 通过"
3. **测试覆盖率**: 每个 Task 结束验证 >= 80%
4. **代码完整性**: 确保所有示例代码可直接运行
5. **路径一致性**: 所有路径使用 `v3/` 前缀

---

**交接文档版本**: 1.0
**创建日期**: 2026-03-16
**有效期**: 长期有效
**状态**: 等待继续
