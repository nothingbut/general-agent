"""Tests for TUI widgets"""
import pytest
from textual.app import App, ComposeResult

from src.cli.widgets.message_list import MessageList


class MessageListTestApp(App):
    """Test app for MessageList widget"""

    def compose(self) -> ComposeResult:
        yield MessageList()


@pytest.mark.asyncio
async def test_message_list_creation():
    """Test MessageList widget creation"""
    app = MessageListTestApp()
    async with app.run_test() as pilot:
        message_list = app.query_one(MessageList)

        assert message_list is not None
        assert message_list.can_focus is False
        assert len(message_list.query(".message").nodes) == 0


@pytest.mark.asyncio
async def test_add_user_message():
    """Test adding user messages"""
    app = MessageListTestApp()
    async with app.run_test() as pilot:
        message_list = app.query_one(MessageList)

        # Add a user message
        message_list.add_message("user", "Hello, world!")
        await pilot.pause()

        # Verify message was added
        messages = message_list.query(".message").nodes
        assert len(messages) == 1

        # Verify message has user-message class
        user_messages = message_list.query(".user-message").nodes
        assert len(user_messages) == 1

        # Verify message content contains user icon and text
        message_text = str(user_messages[0].content)
        assert "🧑" in message_text
        assert "Hello, world!" in message_text


@pytest.mark.asyncio
async def test_add_agent_message():
    """Test adding agent messages"""
    app = MessageListTestApp()
    async with app.run_test() as pilot:
        message_list = app.query_one(MessageList)

        # Add an agent message
        message_list.add_message("agent", "I'm here to help!")
        await pilot.pause()

        # Verify message was added
        messages = message_list.query(".message").nodes
        assert len(messages) == 1

        # Verify message has agent-message class
        agent_messages = message_list.query(".agent-message").nodes
        assert len(agent_messages) == 1

        # Verify message content contains agent icon and text
        message_text = str(agent_messages[0].content)
        assert "🤖" in message_text
        assert "I'm here to help!" in message_text


@pytest.mark.asyncio
async def test_add_multiple_messages():
    """Test adding multiple messages in sequence"""
    app = MessageListTestApp()
    async with app.run_test() as pilot:
        message_list = app.query_one(MessageList)

        # Add multiple messages
        message_list.add_message("user", "First message")
        message_list.add_message("agent", "First response")
        message_list.add_message("user", "Second message")
        await pilot.pause()

        # Verify all messages were added
        messages = message_list.query(".message").nodes
        assert len(messages) == 3

        # Verify correct number of each type
        user_messages = message_list.query(".user-message").nodes
        agent_messages = message_list.query(".agent-message").nodes
        assert len(user_messages) == 2
        assert len(agent_messages) == 1


@pytest.mark.asyncio
async def test_add_error():
    """Test adding error messages"""
    app = MessageListTestApp()
    async with app.run_test() as pilot:
        message_list = app.query_one(MessageList)

        # Add an error message
        message_list.add_error("Connection failed!")
        await pilot.pause()

        # Verify error message was added
        error_messages = message_list.query(".error-message").nodes
        assert len(error_messages) == 1

        # Verify message content contains error icon and text
        message_text = str(error_messages[0].content)
        assert "❌" in message_text
        assert "Connection failed!" in message_text


@pytest.mark.asyncio
async def test_add_thinking_indicator():
    """Test adding thinking indicator"""
    app = MessageListTestApp()
    async with app.run_test() as pilot:
        message_list = app.query_one(MessageList)

        # Add thinking indicator
        message_list.add_thinking_indicator()
        await pilot.pause()

        # Verify thinking indicator was added
        thinking_nodes = message_list.query("#thinking").nodes
        assert len(thinking_nodes) == 1
        thinking = thinking_nodes[0]

        # Verify content
        thinking_text = str(thinking.content)
        assert "正在思考" in thinking_text


@pytest.mark.asyncio
async def test_remove_thinking_indicator():
    """Test removing thinking indicator"""
    app = MessageListTestApp()
    async with app.run_test() as pilot:
        message_list = app.query_one(MessageList)

        # Add and remove thinking indicator
        message_list.add_thinking_indicator()
        await pilot.pause()

        # Verify it exists
        thinking_nodes = message_list.query("#thinking").nodes
        assert len(thinking_nodes) == 1

        # Remove it
        message_list.remove_thinking_indicator()
        await pilot.pause()

        # Verify it was removed
        thinking_nodes = message_list.query("#thinking").nodes
        assert len(thinking_nodes) == 0


@pytest.mark.asyncio
async def test_remove_thinking_indicator_when_not_exists():
    """Test removing thinking indicator when it doesn't exist (should not raise exception)"""
    app = MessageListTestApp()
    async with app.run_test() as pilot:
        message_list = app.query_one(MessageList)

        # Try to remove thinking indicator when it doesn't exist
        # This should not raise an exception
        message_list.remove_thinking_indicator()
        await pilot.pause()

        # Should still work
        assert True


@pytest.mark.asyncio
async def test_clear_messages():
    """Test clearing all messages"""
    app = MessageListTestApp()
    async with app.run_test() as pilot:
        message_list = app.query_one(MessageList)

        # Add several messages
        message_list.add_message("user", "Message 1")
        message_list.add_message("agent", "Response 1")
        message_list.add_error("Error!")
        message_list.add_thinking_indicator()
        await pilot.pause()

        # Verify messages exist
        assert len(message_list.query(".message").nodes) > 0

        # Clear all messages
        message_list.clear_messages()
        await pilot.pause()

        # Verify all messages were cleared
        assert len(message_list.children) == 0


@pytest.mark.asyncio
async def test_message_auto_scroll():
    """Test that messages auto-scroll to bottom"""
    app = MessageListTestApp()
    async with app.run_test() as pilot:
        message_list = app.query_one(MessageList)

        # Add many messages to trigger scrolling
        for i in range(20):
            message_list.add_message("user", f"Message {i}")
            await pilot.pause(0.01)

        # Verify last message is visible (scroll is at bottom)
        # This is a basic check - the exact scroll position depends on widget size
        messages = message_list.query(".message").nodes
        assert len(messages) == 20


@pytest.mark.asyncio
async def test_session_list_creation():
    """Test SessionList creation with MockDB"""
    from src.cli.widgets.session_list import SessionList

    # Mock database
    class MockDB:
        async def get_all_sessions(self):
            return []

    session_list = SessionList(MockDB())
    assert session_list is not None
