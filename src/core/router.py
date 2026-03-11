"""Simple router for routing user input to execution plans.

This module provides a simple router that analyzes user input and determines
the appropriate execution plan. In the MVP version, all inputs are routed to
simple_query with LLM processing.
"""

import re
from dataclasses import dataclass
from typing import Any, Literal, Dict


@dataclass(frozen=True)
class ExecutionPlan:
    """Execution plan for processing user input.

    This is an immutable dataclass - all fields are frozen after creation.

    Attributes:
        type: Type of execution ('simple_query' | 'task' | 'skill' | 'mcp')
        requires_llm: Whether LLM processing is required
        requires_rag: Whether RAG (retrieval) is required
        requires_tools: Whether tool/function calling is required
        metadata: Optional metadata dictionary for additional context
    """

    type: Literal["simple_query", "task", "skill", "mcp"]
    requires_llm: bool
    requires_rag: bool = False
    requires_tools: bool = False
    metadata: dict[str, Any] | None = None


class SimpleRouter:
    """Simple router for user input.

    Routes user input to appropriate execution plans. Supports:
    - MCP tool detection (@mcp:server:tool syntax)
    - Skill detection (@skill or /skill syntax)
    - Parameter parsing (key='value' or key="value")
    - Simple query routing (default)

    Future versions will add:
    - Intent recognition (greeting, question, command)
    - RAG requirement detection
    """

    # Pattern to detect MCP explicit invocation: @mcp:server:tool
    MCP_PATTERN = re.compile(r'^@mcp:(\w+):(\w+)')

    # Pattern to detect skill invocation: @skill or /skill (but not @mcp:)
    SKILL_PATTERN = re.compile(r'^(?!@mcp:)[@/](\S+)')

    # Pattern to parse parameters: key='value' or key="value"
    PARAM_PATTERN = re.compile(r"(\w+)=['\"]([^'\"]+)['\"]")

    def route(self, user_input: str, context: Any = None) -> ExecutionPlan:
        """Route user input to an execution plan.

        Priority order:
        1. MCP explicit syntax (@mcp:server:tool)
        2. Skill invocation (@skill or /skill)
        3. Simple query

        Args:
            user_input: The user's input string to route
            context: Optional context for routing decisions

        Returns:
            ExecutionPlan with routing decisions

        Raises:
            ValueError: If user_input is empty or whitespace-only
        """
        # Validate input
        if not user_input or not user_input.strip():
            raise ValueError("User input cannot be empty")

        user_input = user_input.strip()

        # 1. Check for MCP explicit invocation (highest priority)
        mcp_match = self.MCP_PATTERN.match(user_input)
        if mcp_match:
            return self._route_mcp(user_input, mcp_match)

        # 2. Check for skill invocation
        skill_match = self.SKILL_PATTERN.match(user_input)
        if skill_match:
            return self._route_skill(user_input)

        # 3. Default: route to simple_query with LLM
        return ExecutionPlan(
            type="simple_query",
            requires_llm=True,
            requires_rag=False,
            requires_tools=False,
            metadata=None
        )

    def _route_mcp(self, user_input: str, match: re.Match) -> ExecutionPlan:
        """Route MCP tool invocation to execution plan.

        Args:
            user_input: User input starting with @mcp:
            match: Regex match object from MCP_PATTERN

        Returns:
            ExecutionPlan with type="mcp" and parsed metadata
        """
        server_name = match.group(1)
        tool_name = match.group(2)

        # Parse arguments
        arguments = self._parse_parameters(user_input)

        return ExecutionPlan(
            type="mcp",
            requires_llm=False,  # Direct tool execution, no LLM needed
            requires_rag=False,
            requires_tools=True,
            metadata={
                "server": server_name,
                "tool": tool_name,
                "arguments": arguments
            }
        )

    def _route_skill(self, user_input: str) -> ExecutionPlan:
        """Route skill invocation to execution plan.

        Args:
            user_input: User input starting with @ or /

        Returns:
            ExecutionPlan with type="skill" and parsed metadata
        """
        # Extract skill name
        skill_match = self.SKILL_PATTERN.match(user_input)
        skill_name = skill_match.group(1)

        # Parse parameters
        parameters = self._parse_parameters(user_input)

        return ExecutionPlan(
            type="skill",
            requires_llm=True,  # Skills need LLM for execution
            requires_rag=False,
            requires_tools=False,
            metadata={
                "skill_name": skill_name,
                "parameters": parameters
            }
        )

    def _parse_parameters(self, user_input: str) -> Dict[str, str]:
        """Parse parameters from skill invocation.

        Supports: key='value' or key="value" syntax

        Args:
            user_input: User input string

        Returns:
            Dictionary of parameter key-value pairs
        """
        parameters = {}

        # Find all parameter matches
        for match in self.PARAM_PATTERN.finditer(user_input):
            param_name = match.group(1)
            param_value = match.group(2)
            parameters[param_name] = param_value

        return parameters
