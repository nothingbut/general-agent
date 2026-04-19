# V2 继续开发 - 恢复提示词

## 直接使用的提示词

---

### 如果继续 TUI 完善（Week 11）：

```
继续 V2 开发。查看交接文档：v2/docs/V2_WEEK10_TUI_HANDOFF.md

当前状态：
- Phase 3（5个优先功能）全部完成
- Phase 4 Week 10 TUI 增强已完成（BackendRunner + 帮助面板 + 通知系统）
- 全量 766 个测试通过

Week 11 任务：TUI 完善
1. 命令面板（Ctrl+K）— 创建 command_palette.rs，模糊搜索命令列表
2. Markdown 渲染 — 聊天窗口支持代码块高亮（考虑 syntect crate）
3. 主题系统 — 深色/浅色切换，扩展 colors.rs 为 Theme struct
4. 记忆/文件面板 — 新增 FocusArea 变体，侧边栏切换显示
5. 流式渲染优化 — BackendRunner 逐 token 发送而非整段响应

工作目录: v2/crates/agent-tui/
测试要求: 每个新组件至少 3 个单元测试
```

---

### 如果跳到 Web API（Week 12，推荐）：

```
继续 V2 开发。查看交接文档：v2/docs/V2_WEEK10_TUI_HANDOFF.md

当前状态：
- Phase 3（5个优先功能）全部完成
- Phase 4 Week 10 TUI 已完成
- 全量 766 个测试通过
- agent-api crate 已存在但为空壳（仅 lib.rs placeholder）

Week 12 任务：Web API 基础
目标：使用 Axum 0.7 搭建 RESTful API

Day 1: 框架搭建
- 添加 axum, tower-http, serde, utoipa 依赖到 agent-api/Cargo.toml
- 创建 AppState（共享 AgentRuntime + 各服务的 Arc）
- 路由模块结构：routes/sessions.rs, routes/chat.rs, routes/skills.rs
- 健康检查端点 GET /health

Day 2: 会话管理端点
- POST /api/sessions — 创建会话
- GET /api/sessions — 列出会话
- GET /api/sessions/:id — 查看会话详情
- DELETE /api/sessions/:id — 删除会话
- GET /api/sessions/:id/messages — 获取消息列表

Day 3: 对话端点
- POST /api/sessions/:id/chat — 发送消息（非流式）
- POST /api/sessions/:id/chat/stream — SSE 流式响应
- 错误处理中间件

Day 4: 技能 + 记忆端点
- GET /api/skills — 列出技能
- POST /api/skills/:name/invoke — 调用技能
- GET /api/memories — 列出记忆
- POST /api/memories/search — 搜索记忆

Day 5: 测试 + 文档
- 集成测试（axum::test）
- OpenAPI 文档生成（utoipa-swagger-ui）

技术参考：workspace Cargo.toml 已有 axum = "0.7", tower-http 依赖
工作目录: v2/crates/agent-api/
```

---

### 快速验证当前状态：

```
cd /Users/shichang/Workspace/projects/ai-powered/general-agent/v2
cargo test 2>&1 | grep "test result" | awk -F'[; ]' '{sum += $4} END {print "Total:", sum}'
cargo build
cargo run -p agent-cli -- --help
```
