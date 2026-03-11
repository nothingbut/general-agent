# Python TUI方案调研报告

**日期：** 2026-03-04
**项目：** General Agent
**目标：** 为本地部署提供命令行界面（TUI）方案

---

## 1. 调研概述

### 1.1 背景

当前General Agent项目使用FastAPI + Web界面，适合云端部署。为了支持本地部署场景，需要提供一个轻量级的TUI界面，让用户可以在终端中与Agent交互。

### 1.2 需求分析

**核心需求：**
- 聊天对话界面（消息列表 + 输入框）
- 实时流式响应显示
- 会话历史管理
- 技能列表和调用
- 美观的消息格式化（支持Markdown、代码高亮）
- 键盘导航和快捷键

**非功能需求：**
- 跨平台支持（macOS、Linux、Windows）
- 性能良好（响应快速、占用资源少）
- 易于集成到现有FastAPI架构
- 良好的文档和社区支持

---

## 2. 主流方案对比

### 2.1 Textual

**简介：**
Textual是由Textualize.io开发的现代化TUI框架，灵感来自web开发模式，提供类似React/Vue的开发体验。

**核心特性：**
- 🎨 **CSS样式系统**：使用CSS定义UI样式
- 🧩 **组件化架构**：内置丰富的Widget（Button、Input、DataTable等）
- ⚡ **异步优先**：原生支持asyncio
- 🌐 **Web兼容**：可以部署到web浏览器（通过textual-web）
- 📱 **响应式布局**：自适应终端大小

**代码示例：**
```python
from textual.app import App, ComposeResult
from textual.containers import ScrollableContainer
from textual.widgets import Header, Footer, Input, Static

class ChatApp(App):
    """A simple chat TUI."""

    CSS = """
    #messages {
        height: 1fr;
        border: solid green;
    }

    Input {
        dock: bottom;
    }
    """

    BINDINGS = [
        ("q", "quit", "Quit"),
        ("ctrl+c", "clear", "Clear History"),
    ]

    def compose(self) -> ComposeResult:
        yield Header()
        yield ScrollableContainer(id="messages")
        yield Input(placeholder="Type a message...")
        yield Footer()

    async def on_input_submitted(self, event: Input.Submitted) -> None:
        """Handle message submission."""
        message = event.value
        event.input.value = ""

        # Add user message
        messages = self.query_one("#messages")
        messages.mount(Static(f"[bold blue]You:[/] {message}"))

        # Get AI response (streaming)
        async for chunk in self.get_ai_response(message):
            messages.mount(Static(f"[bold green]AI:[/] {chunk}"))

    async def get_ai_response(self, message: str):
        # Integration with existing FastAPI backend
        async with httpx.AsyncClient() as client:
            response = await client.post(
                "http://localhost:8000/api/chat",
                json={"message": message, "session_id": self.session_id}
            )
            yield response.json()["response"]

if __name__ == "__main__":
    app = ChatApp()
    app.run()
```

**优点：**
- ✅ 功能完整，适合构建复杂TUI应用
- ✅ 文档完善，学习曲线平缓
- ✅ 社区活跃（GitHub 25k+ stars）
- ✅ 可选Web部署，一套代码多端运行
- ✅ 内置丰富的Widget，开箱即用

**缺点：**
- ❌ 相对较重（依赖多）
- ❌ 对简单应用可能过度设计

**适用场景：**
- 功能丰富的TUI应用
- 需要复杂布局和交互
- 希望未来扩展到web

---

### 2.2 Rich

**简介：**
Rich是一个终端美化库，专注于提供漂亮的输出格式，不是完整的应用框架。

**核心特性：**
- 🎨 **丰富的格式化**：支持颜色、样式、表格、Markdown
- 📊 **进度条和状态**：Live display和Progress
- 🔍 **语法高亮**：自动高亮代码和数据结构
- 📝 **日志增强**：RichHandler集成到logging
- 🖼️ **渲染能力**：Panel、Table、Tree等组件

**代码示例：**
```python
from rich.console import Console
from rich.markdown import Markdown
from rich.panel import Panel
from rich.live import Live
from rich.layout import Layout
import httpx

console = Console()

def chat_loop():
    """Simple chat loop with Rich."""
    console.print(Panel("Welcome to General Agent TUI", style="bold green"))

    session_id = "cli-session"

    while True:
        # Get user input
        message = console.input("[bold blue]You:[/] ")

        if message.lower() in ["exit", "quit"]:
            break

        # Show loading
        with console.status("[bold green]Thinking..."):
            response = httpx.post(
                "http://localhost:8000/api/chat",
                json={"message": message, "session_id": session_id}
            )
            result = response.json()["response"]

        # Display AI response with Markdown
        md = Markdown(result)
        console.print(Panel(md, title="[bold green]AI", border_style="green"))
        console.print()

if __name__ == "__main__":
    chat_loop()
```

**优点：**
- ✅ 轻量级，易于集成
- ✅ 输出效果优美
- ✅ 文档优秀，易于上手
- ✅ 性能好，资源占用低
- ✅ 可与其他库组合使用

**缺点：**
- ❌ 不是完整的应用框架
- ❌ 需要手动处理布局和交互逻辑
- ❌ 不支持复杂的键盘导航

**适用场景：**
- 简单的CLI工具
- 美化现有命令行输出
- 与Prompt Toolkit组合使用

---

### 2.3 Prompt Toolkit

**简介：**
Prompt Toolkit是一个构建交互式命令行应用的库，专注于输入处理和自动补全。

**核心特性：**
- ⌨️ **强大的输入处理**：多行编辑、历史记录、自动补全
- 🔤 **语法高亮**：集成Pygments
- 🎯 **自动补全**：支持嵌套补全、动态补全
- 📜 **历史管理**：持久化历史记录
- 🖼️ **全屏应用**：支持构建复杂的全屏UI

**代码示例：**
```python
from prompt_toolkit import PromptSession
from prompt_toolkit.completion import WordCompleter
from prompt_toolkit.styles import Style
from prompt_toolkit.formatted_text import HTML
import httpx

# Define custom style
style = Style.from_dict({
    'prompt': '#00aa00 bold',
    'ai': '#00ff00',
})

# Define skill completions
skill_completer = WordCompleter(
    ['@greeting', '@reminder', '@note', '@task', '@brainstorm'],
    ignore_case=True,
)

def chat_with_completer():
    """Chat with autocompletion for skills."""
    session = PromptSession(
        completer=skill_completer,
        style=style,
        multiline=False,
    )

    session_id = "cli-session"

    print("Welcome to General Agent (type 'exit' to quit)")

    while True:
        try:
            # Get user input with autocompletion
            message = session.prompt(
                HTML('<prompt>You:</prompt> '),
                multiline=False,
            )

            if message.lower() in ['exit', 'quit']:
                break

            # Call backend
            response = httpx.post(
                "http://localhost:8000/api/chat",
                json={"message": message, "session_id": session_id}
            )
            result = response.json()["response"]

            # Display response
            print(HTML(f'<ai>AI:</ai> {result}'))
            print()

        except (EOFError, KeyboardInterrupt):
            break

    print("Goodbye!")

if __name__ == "__main__":
    chat_with_completer()
```

**优点：**
- ✅ 输入体验极佳（类似IDE）
- ✅ 自动补全功能强大
- ✅ 支持Emacs/Vi键绑定
- ✅ 历史记录持久化
- ✅ 可构建全屏应用

**缺点：**
- ❌ 布局能力较弱（需要自己管理）
- ❌ 学习曲线较陡（全屏应用）
- ❌ 输出美化需要配合Rich

**适用场景：**
- REPL类应用
- 需要强大自动补全的CLI
- 交互式配置工具

---

## 3. 方案对比矩阵

| 特性 | Textual | Rich | Prompt Toolkit |
|------|---------|------|----------------|
| **应用框架** | ✅ 完整 | ❌ 仅输出 | ⚠️ 输入为主 |
| **布局能力** | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐ |
| **输入处理** | ⭐⭐⭐⭐ | ⭐ | ⭐⭐⭐⭐⭐ |
| **输出美化** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **异步支持** | ✅ 原生 | ❌ 同步 | ✅ 支持 |
| **学习曲线** | ⭐⭐⭐ | ⭐ | ⭐⭐⭐⭐ |
| **文档质量** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **社区活跃度** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **依赖大小** | 中等 | 小 | 小 |
| **适合复杂UI** | ✅ | ❌ | ⚠️ |

---

## 4. 推荐方案

### 4.1 最佳方案：Textual

**理由：**

1. **完整的应用框架**：Textual提供了构建TUI应用所需的一切，无需组合多个库
2. **异步优先**：与现有FastAPI后端完美集成（都是asyncio）
3. **丰富的组件**：内置的Widget可以快速构建聊天界面
4. **可扩展性**：未来可以通过textual-web部署到浏览器
5. **开发体验好**：类似web开发，学习曲线平缓

**架构集成：**
```
┌─────────────────────────────────────────┐
│         General Agent                    │
│                                          │
│  ┌────────────────┐  ┌────────────────┐ │
│  │   Web UI       │  │   TUI Client   │ │
│  │  (FastAPI +    │  │   (Textual)    │ │
│  │   Jinja2)      │  │                │ │
│  └────────┬───────┘  └────────┬───────┘ │
│           │                   │          │
│           └────────┬──────────┘          │
│                    ▼                     │
│         ┌────────────────────┐           │
│         │   FastAPI Backend  │           │
│         │   - /api/chat      │           │
│         │   - /api/skills    │           │
│         │   - /ws/chat       │           │
│         └────────────────────┘           │
└─────────────────────────────────────────┘
```

### 4.2 替代方案：Rich + Prompt Toolkit

如果需要更轻量级的解决方案，可以组合使用：
- **Prompt Toolkit**：处理输入、自动补全、历史记录
- **Rich**：美化输出、显示Markdown、代码高亮

**优点：**
- 更轻量（依赖少）
- 灵活性高
- 性能更好

**缺点：**
- 需要更多手动代码
- 布局能力有限
- 维护成本高

---

## 5. 实现计划

### 5.1 Phase 1: 基础TUI（Week 1）

**目标：** 实现基本的聊天界面

**任务：**
1. ✅ 安装Textual依赖
2. ✅ 创建基础ChatApp类
3. ✅ 实现消息列表显示
4. ✅ 实现输入框和提交逻辑
5. ✅ 集成FastAPI后端（/api/chat）

**文件结构：**
```
src/
├── tui/
│   ├── __init__.py
│   ├── app.py              # Textual应用主类
│   ├── widgets/
│   │   ├── __init__.py
│   │   ├── chat_message.py # 消息组件
│   │   ├── chat_list.py    # 消息列表
│   │   └── input_box.py    # 输入框
│   └── client.py           # Backend API客户端
└── cli.py                  # TUI入口
```

### 5.2 Phase 2: 增强功能（Week 2）

**目标：** 添加高级功能

**任务：**
1. ✅ 实现流式响应显示
2. ✅ 添加会话历史侧边栏
3. ✅ 实现技能列表和快速调用
4. ✅ 添加键盘快捷键
5. ✅ Markdown渲染和代码高亮

### 5.3 Phase 3: 优化与测试（Week 3）

**目标：** 性能优化和测试

**任务：**
1. ✅ 性能优化（减少重绘）
2. ✅ 错误处理和重试
3. ✅ 单元测试和集成测试
4. ✅ 文档编写（README、使用指南）
5. ✅ 打包和发布

---

## 6. 示例实现

### 6.1 完整的Textual应用骨架

```python
# src/tui/app.py
from textual.app import App, ComposeResult
from textual.containers import Container, ScrollableContainer
from textual.widgets import Header, Footer, Input, Static, ListView, ListItem
from textual.binding import Binding
import httpx

class ChatMessage(Static):
    """A chat message widget."""

    def __init__(self, role: str, content: str, **kwargs):
        super().__init__(**kwargs)
        self.role = role
        self.content = content

    def render(self) -> str:
        color = "blue" if self.role == "user" else "green"
        return f"[bold {color}]{self.role}:[/] {self.content}"


class GeneralAgentTUI(App):
    """General Agent TUI Application."""

    CSS = """
    #sidebar {
        width: 30;
        dock: left;
        border-right: solid green;
    }

    #main {
        width: 1fr;
    }

    #messages {
        height: 1fr;
        padding: 1;
    }

    #input-container {
        dock: bottom;
        height: 3;
        padding: 0 1;
    }

    .session-item {
        padding: 0 1;
    }

    .session-item:hover {
        background: $boost;
    }
    """

    BINDINGS = [
        Binding("q", "quit", "Quit"),
        Binding("n", "new_session", "New Session"),
        Binding("ctrl+l", "clear_messages", "Clear"),
    ]

    def __init__(self):
        super().__init__()
        self.session_id = None
        self.backend_url = "http://localhost:8000"

    def compose(self) -> ComposeResult:
        """Create child widgets."""
        yield Header()

        with Container(id="sidebar"):
            yield Static("## Sessions", classes="section-title")
            yield ListView(id="sessions")

        with Container(id="main"):
            yield ScrollableContainer(id="messages")
            with Container(id="input-container"):
                yield Input(placeholder="Type your message... (@skill for skills)")

        yield Footer()

    def on_mount(self) -> None:
        """Initialize on mount."""
        self.action_new_session()

    async def on_input_submitted(self, event: Input.Submitted) -> None:
        """Handle message submission."""
        message = event.value.strip()
        if not message:
            return

        # Clear input
        event.input.value = ""

        # Add user message
        messages = self.query_one("#messages", ScrollableContainer)
        messages.mount(ChatMessage("You", message))

        # Scroll to bottom
        messages.scroll_end(animate=False)

        # Get AI response
        try:
            async with httpx.AsyncClient() as client:
                response = await client.post(
                    f"{self.backend_url}/api/chat",
                    json={
                        "message": message,
                        "session_id": self.session_id
                    },
                    timeout=30.0
                )
                result = response.json()
                ai_message = result.get("response", "Error: No response")

                # Add AI message
                messages.mount(ChatMessage("AI", ai_message))
                messages.scroll_end(animate=False)

        except Exception as e:
            messages.mount(ChatMessage("System", f"Error: {str(e)}"))
            messages.scroll_end(animate=False)

    def action_new_session(self) -> None:
        """Create a new session."""
        import uuid
        self.session_id = f"tui-{uuid.uuid4().hex[:8]}"

        # Add to sessions list
        sessions = self.query_one("#sessions", ListView)
        sessions.mount(ListItem(Static(f"Session {len(sessions.children) + 1}")))

        # Clear messages
        self.action_clear_messages()

    def action_clear_messages(self) -> None:
        """Clear all messages."""
        messages = self.query_one("#messages", ScrollableContainer)
        messages.remove_children()


def main():
    """Entry point for TUI."""
    app = GeneralAgentTUI()
    app.run()


if __name__ == "__main__":
    main()
```

### 6.2 CLI入口

```python
# src/cli.py
import typer
from src.tui.app import main as tui_main

app = typer.Typer()

@app.command()
def chat():
    """Start interactive TUI chat."""
    tui_main()

@app.command()
def version():
    """Show version information."""
    typer.echo("General Agent v1.0.0")

if __name__ == "__main__":
    app()
```

### 6.3 依赖配置

```toml
# pyproject.toml (新增)
[project.optional-dependencies]
tui = [
    "textual>=0.82.0",
    "typer>=0.9.0",
]

[project.scripts]
general-agent = "src.cli:app"
```

---

## 7. 技术细节

### 7.1 流式响应处理

使用WebSocket实现流式响应：

```python
async def stream_response(self, message: str):
    """Stream AI response using WebSocket."""
    import websockets

    uri = f"ws://localhost:8000/ws/chat?session_id={self.session_id}"

    async with websockets.connect(uri) as websocket:
        # Send message
        await websocket.send(message)

        # Receive chunks
        full_response = ""
        async for chunk in websocket:
            full_response += chunk
            # Update widget in real-time
            self.update_last_message(full_response)
```

### 7.2 技能自动补全

使用Textual的Suggester实现：

```python
from textual.suggester import Suggester

class SkillSuggester(Suggester):
    """Auto-suggest skills starting with @."""

    async def get_suggestion(self, value: str) -> str | None:
        if not value.startswith("@"):
            return None

        skills = ["@greeting", "@reminder", "@note", "@task", "@brainstorm"]
        for skill in skills:
            if skill.startswith(value):
                return skill[len(value):]

        return None

# Usage in Input widget
yield Input(
    placeholder="Type your message...",
    suggester=SkillSuggester()
)
```

### 7.3 跨平台兼容性

Textual自动处理跨平台差异：
- 终端颜色支持检测
- 鼠标事件处理
- 窗口大小变化

无需特殊配置。

---

## 8. 总结

### 8.1 推荐使用Textual

**核心理由：**
1. ✅ 完整的应用框架，快速开发
2. ✅ 异步原生支持，与FastAPI无缝集成
3. ✅ 丰富的组件和优秀的文档
4. ✅ 可选web部署，未来扩展性好
5. ✅ 活跃的社区和持续更新

### 8.2 实施建议

**短期（MVP）：**
- 使用Textual快速构建基础聊天界面
- 集成现有FastAPI后端
- 实现基本的会话管理

**中期（功能增强）：**
- 添加技能快速调用面板
- 实现流式响应显示
- 添加Markdown和代码高亮

**长期（生态扩展）：**
- 通过textual-web部署到浏览器
- 支持主题和插件系统
- 提供TUI SDK供用户扩展

### 8.3 性能预期

基于Textual的性能测试：
- **启动时间**：< 1秒
- **内存占用**：~30MB
- **响应延迟**：< 50ms（本地）
- **并发支持**：单用户应用，无并发需求

---

## 9. 参考资源

- **Textual官方文档**：https://textual.textualize.io/
- **Textual GitHub**：https://github.com/textualize/textual
- **Rich官方文档**：https://rich.readthedocs.io/
- **Prompt Toolkit文档**：https://python-prompt-toolkit.readthedocs.io/
- **示例项目**：
  - [Posting](https://github.com/darrenburns/posting) - HTTP客户端TUI
  - [Harlequin](https://github.com/tconbeer/harlequin) - SQL IDE TUI
  - [Kupo](https://github.com/darrenburns/kupo) - Git TUI

---

## 附录A：快速开始

```bash
# 1. 安装依赖
pip install ".[tui]"

# 2. 启动后端
uvicorn src.main:app --reload

# 3. 启动TUI（新终端）
general-agent chat

# 4. 开发模式（热重载）
textual run --dev src/tui/app.py
```

## 附录B：常见问题

**Q: Textual vs Web界面，如何选择？**
A: 本地快速使用选TUI，多用户/跨设备选Web。两者可以共存。

**Q: TUI性能如何？**
A: Textual使用异步渲染，性能优秀。对于聊天应用完全够用。

**Q: 是否支持鼠标？**
A: 是的，Textual完全支持鼠标点击、滚动等操作。

**Q: Windows兼容性？**
A: Textual完全支持Windows Terminal和WSL。

**Q: 如何调试TUI应用？**
A: 使用`textual console`查看日志，或使用VS Code的调试功能。
