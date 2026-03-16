# V3 Phase 1 实施计划 - 会话交接

**创建时间**: 2026-03-16
**上下文使用**: 93% (186KB/200KB)
**状态**: 计划编写进行中

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
- ✅ Chunk 3: 存储层实现开始（Task 8-9）
  - Task 8: 创建 Infrastructure 项目和 DbContext
  - Task 9: 实现 EF Core 实体配置

**代码质量**:
- 所有代码示例完整可运行
- 遵循 TDD 流程（测试先行）
- 步骤粒度合理（2-5分钟）

### 3. 计划审核

**审核代理**: af966ac03f30e85a9

**审核结果**: ❌ ISSUES_FOUND

**发现问题**:
- 2 个 CRITICAL 问题
- 3 个 HIGH 问题
- 4 个 MEDIUM 问题
- 4 个 LOW 问题

---

## 🔴 待处理的关键问题

### CRITICAL 问题

#### 1. 缺少数据库迁移步骤
**位置**: Chunk 3, Task 8 之后

**需要添加**:
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

#### 2. SessionStatus 枚举缺少状态转换说明
**位置**: Chunk 1, Task 2, Step 4

**需要添加注释**:
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

### HIGH 问题

#### 3. Message.Metadata 序列化风险
**位置**: Chunk 3, Task 9, MessageConfiguration

**问题**: `Dictionary<string, object>` 反序列化可能失败

**修复方案（二选一）**:
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

#### 4. 缺少 Hosts.Console 创建任务
**位置**: Chunk 3 结束后

**需要添加**: Task 10 - 创建验证用 Console 应用
- 项目创建
- 依赖注入配置
- 简单 CRUD 演示
- appsettings.json 配置

#### 5. Application 层不在 Phase 1 范围
**状态**: ✅ 已调整

**修改**:
- Goal 更新为 "Application 层留到 Phase 2"
- 文件结构中保留 Application 项目列表（但不实现）

---

## 📋 待完成的工作

### Chunk 3 剩余部分（当前停在 Task 9）

**Task 10**: 实现 SessionRepository（TDD）
- 创建测试项目（Infrastructure.Tests）
- 编写 Repository 测试（内存数据库）
- 实现 SessionRepository
- 验证所有 CRUD 操作

**Task 11**: 实现 MessageRepository（TDD）
- 编写 Repository 测试
- 实现 MessageRepository
- 验证外键级联删除

**Task 12**: 实现依赖注入扩展
- DependencyInjection.cs
- 注册 DbContext
- 注册 Repositories

### Chunk 4: 验证和测试（新增）

**Task 13**: 创建 Hosts.Console 验证应用
- 项目创建
- 依赖注入配置
- appsettings.json
- 简单 CRUD 演示（直接调用 Repository）

**Task 14**: 端到端验证
- 创建会话
- 添加消息
- 查询列表
- 更新和删除
- 验证数据持久化

**Task 15**: 测试覆盖率验证
- 运行覆盖率报告
- 确保 >= 80%
- 生成 HTML 报告

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

## 🚀 如何继续（新会话提示词）

**在新会话中粘贴以下内容**:

```
继续完成 General Agent V3 Phase 1 实施计划。

【当前状态】
- 位置: docs/superpowers/plans/2026-03-16-v3-phase1-core-storage.md
- 进度: 已完成 Chunk 1-3（共 9 个 Task），剩余 Chunk 3 后半部分 + Chunk 4
- 上下文: V3_PHASE1_PLAN_HANDOFF.md（完整交接文档）

【已完成】
✅ Chunk 1: 项目初始化和核心模型（Task 1-4）
✅ Chunk 2: 核心抽象和通用类型（Task 5-7）
✅ Chunk 3 前半: DbContext 和实体配置（Task 8-9）

【待完成】
⏳ Chunk 3 后半: Repository 实现（Task 10-12）
⏳ Chunk 4: 验证和测试（Task 13-15）

【关键修复】
🔴 CRITICAL: 添加数据库迁移步骤（Task 8 后）
🔴 CRITICAL: SessionStatus 状态转换说明
🟠 HIGH: Message.Metadata 序列化改用 JsonElement
🟠 HIGH: 创建 Hosts.Console 验证应用（Task 13）

【审核反馈】
完整问题列表见交接文档第 "🔴 待处理的关键问题" 部分。

【下一步】
请继续编写 Task 10-15，修复上述关键问题，然后进行第二轮审核。
```

---

## 📚 关键参考

**设计规范**: `docs/superpowers/specs/2026-03-16-v3-csharp-architecture-design.md`

**审核代理 ID**: `af966ac03f30e85a9` (可用于 resume)

**Git 提交**:
- `519ba241` - V3 架构设计
- `1df62fdc` - 架构设计修复

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
