# 继续 V3 Phase 3.4 合并 - 提示词

## 快速启动指令

请阅读 `HANDOFF_V3_PHASE34_MERGE.md` 获取完整上下文，然后执行以下操作：

---

## 当前任务

**目标**: 将 `feature/v3-skill-system-redesign` 分支合并到 `main`

**问题**: 存在 11 个文件的 API 不兼容冲突

---

## 立即行动

### 第一步：理解情况

```bash
cd /Users/shichang/Workspace/projects/ai-powered/general-agent
cat HANDOFF_V3_PHASE34_MERGE.md  # 阅读完整交接文档
```

### 第二步：查看两个分支

```bash
# 查看 main 分支状态
git log --oneline -3

# 查看 feature 分支状态
git log --oneline feature/v3-skill-system-redesign -10

# 查看分支差异统计
git diff --stat main..feature/v3-skill-system-redesign
```

### 第三步：开始合并

```bash
git checkout main
git merge feature/v3-skill-system-redesign
# 预期结果: 11 个文件冲突
```

### 第四步：解决冲突

**推荐策略**: 对于核心 API 文件，使用 feature 分支版本（经过完整审查）

**关键冲突文件优先级**:

1. **最高优先级** (核心接口):
   - `v3/src/GeneralAgent.Core/Abstractions/ITool.cs`
   - `v3/src/GeneralAgent.Core/Models/ToolCall.cs`
   - `v3/src/GeneralAgent.Core/Models/ToolDefinition.cs`

2. **高优先级** (实现类):
   - `v3/src/GeneralAgent.Application/Services/ToolRegistry.cs`
   - `v3/src/GeneralAgent.Application/Services/ToolExecutor.cs`

3. **中优先级** (测试和其他):
   - 测试文件
   - `v3/Directory.Packages.props`

**解决技巧**:

```bash
# 查看冲突文件
git status | grep "both modified"

# 对于每个冲突文件，选择最佳版本:
# 使用 feature 分支版本（推荐）:
git checkout --theirs <文件路径>

# 或手动编辑:
code <文件路径>

# 标记为已解决:
git add <文件路径>
```

### 第五步：修复编译错误

合并后可能需要修复的文件:

1. **SkillTool.cs** - 如果 ITool 使用 Dictionary，则改回 Dictionary
2. **ToolCallingOrchestrator.cs** - 移除 `timeout: null` 参数（如果签名不匹配）
3. **ConversationService.cs** - 确保使用正确的属性名（FunctionName vs ToolName）

### 第六步：验证

```bash
cd v3
dotnet build --no-restore
dotnet test --no-restore
```

**预期结果**: 423 个测试全部通过

### 第七步：完成

```bash
git commit
git branch -d feature/v3-skill-system-redesign
git worktree remove .worktrees/v3-skill-system-redesign
```

---

## 关键决策点

### API 设计选择

| API | main 版本 | feature 版本 | 推荐 |
|-----|-----------|--------------|------|
| ITool 参数 | Dictionary | IReadOnlyDictionary | **feature** (不可变) |
| ToolCall 属性 | ToolName | FunctionName | **feature** (标准) |
| ToolCall.Arguments | string | Dictionary | **feature** (类型安全) |

**理由**: Feature 版本经过两阶段代码审查，遵循最佳实践（不可变性、类型安全、行业标准命名）

---

## 如果遇到问题

### 问题 1: 太多冲突，无法手动解决

**解决方案**: 使用 feature 分支覆盖

```bash
git merge --abort
git reset --hard feature/v3-skill-system-redesign
# 然后 cherry-pick main 上需要的提交
```

### 问题 2: 不确定选择哪个版本

**原则**:
1. 优先选择经过审查的代码（feature 分支）
2. 查看测试覆盖率（feature 有 423 个测试）
3. 检查文档完整性（feature 有 391 行文档）

### 问题 3: 合并后测试失败

**步骤**:
1. 运行 `dotnet build` 查看编译错误
2. 对照 HANDOFF 文档的"已知需要修复的问题"
3. 使用 `git diff feature/v3-skill-system-redesign` 查看差异

---

## 验收标准

合并成功的标志:

- ✅ 所有文件冲突已解决
- ✅ `dotnet build` 编译通过（0 错误）
- ✅ 所有 423 个测试通过
- ✅ 性能测试仍然通过（Tool Calling 开销 < 200ms）
- ✅ 集成测试通过（7 个 E2E 测试）

---

## 时间预算

- 阅读交接文档: 10 分钟
- 解决冲突: 30-45 分钟
- 修复编译错误: 15-20 分钟
- 运行测试验证: 10 分钟
- **总计: 约 1 小时**

---

## 紧急联系

如果完全卡住，可以考虑:

1. **创建 PR 让用户决定**:
   ```bash
   git checkout feature/v3-skill-system-redesign
   git push -u origin feature/v3-skill-system-redesign
   gh pr create
   ```

2. **保留两个分支，稍后处理**:
   - main 保持在 `a4c3d4b6`
   - feature 保持在 `dba66d5a`

---

**最后提醒**: Feature 分支是高质量的实现，经过完整的双阶段审查（规范符合性 + 代码质量），值得信任。优先使用它的设计决策。
