# V2 发展路线图 2026

**创建时间**: 2026-04-15
**版本**: 1.0
**目标**: 在保持 V2 技术优势的同时，追赶并超越 V3 功能

---

## 🎯 战略目标

### 短期目标（Q2 2026，3 个月）
**实现 V3 的 5 个优先功能，使 V2 功能完全对齐**

1. ✅ 上下文压缩系统（Week 1-2）
2. ✅ 长期记忆系统（Week 3-4）
3. ✅ 文件上传系统（Week 5-6）
4. ✅ 技能抽取系统（Week 7）
5. ✅ 计划任务系统（Week 8-9）

### 中期目标（Q3 2026，3 个月）
**完成 TUI + Web API，建立 V2 独特优势**

1. ⏳ TUI 界面完成（Week 10-11）
2. ⏳ Web API 服务（Week 12-15）
3. ⏳ 多 Agent 协作（Week 16-18）

### 长期目标（Q4 2026+）
**生态建设和社区驱动**

1. ⏳ 插件系统
2. ⏳ Marketplace
3. ⏳ 企业级功能

---

## 📋 Phase 3: V3 功能对齐（32 天）

**时间**: 2026-04-15 至 2026-05-17
**状态**: 🚀 即将开始

---

### Week 1-2: 上下文压缩系统 ⭐⭐⭐⭐⭐

**耗时**: 5 个工作日（2026-04-15 至 2026-04-19）

#### 目标
- 防止长对话 Token 超限
- 实现 3 种压缩策略
- 自动触发机制

#### 任务清单

**Day 1 (2026-04-15): Token 计数基础**
- [ ] 创建 `agent-context-compression` crate
- [ ] 添加 `tiktoken-rs` 依赖
- [ ] 实现 `TokenCounter` 结构体
- [ ] 支持多种模型的 token 计数
- [ ] 单元测试（5-10 个）

**Day 2 (2026-04-16): 滑动窗口策略**
- [ ] 实现 `SlidingWindowStrategy` 结构体
- [ ] 保留系统消息
- [ ] 保留最近 N 条消息
- [ ] 策略配置（窗口大小）
- [ ] 单元测试（5-10 个）

**Day 3 (2026-04-17): 语义压缩策略**
- [ ] 实现 `SemanticStrategy` 结构体
- [ ] LLM 生成摘要
- [ ] 关键信息保留
- [ ] 摘要质量验证
- [ ] 单元测试（5-10 个）

**Day 4 (2026-04-18): 分层压缩策略**
- [ ] 实现 `HierarchicalStrategy` 结构体
- [ ] 多级压缩逻辑
- [ ] 策略选择算法
- [ ] 单元测试（5-10 个）

**Day 5 (2026-04-19): 集成和测试**
- [ ] `CompressionService` 主服务
- [ ] 自动触发机制（消息数 >= 15）
- [ ] `ConversationFlow` 集成
- [ ] CLI 命令（`agent compress <session-id>`）
- [ ] 集成测试（5-10 个）
- [ ] 文档更新

#### 验收标准
- ✅ 3 种压缩策略全部实现
- ✅ Token 计数准确（误差 < 5%）
- ✅ 自动触发正常工作
- ✅ 20+ 个测试全部通过
- ✅ 文档完整

#### 技术细节
```toml
# Cargo.toml 新增依赖
[dependencies]
tiktoken-rs = "0.5"  # Token 计数
```

```rust
// 核心接口
pub trait CompressionStrategy {
    async fn compress(&self, messages: &[Message]) -> Result<Vec<Message>>;
}

pub struct SlidingWindowStrategy {
    window_size: usize,
}

pub struct SemanticStrategy {
    llm_client: Arc<LlmClient>,
}

pub struct HierarchicalStrategy {
    levels: Vec<CompressionLevel>,
}
```

---

### Week 3-4: 长期记忆系统 ⭐⭐⭐⭐⭐

**耗时**: 8 个工作日（2026-04-22 至 2026-05-01）

#### 目标
- 5 种记忆类型（User/Feedback/Project/Reference/Knowledge）
- LLM 驱动提取
- 向量化和语义搜索
- 混合搜索策略

#### 任务清单

**Day 1-2 (2026-04-22 至 2026-04-23): 基础记忆系统**
- [ ] 创建 `agent-memory` crate
- [ ] 定义 `Memory` 结构体（5 种类型枚举）
- [ ] SQLite 表设计（`memories` 表）
- [ ] 实现 `MemoryRepository`（CRUD）
- [ ] 单元测试（10-15 个）

**Day 3-4 (2026-04-24 至 2026-04-25): LLM 驱动提取**
- [ ] 实现 `MemoryExtractor` 服务
- [ ] 设计提取提示词
- [ ] 从对话消息中提取记忆
- [ ] 记忆类型分类
- [ ] 单元测试（10-15 个）

**Day 5-7 (2026-04-26 至 2026-04-30): 向量化和语义搜索**
- [ ] 复用 `agent-rag` 的 `QdrantClient`
- [ ] 复用 Ollama Embedding 接口
- [ ] 实现 `VectorMemoryStore`（向量索引）
- [ ] 语义搜索（向量相似度）
- [ ] 混合搜索（关键词 + 语义）
- [ ] 自动降级策略（Qdrant 不可用时）
- [ ] 单元测试（15-20 个）
- [ ] 集成测试（5-10 个，需要 Qdrant）

**Day 8 (2026-05-01): 集成和 CLI**
- [ ] `MemoryService` 主服务
- [ ] `ConversationFlow` 注入相关记忆
- [ ] CLI 命令（10 个）
  - `agent memory list [type]`
  - `agent memory show <id>`
  - `agent memory add <type> <content>`
  - `agent memory update <id>`
  - `agent memory delete <id>`
  - `agent memory search <query>`  # 关键词
  - `agent memory semantic-search <query>`  # 向量
  - `agent memory hybrid-search <query>`  # 混合
  - `agent memory extract <session-id>`
  - `agent memory relevant <context>`
- [ ] 文档更新

#### 验收标准
- ✅ 5 种记忆类型全部支持
- ✅ LLM 提取准确率 > 80%
- ✅ 语义搜索性能 < 50ms
- ✅ 自动降级正常工作
- ✅ 40+ 个测试全部通过
- ✅ 文档完整

#### 技术细节
```rust
// 核心数据模型
pub enum MemoryType {
    User,
    Feedback,
    Project,
    Reference,
    Knowledge,
}

pub struct Memory {
    pub id: Uuid,
    pub memory_type: MemoryType,
    pub content: String,
    pub source: Option<String>,
    pub session_id: Option<Uuid>,
    pub created_at: DateTime<Utc>,
    pub metadata: Option<serde_json::Value>,
}

// 复用 agent-rag 的 Qdrant 客户端
use agent_rag::{QdrantClient, OllamaEmbedding};
```

---

### Week 5-6: 文件上传系统 ⭐⭐⭐⭐

**耗时**: 8 个工作日（2026-05-04 至 2026-05-13）

#### 目标
- 文件上传/列表/删除
- 对话中引用（`@file:` 语法）
- 跨会话访问
- 三级权限（Private/Shared/Public）
- 版本控制

#### 任务清单

**Day 1-2 (2026-05-04 至 2026-05-05): 基础文件存储**
- [ ] 创建 `agent-file-storage` crate
- [ ] 文件系统存储（`~/.agent-v2/uploads/`）
- [ ] 定义 `UploadedFile` 结构体
- [ ] SQLite 表设计（`uploaded_files` 表）
- [ ] 实现 `FileRepository`
- [ ] 上传/列表/删除 API
- [ ] MIME 类型检测（`mime_guess` crate）
- [ ] 20+ 种文件类型支持
- [ ] 单元测试（10-15 个）

**Day 3-4 (2026-05-06 至 2026-05-07): 对话引用**
- [ ] `@file:` 语法解析器
- [ ] 文件内容读取
- [ ] 文件引用解析（按名称/ID）
- [ ] 多文件引用支持
- [ ] `ConversationFlow` 集成
- [ ] 单元测试（10-15 个）

**Day 5-6 (2026-05-08 至 2026-05-09): 跨会话和权限**
- [ ] 访问级别枚举（Private/Shared/Public）
- [ ] `file_permissions` 表设计
- [ ] 权限管理（授予/撤销）
- [ ] 权限检查逻辑
- [ ] 所有者验证
- [ ] 单元测试（15-20 个）
- [ ] 集成测试（5-10 个）

**Day 7-8 (2026-05-12 至 2026-05-13): 版本控制和 CLI**
- [ ] `file_versions` 表设计
- [ ] 版本创建（每次上传）
- [ ] 版本列表和查看
- [ ] 版本回滚
- [ ] CLI 命令（11 个）
  - `agent file upload <path> [--access-level <level>]`
  - `agent file list [--level <level>]`
  - `agent file show <id>`
  - `agent file content <id> [--version <n>]`
  - `agent file delete <id>`
  - `agent file search <keyword>`
  - `agent file share <id> --user <user-id>`
  - `agent file revoke <id> --user <user-id>`
  - `agent file permissions <id>`
  - `agent file versions <id>`
  - `agent file restore <id> --version <n>`
- [ ] 文档更新

#### 验收标准
- ✅ 20+ 种文件类型支持
- ✅ `@file:` 引用正常工作
- ✅ 跨会话访问和权限正确
- ✅ 版本控制功能完整
- ✅ 35+ 个测试全部通过
- ✅ 文档完整

#### 技术细节
```toml
# Cargo.toml 新增依赖
[dependencies]
mime_guess = "2.0"  # MIME 类型检测
tokio = { version = "1.35", features = ["fs"] }  # 异步文件 I/O
```

```rust
// 核心数据模型
pub enum AccessLevel {
    Private,   // 仅所有者
    Shared,    // 明确授权
    Public,    // 所有人
}

pub struct UploadedFile {
    pub id: Uuid,
    pub original_filename: String,
    pub stored_filename: String,
    pub file_type: String,
    pub size_in_bytes: i64,
    pub uploaded_at: DateTime<Utc>,
    pub access_level: AccessLevel,
    pub owner_id: String,
    pub current_version: i32,
}

pub struct FileVersion {
    pub id: Uuid,
    pub file_id: Uuid,
    pub version: i32,
    pub stored_filename: String,
    pub uploaded_at: DateTime<Utc>,
}
```

---

### Week 7: 技能抽取系统 ⭐⭐⭐⭐

**耗时**: 4 个工作日（2026-05-14 至 2026-05-17）

#### 目标
- LLM 驱动的技能定义生成
- 从对话中识别可复用模式
- 交互式编辑和确认
- 抽取历史记录

#### 任务清单

**Day 1-2 (2026-05-14 至 2026-05-15): LLM 分析器**
- [ ] 创建 `agent-skill-extraction` crate
- [ ] 设计提取提示词（识别模式）
- [ ] 实现 `SkillExtractor` 服务
- [ ] 技能定义生成（YAML + Markdown）
- [ ] 参数提取和类型推断
- [ ] 单元测试（10-15 个）

**Day 3 (2026-05-16): 交互式编辑**
- [ ] 用户确认流程
- [ ] 交互式参数调整
- [ ] 技能冲突检测
- [ ] 保存到技能目录
- [ ] 单元测试（5-10 个）

**Day 4 (2026-05-17): 历史和 CLI**
- [ ] `extraction_history` 表设计
- [ ] 抽取记录（成功/失败）
- [ ] 统计 API（抽取次数、成功率）
- [ ] CLI 命令（4 个）
  - `agent skill extract <session-id>`
  - `agent skill generate`  # 交互式
  - `agent skill history [--status <status>]`
  - `agent skill stats`
- [ ] 文档更新

#### 验收标准
- ✅ LLM 识别准确率 > 75%
- ✅ 交互式编辑流畅
- ✅ 抽取历史完整记录
- ✅ 20+ 个测试全部通过
- ✅ 文档完整

#### 技术细节
```rust
// 核心数据模型
pub struct SkillDefinition {
    pub name: String,
    pub namespace: Option<String>,
    pub description: String,
    pub parameters: Vec<SkillParameter>,
    pub template: String,
}

pub struct ExtractionRecord {
    pub id: Uuid,
    pub session_id: Uuid,
    pub extracted_at: DateTime<Utc>,
    pub status: ExtractionStatus,  // Success, Failed
    pub skill_name: Option<String>,
}
```

---

### Week 8-9: 计划任务系统 ⭐⭐⭐

**耗时**: 5 个工作日（2026-05-18 至 2026-05-24）

#### 目标
- Cron 表达式支持
- 自然语言支持（中文）
- 3 种任务类型
- 后台调度器
- 重试机制

#### 任务清单

**Day 1 (2026-05-18): 数据模型和存储**
- [ ] 创建 `agent-scheduled-tasks` crate
- [ ] 定义 `ScheduledTask` 结构体
- [ ] SQLite 表设计（`scheduled_tasks` 和 `task_executions`）
- [ ] 实现 `TaskRepository`
- [ ] 单元测试（5-10 个）

**Day 2 (2026-05-19): Cron 解析器**
- [ ] 集成 `cron` crate
- [ ] Cron 表达式验证
- [ ] 计算下次执行时间
- [ ] 自然语言解析器（7 种模式，正则表达式）
- [ ] 单元测试（10-15 个）

**Day 3-4 (2026-05-20 至 2026-05-21): 后台调度器**
- [ ] 实现 `TaskScheduler`（`tokio::time::interval`）
- [ ] 任务队列（`tokio::sync::mpsc`）
- [ ] 实现 `TaskExecutor`（支持 3 种任务类型）
- [ ] 重试逻辑（指数退避）
- [ ] 超时控制（`tokio::time::timeout`）
- [ ] 执行历史记录
- [ ] 单元测试（15-20 个）

**Day 5 (2026-05-24): CLI 和集成**
- [ ] CLI 命令（9 个）
  - `agent task schedule <name> --schedule <expr> --type <type> --payload <json>`
  - `agent task list [--status <status>]`
  - `agent task show <id>`
  - `agent task update <id> [--schedule <expr>]`
  - `agent task pause <id>`
  - `agent task resume <id>`
  - `agent task delete <id>`
  - `agent task run <id>`  # 手动执行
  - `agent task history <id>`
- [ ] `agent-cli` 集成
- [ ] 后台服务启动（`main.rs`）
- [ ] 集成测试（5-10 个）
- [ ] 文档更新

#### 验收标准
- ✅ Cron 解析准确
- ✅ 自然语言支持 7 种模式
- ✅ 后台调度器稳定运行
- ✅ 重试机制正常工作
- ✅ 30+ 个测试全部通过
- ✅ 文档完整

#### 技术细节
```toml
# Cargo.toml 新增依赖
[dependencies]
cron = "0.12"  # Cron 解析
tokio = { version = "1.35", features = ["time", "sync"] }
```

```rust
// 核心数据模型
pub enum TaskType {
    SkillInvocation,   // 调用技能
    MemoryReminder,    // 记忆提醒
    CustomCommand,     // 自定义命令
}

pub struct ScheduledTask {
    pub id: Uuid,
    pub name: String,
    pub schedule: String,  // Cron 或自然语言
    pub task_type: TaskType,
    pub payload: serde_json::Value,
    pub status: TaskStatus,  // Pending, Running, Completed, Failed, Paused
    pub next_execution_at: Option<DateTime<Utc>>,
    pub max_retries: i32,
    pub timeout_seconds: i32,
}

pub struct TaskExecution {
    pub id: Uuid,
    pub task_id: Uuid,
    pub started_at: DateTime<Utc>,
    pub completed_at: Option<DateTime<Utc>>,
    pub status: ExecutionStatus,  // Success, Failed, Timeout
    pub result: Option<String>,
    pub error_message: Option<String>,
}
```

---

## 📊 Phase 3 总结

### 时间线
- **开始**: 2026-04-15
- **结束**: 2026-05-24
- **总耗时**: 32 个工作日（约 6.5 周）

### 里程碑
- ✅ Week 2: 上下文压缩完成
- ✅ Week 4: 长期记忆完成
- ✅ Week 6: 文件上传完成
- ✅ Week 7: 技能抽取完成
- ✅ Week 9: 计划任务完成

### 验收标准
- ✅ 5 个优先功能全部实现
- ✅ 150+ 个测试全部通过
- ✅ 测试覆盖率 > 80%
- ✅ 文档完整更新
- ✅ 性能达标（语义搜索 < 50ms，压缩 < 2s）

---

## 🚀 Phase 4: V2 独特优势建设（9 周）

**时间**: 2026-05-25 至 2026-07-26
**状态**: ⏳ 计划中

---

### Week 10-11: TUI 界面 ⭐⭐

**耗时**: 8 个工作日（2026-05-25 至 2026-06-05）

#### 目标
- 现代化终端界面
- 分栏式布局
- 实时交互

#### 技术栈
- Ratatui 0.26+
- Crossterm 0.27
- Vim-like 快捷键

#### 任务清单
- [ ] 基础 UI 框架（布局、主题）
- [ ] 会话列表组件
- [ ] 聊天窗口组件（流式渲染）
- [ ] 输入框组件
- [ ] 快捷键系统
- [ ] 错误提示和通知
- [ ] 技能调用提示
- [ ] 测试和文档

---

### Week 12-15: Web API 服务 ⭐⭐

**耗时**: 16 个工作日（2026-06-06 至 2026-06-27）

#### 目标
- RESTful API
- WebSocket 支持（流式响应）
- OpenAPI 文档

#### 技术栈
- Axum 0.7
- Tower-HTTP（CORS、Trace）
- Utoipa（OpenAPI 生成）

#### 任务清单

**Week 12 (2026-06-06 至 2026-06-12): 基础 API**
- [ ] API 路由设计
- [ ] 会话管理端点（CRUD）
- [ ] 对话端点（POST /sessions/{id}/chat）
- [ ] 技能端点（列表、调用）
- [ ] 认证中间件（JWT）

**Week 13 (2026-06-13 至 2026-06-19): WebSocket 和流式**
- [ ] WebSocket 连接管理
- [ ] 流式响应（Server-Sent Events）
- [ ] 实时对话

**Week 14 (2026-06-20 至 2026-06-26): 高级功能**
- [ ] 记忆管理端点
- [ ] 文件上传端点
- [ ] 计划任务端点
- [ ] OpenAPI 文档生成

**Week 15 (2026-06-27): 测试和部署**
- [ ] 集成测试（API 端点）
- [ ] 性能测试
- [ ] Docker 容器化
- [ ] 文档完善

---

### Week 16-18: 多 Agent 协作 ⭐⭐⭐

**耗时**: 12 个工作日（2026-06-30 至 2026-07-15）

#### 目标
- Agent 间通信协议
- 任务分发和聚合
- 协作策略

#### 技术方案
- 事件驱动架构（`tokio::sync::mpsc`）
- Agent 注册表
- 消息路由

#### 任务清单
- [ ] Agent 抽象接口
- [ ] Agent 注册表
- [ ] 消息路由器
- [ ] 任务分解器
- [ ] 结果聚合器
- [ ] 协作策略（并行、串行、投票）
- [ ] 示例 Agent（搜索、分析、总结）
- [ ] 测试和文档

---

## 📅 Phase 5: 生态建设（Q4 2026）

**时间**: 2026-07-27 至 2026-12-31
**状态**: 💡 概念阶段

### 计划功能
1. **插件系统**
   - WASM 插件支持
   - 插件 SDK
   - 插件市场

2. **企业级功能**
   - 多租户支持
   - 审计日志
   - RBAC 权限

3. **社区驱动**
   - 开源发布
   - 文档网站
   - 示例仓库

---

## 🎯 关键成功指标（KPI）

### 技术指标
- **测试覆盖率**: > 80%
- **性能**:
  - 语义搜索: < 50ms
  - 上下文压缩: < 2s
  - API 响应: < 100ms (P95)
- **稳定性**: 无内存泄漏，无 panic

### 功能指标
- **Phase 3 完成**: 5/5 优先功能（100%）
- **Phase 4 完成**: TUI + Web API + 多 Agent
- **文档覆盖**: 所有公共 API 有文档

### 社区指标（开源后）
- GitHub Stars: 100+
- Contributors: 5+
- Issues 响应时间: < 24h

---

## ⚠️ 风险管理

### 高风险（需要缓解）
1. **时间估算偏乐观**
   - 缓解：留 20% buffer
   - 应急：优先级降低非关键功能

2. **依赖 crate 稳定性**
   - 缓解：选择 star 多、维护活跃的 crate
   - 应急：准备替代方案

3. **复杂度低估**
   - 缓解：每周 checkpoint
   - 应急：拆分任务，逐步交付

### 中风险（需要监控）
1. **测试和调试时间**
   - 缓解：每个功能完成后立即测试

2. **文档欠债**
   - 缓解：边开发边写文档

### 低风险
1. **技术选型**
   - Rust 生态成熟
   - 已有技术验证（RAG + MCP）

---

## 📚 参考资源

### 学习 V3 设计
- `v3/docs/V2_VS_V3_GAP_ANALYSIS.md` - 功能差距分析
- V3 源码（各 Infrastructure 模块）

### Rust 生态
- `tiktoken-rs` - Token 计数
- `cron` - Cron 解析
- `tokio` - 异步运行时
- `axum` - Web 框架
- `ratatui` - TUI 框架

### V2 现有基础
- `agent-rag` - 向量检索
- `agent-llm` - LLM 客户端
- `agent-storage` - SQLite
- `agent-skills` - 技能系统

---

## 🎉 V2 愿景

完成所有 Phase 后，V2 将成为：

### 🦀 **高性能 AI Agent 框架**
- Rust 原生性能
- 零成本抽象
- 类型安全

### 🚀 **功能最完整的 Agent 系统**
- V3 的 5 个优先功能
- MCP + RAG 生态
- TUI + Web API + 多 Agent

### 🌟 **开发者友好**
- 单一二进制部署
- 完整文档
- 丰富示例

### 🎯 **企业级就绪**
- 多租户支持
- 审计日志
- 高可用架构

---

**最后更新**: 2026-04-15
**版本**: V2 Roadmap 2026 v1.0
**维护者**: General Agent V2 Team
