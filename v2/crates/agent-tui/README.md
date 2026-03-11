# Agent TUI

基于 Ratatui 的终端用户界面，为 General Agent V2 提供现代化的多会话对话体验。

## 特性

- **分栏布局**: 会话列表 + 聊天窗口，清晰的信息层次
- **实时流式响应**: 逐段显示 LLM 响应，无闪烁
- **会话状态标注**: 新消息提醒、等待回复、生成中、错误提示
- **Vim 风格快捷键**: j/k 导航，符合开发者习惯
- **异步后台处理**: Tokio 异步任务，UI 线程始终响应

## 快捷键

### 全局

- `Ctrl+C` / `Ctrl+Q`: 退出应用
- `Tab`: 切换焦点（会话列表 ↔ 输入框）
- `ESC`: 取消焦点

### 会话列表（焦点在会话列表时）

- `j` / `↓`: 下一个会话
- `k` / `↑`: 上一个会话
- `Enter`: 选择会话并加载消息
- `n`: 新建会话
- `d`: 删除当前会话
- `r` / `F5`: 刷新会话列表

### 输入框（焦点在输入框时）

- 输入文字
- `Enter`: 发送消息
- `Backspace`: 删除字符
- `←` / `→`: 移动光标
- `Ctrl+U`: 清空输入

## 架构

### 核心设计

- **单线程事件循环**: 主 UI 线程处理渲染和键盘事件
- **异步后台任务**: Tokio spawn 处理 LLM 调用，避免阻塞 UI
- **双通道通信**:
  - UI → Backend: `BackendCommand`（LoadSessions, SendMessage 等）
  - Backend → UI: `BackendUpdate`（SessionsLoaded, ParagraphComplete 等）

### 模块结构

```
agent-tui/
├── src/
│   ├── app.rs          # TuiApp 主应用循环
│   ├── state.rs        # AppState 状态管理
│   ├── event.rs        # 事件映射和处理
│   ├── backend.rs      # 后台命令和更新定义
│   └── ui/             # UI 组件
│       ├── layout.rs   # 布局计算
│       ├── colors.rs   # 颜色主题
│       ├── session_list.rs   # 会话列表
│       ├── chat_window.rs    # 聊天窗口
│       ├── input_box.rs      # 输入框
│       └── status_bar.rs     # 状态栏
├── examples/
│   └── tui_demo.rs     # 完整演示程序
└── tests/
    ├── app_tests.rs           # 应用逻辑测试
    ├── event_tests.rs         # 事件处理测试
    ├── state_tests.rs         # 状态管理测试
    └── integration_tests.rs   # 集成测试
```

## 使用示例

### 基础用法

```rust
use agent_tui::TuiApp;
use tokio::sync::mpsc;

#[tokio::main]
async fn main() -> Result<()> {
    // 创建应用和命令接收通道
    let (mut app, backend_rx) = TuiApp::new()?;

    // 启动后台任务处理命令
    tokio::spawn(async move {
        while let Some(cmd) = backend_rx.recv().await {
            // 处理后台命令
        }
    });

    // 运行应用
    app.run().await?;

    Ok(())
}
```

### 使用外部更新通道

```rust
use agent_tui::{TuiApp, backend::{BackendCommand, BackendUpdate}};
use tokio::sync::mpsc;

#[tokio::main]
async fn main() -> Result<()> {
    // 创建更新通道
    let (update_tx, update_rx) = mpsc::unbounded_channel();

    // 创建应用
    let (mut app, backend_rx) = TuiApp::new_with_channel(update_rx)?;

    // 启动后台任务
    tokio::spawn(async move {
        while let Some(cmd) = backend_rx.recv().await {
            // 处理命令并发送更新
            update_tx.send(BackendUpdate::SessionsLoaded {
                sessions: vec![]
            }).ok();
        }
    });

    app.run().await?;
    Ok(())
}
```

## 运行演示

确保已安装 Ollama 并启动 qwen3.5:0.8b 模型：

```bash
# 启动演示程序
cargo run -p agent-tui --example tui_demo

# 或使用日志输出
RUST_LOG=info cargo run -p agent-tui --example tui_demo
```

## 测试

```bash
# 运行所有测试
cargo test -p agent-tui

# 运行特定测试
cargo test -p agent-tui --test integration_tests

# 查看测试覆盖率（需要安装 tarpaulin）
cargo tarpaulin -p agent-tui --out Stdout
```

## 技术栈

- **UI 框架**: [Ratatui](https://ratatui.rs/) 0.26
- **终端控制**: [Crossterm](https://github.com/crossterm-rs/crossterm) 0.27
- **异步运行时**: [Tokio](https://tokio.rs/)
- **依赖模块**:
  - `agent-core`: 核心接口定义
  - `agent-workflow`: 会话和对话流程管理
  - `agent-storage`: 数据持久化
  - `agent-llm`: LLM 客户端

## 开发

### 添加新的 UI 组件

1. 在 `src/ui/` 下创建新组件文件
2. 实现 `render_xxx(f: &mut Frame, area: Rect, state: &AppState)` 函数
3. 在 `src/ui/mod.rs` 中导出
4. 在 `src/app.rs` 的 `draw()` 方法中调用

### 添加新的后台命令

1. 在 `src/backend.rs` 中添加 `BackendCommand` 变体
2. 在 `examples/tui_demo.rs` 的 `run_backend` 中处理命令
3. 发送相应的 `BackendUpdate` 更新 UI

## 已知问题

- 输入框暂不支持多行输入
- 未实现历史记录搜索
- 未实现会话导出功能

## License

MIT
