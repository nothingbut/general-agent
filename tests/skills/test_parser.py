"""Tests for skill parser"""
import pytest
from pathlib import Path
from src.skills.parser import SkillParser
from src.skills.models import SkillDefinition
import yaml


def test_parse_valid_skill(tmp_path: Path):
    """Test parsing a valid skill file"""
    skill_file = tmp_path / "reminder.md"
    skill_file.write_text("""---
name: reminder
description: Set a reminder for a future task
parameters:
  - name: task
    type: string
    required: true
    description: Task to remind
  - name: time
    type: string
    required: true
    description: When to remind
---

# Reminder Skill

You are a reminder assistant.

Task: {task}
Time: {time}
""")
    
    parser = SkillParser()
    skill = parser.parse_file(skill_file)
    
    assert skill.name == "reminder"
    assert skill.description == "Set a reminder for a future task"
    assert len(skill.parameters) == 2
    assert skill.parameters[0].name == "task"
    assert skill.parameters[0].type == "string"
    assert skill.parameters[0].required is True
    assert skill.parameters[1].name == "time"
    assert "# Reminder Skill" in skill.content


def test_parse_skill_without_parameters(tmp_path: Path):
    """Test parsing skill without parameters"""
    skill_file = tmp_path / "simple.md"
    skill_file.write_text("""---
name: simple
description: A simple skill
---

# Simple Skill Content
""")
    
    parser = SkillParser()
    skill = parser.parse_file(skill_file)
    
    assert skill.name == "simple"
    assert skill.description == "A simple skill"
    assert len(skill.parameters) == 0
    assert "# Simple Skill Content" in skill.content


def test_parse_skill_with_optional_parameter(tmp_path: Path):
    """Test parsing skill with optional parameter"""
    skill_file = tmp_path / "note.md"
    skill_file.write_text("""---
name: note
description: Take notes
parameters:
  - name: content
    type: string
    required: true
    description: Note content
  - name: category
    type: string
    required: false
    description: Note category
    default: general
---

# Note Taking
Content: {content}
Category: {category}
""")
    
    parser = SkillParser()
    skill = parser.parse_file(skill_file)
    
    assert len(skill.parameters) == 2
    assert skill.parameters[1].required is False
    assert skill.parameters[1].default == "general"


def test_parse_skill_missing_frontmatter(tmp_path: Path):
    """Test error when frontmatter is missing"""
    skill_file = tmp_path / "no_frontmatter.md"
    skill_file.write_text("# Just Content\nNo YAML frontmatter here")
    
    parser = SkillParser()
    with pytest.raises(ValueError, match="No YAML frontmatter found"):
        parser.parse_file(skill_file)


def test_parse_skill_invalid_yaml(tmp_path: Path):
    """Test error when YAML is invalid"""
    skill_file = tmp_path / "bad_yaml.md"
    skill_file.write_text("""---
name: test
invalid yaml: [unclosed bracket
---

# Content
""")
    
    parser = SkillParser()
    with pytest.raises(yaml.YAMLError):
        parser.parse_file(skill_file)


def test_parse_skill_missing_name(tmp_path: Path):
    """Test error when required field 'name' is missing"""
    skill_file = tmp_path / "no_name.md"
    skill_file.write_text("""---
description: Has description but no name
---

# Content
""")
    
    parser = SkillParser()
    with pytest.raises(ValueError, match="Missing required field"):
        parser.parse_file(skill_file)


def test_parse_skill_missing_description(tmp_path: Path):
    """Test error when required field 'description' is missing"""
    skill_file = tmp_path / "no_desc.md"
    skill_file.write_text("""---
name: test
---

# Content
""")
    
    parser = SkillParser()
    with pytest.raises(ValueError, match="Missing required field"):
        parser.parse_file(skill_file)


def test_parse_file_not_found():
    """Test error when file doesn't exist"""
    parser = SkillParser()
    with pytest.raises(FileNotFoundError):
        parser.parse_file(Path("/nonexistent/file.md"))


def test_parse_empty_file(tmp_path: Path):
    """Test error when file is empty"""
    skill_file = tmp_path / "empty.md"
    skill_file.write_text("")
    
    parser = SkillParser()
    with pytest.raises(ValueError, match="File is empty"):
        parser.parse_file(skill_file)


def test_parse_skill_invalid_parameter_structure(tmp_path: Path):
    """Test error when parameter structure is invalid"""
    skill_file = tmp_path / "bad_param.md"
    skill_file.write_text("""---
name: test
description: Test
parameters:
  - name: task
    # Missing required 'type' field
    required: true
    description: Task
---

# Content
""")
    
    parser = SkillParser()
    with pytest.raises(ValueError, match="Invalid parameter"):
        parser.parse_file(skill_file)


def test_parse_skill_with_metadata(tmp_path: Path):
    """Test that parser includes file path in metadata"""
    skill_file = tmp_path / "test.md"
    skill_file.write_text("""---
name: test
description: Test skill
---

# Content
""")
    
    parser = SkillParser()
    skill = parser.parse_file(skill_file)
    
    assert skill.metadata is not None
    assert "file_path" in skill.metadata
    assert skill.metadata["file_path"] == str(skill_file)
