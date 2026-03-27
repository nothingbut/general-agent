# V3 Phase 4 下一步行动指南

**更新日期**: 2026-03-24
**当前状态**: Chunk 1 代码完成，待补充测试

---

## 当前进度

### ✅ 已完成

**Chunk 1: System.CommandLine 集成** (部分完成)
- ✅ Task 1: 添加 System.CommandLine 依赖
- ✅ Task 2: 创建 RootCommand 和基础命令结构
- ✅ Task 3: 实现 `agent new` 命令（NewCommand.cs）
- ✅ Task 4: 实现 `agent list` 命令（ListCommand.cs）
- ✅ Task 5: 实现 `agent chat` 命令（ChatCommand.cs）

**已创建的文件**:
```
v3/src/GeneralAgent.Hosts.Console/Commands/
├── RootCommand.cs (42 lines)
├── NewCommand.cs (74 lines)
├── ListCommand.cs (123 lines)
└── ChatCommand.cs (128 lines)
```

### ❌ 待完成

**Chunk 1 缺失**:
- ❌ 单元测试（计划 10+ 个）
- ❌ 集成测试
- ❌ 验收测试

---

## 🎯 立即行动：补充 Chunk 1 测试

### 优先级 P0：创建测试项目

由于没有 Console 项目的测试项目，需要先创建：

```bash
cd v3/tests
dotnet new xunit -n GeneralAgent.Hosts.Console.Tests
cd GeneralAgent.Hosts.Console.Tests

# 添加项目引用
dotnet add reference ../../src/GeneralAgent.Hosts.Console/GeneralAgent.Hosts.Console.csproj
dotnet add reference ../../src/GeneralAgent.Core/GeneralAgent.Core.csproj
dotnet add reference ../../src/GeneralAgent.Application/GeneralAgent.Application.csproj

# 添加测试依赖（使用中央包管理）
# 已在 Directory.Packages.props 中定义：xunit, NSubstitute, FluentAssertions
```

### 任务清单

#### Task 1.1: 创建测试项目结构
- [ ] 创建 `GeneralAgent.Hosts.Console.Tests` 项目
- [ ] 配置项目引用和依赖
- [ ] 创建测试目录结构

#### Task 1.2: 编写 NewCommand 测试
- [ ] `NewCommand_WithTitle_ShouldCreateSession`
- [ ] `NewCommand_WithoutTitle_ShouldUseDefaultTitle`
- [ ] `NewCommand_WhenSessionCreated_ShouldDisplayId`

#### Task 1.3: 编写 ListCommand 测试
- [ ] `ListCommand_WithLimit_ShouldListSessions`
- [ ] `ListCommand_WhenNoSessions_ShouldDisplayMessage`
- [ ] `ListCommand_ShouldFormatOutput`

#### Task 1.4: 编写 ChatCommand 测试
- [ ] `ChatCommand_WithValidSession_ShouldSendMessage`
- [ ] `ChatCommand_WithInvalidSession_ShouldDisplayError`
- [ ] `ChatCommand_ShouldStreamResponse`

#### Task 1.5: 编写 RootCommand 测试
- [ ] `RootCommand_ShouldHaveAllSubCommands`
- [ ] `RootCommand_WithNoArgs_ShouldStartRepl`

#### Task 1.6: 编写集成测试
- [ ] `Integration_NewAndChat_EndToEnd`
- [ ] `Integration_ListSessions_EndToEnd`

---

## 📊 测试覆盖目标

根据 Phase 4 计划：
- **单元测试**: ≥ 10 个（Chunk 1）
- **集成测试**: 2+ 个
- **测试覆盖率**: ≥ 80%

---

## 🚀 执行方案

### 方案 A：补充测试后继续 Chunk 2（推荐）

**时间**: 1-2 天

**理由**:
- 遵循 TDD 原则，确保代码质量
- 测试为后续开发提供回归保护
- 符合项目质量标准（80% 覆盖率）

**步骤**:
1. 创建测试项目（0.5 天）
2. 编写单元测试（0.5 天）
3. 编写集成测试（0.5 天）
4. 修复发现的 bug（0.5 天）
5. 继续 Chunk 2

### 方案 B：直接继续 Chunk 2，最后补测试

**时间**: 更快（但有风险）

**理由**:
- 快速完成功能开发
- 统一补充所有测试

**风险**:
- 可能积累技术债务
- 后期修复 bug 成本更高
- 不符合 TDD 最佳实践

---

## 📋 Chunk 2-6 计划

根据 `V3_PHASE4_PLAN.md`：

### Chunk 2: 会话管理命令（Day 3-4）
- [ ] Task 6: `agent switch` 命令
- [ ] Task 7: `agent delete` 命令
- [ ] Task 8: `agent export` 命令
- [ ] Task 9: SessionSelector 工具
- [ ] Task 10: ExportHelper 工具

### Chunk 3: 技能命令（Day 5-6）
- [ ] Task 11: `agent skill list`
- [ ] Task 12: `agent skill run`
- [ ] Task 13: `agent skill info`
- [ ] Task 14: 参数解析和验证
- [ ] Task 15: 美化输出

### Chunk 4: 配置管理（Day 7-8）
- [ ] Task 16: `agent config show`
- [ ] Task 17: `agent config set`
- [ ] Task 18: `agent config reset`
- [ ] Task 19: 用户配置文件
- [ ] Task 20: 环境变量支持

### Chunk 5: REPL 增强（Day 9-10）
- [ ] Task 21: 增强 REPL 命令
- [ ] Task 22: 多行输入支持
- [ ] Task 23: 命令历史记录
- [ ] Task 24: 自动补全提示
- [ ] Task 25: 改进错误提示

### Chunk 6: 集成测试和文档（Day 11-12）
- [ ] Task 26: 端到端集成测试
- [ ] Task 27: CLI 使用文档
- [ ] Task 28: 命令参考手册
- [ ] Task 29: 使用示例和教程
- [ ] Task 30: 手动验收测试

---

## 🔍 当前代码质量检查

在继续前，建议验证：

```bash
cd v3

# 1. 构建检查
dotnet build src/GeneralAgent.Hosts.Console/

# 2. 运行现有测试
dotnet test --filter "FullyQualifiedName~GeneralAgent"

# 3. 检查命令是否可执行
dotnet run --project src/GeneralAgent.Hosts.Console/ -- new --title "测试"
dotnet run --project src/GeneralAgent.Hosts.Console/ -- list
```

---

## ⚡ 快速启动命令

### 创建测试项目
```bash
cd v3/tests
dotnet new xunit -n GeneralAgent.Hosts.Console.Tests
cd GeneralAgent.Hosts.Console.Tests

# 添加引用
dotnet add reference ../../src/GeneralAgent.Hosts.Console/
dotnet add reference ../../src/GeneralAgent.Core/
dotnet add reference ../../src/GeneralAgent.Application/

# 创建测试目录
mkdir -p Commands
touch Commands/NewCommandTests.cs
touch Commands/ListCommandTests.cs
touch Commands/ChatCommandTests.cs
touch Commands/RootCommandTests.cs
```

### 运行测试
```bash
cd v3
dotnet test tests/GeneralAgent.Hosts.Console.Tests/
```

---

## 📈 Phase 4 总体进度

| Chunk | 状态 | 进度 |
|-------|------|------|
| Chunk 1 | 🟡 部分完成 | 5/6 任务（缺测试）|
| Chunk 2 | ⏳ 待开始 | 0/5 任务 |
| Chunk 3 | ⏳ 待开始 | 0/5 任务 |
| Chunk 4 | ⏳ 待开始 | 0/5 任务 |
| Chunk 5 | ⏳ 待开始 | 0/5 任务 |
| Chunk 6 | ⏳ 待开始 | 0/5 任务 |

**整体进度**: ~8% (5/30 任务，不含测试)

---

## 💡 推荐下一步

### 立即行动（推荐方案 A）

1. **创建测试项目**（今天）
   ```bash
   cd v3/tests
   # 执行上面的快速启动命令
   ```

2. **编写 Chunk 1 测试**（明天上午）
   - 10+ 单元测试
   - 2+ 集成测试

3. **继续 Chunk 2**（明天下午开始）
   - Task 6-10
   - 会话管理命令

4. **按计划完成 Chunk 3-6**（接下来 10 天）

---

## 📚 相关文档

- [Phase 4 完整计划](V3_PHASE4_PLAN.md)
- [Phase 3.4 合并完成报告](V3_PHASE34_MERGE_COMPLETE.md)
- [项目 ROADMAP](ROADMAP.md)

---

**状态**: ✅ 准备就绪，可以开始补充测试
**下一个命令**: 创建 `GeneralAgent.Hosts.Console.Tests` 项目

🚀 **建议**: 按方案 A 执行，确保代码质量和测试覆盖率。
