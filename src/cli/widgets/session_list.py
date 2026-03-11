"""SessionList widget for selecting sessions"""
import logging
from datetime import datetime
from typing import List

from textual import on
from textual.binding import Binding
from textual.containers import Horizontal, Vertical
from textual.screen import ModalScreen
from textual.widgets import Button, Label, ListItem, ListView, Static
from rich.text import Text

from ...storage.models import Session

logger = logging.getLogger(__name__)


class SessionListItem(ListItem):
    """会话列表项"""

    def __init__(self, session: Session, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.session = session

    def compose(self):
        """Compose the list item with session information"""
        # Format relative time
        now = datetime.now()
        delta = now - self.session.updated_at

        if delta.days > 0:
            time_str = f"{delta.days}天前"
        elif delta.seconds > 3600:
            time_str = f"{delta.seconds // 3600}小时前"
        elif delta.seconds > 60:
            time_str = f"{delta.seconds // 60}分钟前"
        else:
            time_str = "刚刚"

        # Create display text
        text = Text()
        text.append("● ", style="bold green")
        text.append(f"{self.session.id[:12]}... ", style="cyan")
        text.append(f'"{self.session.title}" ', style="white")
        text.append(f"({time_str})", style="dim")

        yield Label(text)


class SessionList(ModalScreen):
    """
    会话列表选择器（模态对话框）

    允许用户选择现有会话或创建新会话
    """

    BINDINGS = [
        Binding("escape", "dismiss", "返回", show=True),
        Binding("n", "new_session", "新建会话", show=True),
    ]

    DEFAULT_CSS = """
    SessionList {
        align: center middle;
    }

    SessionList > Vertical {
        width: 80;
        height: auto;
        max-height: 80%;
        background: $panel;
        border: thick $primary;
    }

    SessionList .title {
        dock: top;
        height: 3;
        content-align: center middle;
        background: $primary;
        color: $text;
    }

    SessionList ListView {
        height: 1fr;
        margin: 1;
    }

    SessionList .buttons {
        dock: bottom;
        height: 3;
        align: center middle;
    }
    """

    def __init__(self, db, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.db = db
        self.sessions: List[Session] = []

    def compose(self):
        """Compose the modal screen layout"""
        with Vertical():
            yield Static("选择会话", classes="title")
            yield ListView(id="session_list")
            with Horizontal(classes="buttons"):
                yield Button("新建会话 (N)", id="new_btn", variant="success")
                yield Button("返回 (Esc)", id="cancel_btn", variant="default")

    async def on_mount(self) -> None:
        """Load sessions when mounted"""
        try:
            # Load sessions from database
            self.sessions = await self.db.get_all_sessions()

            # Populate ListView with SessionListItem
            list_view = self.query_one("#session_list", ListView)

            for session in self.sessions:
                item = SessionListItem(session)
                await list_view.mount(item)

            # Focus first item if exists
            if self.sessions:
                list_view.focus()

        except Exception as e:
            # Log error and continue with empty list
            logger.error(f"Failed to load sessions: {e}")

    @on(ListView.Selected)
    async def on_list_view_selected(self, event: ListView.Selected) -> None:
        """Handle session selection"""
        if isinstance(event.item, SessionListItem):
            # Dismiss with selected session ID
            self.dismiss(event.item.session.id)

    @on(Button.Pressed)
    async def on_button_pressed(self, event: Button.Pressed) -> None:
        """Handle button presses"""
        if event.button.id == "new_btn":
            await self.action_new_session()
        elif event.button.id == "cancel_btn":
            self.dismiss(None)

    async def action_new_session(self) -> None:
        """Create new session action"""
        # Dismiss with special marker for new session
        self.dismiss("__new__")
