# General Agent V2 - Stage 3 TUI 界面设计

**创建日期:** 2026-03-10
**状态:** 已批准
**实施阶段:** Stage 3

---

## 目录

- [概述](#概述)
- [架构设计](#架构设计)
- [UI 布局设计](#ui-布局设计)
- [交互设计](#交互设计)
- [数据流与错误处理](#数据流与错误处理)
- [测试策略](#测试策略)
- [验收标准](#验收标准)

---

## 概述

### 设计目标

为 General Agent V2 开发现代化的 TUI（Terminal User Interface）界面，提供流畅的多会话对话体验。

### 核心特性

- **多会话可见**: 左侧会话列表 + 右侧聊天窗口
- **实时状态标注**: 新消息、等待回复、错误提示
- **流式响应**: 逐段显示 AI 回复，避免闪烁
- **快捷键操作**: 简化 Vim 风格，高效导航
- **并行交互**: 支持同时管理多个会话

### 技术栈

- **UI 框架**: Ratatui 0.26+
- **终端**: Crossterm 0.27
- **异步运行时**: Tokio
- **状态管理**: 集中式（单一 AppState）

---

## 架构设计

### 技术方案：单线程事件循环

**选择理由：**
- 适合 MVP，简单直接
- 状态管理集中，易于调试
- 成熟实践（gitui、bottom 等应用）
- 性能足够，可扩展

**架构图：**
```
┌─────────────────────────────────────────────────────────┐
│                      TuiApp                              │
│  ┌────────────┐         ┌──────────────┐                │
│  │ AppState   │←────────│ EventHandler │                │
│  │            │         └──────────────┘                │
│  │ - sessions │               ↑                          │
│  │ - messages │               │ 键盘事件                  │
│  │ - focus    │         crossterm::event                 │
│  │ - input    │                                          │
│  └─────┬──────┘                                          │
│        │                                                 │
│        │ 读取状态                                          │
│        ↓                                                 │
│  ┌────────────┐                                          │
│  │ UI渲染器   │                                          │
│  │ (Ratatui)  │                                          │
│  └────────────┘                                          │
└─────────────────────────────────────────────────────────┘
         ↑                                      ↓
         │ BackendUpdate                        │ 操作命令
         │                                      │
┌────────┴──────────────────────────────────────┴─────────┐
│                  Backend 后台任务                         │
│                                                          │
│  ┌──────────────┐     ┌─────────────────┐               │
│  │ LLM 调用任务 │────→│ mpsc::Sender    │               │
│  │ (tokio spawn)│     │ (BackendUpdate) │               │
│  └──────────────┘     └─────────────────┘               │
│         ↓                                                │
│  ConversationFlow → SessionManager → Database            │
└──────────────────────────────────────────────────────────┘
```

### 模块结构

```
agent-tui/
├── src/
│   ├── lib.rs              # 公共接口
│   ├── app.rs              # TuiApp 主结构
│   ├── state.rs            # AppState 状态管理
│   ├── event.rs            # 事件处理
│   ├── ui/
│   │   ├── mod.rs
│   │   ├── layout.rs       # 布局管理
│   │   ├── session_list.rs # 会话列表组件
│   │   ├── chat_window.rs  # 聊天窗口组件
│   │   ├── input_box.rs    # 输入框组件
│   │   └── status_bar.rs   # 状态栏组件
│   ├── keymap.rs           # 快捷键映射
│   └── backend.rs          # 后台任务管理
├── examples/
│   └── tui_demo.rs         # 演示程序
└── tests/
    └── integration.rs      # 集成测试
```

### 核心流程

```
启动 → 加载会话列表 → 进入事件循环
                        ↓
    ┌──────────────────┴──────────────────┐
    │                                      │
键盘事件 → 状态更新 → UI 重绘    后台更新 → 状态更新 → UI 重绘
    │                                      │
    └──────────────────┬──────────────────┘
                        ↓
                   退出清理
```

---

## UI 布局设计

### 整体布局

```
┌─────────────────────────────────────────────────────────┐
│ [状态栏] General Agent V2 | Ollama (qwen3.5)  Ctrl+H 帮助│
├──────────────┬──────────────────────────────────────────┤
│              │  ┌─ 标题: 代码重构讨论 ──────────────┐  │
│  会话列表     │  │                                    │  │
│              │  │ User: 如何优化这段代码？             │  │
│ ● 会话1      │  │ 2024-03-10 10:23                   │  │
│   (2条新消息) │  │                                    │  │
│              │  │ AI: 我建议从以下几个方面优化：       │  │
│ ○ 会话2      │  │                                    │  │
│   代码重构... │  │ 1. 使用更高效的数据结构            │  │
│              │  │ 2. 减少不必要的内存分配            │  │
│ ⏳ 会话3      │  │                                    │  │
│   等待回复... │  │ [正在生成...]                      │  │
│              │  │                                    │  │
│ ❌ 会话4      │  │                                    │  │
│   连接失败    │  │                                    │  │
│              │  │                                    │  │
│              │  │                                    │  │
│              │  └────────────────────────────────────┘  │
│ [+ 新建会话]  │                                          │
│              │  ┌─ 输入 ────────────────────────────┐  │
│ j/k: 导航     │  │ 继续优化方案...█                   │  │
│ Enter: 选择   │  │                                    │  │
├──────────────┴──────────────────────────────────────────┤
│ [底部信息] 会话: 3/10 | 消息: 45 | ESC: 取消焦点        │
└─────────────────────────────────────────────────────────┘
```

### 组件详细说明

#### 1. 状态栏（顶部）

**位置**: 顶部，全宽
**内容**:
- 左侧：应用名称 + LLM 提供商/模型
- 右侧：全局快捷键提示（如 `Ctrl+H 帮助`）

**实现要点**:
```rust
// 显示当前 LLM 配置
format!("General Agent V2 | {} ({})", provider, model)
```

#### 2. 会话列表（左侧）

**位置**: 左侧，宽度约 25%
**内容**:
- 会话项：显示标题或 ID 前缀
- 状态指示器：
  - `●` 实心圆点 - 当前选中会话
  - `○` 空心圆点 - 未选中会话
- 状态标注：
  - `(N条新消息)` - 后台会话有新的 AI 回复
  - `⏳ 等待回复...` - 正在处理用户输入
  - `❌ 错误信息` - 发生错误
- 底部：`[+ 新建会话]` 按钮
- 快捷键提示

**会话项格式**:
```
● 会话标题/ID前缀
  (状态标注)
```

**状态优先级**: 错误 > 等待回复 > 新消息 > 无状态

#### 3. 聊天窗口（右侧）

**位置**: 右侧上部，宽度约 75%
**内容**:
- 顶部：会话标题（如果有）
- 消息列表：
  - **User 消息**:
    - 格式：`User: <内容>`
    - 带时间戳（YYYY-MM-DD HH:MM）
  - **AI 消息**:
    - 格式：`AI: <内容>`
    - 逐段显示（段落为单位）
    - 生成中显示 `[正在生成...]`
- 自动滚动到最新消息
- 支持历史消息滚动（未来功能）

**消息渲染**:
```
User: 如何优化这段代码？
2024-03-10 10:23

AI: 我建议从以下几个方面优化：

1. 使用更高效的数据结构
2. 减少不必要的内存分配

[正在生成...]
```

#### 4. 输入框（右下角）

**位置**: 右侧下部
**内容**:
- 边框标题：`输入`
- 单行文本输入
- 光标显示（`█`）

**功能**:
- Enter 发送消息
- Ctrl+C 清空输入
- ESC 取消焦点
- 基础编辑（光标移动、删除）

**实现要点**:
```rust
// 输入状态
struct InputState {
    content: String,
    cursor_pos: usize,
}
```

#### 5. 底部信息栏

**位置**: 底部，全宽
**内容**:
- 左侧：统计信息（会话数、消息数）
- 右侧：当前焦点区域的快捷键提示

**示例**:
```
会话: 3/10 | 消息: 45 | ESC: 取消焦点
```

### 焦点管理

**焦点区域**:
1. **会话列表**: 高亮边框（青色）
2. **输入框**: 高亮边框（绿色）
3. **无焦点**: 灰色边框

**切换逻辑**:
- `Tab`: 会话列表 → 输入框
- `Shift+Tab`: 输入框 → 会话列表
- `ESC`: 取消当前焦点 → 会话列表

---

## 交互设计

### 快捷键映射（简化 Vim 风格）

#### 全局快捷键

```
Ctrl+C / Ctrl+Q  - 退出应用
Ctrl+H           - 显示帮助
Tab              - 切换焦点（会话列表 ↔ 输入框）
Shift+Tab        - 反向切换焦点
ESC              - 取消当前焦点 / 取消操作
```

#### 会话列表焦点

```
j / ↓            - 下一个会话
k / ↑            - 上一个会话
Enter            - 选择/切换到该会话
n / Ctrl+N       - 新建会话
d / Ctrl+D       - 删除当前会话（需确认）
/ / Ctrl+F       - 搜索会话
r / F5           - 刷新会话列表
```

#### 输入框焦点

```
Enter            - 发送消息
Ctrl+C           - 清空输入
ESC              - 取消焦点返回会话列表
左右箭头          - 移动光标
Backspace        - 删除字符
Ctrl+W           - 删除单词
Ctrl+U           - 清空整行
```

#### 聊天窗口（自动滚动）

```
- 始终显示最新消息
- 流式响应自动滚动
- 未来功能：Page Up/Down 滚动历史
```

### 交互流程

#### 1. 启动应用

```
1. 加载会话列表（最近 10 个）
2. 默认焦点在会话列表
3. 如果有会话，选中最新的
4. 显示该会话的聊天历史
```

#### 2. 发送消息

```
1. Tab 切换焦点到输入框
2. 输入消息内容
3. Enter 发送
4. 会话状态变为 "⏳ 等待回复..."
5. 聊天窗口显示 "[正在生成...]"
6. 逐段显示 AI 响应
7. 完成后，状态恢复正常
8. 焦点自动返回输入框（可继续对话）
```

#### 3. 切换会话

```
1. ESC 返回会话列表焦点
2. j/k 或箭头键导航
3. Enter 选择会话
4. 聊天窗口切换到新会话内容
5. 如果新会话有未读消息，清除 "(N条新消息)" 标注
```

#### 4. 新建会话

```
1. 会话列表焦点下按 n 或 Ctrl+N
2. 弹出对话框：
   ┌─ 新建会话 ──────────┐
   │ 标题（可选）：       │
   │ ________________     │
   │                      │
   │ [Enter:确认 ESC:取消] │
   └──────────────────────┘
3. 输入标题或直接 Enter（无标题）
4. 创建成功，自动切换到新会话
```

#### 5. 后台消息提醒

```
场景：当前在会话1，后台会话2收到 AI 响应
1. 会话列表中会话2显示 "(1条新消息)"
2. 状态栏闪烁提示 "会话2 有新消息"
3. 切换到会话2后，标注自动清除
```

#### 6. 错误处理

```
场景：LLM 调用失败
1. 会话状态显示 "❌ 连接失败"
2. 聊天窗口显示错误消息（红色）
   AI: [错误] 无法连接到 LLM 服务
   提示：请检查网络连接或 API 配置
3. 用户可以重试发送
```

### 状态转换

**会话状态机**:
```
Idle (空闲)
  ↓ 用户发送消息
WaitingResponse (⏳ 等待回复...)
  ↓ 开始接收流式响应
Streaming (生成中)
  ↓ 响应完成
Idle (空闲)

  任何状态 → Error (❌ 错误) → 手动恢复 → Idle
```

---

## 数据流与错误处理

### 数据流架构

#### 消息类型定义

```rust
/// 后台 → UI 的更新
enum BackendUpdate {
    /// 流式响应片段
    StreamChunk {
        session_id: Uuid,
        delta: String,
        is_final: bool,
    },
    /// 段落完成（用于逐段显示）
    ParagraphComplete {
        session_id: Uuid,
        paragraph: String,
    },
    /// 响应完成
    ResponseComplete {
        session_id: Uuid,
    },
    /// 错误
    Error {
        session_id: Uuid,
        error: String,
    },
    /// 会话列表更新
    SessionsUpdated {
        sessions: Vec<SessionItem>,
    },
}

/// UI → 后台的命令
enum BackendCommand {
    SendMessage {
        session_id: Uuid,
        content: String,
    },
    CreateSession {
        title: Option<String>,
    },
    DeleteSession {
        session_id: Uuid,
    },
    LoadSessions,
    LoadMessages {
        session_id: Uuid,
    },
}
```

#### 流式响应处理逻辑

```rust
/// 逐段显示的实现思路
async fn handle_stream(
    session_id: Uuid,
    update_tx: mpsc::Sender<BackendUpdate>
) -> Result<()> {
    let mut stream = conversation_flow.send_message_stream(...).await?;
    let mut buffer = String::new();

    while let Some(chunk) = stream.next().await? {
        buffer.push_str(&chunk.delta);

        // 检测段落边界（\n\n 或句号+换行）
        if buffer.contains("\n\n") || buffer.ends_with("。\n") {
            let paragraph = buffer.clone();
            buffer.clear();

            update_tx.send(BackendUpdate::ParagraphComplete {
                session_id,
                paragraph,
            }).await?;
        }
    }

    // 发送剩余内容
    if !buffer.is_empty() {
        update_tx.send(BackendUpdate::ParagraphComplete {
            session_id,
            paragraph: buffer,
        }).await?;
    }

    update_tx.send(BackendUpdate::ResponseComplete {
        session_id
    }).await?;

    Ok(())
}
```

### 错误处理策略

#### 错误分类

```rust
enum TuiError {
    // 致命错误（需要退出）
    DatabaseInitFailed(String),
    TerminalSetupFailed(String),

    // 可恢复错误（显示给用户）
    LLMConnectionFailed(String),
    SessionNotFound(Uuid),
    MessageSendFailed(String),

    // 用户输入错误
    InvalidSessionId(String),
    EmptyMessage,
}
```

#### 错误显示

**致命错误**:
```
显示错误信息 → 清理资源 → 退出应用
```

**可恢复错误**:
- 会话列表标注：`❌ 错误信息`
- 聊天窗口：红色错误消息
- 底部状态栏：提示信息
- 5秒后自动清除（或用户手动清除）

#### 错误恢复

```
LLM 连接失败 → 保留用户输入 → 允许重试
会话加载失败 → 显示错误 → 刷新按钮重新加载
数据库错误 → 尝试重连 → 失败则提示并退出
```

#### 超时处理

```rust
// LLM 调用超时（30秒）
tokio::time::timeout(
    Duration::from_secs(30),
    conversation_flow.send_message(...)
).await
    .map_err(|_| TuiError::MessageSendFailed("请求超时".to_string()))?;
```

### 性能优化

#### UI 刷新频率

```rust
// 定时刷新，避免频繁重绘
let mut tick_interval = tokio::time::interval(Duration::from_millis(100));

// 流式响应批量更新
let mut update_buffer = Vec::new();
let mut last_update = Instant::now();

// 每 100ms 或缓冲区满时更新 UI
if last_update.elapsed() > Duration::from_millis(100) || update_buffer.len() > 10 {
    app_state.apply_updates(update_buffer.drain(..));
    terminal.draw(|f| render(&app_state, f))?;
    last_update = Instant::now();
}
```

#### 消息加载策略

```rust
// 初始加载最近 50 条消息
const INITIAL_MESSAGE_LIMIT: usize = 50;

// 滚动到顶部时懒加载更多（未来功能）
const LAZY_LOAD_BATCH: usize = 20;
```

#### 内存管理

```rust
// 限制内存中保存的会话数
const MAX_CACHED_SESSIONS: usize = 20;

// 切换会话时清理旧消息
if cached_sessions.len() > MAX_CACHED_SESSIONS {
    // 保留当前和最近访问的会话
    // 清理其他会话的消息缓存
}
```

---

## 测试策略

### 测试层次

#### 1. 单元测试（组件独立测试）

```rust
// state.rs 测试
#[cfg(test)]
mod tests {
    #[test]
    fn test_session_state_transitions() {
        // 测试会话状态转换：Idle → WaitingResponse → Streaming → Idle
    }

    #[test]
    fn test_focus_switching() {
        // 测试焦点切换逻辑
    }

    #[test]
    fn test_message_buffering() {
        // 测试段落缓冲和分割
    }
}

// keymap.rs 测试
#[test]
fn test_key_mapping() {
    // 测试快捷键映射正确性
}

#[test]
fn test_focus_dependent_keys() {
    // 测试不同焦点下的快捷键行为
}
```

#### 2. 集成测试（组件交互）

```rust
// tests/integration.rs
#[tokio::test]
async fn test_send_message_flow() {
    // 模拟：输入消息 → 发送 → 接收响应 → UI更新
    let (app, mut backend_rx) = create_test_app().await;

    // 1. 切换焦点到输入框
    app.handle_key(KeyCode::Tab);

    // 2. 输入消息
    app.handle_input("测试消息");

    // 3. 发送
    app.handle_key(KeyCode::Enter);

    // 4. 验证后台收到命令
    let cmd = backend_rx.recv().await.unwrap();
    assert!(matches!(cmd, BackendCommand::SendMessage { .. }));
}

#[tokio::test]
async fn test_stream_response_rendering() {
    // 测试流式响应的逐段显示
}

#[tokio::test]
async fn test_error_handling() {
    // 测试错误状态显示和恢复
}

#[tokio::test]
async fn test_session_switching() {
    // 测试会话切换时的状态管理
}
```

#### 3. 手动测试清单（E2E）

```markdown
- [ ] 启动应用，正常显示界面
- [ ] 创建新会话（有标题/无标题）
- [ ] 发送消息，接收流式响应
- [ ] 响应逐段显示，无闪烁
- [ ] 切换会话，内容正确加载
- [ ] 后台会话收到消息时显示标注
- [ ] 删除会话（带确认）
- [ ] 搜索会话
- [ ] 所有快捷键正常工作
- [ ] 错误场景：
  - [ ] LLM 连接失败显示错误
  - [ ] 超时处理正确
  - [ ] 数据库错误恢复
- [ ] 长时间运行无内存泄漏
- [ ] 终端大小调整时布局自适应
```

#### 4. 性能基准测试

```rust
#[tokio::test]
async fn bench_ui_refresh_rate() {
    // 测试 UI 刷新频率（目标：60fps，约 16ms）
}

#[tokio::test]
async fn bench_message_rendering() {
    // 测试渲染 1000 条消息的性能
}

#[tokio::test]
async fn bench_memory_usage() {
    // 测试长时间运行的内存使用
}
```

---

## 验收标准

### Stage 3 完成标准

- [ ] UI 框架搭建完成（Ratatui + Crossterm）
- [ ] 分栏布局实现（会话列表 + 聊天窗口）
- [ ] 会话列表组件（导航、状态标注）
- [ ] 聊天窗口组件（消息显示、滚动）
- [ ] 输入框组件（单行输入、光标）
- [ ] 快捷键系统（简化 Vim 风格）
- [ ] 流式响应逐段显示
- [ ] 错误提示和状态管理
- [ ] 焦点切换正常
- [ ] 后台任务通信正常
- [ ] 测试覆盖率 > 70%
- [ ] 无明显性能问题
- [ ] 手动测试清单全部通过

### 质量要求

**功能性**:
- 所有核心功能正常工作
- 边界情况处理正确
- 错误恢复机制有效

**性能**:
- UI 响应 < 100ms
- 流式渲染流畅（无卡顿）
- 内存使用稳定（< 50MB）

**可用性**:
- 快捷键直观易记
- 状态提示清晰
- 错误信息友好

**可维护性**:
- 代码结构清晰
- 测试覆盖充分
- 文档完整

---

## 实施计划

### 阶段划分

**Phase 1: 基础框架（2-3小时）**
- 项目脚手架
- 基础事件循环
- 简单 UI 渲染

**Phase 2: 核心组件（4-5小时）**
- 会话列表组件
- 聊天窗口组件
- 输入框组件

**Phase 3: 交互实现（3-4小时）**
- 快捷键系统
- 焦点管理
- 状态更新

**Phase 4: 后台集成（3-4小时）**
- 后台任务管理
- 流式响应处理
- 错误处理

**Phase 5: 测试完善（2-3小时）**
- 单元测试
- 集成测试
- 手动测试

**Phase 6: 文档和收尾（1-2小时）**
- 使用文档
- 代码注释
- 最终验收

---

**总计时间**: 15-21 小时（约 2-3 天）

**更新日期**: 2026-03-10
**状态**: 已批准，待实施
