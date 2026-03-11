"""Tests for the simple router module."""

import pytest
from src.core.router import SimpleRouter, ExecutionPlan


class TestExecutionPlan:
    """Test ExecutionPlan dataclass."""

    def test_execution_plan_creation(self):
        """Test creating an execution plan."""
        plan = ExecutionPlan(
            type="simple_query",
            requires_llm=True,
            requires_rag=False,
            requires_tools=False,
            metadata=None
        )
        assert plan.type == "simple_query"
        assert plan.requires_llm is True
        assert plan.requires_rag is False
        assert plan.requires_tools is False
        assert plan.metadata is None

    def test_execution_plan_with_metadata(self):
        """Test creating execution plan with metadata."""
        metadata = {"confidence": 0.95, "intent": "greeting"}
        plan = ExecutionPlan(
            type="simple_query",
            requires_llm=True,
            metadata=metadata
        )
        assert plan.metadata == metadata
        assert plan.metadata["confidence"] == 0.95

    def test_execution_plan_is_immutable(self):
        """Test that ExecutionPlan is immutable (frozen)."""
        plan = ExecutionPlan(
            type="simple_query",
            requires_llm=True
        )

        # Attempting to modify any field should raise FrozenInstanceError
        with pytest.raises(Exception):  # dataclasses.FrozenInstanceError
            plan.type = "task"

        with pytest.raises(Exception):
            plan.requires_llm = False

        with pytest.raises(Exception):
            plan.requires_rag = True


class TestSimpleRouter:
    """Test SimpleRouter class."""

    def test_router_creation(self):
        """Test creating router instance."""
        router = SimpleRouter()
        assert router is not None
        assert isinstance(router, SimpleRouter)

    def test_simple_query_routing(self):
        """Test simple query routing."""
        router = SimpleRouter()
        plan = router.route("What is the weather today?", context=None)

        assert isinstance(plan, ExecutionPlan)
        assert plan.type == "simple_query"
        assert plan.requires_llm is True
        assert plan.requires_rag is False
        assert plan.requires_tools is False

    def test_greeting_routing(self):
        """Test greeting detection routing."""
        router = SimpleRouter()

        # Test various greetings - MVP routes all to simple_query
        greetings = ["hello", "hi there", "good morning"]
        for greeting in greetings:
            plan = router.route(greeting, context=None)
            assert isinstance(plan, ExecutionPlan)
            assert plan.type == "simple_query"
            assert plan.requires_llm is True

    def test_task_routing(self):
        """Test task routing (MVP: all go to simple_query)."""
        router = SimpleRouter()

        # Test task-like inputs - MVP routes all to simple_query
        task_inputs = [
            "create a new project",
            "analyze the database schema",
            "refactor the authentication module"
        ]

        for task_input in task_inputs:
            plan = router.route(task_input, context=None)
            assert isinstance(plan, ExecutionPlan)
            assert plan.type == "simple_query"
            assert plan.requires_llm is True

    def test_empty_input_raises_error(self):
        """Test that empty input raises ValueError."""
        router = SimpleRouter()

        with pytest.raises(ValueError, match="User input cannot be empty"):
            router.route("", context=None)

    def test_whitespace_input_raises_error(self):
        """Test that whitespace-only input raises ValueError."""
        router = SimpleRouter()

        with pytest.raises(ValueError, match="User input cannot be empty"):
            router.route("   ", context=None)

    def test_route_with_context(self):
        """Test routing with context parameter."""
        router = SimpleRouter()
        context = {"user_id": 123, "session": "abc"}

        plan = router.route("What is Python?", context=context)
        assert isinstance(plan, ExecutionPlan)
        assert plan.type == "simple_query"
        # Context is accepted but not used in MVP


class TestMCPRouting:
    """Test MCP routing functionality."""

    def test_route_mcp_explicit_syntax(self):
        """Test routing MCP explicit syntax."""
        router = SimpleRouter()

        plan = router.route("@mcp:filesystem:read_file path='/tmp/test.txt'")

        assert plan.type == "mcp"
        assert plan.requires_tools is True
        assert plan.metadata["server"] == "filesystem"
        assert plan.metadata["tool"] == "read_file"
        assert plan.metadata["arguments"]["path"] == "/tmp/test.txt"

    def test_route_mcp_with_multiple_arguments(self):
        """Test MCP routing with multiple arguments."""
        router = SimpleRouter()

        plan = router.route(
            '@mcp:filesystem:write_file path="/tmp/test.txt" content="Hello World"'
        )

        assert plan.type == "mcp"
        assert plan.metadata["tool"] == "write_file"
        assert plan.metadata["arguments"]["path"] == "/tmp/test.txt"
        assert plan.metadata["arguments"]["content"] == "Hello World"

    def test_route_mcp_takes_precedence_over_skill(self):
        """Test MCP syntax takes precedence over skill."""
        router = SimpleRouter()

        # @mcp: should match before @skill:
        plan = router.route("@mcp:filesystem:read_file path='/tmp/test.txt'")

        assert plan.type == "mcp"

    def test_route_invalid_mcp_syntax(self):
        """Test invalid MCP syntax falls back to simple query."""
        router = SimpleRouter()

        # Missing colon between server and tool
        plan = router.route("@mcp:filesystem-read_file")

        # Should not match MCP pattern, fall back to simple query
        assert plan.type == "simple_query"
