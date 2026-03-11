use thiserror::Error;

#[derive(Debug, Error)]
pub enum SubagentError {
    #[error("Too many concurrent subagents (max: {0})")]
    TooManyConcurrentSubagents(usize),

    #[error("Configuration error: {0}")]
    ConfigError(String),
}

pub type SubagentResult<T> = Result<T, SubagentError>;
