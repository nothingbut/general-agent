# 继续 General Agent V3 Phase 3 - Task 8 (集成测试)

## 📊 当前进度

- ✅ Phase 1: Core + Storage 已完成
- ✅ Phase 2: LLM Integration 已完成
- 🚀 Phase 3: Skills System 进行中（78% 完成）
  - ✅ Task 1-2: 核心模型和解析器
  - ✅ Task 3-4: 加载器和注册表
  - ✅ Task 5: 执行器
  - ✅ Task 6: ConversationService 集成
  - ✅ Task 7: 创建示例技能（刚完成！）
  - ⏳ Task 8: 集成测试（下一步）
  - ⏳ Task 9: 文档和手动验收

## 📝 Task 7 完成情况

✅ 创建了 6 个示例技能：
1. **personal/greeting.md** - 个性化问候（条件判断）
2. **personal/reminder.md** - 提醒事项（布尔参数）
3. **productivity/task.md** - 任务创建（数组、循环）
4. **productivity/meeting.md** - 会议安排（for.index）
5. **utilities/calculate.md** - 数学计算（show_steps）
6. **utilities/format.md** - 文本格式化（变量赋值、过滤器）

✅ 创建了 .ignore 文件和 README.md
✅ 所有 258 个测试通过
✅ 编译成功，无警告

## 🎯 Task 8: 集成测试

### 目标
创建端到端集成测试，验证技能系统的完整工作流程。

### 测试范围

#### 1. 技能加载集成测试
- 从文件系统加载技能
- 解析 YAML frontmatter
- 验证参数定义
- 处理 .ignore 文件
- 命名空间解析

#### 2. 技能执行集成测试
- 解析 @ 和 / 语法
- 提取参数值
- 执行 Scriban 模板
- 返回渲染结果
- 错误处理

#### 3. ConversationService 集成测试
- 完整的对话流程
- 技能调用识别
- LLM 集成（模拟）
- 消息存储

### 实施步骤

#### Step 1: 创建测试类
```
tests/GeneralAgent.Application.Tests/Integration/
└── SkillSystemIntegrationTests.cs
```

#### Step 2: 测试用例设计

```csharp
public class SkillSystemIntegrationTests
{
    [Fact]
    public async Task LoadAndExecuteSkill_FromFileSystem_Success()
    {
        // 加载 greeting 技能并执行
    }

    [Fact]
    public async Task ParseAndExecuteSkillCall_AtSyntax_Success()
    {
        // 测试 @greeting user_name='张三'
    }

    [Fact]
    public async Task ParseAndExecuteSkillCall_SlashSyntax_Success()
    {
        // 测试 /greeting user_name='张三'
    }

    [Fact]
    public async Task ExecuteSkill_WithArrayParameter_Success()
    {
        // 测试 @task tags=['bug','urgent']
    }

    [Fact]
    public async Task ExecuteSkill_WithBoolParameter_Success()
    {
        // 测试 @reminder is_urgent=true
    }

    [Fact]
    public async Task ExecuteSkill_WithNamespace_Success()
    {
        // 测试 @personal:greeting
    }

    [Fact]
    public async Task ExecuteSkill_MissingRequiredParameter_ThrowsException()
    {
        // 测试缺少必需参数
    }

    [Fact]
    public async Task ConversationService_SkillCallInMessage_ExecutesSkill()
    {
        // 端到端测试：发送包含技能调用的消息
    }
}
```

#### Step 3: 测试数据准备

1. **使用 skills/ 目录的示例技能**
   - 无需创建临时文件
   - 直接使用已创建的技能

2. **模拟 LLM 响应**
   - 使用 ILLMClient 的 Mock
   - 或使用测试专用的 FakeLLMClient

#### Step 4: 断言验证

- 验证技能执行结果包含预期文本
- 验证参数正确传递和渲染
- 验证错误消息格式
- 验证日志输出

### 文件位置

- 测试类: `tests/GeneralAgent.Application.Tests/Integration/SkillSystemIntegrationTests.cs`
- 测试技能: `skills/{personal,productivity,utilities}/` (已存在)
- 测试报告: `V3_PHASE3_TASK8_COMPLETION.md` (待创建)

### 验收标准

- ✅ 至少 8 个集成测试用例
- ✅ 覆盖加载、解析、执行、错误处理
- ✅ 所有测试通过
- ✅ 编译无警告
- ✅ 覆盖率保持 80%+

## 📚 参考资料

- Task 7 完成报告: `V3_PHASE3_TASK7_COMPLETION.md`
- Task 6 交接文档: `V3_PHASE3_TASK6_HANDOFF.md`
- 实施计划: `V3_PHASE3_PLAN.md`
- 示例技能: `skills/*/`

## 🚀 开始命令

```bash
# 创建集成测试类
mkdir -p tests/GeneralAgent.Application.Tests/Integration

# 运行现有测试确保基线
dotnet test --nologo

# 开发新的集成测试
# （在 SkillSystemIntegrationTests.cs 中编写测试）

# 运行新测试
dotnet test tests/GeneralAgent.Application.Tests/ --filter SkillSystemIntegrationTests
```

## 💡 提示

1. **使用真实技能文件**: 直接使用 `skills/` 目录下的示例技能
2. **Mock LLM 客户端**: 集成测试不需要真实的 LLM 调用
3. **测试隔离**: 每个测试独立创建服务实例
4. **清晰断言**: 使用 FluentAssertions 提高可读性
5. **错误场景**: 不要忘记测试错误情况

---

**准备好了吗？输入以下命令开始 Task 8：**

```
继续 Task 8
```
