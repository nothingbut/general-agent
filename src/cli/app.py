"""TUI application implementation using Textual."""
import asyncio
import logging
import uuid
from datetime import datetime
from typing import Optional

from textual import on
from textual.app import App, ComposeResult
from textual.binding import Binding
from textual.events import Key
from textual.widgets import Header, Footer, Input

from .widgets.message_list import MessageList
from .core_init import initialize_database, initialize_executor
from ..storage.database import Database
from ..storage.models import Session
from ..core.executor import AgentExecutor

logger = logging.getLogger(__name__)


class AgentTUI(App):
    """TUI Application for General Agent"""

    CSS_PATH = "app.css"

    BINDINGS = [
        Binding("ctrl+q", "quit", "退出", show=True),
        Binding("ctrl+n", "new_session", "New Session"),
        Binding("ctrl+k", "clear_screen", "Clear"),
        Binding("ctrl+l", "list_sessions", "会话列表", show=True),
    ]

    def __init__(
        self,
        session_id: Optional[str] = None,
        verbose: bool = False,
    ):
        """Initialize the TUI application

        Args:
            session_id: Optional session ID to resume
            verbose: Enable verbose logging
        """
        super().__init__()
        self.session_id = session_id
        self.verbose = verbose
        self.db: Optional[Database] = None
        self.executor: Optional[AgentExecutor] = None
        self.title = "General Agent"
        self.current_session: Optional[Session] = None

    def compose(self) -> ComposeResult:
        """Compose the UI layout"""
        yield Header()
        yield MessageList(id="messages")
        yield Input(
            placeholder="输入消息... (Enter 发送)",
            id="input"
        )
        yield Footer()

    async def on_mount(self) -> None:
        """Initialize components when app is mounted"""
        try:
            # Show initialization notification
            self.notify("正在初始化...")

            # Initialize database
            self.db = await initialize_database()

            # Initialize executor
            self.executor = await initialize_executor(
                self.db,
                verbose=self.verbose
            )

            # Load or create session
            await self.load_session(self.session_id)

            # Focus the input widget
            self.query_one("#input", Input).focus()

            # Show completion notification
            self.notify("初始化完成！", severity="information")

        except Exception as e:
            logger.error(f"Failed to initialize app: {e}")
            self.notify(f"初始化失败: {str(e)}", severity="error")

    def on_key(self, event: Key) -> None:
        """Handle key events - intercept Ctrl+K before Input widget"""
        if event.key == "ctrl+k":
            self.action_clear_screen()
            event.prevent_default()
            event.stop()

    @on(Input.Submitted)
    async def on_input_submitted(self, event: Input.Submitted) -> None:
        """Handle user input submission"""
        user_input = event.value.strip()

        if not user_input:
            return

        # Clear the input
        input_widget = self.query_one("#input", Input)
        input_widget.value = ""

        # Get message list widget
        message_list = self.query_one("#messages", MessageList)

        # Add user message
        message_list.add_message("user", user_input)

        # Show thinking indicator
        message_list.add_thinking_indicator()

        try:
            # Execute query
            if not self.executor or not self.current_session:
                raise RuntimeError("Application not properly initialized")

            result = await self.executor.execute(
                user_input=user_input,
                session_id=self.current_session.id
            )

            # Remove thinking indicator
            message_list.remove_thinking_indicator()

            # Add agent response
            message_list.add_message("agent", result["response"])

        except ConnectionError as e:
            message_list.remove_thinking_indicator()
            message_list.add_error(f"连接错误: {str(e)}")
            logger.error(f"Connection error: {e}")

        except Exception as e:
            message_list.remove_thinking_indicator()
            message_list.add_error(f"处理失败: {str(e)}")
            logger.error(f"Error processing query: {e}")

    async def action_new_session(self) -> None:
        """Create a new session"""
        try:
            await self.create_new_session()

            # Clear messages
            message_list = self.query_one("#messages", MessageList)
            message_list.clear_messages()

            # Notify user
            self.notify("已创建新会话", severity="information")

        except Exception as e:
            logger.error(f"Failed to create new session: {e}")
            self.notify(f"创建会话失败: {str(e)}", severity="error")

    def action_clear_screen(self) -> None:
        """Clear all messages from screen"""
        try:
            message_list = self.query_one("#messages", MessageList)
            message_list.clear_messages()
            self.notify("屏幕已清空", severity="information")

        except Exception as e:
            logger.error(f"Failed to clear screen: {e}")
            self.notify(f"清空失败: {str(e)}", severity="error")

    async def action_list_sessions(self) -> None:
        """显示会话列表（Ctrl+L）"""
        from .widgets import SessionList

        async def handle_selection(session_id: Optional[str]) -> None:
            """处理会话选择"""
            if session_id == "__new__":
                # 创建新会话
                await self.create_new_session()
                self.query_one("#messages", MessageList).clear_messages()
            elif session_id:
                # 加载选中的会话
                await self.load_session(session_id)
                self.query_one("#messages", MessageList).clear_messages()

        # 显示会话列表
        await self.push_screen(SessionList(self.db), handle_selection)

    async def create_new_session(self) -> None:
        """Create a new session object

        This is a synchronous helper that creates a Session object.
        Database persistence happens in load_session.
        """
        session_id = f"session-{uuid.uuid4()}"
        now = datetime.now()

        self.current_session = Session(
            id=session_id,
            title="新对话",
            created_at=now,
            updated_at=now,
            metadata=None
        )

        # Update subtitle
        self.sub_title = f"会话: {session_id[:8]}..."

        # Persist to database
        if self.db:
            await self.db.create_session(self.current_session)

    def _load_messages_to_list(self, messages: list, message_list: MessageList) -> None:
        """Load messages into the message list widget.

        Args:
            messages: List of Message objects to load
            message_list: MessageList widget to add messages to
        """
        for msg in messages:
            if msg.role == "user":
                message_list.add_message("user", msg.content)
            elif msg.role == "assistant":
                message_list.add_message("agent", msg.content)

    async def load_session(self, session_id: Optional[str]) -> None:
        """Load an existing session or create a new one

        Args:
            session_id: Session ID to load, or None to create new
        """
        if not self.db:
            raise RuntimeError("Database not initialized")

        if session_id:
            # Try to load existing session
            session = await self.db.get_session(session_id)

            if session:
                self.current_session = session
                self.sub_title = f"会话: {session_id[:8]}..."

                # Load message history
                messages = await self.db.get_messages(session_id)
                message_list = self.query_one("#messages", MessageList)

                self._load_messages_to_list(messages, message_list)

                logger.info(f"Loaded session {session_id} with {len(messages)} messages")
                return

            else:
                logger.warning(f"Session {session_id} not found, creating new session")
                self.notify(f"会话 {session_id} 不存在，创建新会话", severity="warning")

        # Create new session if not found or no session_id provided
        await self.create_new_session()

        logger.info(f"Created new session {self.current_session.id}")

    async def on_unmount(self) -> None:
        """Clean up resources when app is unmounted"""
        if self.db:
            try:
                await self.db.close()
                logger.info("Database connection closed")
            except Exception as e:
                logger.error(f"Error closing database: {e}")


def run_tui(session_id: Optional[str] = None, verbose: bool = False) -> None:
    """Run the TUI application

    Args:
        session_id: Optional session ID to resume
        verbose: Enable verbose logging
    """
    from .startup import startup_checks

    # Set logging level
    if verbose:
        logging.basicConfig(level=logging.DEBUG)
    else:
        logging.basicConfig(level=logging.INFO)

    # Startup checks
    try:
        asyncio.run(startup_checks(verbose))
    except RuntimeError as e:
        print(str(e))
        return

    # Create and run app
    app = AgentTUI(session_id=session_id, verbose=verbose)
    app.run()
