"""Tests for TUI application"""
import pytest
from textual.widgets import Header, Footer, Input

from src.cli.app import AgentTUI
from src.cli.widgets.message_list import MessageList


@pytest.mark.asyncio
async def test_agent_tui_creation():
    """Test AgentTUI app creation"""
    app = AgentTUI(session_id=None, verbose=False)

    assert app is not None
    assert app.title == "General Agent"
    assert app.session_id is None
    assert app.verbose is False
    assert app.db is None
    assert app.executor is None


@pytest.mark.asyncio
async def test_agent_tui_has_required_widgets():
    """Test TUI has #messages and #input widgets"""
    app = AgentTUI(session_id=None, verbose=False)

    async with app.run_test() as pilot:
        # Test that required widgets exist
        messages = app.query_one("#messages", MessageList)
        assert messages is not None

        input_widget = app.query_one("#input", Input)
        assert input_widget is not None

        # Test that header and footer exist
        header = app.query_one(Header)
        assert header is not None

        footer = app.query_one(Footer)
        assert footer is not None


@pytest.mark.asyncio
async def test_agent_tui_with_session_id():
    """Test AgentTUI creation with session_id"""
    app = AgentTUI(session_id="test-session-123", verbose=True)

    assert app.session_id == "test-session-123"
    assert app.verbose is True


@pytest.mark.asyncio
async def test_agent_tui_keybindings():
    """Test that key bindings are registered"""
    app = AgentTUI(session_id=None, verbose=False)

    # Check that bindings exist
    bindings = {binding.key for binding in app.BINDINGS}
    assert "ctrl+q" in bindings
    assert "ctrl+n" in bindings
    assert "ctrl+k" in bindings
