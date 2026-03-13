"""Shared fixtures for skill tests"""
import pytest
from pathlib import Path
from src.skills.parser import SkillParser


@pytest.fixture
def test_skills_dir(tmp_path: Path) -> Path:
    """Create temporary skills directory with sample skills"""
    skills_dir = tmp_path / "skills"
    skills_dir.mkdir()

    # Create personal namespace
    personal_dir = skills_dir / "personal"
    personal_dir.mkdir()

    # Create sample reminder skill
    reminder_skill = personal_dir / "reminder.md"
    reminder_skill.write_text("""---
name: reminder
description: Set a reminder
parameters:
  - name: task
    type: string
    required: true
    description: Task to remind
---
# Reminder
Task: {task}
""")

    # Create sample note skill
    note_skill = personal_dir / "note.md"
    note_skill.write_text("""---
name: note
description: Take notes
---
# Note Taking
""")

    # Create productivity namespace
    prod_dir = skills_dir / "productivity"
    prod_dir.mkdir()

    # Create task skill
    task_skill = prod_dir / "task.md"
    task_skill.write_text("""---
name: task
description: Manage tasks
---
# Task Management
""")

    return skills_dir


@pytest.fixture
def skill_parser() -> SkillParser:
    """Create SkillParser instance"""
    return SkillParser()
