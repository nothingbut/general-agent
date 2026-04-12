# V3 优先功能路线图

**创建时间**: 2026-04-02
**最后更新**: 2026-04-09
**基于**: 用户在多次会话中明确的优先功能需求

---

## 🎉 所有优先功能已完成！

用户在历史会话中明确提出的 5 个优先功能**全部完成**：

1. **长期记忆** ✅
2. **上下文压缩** ✅
3. **上传文件** ✅
4. **对话中抽取 skill** ✅
5. **计划任务** ✅

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
- ✅ N+1 查询问题（待优化 `GetByIdsAsync`）- **已解决** (2026-04-06)
- ✅ 降级策略较慢（50-100秒，可优化到 1-5秒）- **已优化** (2026-04-06)
- ✅ 向量搜索排序测试失败（非关键）- **已修复** (2026-04-06)

**性能提升** (Phase 1-3 优化，2026-04-06 至 2026-04-07):
- 记忆批量加载：**500ms → <100ms**（5-10x 提升）
- 关键词搜索降级：**1-5秒 → 100-500ms**（缓存未命中）或 **50-100ms**（缓存命中）
- 索引构建（100 条记忆）：**<200ms**
- 记忆索引更新：**~1秒 → ~100ms**（10x 提升）✅ Phase 2
- 语义压缩（缓存命中）：**2-5秒 → 50-200ms**（10-25x 提升）✅ Phase 3

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

---

### 4. 上传文件功能 ⭐⭐⭐⭐

**状态**: ✅ **已完成** (Phase 1-9)

**完成时间**: 
- Phase 1-6: 2026-04-03
- Phase 7-8: 2026-04-08
- Phase 9: 2026-04-08

**核心功能**:
- ✅ 文件上传和管理（上传、列表、查看、删除）
- ✅ 多种文件类型支持（文本、代码、配置等 20+ 种）
- ✅ **对话中引用文件**（`@file:` 语法）⭐ 核心功能
- ✅ **同时引用多个文件**
- ✅ 文件内容自动提取到记忆系统
- ✅ 文件类型检测和内容处理
- ✅ 文件大小限制和内容截断
- ✅ 完整的错误处理
- ✅ **跨会话文件访问**（Phase 7-8）⭐ 核心功能
- ✅ **三级访问控制**（Private/Shared/Public）
- ✅ **权限管理**（授予/撤销读写权限）
- ✅ **版本控制**（历史版本、恢复）
- ✅ **CLI 命令集成**（11 个新命令）（Phase 9）

**代码位置**:
- `v3/src/GeneralAgent.Infrastructure.FileStorage/`
- `v3/tests/GeneralAgent.Infrastructure.FileStorage.Tests/`

**REPL 命令**:
```bash
# 基础文件操作（Phase 1-6）
/file upload <path>          # 上传文件
/file upload <path> --access-level <private|shared|public>  # 指定访问级别
/file list                   # 列出文件
/file show <id>              # 查看详情
/file content <id>           # 查看内容
/file content <id> --version <number>  # 查看特定版本
/file delete <id>            # 删除文件

# 全局文件库（Phase 9）
/file library list           # 列出所有可访问文件
/file library list --level <private|shared|public>  # 按访问级别过滤
/file library search <keyword>  # 搜索文件
/file library owned          # 列出我拥有的文件
/file library shared         # 列出共享给我的文件
/file library public         # 列出所有公开文件

# 权限管理（Phase 9）
/file share <id> --user <user-id> [--permission <read|write>]  # 共享文件
/file revoke <id> --user <user-id>  # 撤销权限
/file access <id> --level <private|shared|public>  # 修改访问级别
/file permissions <id>       # 查看权限列表

# 版本管理（Phase 9）
/file versions <id>          # 查看版本历史
/file restore <id> --version <number>  # 恢复到特定版本

# 在对话中引用
@file:filename.txt           # 按文件名引用
@file:<id>                   # 按 ID 引用
@file:config.json @file:app.cs  # 引用多个文件
```

**文档**:
- [文件上传用户指南](../guides/FILE_UPLOAD_USER_GUIDE.md)
- [文件上传实施计划](./file-upload-plan.md)
- [跨会话文件访问设计](./cross-session-file-access-design.md)（Phase 7）
- [跨会话文件访问实施总结](./cross-session-file-access-implementation-summary.md)（Phase 7）
- [Phase 8 测试完善报告](./file-storage-phase8-completion.md)
- [Phase 9 CLI 命令集成报告](./file-storage-phase9-completion.md)
- [验收测试指南](../FILE_UPLOAD_ACCEPTANCE_TEST.md)
- [CLI 使用指南](../guides/CLI_GUIDE.md) - 文件管理部分
- [CLI 命令参考](../guides/CLI_REFERENCE.md) - 文件管理命令

**测试覆盖**:
- Phase 1-6: 31 个单元/集成测试
- Phase 7-8: 111 个测试（98 单元 + 13 集成）
- 100% 测试通过率
- 覆盖率 > 80%

**已知限制**:
- 暂不支持 PDF 和 DOCX（计划在增强功能中支持）
- 暂不支持图片 OCR
- ~~文件只能在上传的会话中访问（会话隔离）~~ ✅ **已解决**（Phase 7-9 实现跨会话访问）

---

### 5. 对话中抽取 Skill ⭐⭐⭐⭐

**状态**: ✅ **已完成** (Phase 1 + 增强功能)

**完成时间**: 2026-04-06

**核心功能**:
- ✅ 自动从对话中抽取技能
- ✅ LLM 驱动的技能定义生成（YAML + Markdown）
- ✅ 模式识别和参数提取
- ✅ 交互式技能编辑和确认
- ✅ 技能命名空间管理
- ✅ 技能验证和冲突检测
- ✅ 抽取历史记录和统计
- ✅ **缓存优化**（重复请求 <10ms）（增强功能）
- ✅ **分页支持**（大量历史记录）（增强功能）
- ✅ **高级搜索**（时间范围、状态过滤）（增强功能）

**代码位置**:
- `v3/src/GeneralAgent.Infrastructure.SkillExtraction/`
- `v3/tests/GeneralAgent.Infrastructure.Tests/SkillExtraction/`

**REPL 命令**:
```bash
/skill extract <session-id>  # 从会话中抽取技能
/skill generate              # 交互式生成技能
/skill validate <path>       # 验证技能定义
/skill history               # 查看抽取历史
/skill history --status success  # 按状态过滤
/skill history --from "2024-01-01" --to "2024-12-31"  # 时间范围
/skill stats                 # 查看统计信息
```

**文档**:
- [技能抽取使用指南](./skill-extraction-usage.md)
- [技能抽取完成总结](./skill-extraction-completion-summary.md)
- [CLI 示例](./skill-extraction-cli-example.md)

**测试覆盖**:
- 56 个单元/集成测试全部通过
- 100% 核心场景覆盖
- 覆盖率 > 85%

**实现亮点**:
- **Phase 1**: 基础架构（5 个核心服务）
- **Phase 2**: 完整功能（7 个 CLI 命令）
- **Phase 3-5**: 高级功能（缓存、分页、搜索、统计）
- 总耗时: 实际 2 天（预计 2-3 周）
- 代码质量: 0 编译错误/警告，100% 测试通过

---

### 6. 计划任务功能 ⭐⭐⭐

**状态**: ✅ **已完成** (Phase 1-4)

**完成时间**: 2026-04-08

**核心功能**:
- ✅ 支持 Cron 表达式（5 字段格式）
- ✅ 支持自然语言时间表达式（中文，7 种模式）
- ✅ 三种任务类型（技能调用、记忆提醒、自定义命令）
- ✅ 任务生命周期管理（创建、暂停、恢复、删除、手动执行）
- ✅ 重试机制（指数退避，最大重试次数可配置）
- ✅ 超时控制（可配置超时时间）
- ✅ 执行历史记录和查询
- ✅ 后台服务自动调度
- ✅ 时间范围限制（start-at / end-at）

**代码位置**:
- `v3/src/GeneralAgent.Infrastructure.ScheduledTasks/`
- `v3/tests/GeneralAgent.Infrastructure.Tests/ScheduledTasks/`
- `v3/tests/GeneralAgent.Integration.Tests/ScheduledTasks/`

**REPL 命令**:
```bash
agent task schedule <name> --schedule <expression> --type <type> --payload <json>
agent task list [--status <status>] [--type <type>] [--format <table|json>]
agent task show <task-id> [--format <table|json>]
agent task update <task-id> [--schedule <expression>] [--description <text>]
agent task pause <task-id>
agent task resume <task-id>
agent task delete <task-id> [--force]
agent task run <task-id>
agent task history <task-id> [--limit <n>] [--format <table|json>]
```

**技术实现**:
1. **任务调度**：
   - `System.Threading.Timer` 轻量级调度器
   - `PriorityQueue<Guid, DateTime>` 任务队列（按执行时间排序）
   - `ConcurrentDictionary` 线程安全的运行任务跟踪
   - Cronos 库解析 Cron 表达式
   - 自定义正则表达式解析自然语言

2. **任务存储**：
   - SQLite 持久化
   - 两张表：`scheduled_tasks` 和 `task_executions`
   - 自动数据库初始化（嵌入式 SQL 脚本）

3. **任务执行**：
   - `TaskExecutor` 支持三种任务类型
   - 指数退避重试策略（2^n 秒）
   - 可配置超时和取消
   - 执行历史完整记录

4. **后台服务**：
   - `ScheduledTasksBackgroundService` 继承 `BackgroundService`
   - 自动启动/停止调度器
   - 可选禁用（测试场景）

**文档**:
- [用户指南](./scheduled-tasks-user-guide.md)
- [设计文档](./scheduled-tasks-design.md)
- [实施计划](./scheduled-tasks-implementation-plan.md)

**测试覆盖**:
- 99 个单元测试全部通过
- 12 个 E2E 集成测试全部通过
- 100% 核心场景覆盖
- 覆盖率 > 80%

**支持的调度表达式**:

**Cron 表达式**:
```
0 9 * * *          # 每天 9:00
30 14 * * *        # 每天 14:30
0 9 * * 1          # 每周一 9:00
0 9 1 * *          # 每月 1 号 9:00
*/30 * * * *       # 每 30 分钟
0 9-17 * * 1-5     # 工作日 9:00-17:00 每小时
```

**自然语言**:
```
每天9:00           # 每天早上 9 点
每天下午5点        # 每天下午 5 点（17:00）
每周一9:00         # 每周一早上 9 点
每周五下午5点      # 每周五下午 5 点
每月1号9:00        # 每月 1 号早上 9 点
每小时             # 每小时执行
每30分钟           # 每 30 分钟执行
```

**实现亮点**:
- **Phase 1**: 数据模型、仓储层、解析器（Cron + 自然语言）
- **Phase 2**: 调度引擎、执行引擎、重试和超时
- **Phase 3**: 任务管理服务、9 个 CLI 命令
- **Phase 4**: 后台服务、E2E 测试、用户文档
- 总耗时: 实际 4 天（预计 2-3 周）
- 代码质量: 0 编译错误/警告，100% 测试通过

---

## 🚧 未完成功能

无 - 所有用户优先功能已完成 🎉

---

## 📊 功能优先级矩阵

| 功能 | 状态 | 优先级 | 难度 | 实际耗时 | 用户价值 | 评分 |
|------|------|--------|------|----------|----------|------|
| 长期记忆 | ✅ 完成 | ⭐⭐⭐⭐⭐ | 高 | - | 极高 | ⭐⭐⭐⭐⭐ |
| 上下文压缩 | ✅ 完成 | ⭐⭐⭐⭐⭐ | 中 | - | 高 | ⭐⭐⭐⭐⭐ |
| 技能系统 | ✅ 完成 | ⭐⭐⭐⭐ | 中 | - | 高 | ⭐⭐⭐⭐ |
| **上传文件** | ✅ 完成 | ⭐⭐⭐⭐ | 中 | - | 高 | ⭐⭐⭐⭐ |
| **对话抽取Skill** | ✅ 完成 | ⭐⭐⭐⭐ | 中-高 | 2天 | 中-高 | ⭐⭐⭐⭐ |
| **计划任务** | ✅ 完成 | ⭐⭐⭐ | 中 | 4天 | 中 | ⭐⭐⭐ |

---

## 🚀 推荐实施顺序

### ✅ V3 核心功能 - 全部完成！🎉

**完成时间**: 2026-03-17 至 2026-04-09

**已完成任务**:
1. ✅ 长期记忆系统（Phase 1 + Phase 2）
2. ✅ 上下文压缩系统
3. ✅ 技能系统
4. ✅ 文件上传功能（Phase 1-9）⭐
5. ✅ 对话中抽取 Skill（Phase 1 + 增强功能）⭐
6. ✅ 计划任务功能（Phase 1-4）⭐ **NEW!**
7. ✅ Phase 2 技术债务清理
8. ✅ 测试基础设施建设
9. ✅ 文档重组

**成果**:
- **5 个用户优先功能全部完成** 🎉🎉🎉
- 586 个单元测试 + 111 个文件存储测试 + 56 个技能抽取测试 + 111 个计划任务测试
- 文档结构清晰，80%+ 测试覆盖率
- 100% 测试通过率

---

### 所有里程碑已达成 🏆

**用户优先功能完成度**: 5/5 (100%)

**里程碑**:
1. ✅ 长期记忆（已完成）
2. ✅ 上下文压缩（已完成）
3. ✅ 技能系统（已完成）
4. ✅ 上传文件（已完成）⭐
5. ✅ 对话抽取 Skill（已完成）⭐
6. ✅ 计划任务（已完成）⭐ **NEW!**

**总结**:
- 所有用户明确要求的优先功能已实现
- 系统已具备完整的通用 Agent 核心能力
- 可以开始下一阶段的增强和优化工作

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

**最后更新**: 2026-04-08
**维护者**: General Agent Team
**版本**: V3 Priority Features Roadmap v2.0
