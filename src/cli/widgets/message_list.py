"""MessageList widget for displaying chat messages in the TUI"""
from textual.widgets import Label
from textual.containers import VerticalScroll
from rich.text import Text


class MessageList(VerticalScroll):
    """Widget for displaying chat messages with auto-scroll"""

    DEFAULT_CSS = """
    MessageList {
        height: 1fr;
        width: 1fr;
        border: solid $primary;
    }

    .message {
        padding: 1 2;
        margin: 0 1;
    }

    .user-message {
        background: $boost;
        color: $text;
    }

    .agent-message {
        background: $panel;
        color: $text;
    }

    .error-message {
        background: $error;
        color: $text;
    }

    #thinking {
        background: $warning;
        color: $text;
        padding: 1;
        margin: 0 1;
    }
    """

    def __init__(self, **kwargs) -> None:
        """Initialize the MessageList widget"""
        super().__init__(**kwargs)
        self.can_focus = False

    def add_message(self, role: str, content: str) -> None:
        """Add a user or agent message to the list

        Args:
            role: Either 'user' or 'agent'
            content: The message content
        """
        # Choose icon based on role
        icon = "🧑" if role == "user" else "🤖"

        # Create formatted message text
        text = Text()
        text.append(f"{icon} ", style="bold")
        text.append(f"{role.title()}: ", style="bold")
        text.append(content)

        # Determine CSS class based on role
        css_class = f"{role}-message message"

        # Create and mount the message label
        message_label = Label(text, classes=css_class)
        self.mount(message_label)

        # Auto-scroll to bottom
        self.scroll_end(animate=False)

    def add_error(self, error_msg: str) -> None:
        """Add an error message to the list

        Args:
            error_msg: The error message to display
        """
        # Create formatted error text
        text = Text()
        text.append("❌ ", style="bold red")
        text.append("错误: ", style="bold red")
        text.append(error_msg, style="red")

        # Create and mount the error label
        error_label = Label(text, classes="error-message message")
        self.mount(error_label)

        # Auto-scroll to bottom
        self.scroll_end(animate=False)

    def add_thinking_indicator(self) -> None:
        """Add a 'thinking' indicator to show the agent is processing"""
        text = Text()
        text.append("🤖 ", style="bold")
        text.append("Agent: ", style="bold")
        text.append("正在思考... ", style="italic")

        # Create and mount the thinking indicator with specific ID
        thinking_label = Label(text, id="thinking", classes="message")
        self.mount(thinking_label)

        # Auto-scroll to bottom
        self.scroll_end(animate=False)

    def remove_thinking_indicator(self) -> None:
        """Remove the thinking indicator

        Gracefully handles the case where the indicator doesn't exist
        """
        try:
            thinking = self.query_one("#thinking")
            thinking.remove()
        except Exception:
            # Silently ignore if thinking indicator doesn't exist
            pass

    def clear_messages(self) -> None:
        """Clear all messages from the list"""
        # Remove only .message elements and #thinking
        for message in self.query(".message"):
            message.remove()
        try:
            self.query_one("#thinking").remove()
        except Exception:
            pass
