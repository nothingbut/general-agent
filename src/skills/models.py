"""Immutable data models for skill system"""
from dataclasses import dataclass, field, asdict
from typing import Optional, Dict, Any, List


@dataclass(frozen=True)
class SkillParameter:
    """Skill parameter definition (immutable)"""
    name: str
    type: str  # 'string' | 'number' | 'boolean' | 'array' | 'object'
    required: bool
    description: str
    default: Optional[Any] = None


@dataclass(frozen=True)
class SkillDefinition:
    """Immutable skill definition"""
    name: str                          # Short name (e.g., "reminder")
    full_name: str                     # Namespace name (e.g., "personal:reminder")
    description: str
    content: str                       # Markdown content (skill instructions)
    parameters: List[SkillParameter] = field(default_factory=list)
    metadata: Optional[Dict[str, Any]] = None  # File path, namespace, etc.

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization"""
        return {
            "name": self.name,
            "full_name": self.full_name,
            "description": self.description,
            "content": self.content,
            "parameters": [
                {
                    "name": p.name,
                    "type": p.type,
                    "required": p.required,
                    "description": p.description,
                    "default": p.default
                }
                for p in self.parameters
            ],
            "metadata": self.metadata
        }


@dataclass(frozen=True)
class SkillExecutionResult:
    """Result of skill execution (immutable)"""
    skill_name: str
    success: bool
    output: str
    error: Optional[str] = None
    metadata: Optional[Dict[str, Any]] = None
