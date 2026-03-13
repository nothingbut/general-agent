"""Tests for skill registry"""
import pytest
from src.skills.models import SkillDefinition
from src.skills.registry import SkillRegistry, AmbiguousSkillError, SkillNotFoundError


@pytest.fixture
def sample_skills():
    """Create sample skills for testing"""
    return [
        SkillDefinition(
            name="reminder",
            full_name="personal:reminder",
            description="Create reminders",
            content="# Reminder skill",
            metadata={"namespace": "personal"}
        ),
        SkillDefinition(
            name="note",
            full_name="personal:note",
            description="Take notes",
            content="# Note skill",
            metadata={"namespace": "personal"}
        ),
        SkillDefinition(
            name="task",
            full_name="productivity:task",
            description="Manage tasks",
            content="# Task skill",
            metadata={"namespace": "productivity"}
        ),
        # Duplicate name in different namespace
        SkillDefinition(
            name="reminder",
            full_name="work:reminder",
            description="Work reminders",
            content="# Work reminder skill",
            metadata={"namespace": "work"}
        ),
    ]


def test_register_skill(sample_skills):
    """Test registering a single skill"""
    registry = SkillRegistry()
    skill = sample_skills[0]

    registry.register(skill)

    # Should be retrievable by full name
    retrieved = registry.get("personal:reminder")
    assert retrieved == skill


def test_register_multiple_skills(sample_skills):
    """Test registering multiple skills"""
    registry = SkillRegistry()

    for skill in sample_skills:
        registry.register(skill)

    # Should have 4 skills total
    all_skills = registry.list_all()
    assert len(all_skills) == 4


def test_get_by_short_name_unique(sample_skills):
    """Test getting skill by short name when unique"""
    registry = SkillRegistry()
    registry.register(sample_skills[1])  # note (unique)

    # Should work with short name
    skill = registry.get("note")
    assert skill.name == "note"
    assert skill.full_name == "personal:note"


def test_get_by_short_name_ambiguous(sample_skills):
    """Test getting skill by short name when ambiguous"""
    registry = SkillRegistry()
    registry.register(sample_skills[0])  # personal:reminder
    registry.register(sample_skills[3])  # work:reminder

    # Should raise AmbiguousSkillError
    with pytest.raises(AmbiguousSkillError) as exc_info:
        registry.get("reminder")

    # Error should mention both options
    error_msg = str(exc_info.value)
    assert "personal:reminder" in error_msg
    assert "work:reminder" in error_msg


def test_get_by_full_name_with_ambiguity(sample_skills):
    """Test getting skill by full name when short name is ambiguous"""
    registry = SkillRegistry()
    registry.register(sample_skills[0])  # personal:reminder
    registry.register(sample_skills[3])  # work:reminder

    # Should work with full name despite ambiguity
    skill = registry.get("personal:reminder")
    assert skill.full_name == "personal:reminder"

    skill = registry.get("work:reminder")
    assert skill.full_name == "work:reminder"


def test_get_nonexistent_skill():
    """Test getting a skill that doesn't exist"""
    registry = SkillRegistry()

    with pytest.raises(SkillNotFoundError) as exc_info:
        registry.get("nonexistent")

    assert "nonexistent" in str(exc_info.value)


def test_list_all(sample_skills):
    """Test listing all skills"""
    registry = SkillRegistry()

    for skill in sample_skills:
        registry.register(skill)

    all_skills = registry.list_all()
    assert len(all_skills) == 4

    # Should be sorted by full_name
    full_names = [s.full_name for s in all_skills]
    assert full_names == sorted(full_names)


def test_list_by_namespace(sample_skills):
    """Test listing skills by namespace"""
    registry = SkillRegistry()

    for skill in sample_skills:
        registry.register(skill)

    # List personal skills
    personal_skills = registry.list_by_namespace("personal")
    assert len(personal_skills) == 2
    assert all(s.metadata["namespace"] == "personal" for s in personal_skills)

    # List productivity skills
    productivity_skills = registry.list_by_namespace("productivity")
    assert len(productivity_skills) == 1
    assert productivity_skills[0].name == "task"

    # List work skills
    work_skills = registry.list_by_namespace("work")
    assert len(work_skills) == 1
    assert work_skills[0].full_name == "work:reminder"


def test_list_by_nonexistent_namespace(sample_skills):
    """Test listing skills from namespace that doesn't exist"""
    registry = SkillRegistry()

    for skill in sample_skills:
        registry.register(skill)

    # Should return empty list
    skills = registry.list_by_namespace("nonexistent")
    assert skills == []


def test_has_skill(sample_skills):
    """Test checking if skill exists"""
    registry = SkillRegistry()
    registry.register(sample_skills[0])

    # Should find by full name
    assert registry.has("personal:reminder") is True

    # Should find by short name if unique
    registry.register(sample_skills[1])
    assert registry.has("note") is True

    # Should not find nonexistent
    assert registry.has("nonexistent") is False


def test_register_duplicate_full_name(sample_skills):
    """Test registering skill with duplicate full_name"""
    registry = SkillRegistry()
    skill = sample_skills[0]

    registry.register(skill)

    # Registering again should replace
    skill2 = SkillDefinition(
        name="reminder",
        full_name="personal:reminder",
        description="Updated reminder",
        content="# Updated",
        metadata={"namespace": "personal"}
    )

    registry.register(skill2)

    # Should have the new version
    retrieved = registry.get("personal:reminder")
    assert retrieved.description == "Updated reminder"

    # Should still have only one skill
    assert len(registry.list_all()) == 1


def test_empty_registry():
    """Test operations on empty registry"""
    registry = SkillRegistry()

    # list_all should return empty list
    assert registry.list_all() == []

    # list_by_namespace should return empty list
    assert registry.list_by_namespace("any") == []

    # has should return False
    assert registry.has("any") is False

    # get should raise SkillNotFoundError
    with pytest.raises(SkillNotFoundError):
        registry.get("any")
