"""Tests for skill models"""
from datetime import datetime
from src.skills.models import SkillParameter, SkillDefinition, SkillExecutionResult
import pytest


def test_skill_parameter_creation():
    """Test creating SkillParameter"""
    param = SkillParameter(
        name="task",
        type="string",
        required=True,
        description="Task to remind",
        default=None
    )
    assert param.name == "task"
    assert param.type == "string"
    assert param.required is True
    assert param.description == "Task to remind"
    assert param.default is None


def test_skill_parameter_with_default():
    """Test SkillParameter with default value"""
    param = SkillParameter(
        name="count",
        type="number",
        required=False,
        description="Number of items",
        default=10
    )
    assert param.default == 10


def test_skill_parameter_is_immutable():
    """Test that SkillParameter is immutable (frozen)"""
    param = SkillParameter(
        name="task",
        type="string",
        required=True,
        description="Task"
    )
    with pytest.raises(Exception):  # FrozenInstanceError
        param.name = "new_name"


def test_skill_definition_creation():
    """Test creating SkillDefinition"""
    skill = SkillDefinition(
        name="reminder",
        full_name="personal:reminder",
        description="Set a reminder",
        content="# Reminder\nTask: {task}",
        parameters=[],
        metadata={"file_path": "personal/reminder.md"}
    )
    assert skill.name == "reminder"
    assert skill.full_name == "personal:reminder"
    assert skill.description == "Set a reminder"
    assert skill.content == "# Reminder\nTask: {task}"
    assert len(skill.parameters) == 0
    assert skill.metadata["file_path"] == "personal/reminder.md"


def test_skill_definition_with_parameters():
    """Test SkillDefinition with parameters"""
    param = SkillParameter(
        name="task",
        type="string",
        required=True,
        description="Task to remind"
    )
    skill = SkillDefinition(
        name="reminder",
        full_name="personal:reminder",
        description="Set a reminder",
        content="Task: {task}",
        parameters=[param]
    )
    assert len(skill.parameters) == 1
    assert skill.parameters[0].name == "task"


def test_skill_definition_is_immutable():
    """Test that SkillDefinition is immutable (frozen)"""
    skill = SkillDefinition(
        name="reminder",
        full_name="personal:reminder",
        description="Set a reminder",
        content="Content"
    )
    with pytest.raises(Exception):  # FrozenInstanceError
        skill.name = "new_name"


def test_skill_definition_to_dict():
    """Test SkillDefinition.to_dict() serialization"""
    param = SkillParameter(
        name="task",
        type="string",
        required=True,
        description="Task",
        default="Buy milk"
    )
    skill = SkillDefinition(
        name="reminder",
        full_name="personal:reminder",
        description="Set a reminder",
        content="Task: {task}",
        parameters=[param],
        metadata={"file_path": "reminder.md"}
    )
    
    data = skill.to_dict()
    
    assert data["name"] == "reminder"
    assert data["full_name"] == "personal:reminder"
    assert data["description"] == "Set a reminder"
    assert data["content"] == "Task: {task}"
    assert len(data["parameters"]) == 1
    assert data["parameters"][0]["name"] == "task"
    assert data["parameters"][0]["type"] == "string"
    assert data["parameters"][0]["required"] is True
    assert data["parameters"][0]["default"] == "Buy milk"
    assert data["metadata"]["file_path"] == "reminder.md"


def test_skill_execution_result_creation():
    """Test creating SkillExecutionResult"""
    result = SkillExecutionResult(
        skill_name="reminder",
        success=True,
        output="Reminder set successfully"
    )
    assert result.skill_name == "reminder"
    assert result.success is True
    assert result.output == "Reminder set successfully"
    assert result.error is None
    assert result.metadata is None


def test_skill_execution_result_with_error():
    """Test SkillExecutionResult with error"""
    result = SkillExecutionResult(
        skill_name="reminder",
        success=False,
        output="",
        error="Missing required parameter: task"
    )
    assert result.success is False
    assert result.error == "Missing required parameter: task"


def test_skill_execution_result_is_immutable():
    """Test that SkillExecutionResult is immutable"""
    result = SkillExecutionResult(
        skill_name="reminder",
        success=True,
        output="Done"
    )
    with pytest.raises(Exception):  # FrozenInstanceError
        result.success = False
