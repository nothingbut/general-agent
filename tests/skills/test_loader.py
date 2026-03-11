"""Tests for skill loader"""
import pytest
from pathlib import Path
from src.skills.loader import SkillLoader
from src.skills.parser import SkillParser


def test_load_single_skill(test_skills_dir: Path):
    """Test loading a single skill file"""
    loader = SkillLoader(test_skills_dir)
    skills = loader.load_all()

    # Should load 3 skills (reminder, note, task)
    assert len(skills) >= 3
    skill_names = [s.name for s in skills]
    assert "reminder" in skill_names
    assert "note" in skill_names
    assert "task" in skill_names


def test_namespace_extraction(test_skills_dir: Path):
    """Test namespace extraction from directory structure"""
    loader = SkillLoader(test_skills_dir)
    skills = loader.load_all()

    # Check namespaces
    reminder = next(s for s in skills if s.name == "reminder")
    assert reminder.full_name == "personal:reminder"

    task = next(s for s in skills if s.name == "task")
    assert task.full_name == "productivity:task"


def test_skill_without_namespace(tmp_path: Path):
    """Test skill at root level (no namespace)"""
    skills_dir = tmp_path / "skills"
    skills_dir.mkdir()

    # Create skill at root
    root_skill = skills_dir / "root.md"
    root_skill.write_text("""---
name: root
description: Root skill
---
# Root
""")

    loader = SkillLoader(skills_dir)
    skills = loader.load_all()

    assert len(skills) == 1
    assert skills[0].full_name == "root"  # No namespace


def test_load_with_ignore_file(test_skills_dir: Path):
    """Test that .ignore file excludes matching files"""
    # Create experimental directory
    exp_dir = test_skills_dir / "experimental"
    exp_dir.mkdir()
    exp_skill = exp_dir / "exp.md"
    exp_skill.write_text("""---
name: exp
description: Experimental
---
# Experimental
""")

    # Create .ignore file
    ignore_file = test_skills_dir / ".ignore"
    ignore_file.write_text("experimental/*\n")

    loader = SkillLoader(test_skills_dir)
    skills = loader.load_all()

    # Experimental skill should be ignored
    skill_names = [s.name for s in skills]
    assert "exp" not in skill_names


def test_ignore_draft_files(test_skills_dir: Path):
    """Test ignoring draft files with pattern"""
    # Create draft file
    personal_dir = test_skills_dir / "personal"
    draft_skill = personal_dir / "draft-feature.md"
    draft_skill.write_text("""---
name: draft
description: Draft skill
---
# Draft
""")

    # Create .ignore file
    ignore_file = test_skills_dir / ".ignore"
    ignore_file.write_text("**/draft-*.md\n")

    loader = SkillLoader(test_skills_dir)
    skills = loader.load_all()

    # Draft skill should be ignored
    skill_names = [s.name for s in skills]
    assert "draft" not in skill_names


def test_load_all_with_nonexistent_directory():
    """Test error when skills directory doesn't exist"""
    loader = SkillLoader(Path("/nonexistent/skills"))
    with pytest.raises(FileNotFoundError):
        loader.load_all()


def test_load_with_parse_error(test_skills_dir: Path):
    """Test that parse errors are logged and skipped"""
    # Create invalid skill file
    personal_dir = test_skills_dir / "personal"
    bad_skill = personal_dir / "bad.md"
    bad_skill.write_text("No YAML frontmatter here")

    loader = SkillLoader(test_skills_dir)
    # Should not raise error, just skip bad file
    skills = loader.load_all()

    # Should still load valid skills
    assert len(skills) >= 2
    skill_names = [s.name for s in skills]
    assert "bad" not in skill_names


def test_nested_namespace_extraction(tmp_path: Path):
    """Test extracting nested namespace from deep directory"""
    skills_dir = tmp_path / "skills"
    deep_dir = skills_dir / "productivity" / "tasks" / "advanced"
    deep_dir.mkdir(parents=True)

    deep_skill = deep_dir / "gtd.md"
    deep_skill.write_text("""---
name: gtd
description: GTD methodology
---
# GTD
""")

    loader = SkillLoader(skills_dir)
    skills = loader.load_all()

    assert len(skills) == 1
    assert skills[0].full_name == "productivity/tasks/advanced:gtd"


def test_ignore_file_missing(test_skills_dir: Path):
    """Test that missing .ignore file is handled gracefully"""
    # No .ignore file created
    loader = SkillLoader(test_skills_dir)
    skills = loader.load_all()

    # Should load all skills without error
    assert len(skills) >= 3


def test_empty_ignore_file(test_skills_dir: Path):
    """Test empty .ignore file"""
    ignore_file = test_skills_dir / ".ignore"
    ignore_file.write_text("")

    loader = SkillLoader(test_skills_dir)
    skills = loader.load_all()

    # Should load all skills
    assert len(skills) >= 3


def test_ignore_comments_in_ignore_file(test_skills_dir: Path):
    """Test that comments in .ignore are handled"""
    ignore_file = test_skills_dir / ".ignore"
    ignore_file.write_text("""# This is a comment
# Ignore experimental
experimental/*
""")

    exp_dir = test_skills_dir / "experimental"
    exp_dir.mkdir()
    exp_skill = exp_dir / "exp.md"
    exp_skill.write_text("""---
name: exp
description: Experimental
---
# Exp
""")

    loader = SkillLoader(test_skills_dir)
    skills = loader.load_all()

    skill_names = [s.name for s in skills]
    assert "exp" not in skill_names


def test_skill_metadata_includes_namespace(test_skills_dir: Path):
    """Test that skill metadata includes namespace info"""
    loader = SkillLoader(test_skills_dir)
    skills = loader.load_all()

    reminder = next(s for s in skills if s.name == "reminder")
    assert reminder.metadata is not None
    assert "namespace" in reminder.metadata
    assert reminder.metadata["namespace"] == "personal"
