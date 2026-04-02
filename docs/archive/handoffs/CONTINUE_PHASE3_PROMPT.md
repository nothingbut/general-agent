# 继续 Phase 3 开发提示词

复制以下内容到新的 Claude Code 会话中：

---

继续 General Agent V3 Phase 3 - Skills System 开发。

【当前状态】
- ✅ Phase 1: Core + Storage 已完成
- ✅ Phase 2: LLM Integration 已完成
- 🚀 Phase 3: Skills System 进行中（67% 完成）
  - ✅ Task 1-2: 核心模型和解析器
  - ✅ Task 3-4: 加载器和注册表
  - ✅ Task 5: 执行器
  - ✅ Task 6: ConversationService 集成
  - ⏳ Task 7: 创建示例技能
  - ⏳ Task 8: 集成测试
  - ⏳ Task 9: 文档和手动验收
- 分支: v3-phase1
- 工作目录: .worktrees/v3-phase1/v3

【测试状态】
✅ 所有 258 个测试通过
- Core: 73/73
- Infrastructure: 14/14
- Infrastructure.LLM: 76/76 (1 跳过)
- Infrastructure.Skills: 41/41
- Application: 54/54

【上次完成】
✅ Task 6: 集成到 ConversationService
- 创建 SkillService（技能管理服务）
- 创建 SkillCallParser（@/@ 语法解析器）
- 修改 ConversationService 集成技能调用
- 更新 Application DI 注册
- 编译成功，所有测试通过

【技能系统架构】
src/GeneralAgent.Infrastructure.Skills/
├── Models/ (Skill, SkillParameter, SkillMetadata)
├── Parsers/ (MarkdownSkillParser)
├── Loaders/ (FileSystemSkillLoader)
├── Registry/ (SkillRegistry - 线程安全)
├── Executors/ (SkillExecutor - Scriban 集成)
└── DependencyInjection.cs

src/GeneralAgent.Application/Services/
├── SkillService.cs (技能管理)
├── SkillCallParser.cs (@/@ 语法)
└── ConversationService.cs (已集成)

【技能文件格式】
```markdown
---
name: skill_name
description: 技能描述
parameters:
  - name: param_name
    type: string|int|bool|array
    required: true|false
    description: 参数说明
    default_value: 默认值
---

模板内容：{{ variable }}
{{ if condition }} ... {{ end }}
{{ for item in items }} ... {{ end }}
```

【调用语法】
```
@greeting user_name='张三'
/personal:reminder task='买牛奶' time='5pm'
@task title='Review PR' priority=high is_urgent=true
```

【下一步：Task 7 - 创建示例技能】

请执行以下步骤：

1. **创建技能目录结构**
```bash
mkdir -p skills/{personal,productivity,utilities}
```

2. **创建示例技能**（至少 5 个）
   - skills/personal/greeting.md
   - skills/personal/reminder.md
   - skills/productivity/task.md
   - skills/productivity/meeting.md
   - skills/utilities/calculate.md

3. **创建 .ignore 文件**
```bash
cat > skills/.ignore << 'EOF'
draft_*.md
_*.md
*.tmp.md
README.md
EOF
```

【参考实现】
详细示例技能代码见交接文档：V3_PHASE3_TASK6_HANDOFF.md

【验收标准】
- ✅ 至少 5 个示例技能
- ✅ 覆盖不同参数类型（string, int, bool, array）
- ✅ 展示 Scriban 功能（条件、循环、过滤器）
- ✅ 不同命名空间（personal, productivity, utilities）

【关键文件】
- 交接文档: V3_PHASE3_TASK6_HANDOFF.md
- 实施计划: V3_PHASE3_PLAN.md
- Phase 2 完成报告: V3_PHASE2_COMPLETION_REPORT.md

开始创建示例技能吧！
