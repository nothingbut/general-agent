"""Execute skills with parameter validation and LLM integration"""
import logging
from typing import Dict, Any

from .models import SkillDefinition, SkillExecutionResult

logger = logging.getLogger(__name__)


class SkillExecutionError(Exception):
    """Raised when skill execution fails due to validation errors"""
    pass


class SkillExecutor:
    """Execute skills with parameter substitution and LLM integration

    Handles:
    - Parameter validation (required, types, empty values)
    - Prompt building with parameter substitution
    - LLM API calls
    - Error handling and result wrapping
    """

    def __init__(self, llm_client):
        """Initialize executor

        Args:
            llm_client: LLM client with async chat() method
        """
        self.llm_client = llm_client

    async def execute(
        self,
        skill: SkillDefinition,
        parameters: Dict[str, Any]
    ) -> SkillExecutionResult:
        """Execute a skill with given parameters

        Args:
            skill: SkillDefinition to execute
            parameters: Dictionary of parameter values

        Returns:
            SkillExecutionResult with success status and output

        Raises:
            SkillExecutionError: If parameter validation fails
        """
        try:
            # Validate and prepare parameters
            validated_params = self._validate_parameters(skill, parameters)

            # Build prompt with parameter substitution
            prompt = self._build_prompt(skill, validated_params)

            # Call LLM
            output = await self._call_llm(prompt)

            # Return success result
            return SkillExecutionResult(
                skill_name=skill.full_name,
                success=True,
                output=output,
                error=None,
                metadata={
                    "skill_name": skill.full_name,
                    "parameters": validated_params
                }
            )

        except SkillExecutionError:
            # Re-raise validation errors
            raise

        except Exception as e:
            # Catch LLM errors and return failure result
            logger.error(f"Skill execution failed: {e}")
            return SkillExecutionResult(
                skill_name=skill.full_name,
                success=False,
                output="",
                error=str(e),
                metadata={"skill_name": skill.full_name}
            )

    def _validate_parameters(
        self,
        skill: SkillDefinition,
        parameters: Dict[str, Any]
    ) -> Dict[str, str]:
        """Validate and normalize parameters

        Args:
            skill: SkillDefinition with parameter definitions
            parameters: Raw parameter values

        Returns:
            Dictionary of validated and normalized parameters (all strings)

        Raises:
            SkillExecutionError: If validation fails
        """
        validated = {}

        # Check each defined parameter
        for param_def in skill.parameters:
            param_name = param_def.name
            param_value = parameters.get(param_name)

            # Handle missing parameters
            if param_value is None:
                if param_def.required:
                    # Use default if available
                    if param_def.default is not None:
                        validated[param_name] = str(param_def.default)
                        continue
                    else:
                        raise SkillExecutionError(
                            f"Required parameter '{param_name}' is missing"
                        )
                else:
                    # Optional parameter - use default or skip
                    if param_def.default is not None:
                        validated[param_name] = str(param_def.default)
                    continue

            # Convert to string
            param_str = str(param_value)

            # Check for empty values
            if not param_str.strip():
                raise SkillExecutionError(
                    f"Parameter '{param_name}' cannot be empty"
                )

            validated[param_name] = param_str

        return validated

    def _build_prompt(
        self,
        skill: SkillDefinition,
        parameters: Dict[str, str]
    ) -> str:
        """Build prompt with parameter substitution

        Args:
            skill: SkillDefinition with content template
            parameters: Validated parameter values

        Returns:
            Prompt string with parameters substituted
        """
        prompt = skill.content

        # Substitute parameters using {param_name} syntax
        for param_name, param_value in parameters.items():
            placeholder = f"{{{param_name}}}"
            prompt = prompt.replace(placeholder, param_value)

        return prompt

    async def _call_llm(self, prompt: str) -> str:
        """Call LLM with the prompt

        Args:
            prompt: The prompt to send to LLM

        Returns:
            LLM response string

        Raises:
            Exception: If LLM call fails
        """
        messages = [
            {"role": "user", "content": prompt}
        ]

        response = await self.llm_client.chat(messages)
        return response
