"""In-memory skill registry with namespace resolution"""
import logging
from typing import Dict, List

from .models import SkillDefinition

logger = logging.getLogger(__name__)


class SkillNotFoundError(Exception):
    """Raised when skill is not found in registry"""
    pass


class AmbiguousSkillError(Exception):
    """Raised when short name matches multiple skills"""
    pass


class SkillRegistry:
    """In-memory registry for skills with namespace resolution

    Supports lookups by:
    - Full name (namespace:skill) - always unambiguous
    - Short name (skill) - only if unique across all namespaces
    """

    def __init__(self):
        """Initialize empty registry"""
        # Primary storage: full_name -> SkillDefinition
        self._skills: Dict[str, SkillDefinition] = {}

        # Index: short name -> list of full names
        # Used to detect ambiguity
        self._short_name_index: Dict[str, List[str]] = {}

    def register(self, skill: SkillDefinition) -> None:
        """Register a skill in the registry

        Args:
            skill: SkillDefinition to register

        If a skill with the same full_name already exists, it will be replaced.
        """
        full_name = skill.full_name
        short_name = skill.name

        # Remove old entry from short name index if replacing
        if full_name in self._skills:
            old_short_name = self._skills[full_name].name
            if old_short_name in self._short_name_index:
                self._short_name_index[old_short_name] = [
                    fn for fn in self._short_name_index[old_short_name]
                    if fn != full_name
                ]
                # Clean up empty lists
                if not self._short_name_index[old_short_name]:
                    del self._short_name_index[old_short_name]

        # Store skill
        self._skills[full_name] = skill

        # Update short name index
        if short_name not in self._short_name_index:
            self._short_name_index[short_name] = []

        if full_name not in self._short_name_index[short_name]:
            self._short_name_index[short_name].append(full_name)

        logger.debug(f"Registered skill: {full_name}")

    def get(self, name: str) -> SkillDefinition:
        """Get a skill by name (short or full)

        Args:
            name: Short name (e.g., "reminder") or full name (e.g., "personal:reminder")

        Returns:
            SkillDefinition

        Raises:
            SkillNotFoundError: If skill not found
            AmbiguousSkillError: If short name matches multiple skills
        """
        # Try full name first (exact match)
        if name in self._skills:
            return self._skills[name]

        # Try short name lookup
        if name in self._short_name_index:
            full_names = self._short_name_index[name]

            if len(full_names) == 1:
                # Unique - return it
                return self._skills[full_names[0]]
            else:
                # Ambiguous - raise error with options
                options = ", ".join(sorted(full_names))
                raise AmbiguousSkillError(
                    f"Skill name '{name}' is ambiguous. "
                    f"Use full name instead: {options}"
                )

        # Not found
        raise SkillNotFoundError(f"Skill '{name}' not found in registry")

    def has(self, name: str) -> bool:
        """Check if skill exists (by short or full name)

        Args:
            name: Short name or full name

        Returns:
            True if skill exists and is unambiguous
        """
        try:
            self.get(name)
            return True
        except (SkillNotFoundError, AmbiguousSkillError):
            return False

    def list_all(self) -> List[SkillDefinition]:
        """List all registered skills

        Returns:
            List of all SkillDefinitions, sorted by full_name
        """
        skills = list(self._skills.values())
        return sorted(skills, key=lambda s: s.full_name)

    def list_by_namespace(self, namespace: str) -> List[SkillDefinition]:
        """List skills in a specific namespace

        Args:
            namespace: Namespace to filter by (e.g., "personal", "productivity")

        Returns:
            List of SkillDefinitions in the namespace, sorted by full_name
        """
        skills = [
            skill for skill in self._skills.values()
            if skill.metadata and skill.metadata.get("namespace") == namespace
        ]
        return sorted(skills, key=lambda s: s.full_name)
