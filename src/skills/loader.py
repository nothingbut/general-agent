"""Load skills from filesystem with .ignore support"""
import fnmatch
import logging
from pathlib import Path
from typing import List, Set, Optional

from .models import SkillDefinition
from .parser import SkillParser

logger = logging.getLogger(__name__)


class SkillLoader:
    """Load skills from filesystem with .ignore support"""

    def __init__(self, skills_dir: Path, parser: Optional[SkillParser] = None):
        """Initialize loader

        Args:
            skills_dir: Root directory containing skills
            parser: Optional SkillParser (DI for testing)
        """
        self.skills_dir = Path(skills_dir)
        self.parser = parser or SkillParser()
        self.ignore_patterns: Set[str] = set()

    def load_all(self) -> List[SkillDefinition]:
        """Load all skills, respecting .ignore patterns

        Returns:
            List of SkillDefinition objects

        Raises:
            FileNotFoundError: If skills_dir doesn't exist
        """
        if not self.skills_dir.exists():
            raise FileNotFoundError(f"Skills directory not found: {self.skills_dir}")

        # Load .ignore patterns
        self._load_ignore_patterns()

        skills = []

        # Scan directory recursively for *.md files
        for skill_file in self.skills_dir.rglob("*.md"):
            # Skip if matches ignore pattern
            if self._should_ignore(skill_file):
                logger.debug(f"Ignoring skill file: {skill_file}")
                continue

            # Parse skill file
            try:
                skill = self.parser.parse_file(skill_file)

                # Extract namespace from directory structure
                namespace = self._extract_namespace(skill_file)

                # Set full_name with namespace
                if namespace:
                    full_name = f"{namespace}:{skill.name}"
                else:
                    full_name = skill.name

                # Create new skill with updated full_name and metadata
                skill_with_namespace = SkillDefinition(
                    name=skill.name,
                    full_name=full_name,
                    description=skill.description,
                    content=skill.content,
                    parameters=skill.parameters,
                    metadata={
                        **(skill.metadata or {}),
                        "namespace": namespace
                    }
                )

                skills.append(skill_with_namespace)
                logger.info(f"Loaded skill: {skill_with_namespace.full_name}")

            except Exception as e:
                logger.error(f"Failed to parse skill file {skill_file}: {e}")
                # Skip this file and continue
                continue

        logger.info(f"Loaded {len(skills)} skills from {self.skills_dir}")
        return skills

    def _load_ignore_patterns(self) -> None:
        """Load patterns from .ignore file"""
        ignore_file = self.skills_dir / ".ignore"

        if not ignore_file.exists():
            return

        try:
            content = ignore_file.read_text(encoding="utf-8")

            for line in content.splitlines():
                line = line.strip()

                # Skip empty lines and comments
                if not line or line.startswith("#"):
                    continue

                self.ignore_patterns.add(line)

            logger.debug(f"Loaded {len(self.ignore_patterns)} ignore patterns")

        except Exception as e:
            logger.warning(f"Failed to load .ignore file: {e}")

    def _should_ignore(self, file_path: Path) -> bool:
        """Check if file matches any ignore pattern

        Args:
            file_path: Path to check

        Returns:
            True if should be ignored

        Uses fnmatch for glob pattern matching
        """
        if not self.ignore_patterns:
            return False

        # Get relative path from skills_dir
        try:
            relative_path = file_path.relative_to(self.skills_dir)
        except ValueError:
            # File is not under skills_dir
            return False

        relative_str = str(relative_path)

        # Check against all patterns
        for pattern in self.ignore_patterns:
            if fnmatch.fnmatch(relative_str, pattern):
                return True

        return False

    def _extract_namespace(self, file_path: Path) -> Optional[str]:
        """Extract namespace from directory structure

        Args:
            file_path: Path to skill file

        Returns:
            Namespace string or None if at root level

        Examples:
            skills/personal/reminder.md → "personal"
            skills/productivity/tasks/gtd.md → "productivity/tasks"
            skills/reminder.md → None
        """
        try:
            relative_path = file_path.relative_to(self.skills_dir)
        except ValueError:
            return None

        # Get parent directories (excluding file itself)
        parts = relative_path.parts[:-1]

        if not parts:
            return None

        # Join parts with /
        return "/".join(parts)
