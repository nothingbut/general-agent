# General Agent V2 - 实施计划

**版本:** 1.0
**创建日期:** 2026-03-09
**预计时间:** 12 周

---

## 概览

本文档详细规划了 General Agent V2 (Rust 重写) 的实施路线图。

---

## 阶段划分

### Phase 1: 基础设施（Week 1-2）

**目标:** 建立项目基础和核心架构

#### Week 1: 项目初始化

**Day 1-2: 工作区设置**
- [x] 创建 Cargo 工作区
- [ ] 创建所有 crate 骨架
- [ ] 设置 CI/CD (GitHub Actions)
- [ ] 配置 clippy, rustfmt
- [ ] 设置 pre-commit hooks

**Day 3-4: 核心模型定义**
- [ ] `agent-core`: 定义领域实体
  - Message, Session, SessionContext
  - MessageRole, MessageMetadata
- [ ] `agent-core`: 定义核心 traits
  - SessionRepository, MessageRepository
  - LLMClient, SkillExecutor
- [ ] `agent-core`: 统一错误类型
  - Error enum with thiserror
  - Result type alias

**Day 5: 文档和测试框架**
- [ ] 设置单元测试框架
- [ ] 编写架构文档
- [ ] README 和贡献指南
- [ ] 代码注释标准

#### Week 2: 数据层实现

**Day 1-2: 数据库设置**
- [ ] `agent-storage`: SQLx 配置
- [ ] 数据库迁移文件
  - `001_create_sessions.sql`
  - `002_create_messages.sql`
- [ ] Database 连接池实现

**Day 3-4: Repository 实现**
- [ ] SqliteSessionRepository
  - create, find_by_id, list, update, delete
- [ ] SqliteMessageRepository
  - create, list_by_session, delete_by_session
- [ ] 单元测试（使用内存 SQLite）

**Day 5: 集成测试**
- [ ] 端到端数据库测试
- [ ] 并发测试
- [ ] 性能基准测试

**Week 1-2 里程碑:** ✅ 数据层稳定，可进行 CRUD 操作

---

### Phase 2: LLM 集成（Week 3-4）

**目标:** 完整的 LLM 提供商支持

#### Week 3: OpenAI 和 Ollama

**Day 1-2: OpenAI 集成**
- [ ] `agent-llm/openai`: 基础客户端
- [ ] 实现 LLMClient trait
- [ ] complete() 方法
- [ ] 错误处理和重试

**Day 3-4: Ollama 集成**
- [ ] `agent-llm/ollama`: HTTP 客户端
- [ ] 实现 LLMClient trait
- [ ] 模型列表和下载

**Day 5: 单元测试**
- [ ] Mock LLM 响应
- [ ] 错误场景测试
- [ ] 超时测试

#### Week 4: 流式响应和路由

**Day 1-2: 流式响应**
- [ ] stream() 方法实现
- [ ] Server-Sent Events (SSE)
- [ ] 取消机制

**Day 3-4: LLM 路由**
- [ ] `agent-llm/router`: LLMRouter
- [ ] 配置加载
- [ ] 提供商选择逻辑
- [ ] 降级策略

**Day 5: 集成测试**
- [ ] 完整流程测试
- [ ] 多提供商切换
- [ ] 性能测试

**Week 3-4 里程碑:** ✅ LLM 客户端稳定，支持多提供商

---

### Phase 3: REST API（Week 5-6）

**目标:** 完整的 HTTP API

#### Week 5: 基础 API

**Day 1-2: Axum 框架设置**
- [ ] `agent-api`: 基础路由
- [ ] AppState 和依赖注入
- [ ] 健康检查端点
- [ ] 中间件（CORS, 日志）

**Day 3-4: 聊天 API**
- [ ] POST /api/chat
  - 处理用户消息
  - 调用 LLM
  - 保存对话历史
- [ ] GET /api/sessions
- [ ] GET /api/sessions/:id

**Day 5: 错误处理**
- [ ] 统一错误响应格式
- [ ] HTTP 状态码映射
- [ ] 错误日志记录

#### Week 6: 高级功能

**Day 1-2: 流式响应 API**
- [ ] POST /api/chat/stream
- [ ] SSE 实现
- [ ] 前端集成测试

**Day 3-4: 会话管理 API**
- [ ] PUT /api/sessions/:id
- [ ] DELETE /api/sessions/:id
- [ ] PATCH /api/sessions/:id/context

**Day 5: API 测试**
- [ ] 集成测试套件
- [ ] API 文档（OpenAPI）
- [ ] 性能测试

**Week 5-6 里程碑:** ✅ REST API 完整，V1 前端可连接

---

### Phase 4: 技能系统（Week 7-8）

**目标:** 技能定义和执行

#### Week 7: 技能解析

**Day 1-2: Markdown 解析器**
- [ ] `agent-skills/parser`: 解析 .md 文件
- [ ] YAML frontmatter 提取
- [ ] 参数定义解析

**Day 3-4: 技能注册表**
- [ ] `agent-skills/registry`: SkillRegistry
- [ ] 从文件系统加载技能
- [ ] .ignore 支持
- [ ] 命名空间解析

**Day 5: 单元测试**
- [ ] 解析器测试
- [ ] 注册表测试

#### Week 8: 技能执行

**Day 1-2: 参数验证和替换**
- [ ] 参数类型检查
- [ ] 占位符替换
- [ ] 默认值处理

**Day 3-4: 技能执行器**
- [ ] `agent-skills/executor`: SkillExecutor
- [ ] 与 LLM 集成
- [ ] 执行结果缓存

**Day 5: 集成测试**
- [ ] 端到端技能执行
- [ ] 错误场景测试
- [ ] 性能测试

**Week 7-8 里程碑:** ✅ 技能系统可用，兼容 V1 技能定义

---

### Phase 5: TUI 界面（Week 9-10）

**目标:** 终端用户界面

#### Week 9: 基础 TUI

**Day 1-2: Ratatui 设置**
- [ ] `agent-tui`: 基础应用结构
- [ ] 事件循环
- [ ] 状态管理

**Day 3-4: UI 组件**
- [ ] 消息列表组件
- [ ] 输入框组件
- [ ] 状态栏组件

**Day 5: 样式和布局**
- [ ] 颜色主题
- [ ] 响应式布局
- [ ] 滚动处理

#### Week 10: 高级功能

**Day 1-2: 会话管理 UI**
- [ ] 会话列表
- [ ] 会话切换
- [ ] 新建会话

**Day 3-4: 快捷键和交互**
- [ ] Ctrl+N: 新建会话
- [ ] Ctrl+L: 会话列表
- [ ] Ctrl+K: 清屏
- [ ] Ctrl+Q: 退出

**Day 5: 集成测试**
- [ ] UI 快照测试
- [ ] 事件处理测试
- [ ] API 集成测试

**Week 9-10 里程碑:** ✅ TUI 功能完整

---

### Phase 6: 工作流引擎（Week 11-12）

**目标:** 工作流编排和执行

#### Week 11: DAG 和执行引擎

**Day 1-2: 工作流模型**
- [ ] `agent-workflow`: Workflow, Task 模型
- [ ] DAG 依赖解析
- [ ] 拓扑排序

**Day 3-4: 任务执行器**
- [ ] 并行执行
- [ ] 重试机制
- [ ] 超时处理

**Day 5: 单元测试**
- [ ] DAG 解析测试
- [ ] 执行器测试

#### Week 12: 监控和测试

**Day 1-2: 性能监控**
- [ ] 指标收集
- [ ] 链路追踪
- [ ] 报告生成

**Day 3-4: 集成测试**
- [ ] 端到端工作流测试
- [ ] 复杂 DAG 测试
- [ ] 故障恢复测试

**Day 5: 文档和发布**
- [ ] 用户文档
- [ ] API 文档
- [ ] 发布 v0.1.0

**Week 11-12 里程碑:** ✅ V2 基础版本完成

---

## 测试策略

### 单元测试

```rust
#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_create_session() {
        let repo = create_test_repo().await;
        let session = Session::new();
        let created = repo.create(session).await.unwrap();
        assert_eq!(created.id, session.id);
    }
}
```

**目标覆盖率:** 80%+

### 集成测试

```rust
// tests/integration_test.rs
#[tokio::test]
async fn test_chat_flow() {
    let app = create_test_app().await;

    // 发送消息
    let response = app.post("/api/chat")
        .json(&json!({"message": "Hello"}))
        .send()
        .await
        .unwrap();

    assert_eq!(response.status(), 200);
}
```

### 性能测试

```rust
#[bench]
fn bench_llm_complete(b: &mut Bencher) {
    b.iter(|| {
        // 性能测试代码
    });
}
```

---

## 质量保证

### CI/CD 流水线

```yaml
# .github/workflows/ci.yml
name: CI

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions-rs/toolchain@v1
        with:
          toolchain: stable

      - name: Run tests
        run: cargo test --all-features

      - name: Run clippy
        run: cargo clippy -- -D warnings

      - name: Check formatting
        run: cargo fmt -- --check
```

### 代码审查清单

- [ ] 代码通过 clippy 检查
- [ ] 代码格式化（rustfmt）
- [ ] 单元测试覆盖率 > 80%
- [ ] 文档注释完整
- [ ] 错误处理完善
- [ ] 无 unsafe 代码（除非必要且有注释）

---

## 风险管理

| 风险 | 影响 | 概率 | 缓解措施 | 负责人 |
|------|------|------|----------|--------|
| Rust 学习曲线 | 高 | 中 | 分阶段学习，参考成熟项目 | 开发团队 |
| 功能差距 | 中 | 低 | 严格对照 V1 功能清单 | 技术负责人 |
| 性能不达标 | 中 | 低 | 早期性能测试，持续优化 | 性能工程师 |
| 依赖包不稳定 | 低 | 低 | 选择成熟依赖，版本锁定 | 架构师 |

---

## 资源需求

### 人力资源

- **全职开发:** 2-3 人
- **架构师:** 1 人（兼职）
- **测试工程师:** 1 人（兼职）

### 技术资源

- **开发环境:** Rust 1.75+, Tokio
- **CI/CD:** GitHub Actions
- **测试环境:** Docker, SQLite
- **监控:** tracing, metrics

---

## 成功标准

### 功能完整性
- [ ] V1 的 Phase 1-7 功能 100% 实现
- [ ] API 向后兼容
- [ ] 技能定义兼容
- [ ] 数据迁移工具可用

### 性能指标
- [ ] 启动时间 < 100ms
- [ ] API P95 延迟 < 50ms
- [ ] 内存占用 < 50MB (idle)
- [ ] 支持 100+ RPS

### 质量指标
- [ ] 测试覆盖率 > 80%
- [ ] 零严重 bug
- [ ] 文档完整

---

## 里程碑时间表

| 里程碑 | 时间 | 交付物 |
|--------|------|--------|
| M1: 基础设施 | Week 2 | 数据层可用 |
| M2: LLM 集成 | Week 4 | 多提供商支持 |
| M3: REST API | Week 6 | API 完整 |
| M4: 技能系统 | Week 8 | 技能可用 |
| M5: TUI 界面 | Week 10 | TUI 功能完整 |
| M6: 工作流引擎 | Week 12 | V2 v0.1.0 发布 |

---

## 下一步行动

### 本周（Week 1）

1. **Day 1:** 创建所有 crate 骨架
2. **Day 2:** 设置 CI/CD
3. **Day 3-4:** 实现核心模型
4. **Day 5:** 编写测试框架

### 下周（Week 2）

1. 实现数据库层
2. 实现 Repository
3. 编写集成测试

---

**文档维护者:** 项目经理
**审阅者:** 架构师、技术负责人
**最后更新:** 2026-03-09
**下次审阅:** Week 3 开始时
