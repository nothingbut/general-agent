"""测试工具编排器"""
import pytest
from unittest.mock import AsyncMock, MagicMock
from src.workflow.orchestrator import (
    ToolType,
    ToolRegistry,
    ToolOrchestrator
)
from src.workflow.models import ExecutionContext, ToolResult
from src.skills.models import SkillDefinition, SkillParameter, SkillExecutionResult


class TestToolType:
    """测试 ToolType 枚举"""

    def test_tool_type_values(self):
        """测试工具类型枚举值"""
        assert ToolType.MCP.value == "mcp"
        assert ToolType.SKILL.value == "skill"
        assert ToolType.RAG.value == "rag"
        assert ToolType.LLM.value == "llm"

    def test_from_tool_name_valid(self):
        """测试从有效工具名称解析类型"""
        assert ToolType.from_tool_name("mcp:filesystem:read") == ToolType.MCP
        assert ToolType.from_tool_name("skill:summarize") == ToolType.SKILL
        assert ToolType.from_tool_name("rag:search") == ToolType.RAG
        assert ToolType.from_tool_name("llm:chat") == ToolType.LLM

    def test_from_tool_name_invalid_format(self):
        """测试无效格式"""
        with pytest.raises(ValueError, match="Invalid tool name format"):
            ToolType.from_tool_name("invalid")

    def test_from_tool_name_unknown_type(self):
        """测试未知工具类型"""
        with pytest.raises(ValueError, match="Unknown tool type"):
            ToolType.from_tool_name("unknown:tool")


class TestToolRegistry:
    """测试工具注册表"""

    def test_register_and_get_tool(self):
        """测试注册和获取工具"""
        registry = ToolRegistry()

        registry.register_tool(
            tool_name="mcp:filesystem:read",
            tool_type=ToolType.MCP,
            description="Read file"
        )

        tool = registry.get_tool("mcp:filesystem:read")
        assert tool is not None
        assert tool["name"] == "mcp:filesystem:read"
        assert tool["type"] == "mcp"
        assert tool["description"] == "Read file"

    def test_get_nonexistent_tool(self):
        """测试获取不存在的工具"""
        registry = ToolRegistry()
        tool = registry.get_tool("nonexistent")
        assert tool is None

    def test_has_tool(self):
        """测试检查工具是否存在"""
        registry = ToolRegistry()

        registry.register_tool(
            tool_name="skill:test",
            tool_type=ToolType.SKILL,
            description="Test skill"
        )

        assert registry.has_tool("skill:test") is True
        assert registry.has_tool("skill:nonexistent") is False

    def test_list_all_tools(self):
        """测试列出所有工具"""
        registry = ToolRegistry()

        registry.register_tool("mcp:test", ToolType.MCP, "MCP tool")
        registry.register_tool("skill:test", ToolType.SKILL, "Skill tool")

        tools = registry.list_tools()
        assert len(tools) == 2

    def test_list_tools_by_type(self):
        """测试按类型列出工具"""
        registry = ToolRegistry()

        registry.register_tool("mcp:test1", ToolType.MCP, "MCP 1")
        registry.register_tool("mcp:test2", ToolType.MCP, "MCP 2")
        registry.register_tool("skill:test", ToolType.SKILL, "Skill")

        mcp_tools = registry.list_tools(ToolType.MCP)
        assert len(mcp_tools) == 2
        assert all(t["type"] == "mcp" for t in mcp_tools)

        skill_tools = registry.list_tools(ToolType.SKILL)
        assert len(skill_tools) == 1
        assert skill_tools[0]["type"] == "skill"

    def test_register_tool_with_metadata(self):
        """测试注册带元数据的工具"""
        registry = ToolRegistry()

        registry.register_tool(
            tool_name="mcp:test",
            tool_type=ToolType.MCP,
            description="Test",
            params_schema={"type": "object"},
            metadata={"server": "test_server"}
        )

        tool = registry.get_tool("mcp:test")
        assert tool["params_schema"] == {"type": "object"}
        assert tool["metadata"]["server"] == "test_server"


class TestToolOrchestrator:
    """测试工具编排器"""

    def test_initialization(self):
        """测试初始化"""
        orchestrator = ToolOrchestrator()
        assert orchestrator.registry is not None

    def test_initialization_with_executors(self):
        """测试带执行器的初始化"""
        mcp_mock = MagicMock()
        skill_mock = MagicMock()
        rag_mock = MagicMock()
        llm_mock = MagicMock()

        orchestrator = ToolOrchestrator(
            mcp_executor=mcp_mock,
            skill_executor=skill_mock,
            rag_engine=rag_mock,
            llm_client=llm_mock
        )

        assert orchestrator.mcp_executor is mcp_mock
        assert orchestrator.skill_executor is skill_mock
        assert orchestrator.rag_engine is rag_mock
        assert orchestrator.llm_client is llm_mock

    @pytest.mark.asyncio
    async def test_discover_tools_with_skills(self):
        """测试发现 Skills"""
        # Mock skill registry
        skill_registry_mock = MagicMock()
        skill = SkillDefinition(
            name="test_skill",
            full_name="test:test_skill",
            description="Test skill",
            content="Test content",
            parameters=[],
            metadata={"namespace": "test"}
        )
        skill_registry_mock.list_all.return_value = [skill]

        orchestrator = ToolOrchestrator(skill_registry=skill_registry_mock)
        discovered = await orchestrator.discover_tools()

        assert len(discovered["skill"]) == 1
        assert discovered["skill"][0]["name"] == "skill:test:test_skill"

    @pytest.mark.asyncio
    async def test_discover_tools_with_rag(self):
        """测试发现 RAG 方法"""
        rag_mock = MagicMock()
        orchestrator = ToolOrchestrator(rag_engine=rag_mock)
        discovered = await orchestrator.discover_tools()

        assert len(discovered["rag"]) == 3
        assert any(t["name"] == "rag:search" for t in discovered["rag"])
        assert any(t["name"] == "rag:query" for t in discovered["rag"])

    @pytest.mark.asyncio
    async def test_discover_tools_with_llm(self):
        """测试发现 LLM 方法"""
        llm_mock = MagicMock()
        orchestrator = ToolOrchestrator(llm_client=llm_mock)
        discovered = await orchestrator.discover_tools()

        assert len(discovered["llm"]) == 2
        assert any(t["name"] == "llm:chat" for t in discovered["llm"])
        assert any(t["name"] == "llm:analyze" for t in discovered["llm"])

    @pytest.mark.asyncio
    async def test_execute_mcp_tool(self):
        """测试执行 MCP 工具"""
        # Mock MCP executor
        mcp_mock = AsyncMock()
        mcp_mock.call_tool.return_value = {"content": [{"type": "text", "text": "result"}]}

        orchestrator = ToolOrchestrator(mcp_executor=mcp_mock)
        context = ExecutionContext(workflow_id="wf-1", session_id="session-1")

        result = await orchestrator.execute_tool(
            tool_name="mcp:filesystem:read_file",
            params={"path": "/test.txt"},
            context=context
        )

        assert result.success is True
        assert result.data is not None
        mcp_mock.call_tool.assert_called_once_with(
            server_name="filesystem",
            tool_name="read_file",
            arguments={"path": "/test.txt"},
            session_id="session-1"
        )

    @pytest.mark.asyncio
    async def test_execute_mcp_tool_without_executor(self):
        """测试执行 MCP 工具但未初始化执行器"""
        orchestrator = ToolOrchestrator()
        context = ExecutionContext(workflow_id="wf-1", session_id="session-1")

        result = await orchestrator.execute_tool(
            tool_name="mcp:filesystem:read_file",
            params={"path": "/test.txt"},
            context=context
        )

        assert result.success is False
        assert "not initialized" in result.error.lower()

    @pytest.mark.asyncio
    async def test_execute_skill(self):
        """测试执行 Skill"""
        # Mock skill registry and executor
        skill = SkillDefinition(
            name="test_skill",
            full_name="test:test_skill",
            description="Test",
            content="Test",
            parameters=[],
            metadata={"namespace": "test"}
        )
        skill_registry_mock = MagicMock()
        skill_registry_mock.get.return_value = skill

        skill_executor_mock = AsyncMock()
        skill_executor_mock.execute.return_value = SkillExecutionResult(
            skill_name="test:test_skill",
            success=True,
            output="Skill output",
            error=None,
            metadata={}
        )

        orchestrator = ToolOrchestrator(
            skill_executor=skill_executor_mock,
            skill_registry=skill_registry_mock
        )
        context = ExecutionContext(workflow_id="wf-1", session_id="session-1")

        result = await orchestrator.execute_tool(
            tool_name="skill:test:test_skill",
            params={"input": "test"},
            context=context
        )

        assert result.success is True
        assert result.data == "Skill output"

    @pytest.mark.asyncio
    async def test_execute_rag_search(self):
        """测试执行 RAG 搜索"""
        # Mock RAG engine
        rag_mock = AsyncMock()
        rag_mock.retrieve.return_value = [
            MagicMock(doc_id="doc1", text="text1", score=0.9, metadata={})
        ]

        orchestrator = ToolOrchestrator(rag_engine=rag_mock)
        context = ExecutionContext(workflow_id="wf-1", session_id="session-1")

        result = await orchestrator.execute_tool(
            tool_name="rag:search",
            params={"query": "test query", "top_k": 5},
            context=context
        )

        assert result.success is True
        rag_mock.retrieve.assert_called_once()

    @pytest.mark.asyncio
    async def test_execute_llm_chat(self):
        """测试执行 LLM 聊天"""
        # Mock LLM client
        llm_mock = AsyncMock()
        llm_mock.chat.return_value = "LLM response"

        orchestrator = ToolOrchestrator(llm_client=llm_mock)
        context = ExecutionContext(workflow_id="wf-1", session_id="session-1")

        result = await orchestrator.execute_tool(
            tool_name="llm:chat",
            params={"messages": [{"role": "user", "content": "Hello"}]},
            context=context
        )

        assert result.success is True
        assert result.data == "LLM response"
        llm_mock.chat.assert_called_once()

    @pytest.mark.asyncio
    async def test_execute_llm_analyze_with_context(self):
        """测试带上下文的 LLM 分析"""
        # Mock LLM client
        llm_mock = AsyncMock()
        llm_mock.chat.return_value = "Analysis result"

        orchestrator = ToolOrchestrator(llm_client=llm_mock)
        context = ExecutionContext(workflow_id="wf-1", session_id="session-1")
        context.metadata["rag_context"] = "Context from RAG"

        result = await orchestrator.execute_tool(
            tool_name="llm:analyze",
            params={"query": "Analyze this", "use_context": True},
            context=context
        )

        assert result.success is True
        assert result.data == "Analysis result"

        # 验证消息包含上下文
        call_args = llm_mock.chat.call_args[0][0]
        assert len(call_args) == 2
        assert "system" in call_args[0]["role"]
        assert "Context from RAG" in call_args[0]["content"]

    @pytest.mark.asyncio
    async def test_execute_tool_invalid_format(self):
        """测试无效的工具名称格式"""
        orchestrator = ToolOrchestrator()
        context = ExecutionContext(workflow_id="wf-1", session_id="session-1")

        result = await orchestrator.execute_tool(
            tool_name="invalid",
            params={},
            context=context
        )

        assert result.success is False
        assert "format" in result.error.lower()

    @pytest.mark.asyncio
    async def test_execute_tool_unknown_type(self):
        """测试未知工具类型"""
        orchestrator = ToolOrchestrator()
        context = ExecutionContext(workflow_id="wf-1", session_id="session-1")

        result = await orchestrator.execute_tool(
            tool_name="unknown:tool",
            params={},
            context=context
        )

        assert result.success is False
        assert "unknown" in result.error.lower()

    def test_get_tool_info(self):
        """测试获取工具信息"""
        orchestrator = ToolOrchestrator()
        orchestrator.registry.register_tool(
            tool_name="test:tool",
            tool_type=ToolType.SKILL,
            description="Test tool"
        )

        info = orchestrator.get_tool_info("test:tool")
        assert info is not None
        assert info["description"] == "Test tool"

    def test_list_all_tools(self):
        """测试列出所有工具"""
        orchestrator = ToolOrchestrator()
        orchestrator.registry.register_tool("mcp:test", ToolType.MCP, "MCP")
        orchestrator.registry.register_tool("skill:test", ToolType.SKILL, "Skill")

        all_tools = orchestrator.list_all_tools()
        assert "mcp" in all_tools
        assert "skill" in all_tools
        assert len(all_tools["mcp"]) == 1
        assert len(all_tools["skill"]) == 1

    @pytest.mark.asyncio
    async def test_execute_rag_index(self):
        """测试执行 RAG 索引"""
        rag_mock = AsyncMock()
        rag_mock.index_documents.return_value = {
            "total_files": 10,
            "indexed_files": 10
        }

        orchestrator = ToolOrchestrator(rag_engine=rag_mock)
        context = ExecutionContext(workflow_id="wf-1", session_id="session-1")

        result = await orchestrator.execute_tool(
            tool_name="rag:index",
            params={"path": "/docs", "recursive": True},
            context=context
        )

        assert result.success is True
        assert result.data["total_files"] == 10
        rag_mock.index_documents.assert_called_once()

    @pytest.mark.asyncio
    async def test_execute_tool_with_execution_time(self):
        """测试工具执行时间记录"""
        llm_mock = AsyncMock()
        llm_mock.chat.return_value = "Response"

        orchestrator = ToolOrchestrator(llm_client=llm_mock)
        context = ExecutionContext(workflow_id="wf-1", session_id="session-1")

        result = await orchestrator.execute_tool(
            tool_name="llm:chat",
            params={"messages": [{"role": "user", "content": "Hi"}]},
            context=context
        )

        assert result.success is True
        assert result.execution_time is not None
        assert result.execution_time > 0

    @pytest.mark.asyncio
    async def test_execute_mcp_tool_invalid_format(self):
        """测试无效的 MCP 工具名称格式"""
        mcp_mock = AsyncMock()
        orchestrator = ToolOrchestrator(mcp_executor=mcp_mock)
        context = ExecutionContext(workflow_id="wf-1", session_id="session-1")

        # MCP 工具名称必须是 mcp:server:tool 格式
        result = await orchestrator.execute_tool(
            tool_name="mcp:onlyserver",
            params={},
            context=context
        )

        assert result.success is False
        assert "format" in result.error.lower()
