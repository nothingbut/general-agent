# V3 优先功能路线图

**创建时间**: 2026-04-02
**最后更新**: 2026-04-02
**基于**: 用户在多次会话中明确的优先功能需求

---

## 📋 优先功能列表

用户在历史会话中明确提出的 5 个优先功能：

1. **长期记忆** 
2. **上下文压缩**
3. **上传文件**
4. **对话中抽取 skill**
5. **计划任务**

---

## ✅ 已完成功能

### 1. 长期记忆系统 ⭐⭐⭐⭐⭐

**状态**: ✅ **已完成** (Phase 1 + Phase 2)

**完成时间**: 
- Phase 1: 2026-03-17
- Phase 2: 2026-04-01

**核心功能**:
- ✅ 记忆 CRUD（增删改查）
- ✅ 记忆类型系统（User/Feedback/Project/Reference/Knowledge）
- ✅ LLM 驱动的记忆提取（`MemoryExtractionService`）
- ✅ 记忆检索和相关性评分
- ✅ **Embedding 向量化**（Phase 2）
- ✅ **Qdrant 向量数据库集成**（Phase 2）
- ✅ **语义搜索**（10-50ms，性能提升 1000-10000x）
- ✅ **混合搜索**（关键词 + 语义）
- ✅ **自动降级**（Qdrant 不可用时降级到 LLM 评分）
- ✅ **迁移工具**（`/memory migrate-to-vectors`）

**代码位置**:
- `v3/src/GeneralAgent.Infrastructure.Memory/`
- `v3/src/GeneralAgent.Infrastructure.Embedding/`
- `v3/src/GeneralAgent.Infrastructure.VectorDB/`

**REPL 命令**:
```bash
/memory list [type]              # 列出记忆
/memory show <name>              # 查看记忆详情
/memory add <type> <name>        # 添加记忆
/memory update <name>            # 更新记忆
/memory delete <name>            # 删除记忆
/memory search <query>           # 关键词搜索
/memory semantic-search <query>  # 语义搜索（向量）
/memory hybrid-search <query>    # 混合搜索
/memory extract <message>        # 从消息提取记忆
/memory relevant <context>       # 检索相关记忆
/memory migrate-to-vectors       # 迁移到向量数据库
```

**文档**:
- [CLI 使用指南](./CLI_GUIDE.md) - 记忆系统部分
- [CLI 命令参考](./CLI_REFERENCE.md) - `/memory` 命令
- [Phase 2 部署指南](./DEPLOYMENT_PHASE2.md)
- [Phase 2 验收测试](../V3_PHASE2_ACCEPTANCE_TEST_GUIDE.md)

**已知问题**:
- N+1 查询问题（待优化 `GetByIdsAsync`）
- 降级策略较慢（50-100秒，可优化到 1-5秒）
- 向量搜索排序测试失败（非关键）

---

### 2. 上下文压缩系统 ⭐⭐⭐⭐⭐

**状态**: ✅ **已完成** (Phase 1)

**完成时间**: 2026-03-17

**核心功能**:
- ✅ 三种压缩策略（Sliding Window / Semantic / Hierarchical）
- ✅ 智能策略选择（基于消息数量和复杂度）
- ✅ Token 计数和统计
- ✅ 压缩历史记录
- ✅ 自动压缩触发（消息数 >= 15）

**代码位置**:
- `v3/src/GeneralAgent.Infrastructure.Compression/`

**压缩策略**:
1. **Sliding Window** - 简单滑动窗口，保留最近 N 条消息
2. **Semantic** - LLM 生成摘要，保留关键信息
3. **Hierarchical** - 分层压缩，适合长对话

**文档**:
- 代码中的 XML 注释
- Phase 1 完成报告

**测试覆盖率**: 90%+

---

### 3. 技能系统 ⭐⭐⭐⭐

**状态**: ✅ **已完成** (Phase 1)

**完成时间**: 2026-03-17

**核心功能**:
- ✅ YAML + Markdown 技能定义格式
- ✅ 文件系统加载器（支持 `.ignore`）
- ✅ 命名空间解析（`personal:greeting`）
- ✅ 参数验证和类型检查
- ✅ LLM 集成（技能作为工具调用）
- ✅ 技能注册表和发现

**代码位置**:
- `v3/src/GeneralAgent.Infrastructure.Skills/`

**REPL 命令**:
```bash
/skills [namespace]        # 列出技能
/skill <name>             # 查看技能详情
/skill <name> --template  # 查看提示词模板
```

**技能调用语法**:
```bash
@skill-name arg1="value1"    # @ 语法（对话中）
/skill skill-name --arg1 value1  # / 命令语法
```

**文档**:
- [技能系统指南](./SKILLS_GUIDE.md)
- [CLI 使用指南](./CLI_GUIDE.md) - 技能部分

**注意**: 当前是手动定义技能，不是从对话中自动抽取（见"未完成功能"）

---

## 🚧 未完成功能

### 4. 上传文件功能 ⭐⭐⭐⭐

**状态**: ❌ **未实现**

**优先级**: 高

**需求描述**:
- 用户可以在对话中上传文件（文档、图片、代码等）
- 文件内容可以被 Agent 分析和处理
- 支持的文件类型：
  - 文本文件（.txt, .md, .json, .yaml）
  - 代码文件（.cs, .py, .js, .rs 等）
  - 文档文件（.pdf, .docx）
  - 图片文件（.png, .jpg - 如果支持多模态）

**技术方案**（待设计）:
1. **文件存储**：
   - 本地文件系统存储
   - 关联到会话或记忆
   - 文件元数据管理

2. **文件处理**：
   - 文本提取（PDF, DOCX）
   - 代码解析和语法高亮
   - 图片 OCR（可选）

3. **集成点**：
   - REPL 命令：`/upload <file-path>`
   - 对话中引用：`@file:filename.txt`
   - 自动记忆关联

**相关技术**:
- .NET 文件 I/O
- PDF 解析库（如 PdfPig）
- 图片处理库（如 ImageSharp）

**预计耗时**: 1-2 周

**设计文档**: 待创建 `V3_PHASE_FILE_UPLOAD_DESIGN.md`

---

### 5. 对话中抽取 Skill ⭐⭐⭐⭐

**状态**: ❌ **未实现**

**优先级**: 中-高

**需求描述**:
- Agent 在对话中识别重复性任务模式
- 自动建议将任务模式转换为技能
- 用户确认后自动生成技能定义文件
- 技能可立即使用和分享

**示例场景**:
```
用户: 帮我查看这个 API 的文档，然后生成示例代码
Agent: [完成任务]
Agent: 💡 我注意到你经常需要"查API文档+生成示例"，
      是否要创建一个 api-helper 技能？
用户: 好的
Agent: ✓ 已创建技能 dev:api-helper，可以用 @dev:api-helper api="..." 调用
```

**技术方案**（待设计）:
1. **模式识别**：
   - 分析对话历史，识别重复模式
   - LLM 提取任务步骤和参数
   - 计算置信度和频率

2. **技能生成**：
   - 自动生成 YAML frontmatter
   - 自动生成 Markdown 提示词模板
   - 自动提取参数定义

3. **用户交互**：
   - REPL 命令：`/skill extract [session-id]`
   - 自动建议（置信度 > 0.8）
   - 交互式编辑和确认

**相关功能**:
- 借鉴 `MemoryExtractionService` 的设计
- 创建 `SkillExtractionService`
- 集成到对话流程中

**预计耗时**: 2-3 周

**设计文档**: 待创建 `V3_PHASE_SKILL_EXTRACTION_DESIGN.md`

---

### 6. 计划任务功能 ⭐⭐⭐

**状态**: ❌ **未实现**

**优先级**: 中

**需求描述**:
- 用户可以创建定时执行的任务
- 支持 cron 表达式或简单的时间描述
- 任务可以是技能调用、记忆提醒、或自定义命令
- 任务状态管理和日志记录

**示例用例**:
```bash
# 每天早上 9 点提醒查看任务
/task schedule "每天 9:00" @personal:reminder task="查看今日任务"

# 每周五下午生成周报
/task schedule "每周五 17:00" @productivity:weekly-report

# 每小时检查服务状态
/task schedule "0 * * * *" @dev:health-check service="api"

# 查看计划任务
/task list

# 取消任务
/task cancel <task-id>
```

**技术方案**（待设计）:
1. **任务调度**：
   - 使用 `System.Threading.Timer` 或 `Quartz.NET`
   - 支持 cron 表达式解析
   - 自然语言时间解析（"每天9点"）

2. **任务存储**：
   - SQLite 持久化
   - 任务状态跟踪（Pending/Running/Completed/Failed）
   - 执行历史和日志

3. **任务执行**：
   - 后台服务执行（类似 `BackgroundTaskService`）
   - 技能调用集成
   - 错误处理和重试

4. **用户交互**：
   - REPL 命令：`/task schedule/list/cancel/pause/resume`
   - 任务通知（完成/失败提醒）

**注意**: 当前的 `BackgroundTaskService` 只处理标签建议，不是通用的计划任务系统。

**相关技术**:
- Quartz.NET（功能强大）或 System.Threading.Timer（简单轻量）
- Cronos（cron 表达式解析）
- Humanizer（自然语言时间处理）

**预计耗时**: 2-3 周

**设计文档**: 待创建 `V3_PHASE_SCHEDULED_TASKS_DESIGN.md`

---

## 📊 功能优先级矩阵

| 功能 | 状态 | 优先级 | 难度 | 预计耗时 | 用户价值 | 评分 |
|------|------|--------|------|----------|----------|------|
| 长期记忆 | ✅ 完成 | ⭐⭐⭐⭐⭐ | 高 | - | 极高 | ⭐⭐⭐⭐⭐ |
| 上下文压缩 | ✅ 完成 | ⭐⭐⭐⭐⭐ | 中 | - | 高 | ⭐⭐⭐⭐⭐ |
| 技能系统 | ✅ 完成 | ⭐⭐⭐⭐ | 中 | - | 高 | ⭐⭐⭐⭐ |
| **上传文件** | ❌ 待开发 | ⭐⭐⭐⭐ | 中 | 1-2周 | 高 | ⭐⭐⭐⭐ |
| **对话抽取Skill** | ❌ 待开发 | ⭐⭐⭐⭐ | 中-高 | 2-3周 | 中-高 | ⭐⭐⭐⭐ |
| **计划任务** | ❌ 待开发 | ⭐⭐⭐ | 中 | 2-3周 | 中 | ⭐⭐⭐ |

---

## 🚀 推荐实施顺序

### 短期（下周）- 解决技术债务

**目标**: 巩固 Phase 2 成果

**任务**:
1. 修复 N+1 查询问题（`GetByIdsAsync`）
2. 优化降级策略（关键词搜索）
3. 修复集成测试配置
4. 修复向量搜索排序测试

**耗时**: 1-2 天

**文档**: 参考 `docs/superpowers/handoffs/V3_PHASE2_ITERATION3_COMPLETE.md` 的"已知问题"部分

---

### 中期（2-3周）- 新功能开发

**方案 A: 上传文件功能** ⭐⭐⭐⭐ （推荐）

**理由**:
- ✅ 用户需求明确
- ✅ 技术方案相对简单
- ✅ 能立即提升用户体验
- ✅ 为后续功能（如代码分析、文档 RAG）打基础

**步骤**:
1. 创建设计文档 `V3_PHASE_FILE_UPLOAD_DESIGN.md`
2. 实现文件存储和元数据管理
3. 实现文件解析器（文本/PDF/图片）
4. 集成到 REPL（`/upload` 命令）
5. 集成到对话流程（`@file:` 引用）
6. 编写测试（单元 + E2E）
7. 更新文档

---

**方案 B: 对话抽取 Skill** ⭐⭐⭐

**理由**:
- ⚠️ 技术复杂度较高（需要模式识别 + LLM 生成）
- ⚠️ 用户价值相对较低（手动创建 Skill 也不复杂）
- ✅ 长期价值高（形成技能生态）

**建议**: 在完成"上传文件"后考虑

---

**方案 C: 计划任务** ⭐⭐⭐

**理由**:
- ⚠️ 用户需求相对较低
- ⚠️ 需要持续运行的后台服务
- ✅ 技术方案成熟（Quartz.NET）

**建议**: 优先级最低，或根据用户反馈调整

---

### 长期（1-2月）- 完成所有优先功能

**目标**: 完成用户提出的所有 5 个优先功能

**里程碑**:
1. ✅ 长期记忆（已完成）
2. ✅ 上下文压缩（已完成）
3. ✅ 技能系统（已完成）
4. 🚧 上传文件（进行中）
5. 📋 对话抽取 Skill（计划中）
6. 📋 计划任务（计划中）

---

## 📝 待创建的文档

1. `V3_PHASE_FILE_UPLOAD_DESIGN.md` - 文件上传功能设计
2. `V3_PHASE_SKILL_EXTRACTION_DESIGN.md` - 技能抽取功能设计
3. `V3_PHASE_SCHEDULED_TASKS_DESIGN.md` - 计划任务功能设计
4. `V3_IMPLEMENTATION_PLAN_FILE_UPLOAD.md` - 文件上传实施计划
5. `V3_IMPLEMENTATION_PLAN_SKILL_EXTRACTION.md` - 技能抽取实施计划
6. `V3_IMPLEMENTATION_PLAN_SCHEDULED_TASKS.md` - 计划任务实施计划

---

## 📞 反馈和讨论

如果你对功能优先级、技术方案或时间估算有任何建议，欢迎在 GitHub Issues 中讨论：
https://github.com/nothingbut/general-agent/issues

---

**最后更新**: 2026-04-02
**维护者**: General Agent Team
**版本**: V3 Priority Features Roadmap v1.0
