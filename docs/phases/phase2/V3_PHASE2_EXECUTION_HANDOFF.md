# V3 Phase 2 执行交接文档

**创建时间**: 2026-03-16
**状态**: ✅ 计划完成，准备执行
**执行方式**: Subagent-driven development（推荐）

---

## 背景

### Phase 1 状态
- ✅ 100% 完成（17/17 任务）
- ✅ 41 个测试通过（Core 27 + Infrastructure 14）
- ✅ 数据库迁移完成（agent.db）
- ✅ 测试覆盖率 85%+

### Phase 2 目标
实现 LLM 集成，让 General Agent V3 能够与本地 LLM 平台（Ollama、LM Studio、llama.cpp、OMLX）进行对话。

---

## 工作环境

### 工作目录
```bash
cd /Users/shichang/Workspace/projects/ai-powered/general-agent/.worktrees/v3-phase1/v3
```

### 计划文档
- **实施计划**: `docs/superpowers/plans/2026-03-16-v3-phase2-llm-integration.md`
- **设计规范**: `docs/superpowers/specs/2026-03-16-v3-phase2-llm-integration-design.md`

### Git 分支
- **当前分支**: `main`（在 v3-phase1 工作树）
- **提交前缀**: `feat(v3-*)`, `test(v3-*)`, `docs(v3-*)`

---

## 执行命令

### 方式 1: Subagent-driven Development（推荐）

在新会话中，使用以下提示词：

```
我需要执行 General Agent V3 Phase 2 的实施计划。

工作目录: /Users/shichang/Workspace/projects/ai-powered/general-agent/.worktrees/v3-phase1/v3

计划文档: docs/superpowers/plans/2026-03-16-v3-phase2-llm-integration.md

请使用 superpowers:subagent-driven-development skill 执行这个计划。

计划包含 25 个任务，分为 5 个 chunk：
- Chunk 1: Core 层扩展（3 个任务）
- Chunk 2: Infrastructure.LLM 层（6 个任务）
- Chunk 3: Application 层（5 个任务）
- Chunk 4: Console REPL（5 个任务）
- Chunk 5: 集成测试和验收（6 个任务）

关键注意事项：
1. 严格遵循 TDD 流程（测试先行）
2. 每个任务完成后进行提交
3. 使用中文编写注释和文档
4. 测试覆盖率目标 80%+
5. 预期交付 97 个单元测试全部通过

请开始执行。
```

### 方式 2: Executing Plans（无子代理环境）

```
我需要执行 General Agent V3 Phase 2 的实施计划。

工作目录: /Users/shichang/Workspace/projects/ai-powered/general-agent/.worktrees/v3-phase1/v3

计划文档: docs/superpowers/plans/2026-03-16-v3-phase2-llm-integration.md

请使用 superpowers:executing-plans skill 执行这个计划。

注意：这个计划包含 25 个任务，预计需要 8-12 小时完成。
```

---

## 计划概览

### 交付物清单

**新增项目**:
- ✅ GeneralAgent.Infrastructure.LLM（LLM 客户端实现）
- ✅ GeneralAgent.Application（业务逻辑层）
- ✅ GeneralAgent.Integration.Tests（集成测试）
- ✅ GeneralAgent.Hosts.Console（升级为 REPL）

**Core 层扩展**:
- ILLMClient, ILLMClientFactory 接口
- CompletionRequest/Response, StreamChunk, TokenUsage 模型
- LLMException 异常类型

**主要功能**:
- OpenAI 兼容 API 客户端（支持 Ollama/LMStudio/llama.cpp/OMLX）
- 流式和非流式补全
- 多提供商管理和运行时切换
- SessionService（会话 CRUD）
- ConversationService（对话编排）
- 交互式 Console REPL（7 个命令）

**测试**:
- 单元测试: 97 个
- 集成测试: 4 个（2 个快速 + 2 个需要 Ollama）
- 覆盖率目标: 80%+

---

## 任务分解

### Chunk 1: Core 层扩展（3 任务）
1. LLM 接口定义（ILLMClient, ILLMClientFactory）
2. LLM 模型（CompletionRequest/Response, StreamChunk, TokenUsage）
3. LLM 异常（LLMException, LLMErrorType）

### Chunk 2: Infrastructure.LLM 层（6 任务）
4. 创建 Infrastructure.LLM 项目
5. 配置模型和依赖注入
6. OpenAI DTO 模型
7. OpenAICompatibleClient 非流式实现
8. OpenAICompatibleClient 流式实现
9. LLMClientFactory 实现

### Chunk 3: Application 层（5 任务）
10. 创建 Application 项目
11. SessionService 实现
12. MockLLMClient（测试辅助）
13. ConversationService 实现
14. 依赖注入配置

### Chunk 4: Console REPL（5 任务）
15. 配置文件扩展（appsettings.json）
16. 更新项目依赖
17. AgentRepl 框架实现
18. REPL 命令实现
19. Program.cs 重写

### Chunk 5: 集成测试和验收（6 任务）
20. 创建集成测试项目
21. 端到端测试（含 Ollama 集成测试）
22. 运行完整测试套件
23. Console REPL 手动验收
24. 创建 README 文档
25. 最终验收和清理

---

## 验收标准

### 必须达到的指标

✅ **测试通过率**: 97/97 单元测试通过（100%）

✅ **测试覆盖率**:
- Core 模块: ≥ 85%
- Infrastructure.LLM: ≥ 70%
- Application: ≥ 75%
- 总体: ≥ 80%

✅ **功能验收**:
- Console 应用成功启动
- 所有 REPL 命令正常工作（/new, /list, /switch, /provider, /history, /help, /exit）
- 会话创建、列表、切换功能正常
- 数据持久化到 agent.db

✅ **代码质量**:
- 所有代码编译通过
- 无编译警告
- 遵循 TDD 流程
- 25 次清晰的 Git 提交

---

## 技术细节

### 依赖包版本
- .NET 10.0
- EF Core 9.0
- Spectre.Console (最新版本)
- xUnit (测试框架)
- Moq (Mock 框架)

### 配置示例

`appsettings.json`:
```json
{
  "LLM": {
    "DefaultProvider": "Ollama",
    "Providers": {
      "Ollama": {
        "Name": "Ollama",
        "BaseUrl": "http://localhost:11434",
        "DefaultModel": "llama3.2",
        "TimeoutSeconds": 120
      }
    }
  }
}
```

### 命令行参数
```bash
dotnet run                        # 使用默认提供商
dotnet run --provider=LMStudio    # 指定提供商
```

---

## 注意事项

### 开发规范
1. **使用中文**: 所有注释、文档、提交信息使用中文
2. **TDD 严格执行**: 每个功能先写测试，确保测试失败，再实现功能
3. **频繁提交**: 每个任务完成后立即提交，提交信息清晰
4. **测试覆盖**: 每个功能都要有对应的单元测试

### 已知问题和解决方案
1. **导入错误**: 使用 `Microsoft.Extensions.Logging.Abstractions`（不是 Nullogger）
2. **流式实现**: 需要 `using System.Runtime.CompilerServices;` 支持 `[EnumeratorCancellation]`
3. **内存数据库**: 测试使用 `UseInMemoryDatabase($"test_db_{Guid.NewGuid()}")` 确保隔离

### 可选项
- **集成测试**: 标记为 `[Trait("Category", "Integration")]` 的测试需要 Ollama 运行，可选择性跳过
- **手动验收**: Task 23 包含手动验收步骤，可在最后执行

---

## 快速启动检查清单

在开始执行前，确认以下内容：

- [ ] 已切换到工作目录：`cd /Users/shichang/Workspace/projects/ai-powered/general-agent/.worktrees/v3-phase1/v3`
- [ ] 确认 Phase 1 数据库存在：`ls agent.db`
- [ ] 确认计划文档存在：`ls docs/superpowers/plans/2026-03-16-v3-phase2-llm-integration.md`
- [ ] 确认设计文档存在：`ls docs/superpowers/specs/2026-03-16-v3-phase2-llm-integration-design.md`
- [ ] Git 状态清洁：`git status` 显示 working tree clean

---

## 故障排查

### 如果编译失败
```bash
dotnet clean
dotnet restore
dotnet build
```

### 如果测试失败
```bash
# 运行单个测试项目
dotnet test tests/GeneralAgent.Core.Tests/

# 查看详细输出
dotnet test --verbosity detailed

# 跳过集成测试
dotnet test --filter "Category!=Integration"
```

### 如果数据库问题
```bash
# 删除数据库重新创建
rm agent.db
cd src/GeneralAgent.Hosts.Console
dotnet run
```

---

## 完成后的检查

执行完成后，验证以下内容：

- [ ] 所有 97 个单元测试通过
- [ ] 测试覆盖率报告生成（`TestResults/CoverageReport/index.html`）
- [ ] Console 应用可以启动并正常工作
- [ ] 25 次 Git 提交历史清晰
- [ ] README_PHASE2.md 文档已创建
- [ ] V3_PHASE2_COMPLETION_REPORT.md 完成报告已创建

---

**交接文档版本**: 1.0
**创建日期**: 2026-03-16
**有效期**: 长期有效
**下一步**: 在新会话中使用上述提示词启动执行
