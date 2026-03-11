//! Configuration structures for subagent system

use crate::subagent::error::SubagentResult;
use crate::subagent::SubagentError;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::time::Duration;
use uuid::Uuid;

/// Task type for progress estimation and LLM selection
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum TaskType {
    CodeReview,
    Research,
    Analysis,
    Documentation,
    Testing,
    Custom,
}

/// Task complexity level
#[derive(Debug, Clone, PartialEq)]
pub enum TaskComplexity {
    Simple,
    Medium,
    Complex,
    Custom(LLMConfig),
}

impl TaskComplexity {
    /// Infer complexity from task type
    pub fn from_task_type(task_type: &TaskType) -> Self {
        match task_type {
            TaskType::CodeReview | TaskType::Analysis => Self::Medium,
            TaskType::Research => Self::Complex,
            TaskType::Documentation | TaskType::Testing => Self::Simple,
            TaskType::Custom => Self::Medium,
        }
    }

    /// Select LLM configuration based on complexity
    pub fn select_llm_config(&self) -> LLMConfig {
        match self {
            Self::Simple => LLMConfig {
                provider: "ollama".to_string(),
                model: "qwen2.5:0.5b".to_string(),
                max_tokens: 1024,
                temperature: 0.3,
            },
            Self::Medium => LLMConfig {
                provider: "ollama".to_string(),
                model: "qwen2.5:7b".to_string(),
                max_tokens: 2048,
                temperature: 0.5,
            },
            Self::Complex => LLMConfig {
                provider: "anthropic".to_string(),
                model: "claude-3-5-sonnet-20241022".to_string(),
                max_tokens: 4096,
                temperature: 0.7,
            },
            Self::Custom(config) => config.clone(),
        }
    }
}

/// LLM configuration
///
/// Valid ranges:
/// - `temperature`: 0.0 to 2.0 (controls randomness in LLM output)
/// - `max_tokens`: must be greater than 0 (maximum tokens to generate)
/// - `provider`: cannot be empty
/// - `model`: cannot be empty
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct LLMConfig {
    pub provider: String,
    pub model: String,
    pub max_tokens: usize,
    pub temperature: f32,
}

impl LLMConfig {
    /// Validates the LLM configuration
    ///
    /// # Errors
    ///
    /// Returns `SubagentError::ConfigError` if:
    /// - `temperature` is not in the range [0.0, 2.0]
    /// - `max_tokens` is 0
    /// - `provider` is empty
    /// - `model` is empty
    pub fn validate(&self) -> SubagentResult<()> {
        if self.temperature < 0.0 || self.temperature > 2.0 {
            return Err(SubagentError::ConfigError(format!(
                "Temperature must be between 0.0 and 2.0, got {}",
                self.temperature
            )));
        }

        if self.max_tokens == 0 {
            return Err(SubagentError::ConfigError(
                "max_tokens must be greater than 0".to_string(),
            ));
        }

        if self.provider.is_empty() {
            return Err(SubagentError::ConfigError(
                "provider cannot be empty".to_string(),
            ));
        }

        if self.model.is_empty() {
            return Err(SubagentError::ConfigError(
                "model cannot be empty".to_string(),
            ));
        }

        Ok(())
    }
}

impl Default for LLMConfig {
    fn default() -> Self {
        Self {
            provider: "ollama".to_string(),
            model: "qwen2.5:7b".to_string(),
            max_tokens: 2048,
            temperature: 0.5,
        }
    }
}

/// Shared context between parent and subagent
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct SharedContext {
    pub recent_messages: Option<usize>,
    pub variables: HashMap<String, String>,
    pub system_prompt: Option<String>,
}

/// Subagent execution configuration
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SubagentConfig {
    pub title: String,
    pub initial_prompt: String,
    pub shared_context: SharedContext,
    pub llm_config: LLMConfig,
    pub keep_alive: bool,
    pub timeout: Option<Duration>,
}

/// Subagent task configuration
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SubagentTaskConfig {
    pub id: Uuid,
    pub config: SubagentConfig,
    pub parent_id: Uuid,
    pub stage_id: String,
    pub priority: u8,
    pub task_type: TaskType,
}

/// Stage execution strategy
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum StageStrategy {
    Parallel,
    Sequential,
    ParallelWithLimit(usize),
}

/// Failure handling strategy
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum FailureStrategy {
    IgnoreAndContinue,
    FailStage,
    RetryOnce,
    AskUser,
}

/// Stage configuration
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Stage {
    pub id: String,
    pub name: String,
    pub tasks: Vec<SubagentTaskConfig>,
    pub strategy: StageStrategy,
    pub failure_strategy: FailureStrategy,
}
