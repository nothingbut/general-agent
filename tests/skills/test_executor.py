"""Tests for skill executor"""
import pytest
from src.skills.models import SkillDefinition, SkillParameter
from src.skills.executor import SkillExecutor, SkillExecutionError
from src.core.llm_client import MockLLMClient


@pytest.fixture
def mock_llm_client():
    """Create mock LLM client for testing"""
    return MockLLMClient()


@pytest.fixture
def simple_skill():
    """Create a simple skill without parameters"""
    return SkillDefinition(
        name="greeting",
        full_name="test:greeting",
        description="Say hello",
        content="Please greet the user warmly.",
        parameters=[],
        metadata={}
    )


@pytest.fixture
def parameterized_skill():
    """Create a skill with parameters"""
    return SkillDefinition(
        name="reminder",
        full_name="personal:reminder",
        description="Create a reminder",
        content="Create a reminder for {task} at {time}.",
        parameters=[
            SkillParameter(
                name="task",
                type="string",
                required=True,
                description="The task to remind"
            ),
            SkillParameter(
                name="time",
                type="string",
                required=True,
                description="When to remind"
            )
        ],
        metadata={}
    )


@pytest.fixture
def skill_with_default():
    """Create a skill with default parameter"""
    return SkillDefinition(
        name="note",
        full_name="personal:note",
        description="Take a note",
        content="Take a note titled '{title}' with priority {priority}.",
        parameters=[
            SkillParameter(
                name="title",
                type="string",
                required=True,
                description="Note title"
            ),
            SkillParameter(
                name="priority",
                type="string",
                required=False,
                description="Priority level",
                default="normal"
            )
        ],
        metadata={}
    )


@pytest.mark.asyncio
async def test_execute_simple_skill(simple_skill, mock_llm_client):
    """Test executing a skill without parameters"""
    executor = SkillExecutor(mock_llm_client)

    result = await executor.execute(simple_skill, {})

    # Should succeed
    assert result.success is True
    assert result.skill_name == "test:greeting"
    assert result.error is None
    assert len(result.output) > 0


@pytest.mark.asyncio
async def test_execute_with_parameters(parameterized_skill, mock_llm_client):
    """Test executing a skill with parameters"""
    executor = SkillExecutor(mock_llm_client)

    params = {
        "task": "Buy groceries",
        "time": "5pm"
    }

    result = await executor.execute(parameterized_skill, params)

    # Should succeed
    assert result.success is True
    assert result.skill_name == "personal:reminder"
    assert result.error is None


@pytest.mark.asyncio
async def test_parameter_substitution(parameterized_skill, mock_llm_client):
    """Test that parameters are substituted correctly in prompt"""
    executor = SkillExecutor(mock_llm_client)

    params = {
        "task": "Call mom",
        "time": "3pm"
    }

    result = await executor.execute(parameterized_skill, params)

    # Check that the LLM received the substituted prompt
    # (mock LLM echoes back what it receives)
    assert "Call mom" in result.output
    assert "3pm" in result.output


@pytest.mark.asyncio
async def test_missing_required_parameter(parameterized_skill, mock_llm_client):
    """Test error when required parameter is missing"""
    executor = SkillExecutor(mock_llm_client)

    # Missing 'time' parameter
    params = {
        "task": "Buy groceries"
    }

    with pytest.raises(SkillExecutionError) as exc_info:
        await executor.execute(parameterized_skill, params)

    error_msg = str(exc_info.value)
    assert "required" in error_msg.lower()
    assert "time" in error_msg


@pytest.mark.asyncio
async def test_default_parameter_used(skill_with_default, mock_llm_client):
    """Test that default parameter value is used when not provided"""
    executor = SkillExecutor(mock_llm_client)

    # Only provide required parameter
    params = {
        "title": "My Note"
    }

    result = await executor.execute(skill_with_default, params)

    # Should succeed with default value
    assert result.success is True
    # Default "normal" should be in the output
    assert "normal" in result.output


@pytest.mark.asyncio
async def test_default_parameter_override(skill_with_default, mock_llm_client):
    """Test that provided value overrides default"""
    executor = SkillExecutor(mock_llm_client)

    params = {
        "title": "Urgent Note",
        "priority": "high"
    }

    result = await executor.execute(skill_with_default, params)

    # Should use provided value
    assert result.success is True
    assert "high" in result.output
    assert "normal" not in result.output


@pytest.mark.asyncio
async def test_extra_parameters_ignored(simple_skill, mock_llm_client):
    """Test that extra parameters are ignored"""
    executor = SkillExecutor(mock_llm_client)

    # Provide parameters even though skill doesn't expect any
    params = {
        "extra": "value",
        "another": "param"
    }

    result = await executor.execute(simple_skill, params)

    # Should still succeed (extra params ignored)
    assert result.success is True


@pytest.mark.asyncio
async def test_llm_error_handling(simple_skill):
    """Test handling of LLM errors"""

    class FailingLLMClient:
        """Mock client that always fails"""
        async def chat(self, messages):
            raise RuntimeError("LLM API error")

    executor = SkillExecutor(FailingLLMClient())

    result = await executor.execute(simple_skill, {})

    # Should return failure result, not raise exception
    assert result.success is False
    assert result.error is not None
    assert "LLM API error" in result.error


@pytest.mark.asyncio
async def test_empty_parameter_value(parameterized_skill, mock_llm_client):
    """Test validation of empty parameter values"""
    executor = SkillExecutor(mock_llm_client)

    params = {
        "task": "",  # Empty string
        "time": "5pm"
    }

    with pytest.raises(SkillExecutionError) as exc_info:
        await executor.execute(parameterized_skill, params)

    error_msg = str(exc_info.value)
    assert "empty" in error_msg.lower() or "blank" in error_msg.lower()


@pytest.mark.asyncio
async def test_parameter_type_validation(parameterized_skill, mock_llm_client):
    """Test basic parameter type validation"""
    executor = SkillExecutor(mock_llm_client)

    # All parameters should be converted to strings for now
    params = {
        "task": "Buy milk",
        "time": 123  # Number - should be converted to string
    }

    result = await executor.execute(parameterized_skill, params)

    # Should succeed - numbers converted to strings
    assert result.success is True


@pytest.mark.asyncio
async def test_execution_metadata(simple_skill, mock_llm_client):
    """Test that execution result includes metadata"""
    executor = SkillExecutor(mock_llm_client)

    result = await executor.execute(simple_skill, {})

    # Should include metadata
    assert result.metadata is not None
    assert "skill_name" in result.metadata or result.skill_name is not None
