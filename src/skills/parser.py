"""Parse skill files (YAML frontmatter + Markdown content)"""
import re
from pathlib import Path
from typing import Dict, Any, Tuple, List
import yaml

from .models import SkillDefinition, SkillParameter


class SkillParser:
    """Parse skill files with YAML frontmatter and Markdown content"""

    def parse_file(self, file_path: Path) -> SkillDefinition:
        """Parse a skill file and return SkillDefinition

        Args:
            file_path: Path to skill markdown file

        Returns:
            Parsed SkillDefinition

        Raises:
            ValueError: If file format is invalid
            FileNotFoundError: If file doesn't exist
            yaml.YAMLError: If YAML parsing fails
        """
        if not file_path.exists():
            raise FileNotFoundError(f"Skill file not found: {file_path}")

        # Read file content
        content_text = file_path.read_text(encoding="utf-8")

        if not content_text or not content_text.strip():
            raise ValueError("File is empty")

        # Parse YAML frontmatter and content
        yaml_data, markdown_content = self._parse_yaml_frontmatter(content_text)

        # Validate required fields
        self._validate_skill_data(yaml_data)

        # Build SkillParameter objects
        parameters = self._parse_parameters(yaml_data.get("parameters", []))

        # Build SkillDefinition
        skill = SkillDefinition(
            name=yaml_data["name"],
            full_name=yaml_data["name"],  # Will be set by loader with namespace
            description=yaml_data["description"],
            content=markdown_content,
            parameters=parameters,
            metadata={"file_path": str(file_path)}
        )

        return skill

    def _parse_yaml_frontmatter(self, text: str) -> Tuple[Dict[str, Any], str]:
        """Extract YAML frontmatter and remaining content

        Args:
            text: Full file content

        Returns:
            (yaml_dict, markdown_content)

        Raises:
            ValueError: If frontmatter format is invalid
            yaml.YAMLError: If YAML parsing fails
        """
        # Match YAML frontmatter between --- delimiters
        pattern = r"^---\s*\n(.*?)\n---\s*\n(.*)$"
        match = re.match(pattern, text, re.DOTALL)

        if not match:
            raise ValueError("No YAML frontmatter found (must be between --- delimiters)")

        yaml_text = match.group(1)
        markdown_content = match.group(2).strip()

        # Parse YAML
        try:
            yaml_data = yaml.safe_load(yaml_text)
        except yaml.YAMLError as e:
            raise yaml.YAMLError(f"Invalid YAML syntax: {e}")

        if not isinstance(yaml_data, dict):
            raise ValueError("YAML frontmatter must be a dictionary")

        return yaml_data, markdown_content

    def _validate_skill_data(self, data: Dict[str, Any]) -> None:
        """Validate skill data structure

        Args:
            data: Parsed YAML data

        Raises:
            ValueError: If required fields are missing or invalid
        """
        required_fields = ["name", "description"]

        for field in required_fields:
            if field not in data:
                raise ValueError(f"Missing required field: {field}")

            if not isinstance(data[field], str) or not data[field].strip():
                raise ValueError(f"Field '{field}' must be a non-empty string")

    def _parse_parameters(self, params_data: List[Dict[str, Any]]) -> List[SkillParameter]:
        """Parse parameter definitions

        Args:
            params_data: List of parameter dictionaries

        Returns:
            List of SkillParameter objects

        Raises:
            ValueError: If parameter structure is invalid
        """
        parameters = []

        for i, param_dict in enumerate(params_data):
            if not isinstance(param_dict, dict):
                raise ValueError(f"Invalid parameter at index {i}: must be a dictionary")

            # Required fields
            required_param_fields = ["name", "type", "required", "description"]
            for field in required_param_fields:
                if field not in param_dict:
                    raise ValueError(f"Invalid parameter at index {i}: missing field '{field}'")

            # Create SkillParameter
            param = SkillParameter(
                name=param_dict["name"],
                type=param_dict["type"],
                required=param_dict["required"],
                description=param_dict["description"],
                default=param_dict.get("default")
            )
            parameters.append(param)

        return parameters
