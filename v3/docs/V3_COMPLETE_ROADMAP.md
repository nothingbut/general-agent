# V3 完整功能路线图

**创建时间**: 2026-04-08  
**最后更新**: 2026-04-08  
**状态**: 4/6 优先功能已完成

---

## 📊 当前状态概览

### ✅ 已完成的优先功能（4/6）

| 功能 | 状态 | 完成时间 | 测试覆盖率 | 文档 |
|------|------|----------|-----------|------|
| 长期记忆系统 | ✅ 完成 | 2026-04-06 | 90%+ | ✅ 完整 |
| 上下文压缩系统 | ✅ 完成 | 2026-04-07 | 90%+ | ✅ 完整 |
| 技能系统 | ✅ 完成 | 2026-03-17 | 85%+ | ✅ 完整 |
| 文件上传功能 | ✅ 完成 | 2026-04-08 | 80%+ | 📝 进行中 |

**重要里程碑**：
- ✅ 长期记忆 Phase 1（CRUD + 提取）
- ✅ 长期记忆 Phase 2（向量化 + 语义搜索）
- ✅ 长期记忆 Phase 3（性能优化）
- ✅ 上下文压缩 Phase 3（缓存优化）
- ✅ 文件上传 Phase 1-6（基础功能）
- ✅ 文件上传 Phase 7（跨会话访问基础设施）

### 🚧 待完成的优先功能（2/6）

| 功能 | 状态 | 优先级 | 预计耗时 | 价值 |
|------|------|--------|----------|------|
| 对话中抽取 Skill | 📋 设计阶段 | ⭐⭐⭐⭐ | 2-3 周 | 高 |
| 计划任务功能 | 📋 设计阶段 | ⭐⭐⭐ | 2-3 周 | 中-高 |

---

## 🎯 短期目标（2 周内）

### Week 1: 文件存储功能完善

**Phase 8-9: 测试 + CLI 集成** (5-7 天)

**优先级**: ⭐⭐⭐⭐⭐ 最高

#### Day 1-3: 测试完善
- [ ] FilePermissionService 单元测试
- [ ] FileLibraryService 单元测试
- [ ] FileVersionService 单元测试
- [ ] 跨会话访问集成测试
- [ ] 版本控制集成测试
- [ ] E2E 测试场景

#### Day 4-5: CLI 命令实现
- [ ] `/file library` 命令集（list/search/owned/shared/public）
- [ ] `/file share/revoke/access` 权限管理命令
- [ ] `/file versions/restore` 版本管理命令
- [ ] 更新现有命令（upload/list/show）
- [ ] 帮助文档和错误提示

**预期成果**：
- ✅ 80%+ 测试覆盖率
- ✅ 完整的 CLI 命令集
- ✅ 用户可完整使用跨会话文件访问

**交付物**：
- `FileLibraryServiceTests.cs`
- `FilePermissionServiceTests.cs`
- `FileVersionServiceTests.cs`
- `CrossSessionFileAccessIntegrationTests.cs`
- `FileLibraryCommand.cs`
- `FilePermissionCommand.cs`
- `FileVersionCommand.cs`

---

### Week 2: 文件存储优化 + 文档

**Phase 10-11: 性能优化 + 文档** (4-5 天)

**优先级**: ⭐⭐⭐⭐ 高

#### Day 1-2: 性能优化
- [ ] 实现 CachedFileLibraryService
- [ ] 权限检查缓存（5 分钟）
- [ ] 文件元数据缓存（10 分钟）
- [ ] 版本历史缓存（15 分钟）
- [ ] 查询优化（UNION → JOIN）
- [ ] 分页支持
- [ ] 性能测试和验证

#### Day 3-4: 用户文档
- [ ] `FILE_STORAGE_USER_GUIDE.md` - 完整用户指南
- [ ] `FILE_STORAGE_API.md` - API 参考
- [ ] `FILE_STORAGE_MIGRATION_GUIDE.md` - 迁移指南
- [ ] 更新 `CLI_GUIDE.md` 和 `CLI_REFERENCE.md`

#### Day 5: 验收和发布
- [ ] 完整验收测试
- [ ] 性能验证
- [ ] 文档审查
- [ ] 发布 Release Notes

**预期成果**：
- ✅ 权限检查 < 10ms（缓存命中）
- ✅ 文件列表 < 100ms（100 个文件）
- ✅ 搜索查询 < 200ms（1000 个文件）
- ✅ 完整用户文档

---

## 🚀 中期目标（4-6 周）

### Phase 13: 对话中抽取 Skill 功能

**优先级**: ⭐⭐⭐⭐ 高  
**预计耗时**: 2-3 周  
**状态**: 📋 设计阶段

#### 功能描述

Agent 在对话中自动识别重复性任务模式，建议用户将其转换为可复用的技能。

**示例场景**：
```
用户: 帮我查看这个 API 的文档，然后生成示例代码
Agent: [完成任务]
Agent: 💡 我注意到你经常需要"查API文档+生成示例"，
      是否要创建一个 api-helper 技能？
用户: 好的
Agent: ✓ 已创建技能 dev:api-helper，可以用 @dev:api-helper api="..." 调用
```

#### 设计方案

**1. 模式识别** (1 周)
- 分析对话历史，识别重复模式
- LLM 提取任务步骤和参数
- 计算置信度和频率
- 设置阈值触发建议（频率 >= 3 次，置信度 >= 0.8）

实现：
- [ ] `SkillPatternRecognizer.cs` - 模式识别引擎
- [ ] `ConversationAnalyzer.cs` - 对话分析器
- [ ] `PatternScorer.cs` - 置信度计算

**2. 技能生成** (1 周)
- 自动生成 YAML frontmatter（参数定义）
- 自动生成 Markdown 提示词模板
- 自动提取参数类型和描述
- 技能命名和命名空间建议

实现：
- [ ] `SkillGenerator.cs` - 技能生成器
- [ ] `PromptTemplateGenerator.cs` - 提示词生成
- [ ] `ParameterExtractor.cs` - 参数提取

**3. 用户交互** (3-5 天)
- REPL 命令：`/skill extract [session-id]`
- 自动建议（置信度 > 0.8）
- 交互式编辑和确认
- 技能预览和测试

实现：
- [ ] `SkillExtractionCommand.cs` - CLI 命令
- [ ] `InteractiveSkillEditor.cs` - 交互式编辑器
- [ ] 集成到对话流程

**4. 存储和管理** (2-3 天)
- 提取历史记录（ExtractionRecord）
- 技能使用统计
- 技能版本管理
- 技能评分和推荐

实现：
- [ ] `ExtractionHistoryRepository.cs`
- [ ] `SkillUsageTracker.cs`
- [ ] 数据库表设计

#### 技术栈
- 借鉴 `MemoryExtractionService` 的设计
- LLM 驱动的模式识别
- 对话历史分析
- 交互式用户界面

#### 交付物
- `SkillExtractionService.cs` + 相关类
- `/skill extract` 命令
- 单元测试 + 集成测试
- 用户指南：`SKILL_EXTRACTION_USER_GUIDE.md`
- 设计文档：`SKILL_EXTRACTION_DESIGN.md`

#### 成功标准
- ✅ 可识别 80%+ 的重复性任务模式
- ✅ 生成的技能 70%+ 可直接使用
- ✅ 用户满意度 >= 4/5

---

### Phase 14: 计划任务功能

**优先级**: ⭐⭐⭐ 中-高  
**预计耗时**: 2-3 周  
**状态**: 📋 设计阶段

#### 功能描述

用户可以创建定时执行的任务，支持 cron 表达式或自然语言时间描述。

**示例用例**：
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

#### 设计方案

**1. 任务调度** (1 周)
- 选择调度库：Quartz.NET（功能强大）或 System.Threading.Timer（轻量）
- cron 表达式解析（Cronos 库）
- 自然语言时间解析（Humanizer 或自定义 LLM）
- 任务触发和执行

实现：
- [ ] `ScheduledTaskService.cs` - 任务调度服务
- [ ] `CronParser.cs` - cron 表达式解析
- [ ] `NaturalTimeParser.cs` - 自然语言解析
- [ ] `TaskExecutor.cs` - 任务执行器

**2. 任务存储** (3-5 天)
- SQLite 持久化
- 任务状态跟踪（Pending/Running/Completed/Failed）
- 执行历史和日志
- 任务配置（重试、超时、并发）

实现：
- [ ] `ScheduledTaskRepository.cs`
- [ ] 数据库表设计（scheduled_tasks, task_executions）
- [ ] `TaskExecutionLogger.cs`

**3. 任务执行** (1 周)
- 后台服务执行（类似 `BackgroundTaskService`）
- 技能调用集成
- 错误处理和重试
- 超时和取消
- 并发控制

实现：
- [ ] `BackgroundSchedulerService.cs` - 后台服务
- [ ] 集成 `ISkillRegistry` 和 `ISkillExecutor`
- [ ] `RetryPolicy.cs` - 重试策略
- [ ] `TimeoutHandler.cs` - 超时处理

**4. 用户交互** (3-5 天)
- REPL 命令：`/task schedule/list/cancel/pause/resume`
- 任务通知（完成/失败提醒）
- 任务详情查看
- 任务日志查询

实现：
- [ ] `ScheduledTaskCommand.cs` - CLI 命令
- [ ] `TaskNotificationService.cs` - 通知服务
- [ ] 集成到 REPL

#### 技术栈
- Quartz.NET 或 System.Threading.Timer
- Cronos（cron 表达式）
- Humanizer（自然语言时间）
- SQLite（持久化）

#### 交付物
- `ScheduledTaskService.cs` + 相关类
- `/task` 命令集
- 单元测试 + 集成测试
- 用户指南：`SCHEDULED_TASKS_USER_GUIDE.md`
- 设计文档：`SCHEDULED_TASKS_DESIGN.md`

#### 成功标准
- ✅ 支持 cron 和自然语言两种方式
- ✅ 任务执行准确度 >= 99.9%
- ✅ 错误恢复机制完善

---

## 🌟 长期目标（2-3 个月）

### Phase 15: 文件存储扩展功能

**优先级**: ⭐⭐⭐ 中（按需）  
**预计耗时**: 按需实现（每个 1-2 周）

#### 扩展功能列表

1. **文件共享链接** (1-2 周)
   - 生成临时访问链接
   - 链接过期时间、访问次数限制
   - 密码保护、访问日志

2. **文件夹组织** (2 周)
   - 文件夹层级结构
   - 文件夹权限继承
   - 批量操作

3. **文件标签系统增强** (1 周)
   - 标签自动建议
   - 按标签过滤
   - 标签统计

4. **文件协作功能** (2-3 周)
   - 多用户同时编辑
   - 变更通知
   - 版本合并

5. **配额管理** (1 周)
   - 存储空间限制
   - 文件数量限制
   - 自动清理

6. **文件预览和缩略图** (1-2 周)
   - 图片预览
   - 代码语法高亮
   - PDF 预览

7. **文件类型扩展** (1 周)
   - PDF 支持
   - DOCX 支持
   - 图片 OCR

**实施策略**: 根据用户反馈和需求优先级，逐步实现。

---

### Phase 16: 长期记忆系统优化

**优先级**: ⭐⭐⭐ 中  
**预计耗时**: 按需优化

#### 优化方向

1. **智能记忆提取**
   - 提高提取准确率（从 70% 提升到 90%+）
   - 减少误报和漏报
   - 多轮对话上下文理解

2. **记忆图谱**
   - 记忆之间的关联关系
   - 知识图谱可视化
   - 关联推荐

3. **记忆生命周期管理**
   - 自动过期和归档
   - 记忆重要性评分
   - 冷热数据分层

4. **多模态记忆**
   - 图片记忆
   - 音频记忆
   - 视频记忆

---

### Phase 17: 上下文压缩优化

**优先级**: ⭐⭐⭐ 中  
**预计耗时**: 按需优化

#### 优化方向

1. **自动上下文压缩**
   - 上下文窗口使用率监控
   - 达到 90% 时自动触发压缩
   - 压缩策略自动选择优化

2. **压缩质量提升**
   - 提高摘要准确率
   - 保留更多关键信息
   - 减少信息丢失

3. **增量压缩**
   - 只压缩新增消息
   - 避免重复压缩

4. **压缩结果评估**
   - 自动质量评估
   - 用户反馈收集
   - 持续优化

---

### Phase 18: 技能系统增强

**优先级**: ⭐⭐⭐ 中  
**预计耗时**: 按需增强

#### 增强方向

1. **技能市场**
   - 技能分享和发现
   - 技能评分和评论
   - 技能下载和安装

2. **技能组合**
   - 多个技能串联执行
   - 技能工作流
   - 条件分支

3. **技能调试**
   - 技能执行跟踪
   - 参数验证
   - 错误诊断

4. **技能版本管理**
   - 技能版本控制
   - 版本升级和回滚
   - 兼容性检查

---

## 📅 时间线总览

### 2026-04-08 至 2026-04-19（2 周）
- ✅ Week 1: 文件存储测试 + CLI 集成
- ✅ Week 2: 文件存储性能优化 + 文档

### 2026-04-22 至 2026-05-10（3 周）
- 📋 对话抽取 Skill 功能开发

### 2026-05-13 至 2026-05-31（3 周）
- 📋 计划任务功能开发

### 2026-06-01 起（持续）
- 💡 扩展功能按需实现
- 💡 持续优化和迭代

---

## 📊 优先级矩阵

| 功能 | 优先级 | 价值 | 难度 | 耗时 | 状态 |
|------|--------|------|------|------|------|
| **文件存储测试+CLI** | ⭐⭐⭐⭐⭐ | 极高 | 中 | 1 周 | 🚧 进行中 |
| **文件存储优化+文档** | ⭐⭐⭐⭐ | 高 | 中 | 1 周 | 📋 待开始 |
| **对话抽取 Skill** | ⭐⭐⭐⭐ | 高 | 中-高 | 2-3 周 | 📋 设计阶段 |
| **计划任务功能** | ⭐⭐⭐ | 中-高 | 中 | 2-3 周 | 📋 设计阶段 |
| **文件存储扩展功能** | ⭐⭐⭐ | 中 | 低-中 | 按需 | 💡 规划中 |
| **记忆系统优化** | ⭐⭐⭐ | 中 | 中-高 | 按需 | 💡 规划中 |
| **压缩系统优化** | ⭐⭐⭐ | 中 | 中 | 按需 | 💡 规划中 |
| **技能系统增强** | ⭐⭐⭐ | 中 | 中 | 按需 | 💡 规划中 |

---

## 🎯 成功指标

### 代码质量
- ✅ 测试覆盖率 >= 80%
- ✅ 构建成功率 = 100%
- ✅ 代码审查通过率 >= 95%

### 性能指标
- ✅ 记忆检索 < 50ms（缓存命中）
- ✅ 上下文压缩 < 200ms（缓存命中）
- ✅ 文件权限检查 < 10ms（缓存命中）
- ✅ 技能执行 < 2s（LLM 调用）

### 用户体验
- ✅ CLI 命令响应 < 100ms
- ✅ 文档完整度 >= 90%
- ✅ 用户满意度 >= 4/5
- ✅ 功能可用性 >= 95%

---

## 📝 下一步行动

### 本周行动（2026-04-08 至 2026-04-12）

**Monday-Tuesday: 单元测试**
1. 创建测试文件框架
2. 实现 FilePermissionService 测试
3. 实现 FileLibraryService 测试
4. 实现 FileVersionService 测试

**Wednesday: 集成测试**
1. 跨会话访问集成测试
2. 版本控制集成测试
3. 数据库迁移测试

**Thursday-Friday: CLI 命令**
1. FileLibraryCommand 实现
2. FilePermissionCommand 实现
3. FileVersionCommand 实现
4. 手工测试和调试

### 下周行动（2026-04-15 至 2026-04-19）

**Monday-Tuesday: 性能优化**
1. 实现 CachedFileLibraryService
2. 查询优化和分页支持
3. 性能测试和验证

**Wednesday-Thursday: 文档**
1. 用户指南编写
2. API 文档编写
3. 迁移指南编写

**Friday: 验收和发布**
1. 完整验收测试
2. 性能验证
3. 文档审查
4. 发布准备

---

## 💡 建议和反馈

如果你对功能规划有任何建议，或想调整优先级，请：
1. 在对话中直接提出
2. 或在 GitHub Issues 中讨论
3. 或通过邮件联系团队

我们会根据反馈持续优化路线图。

---

## 📚 相关文档

- [文件存储详细路线图](./features/file-storage-roadmap.md)
- [优先功能列表](./features/priority-features.md)
- [长期记忆系统文档](./features/MEMORY_SYSTEM.md)
- [上下文压缩文档](./features/COMPRESSION_SYSTEM.md)
- [技能系统指南](./features/SKILLS_GUIDE.md)
- [CLI 使用指南](./guides/CLI_GUIDE.md)

---

**最后更新**: 2026-04-08  
**维护者**: General Agent Team  
**版本**: V3 Complete Roadmap v1.0
