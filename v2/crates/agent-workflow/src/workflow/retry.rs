//! 重试策略和机制
//!
//! 本模块实现了任务执行的重试策略：
//! - 指数退避 (Exponential Backoff)
//! - 固定延迟 (Fixed Delay)
//! - 线性增长 (Linear Backoff)
//! - 可配置的重试条件
//! - 重试历史记录
//!
//! # 示例
//!
//! ```rust
//! use agent_workflow::workflow::retry::*;
//!
//! // 创建指数退避策略
//! let strategy = RetryStrategy::exponential(3, 100, 5000, 2.0);
//!
//! // 判断错误是否可重试
//! let should_retry = RetryCondition::default().should_retry("Connection timeout");
//! ```

use serde::{Deserialize, Serialize};
use std::time::Duration;

/// 重试策略
///
/// 定义任务失败后的重试行为。
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum RetryStrategy {
    /// 指数退避 - 每次重试延迟按指数增长
    ///
    /// 参数：
    /// - `max_retries`: 最大重试次数
    /// - `initial_delay_ms`: 初始延迟（毫秒）
    /// - `max_delay_ms`: 最大延迟（毫秒）
    /// - `multiplier`: 延迟增长倍数
    ExponentialBackoff {
        max_retries: u32,
        initial_delay_ms: u64,
        max_delay_ms: u64,
        multiplier: f64,
    },

    /// 固定延迟 - 每次重试固定延迟
    ///
    /// 参数：
    /// - `max_retries`: 最大重试次数
    /// - `delay_ms`: 固定延迟（毫秒）
    FixedDelay {
        max_retries: u32,
        delay_ms: u64,
    },

    /// 线性增长 - 延迟线性增加
    ///
    /// 参数：
    /// - `max_retries`: 最大重试次数
    /// - `initial_delay_ms`: 初始延迟（毫秒）
    /// - `increment_ms`: 每次增加的延迟（毫秒）
    LinearBackoff {
        max_retries: u32,
        initial_delay_ms: u64,
        increment_ms: u64,
    },

    /// 无重试
    None,
}

impl RetryStrategy {
    /// 创建指数退避策略
    ///
    /// # 参数
    ///
    /// - `max_retries`: 最大重试次数
    /// - `initial_delay_ms`: 初始延迟（毫秒）
    /// - `max_delay_ms`: 最大延迟（毫秒）
    /// - `multiplier`: 延迟增长倍数（通常为 2.0）
    pub fn exponential(
        max_retries: u32,
        initial_delay_ms: u64,
        max_delay_ms: u64,
        multiplier: f64,
    ) -> Self {
        Self::ExponentialBackoff {
            max_retries,
            initial_delay_ms,
            max_delay_ms,
            multiplier,
        }
    }

    /// 创建固定延迟策略
    pub fn fixed(max_retries: u32, delay_ms: u64) -> Self {
        Self::FixedDelay {
            max_retries,
            delay_ms,
        }
    }

    /// 创建线性增长策略
    pub fn linear(max_retries: u32, initial_delay_ms: u64, increment_ms: u64) -> Self {
        Self::LinearBackoff {
            max_retries,
            initial_delay_ms,
            increment_ms,
        }
    }

    /// 获取最大重试次数
    pub fn max_retries(&self) -> u32 {
        match self {
            Self::ExponentialBackoff { max_retries, .. } => *max_retries,
            Self::FixedDelay { max_retries, .. } => *max_retries,
            Self::LinearBackoff { max_retries, .. } => *max_retries,
            Self::None => 0,
        }
    }

    /// 计算第 n 次重试的延迟时间
    ///
    /// # 参数
    ///
    /// - `attempt`: 重试次数（从 1 开始）
    ///
    /// # 返回
    ///
    /// 返回 Some(Duration) 如果应该重试，否则返回 None
    pub fn delay_for_attempt(&self, attempt: u32) -> Option<Duration> {
        if attempt == 0 || attempt > self.max_retries() {
            return None;
        }

        let delay_ms = match self {
            Self::ExponentialBackoff {
                initial_delay_ms,
                max_delay_ms,
                multiplier,
                ..
            } => {
                let delay = (*initial_delay_ms as f64) * multiplier.powi((attempt - 1) as i32);
                delay.min(*max_delay_ms as f64) as u64
            }
            Self::FixedDelay { delay_ms, .. } => *delay_ms,
            Self::LinearBackoff {
                initial_delay_ms,
                increment_ms,
                ..
            } => initial_delay_ms + increment_ms * (attempt - 1) as u64,
            Self::None => return None,
        };

        Some(Duration::from_millis(delay_ms))
    }
}

impl Default for RetryStrategy {
    fn default() -> Self {
        // 默认：3 次重试，指数退避（100ms, 200ms, 400ms）
        Self::exponential(3, 100, 5000, 2.0)
    }
}

/// 重试条件 - 判断错误是否应该重试
///
/// 可以基于错误消息、错误类型等条件判断是否重试。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RetryCondition {
    /// 可重试的错误关键词（包含任一关键词即可重试）
    pub retryable_errors: Vec<String>,
    /// 不可重试的错误关键词（包含任一关键词则不重试）
    pub non_retryable_errors: Vec<String>,
    /// 是否默认重试未知错误
    pub retry_unknown_errors: bool,
}

impl RetryCondition {
    /// 创建新的重试条件
    pub fn new() -> Self {
        Self::default()
    }

    /// 添加可重试的错误关键词
    pub fn add_retryable_error(mut self, keyword: impl Into<String>) -> Self {
        self.retryable_errors.push(keyword.into());
        self
    }

    /// 添加不可重试的错误关键词
    pub fn add_non_retryable_error(mut self, keyword: impl Into<String>) -> Self {
        self.non_retryable_errors.push(keyword.into());
        self
    }

    /// 设置是否重试未知错误
    pub fn retry_unknown_errors(mut self, retry: bool) -> Self {
        self.retry_unknown_errors = retry;
        self
    }

    /// 判断错误是否应该重试
    ///
    /// # 判断逻辑
    ///
    /// 1. 如果错误消息包含不可重试关键词，返回 false
    /// 2. 如果错误消息包含可重试关键词，返回 true
    /// 3. 否则根据 retry_unknown_errors 设置返回
    pub fn should_retry(&self, error_message: &str) -> bool {
        let error_lower = error_message.to_lowercase();

        // 检查不可重试错误
        for keyword in &self.non_retryable_errors {
            if error_lower.contains(&keyword.to_lowercase()) {
                return false;
            }
        }

        // 检查可重试错误
        for keyword in &self.retryable_errors {
            if error_lower.contains(&keyword.to_lowercase()) {
                return true;
            }
        }

        // 未知错误
        self.retry_unknown_errors
    }
}

impl Default for RetryCondition {
    fn default() -> Self {
        Self {
            retryable_errors: vec![
                "timeout".to_string(),
                "connection".to_string(),
                "network".to_string(),
                "temporary".to_string(),
                "unavailable".to_string(),
                "rate limit".to_string(),
                "429".to_string(), // HTTP 429 Too Many Requests
                "500".to_string(), // HTTP 500 Internal Server Error
                "502".to_string(), // HTTP 502 Bad Gateway
                "503".to_string(), // HTTP 503 Service Unavailable
                "504".to_string(), // HTTP 504 Gateway Timeout
            ],
            non_retryable_errors: vec![
                "invalid".to_string(),
                "unauthorized".to_string(),
                "forbidden".to_string(),
                "not found".to_string(),
                "bad request".to_string(),
                "400".to_string(), // HTTP 400 Bad Request
                "401".to_string(), // HTTP 401 Unauthorized
                "403".to_string(), // HTTP 403 Forbidden
                "404".to_string(), // HTTP 404 Not Found
            ],
            retry_unknown_errors: false, // 默认不重试未知错误
        }
    }
}

/// 重试尝试记录
///
/// 记录单次重试的详细信息。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RetryAttempt {
    /// 重试次数（从 1 开始）
    pub attempt: u32,
    /// 错误消息
    pub error: String,
    /// 重试延迟（毫秒）
    pub delay_ms: u64,
    /// 时间戳
    pub timestamp: chrono::DateTime<chrono::Utc>,
}

impl RetryAttempt {
    /// 创建新的重试记录
    pub fn new(attempt: u32, error: String, delay_ms: u64) -> Self {
        Self {
            attempt,
            error,
            delay_ms,
            timestamp: chrono::Utc::now(),
        }
    }
}

/// 重试历史
///
/// 记录任务执行过程中所有的重试尝试。
#[derive(Debug, Clone, Serialize, Deserialize, Default)]
pub struct RetryHistory {
    /// 重试尝试列表
    pub attempts: Vec<RetryAttempt>,
    /// 总重试次数
    pub total_retries: u32,
    /// 是否达到最大重试次数
    pub max_retries_reached: bool,
}

impl RetryHistory {
    /// 创建新的重试历史
    pub fn new() -> Self {
        Self::default()
    }

    /// 添加重试记录
    pub fn add_attempt(&mut self, attempt: RetryAttempt) {
        self.total_retries += 1;
        self.attempts.push(attempt);
    }

    /// 标记达到最大重试次数
    pub fn mark_max_retries_reached(&mut self) {
        self.max_retries_reached = true;
    }

    /// 获取最后一次错误
    pub fn last_error(&self) -> Option<&str> {
        self.attempts.last().map(|a| a.error.as_str())
    }

    /// 是否有重试
    pub fn has_retries(&self) -> bool {
        self.total_retries > 0
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_exponential_backoff() {
        let strategy = RetryStrategy::exponential(3, 100, 5000, 2.0);

        assert_eq!(strategy.max_retries(), 3);
        assert_eq!(strategy.delay_for_attempt(1), Some(Duration::from_millis(100)));
        assert_eq!(strategy.delay_for_attempt(2), Some(Duration::from_millis(200)));
        assert_eq!(strategy.delay_for_attempt(3), Some(Duration::from_millis(400)));
        assert_eq!(strategy.delay_for_attempt(4), None);
    }

    #[test]
    fn test_exponential_backoff_max_delay() {
        let strategy = RetryStrategy::exponential(5, 1000, 3000, 2.0);

        assert_eq!(strategy.delay_for_attempt(1), Some(Duration::from_millis(1000)));
        assert_eq!(strategy.delay_for_attempt(2), Some(Duration::from_millis(2000)));
        assert_eq!(strategy.delay_for_attempt(3), Some(Duration::from_millis(3000))); // 限制在 max_delay
        assert_eq!(strategy.delay_for_attempt(4), Some(Duration::from_millis(3000))); // 限制在 max_delay
    }

    #[test]
    fn test_fixed_delay() {
        let strategy = RetryStrategy::fixed(3, 500);

        assert_eq!(strategy.max_retries(), 3);
        assert_eq!(strategy.delay_for_attempt(1), Some(Duration::from_millis(500)));
        assert_eq!(strategy.delay_for_attempt(2), Some(Duration::from_millis(500)));
        assert_eq!(strategy.delay_for_attempt(3), Some(Duration::from_millis(500)));
        assert_eq!(strategy.delay_for_attempt(4), None);
    }

    #[test]
    fn test_linear_backoff() {
        let strategy = RetryStrategy::linear(3, 100, 200);

        assert_eq!(strategy.max_retries(), 3);
        assert_eq!(strategy.delay_for_attempt(1), Some(Duration::from_millis(100)));
        assert_eq!(strategy.delay_for_attempt(2), Some(Duration::from_millis(300)));
        assert_eq!(strategy.delay_for_attempt(3), Some(Duration::from_millis(500)));
        assert_eq!(strategy.delay_for_attempt(4), None);
    }

    #[test]
    fn test_no_retry() {
        let strategy = RetryStrategy::None;

        assert_eq!(strategy.max_retries(), 0);
        assert_eq!(strategy.delay_for_attempt(1), None);
    }

    #[test]
    fn test_retry_condition_retryable() {
        let condition = RetryCondition::default();

        assert!(condition.should_retry("Connection timeout"));
        assert!(condition.should_retry("Network error"));
        assert!(condition.should_retry("Temporary failure"));
        assert!(condition.should_retry("Service unavailable"));
        assert!(condition.should_retry("HTTP 429 Rate limit"));
        assert!(condition.should_retry("HTTP 500 Internal Server Error"));
    }

    #[test]
    fn test_retry_condition_non_retryable() {
        let condition = RetryCondition::default();

        assert!(!condition.should_retry("Invalid request"));
        assert!(!condition.should_retry("Unauthorized"));
        assert!(!condition.should_retry("403 Forbidden"));
        assert!(!condition.should_retry("Not found"));
        assert!(!condition.should_retry("Bad request"));
    }

    #[test]
    fn test_retry_condition_unknown() {
        let condition = RetryCondition::default();

        // 默认不重试未知错误
        assert!(!condition.should_retry("Some unknown error"));

        // 配置重试未知错误
        let condition = RetryCondition::default().retry_unknown_errors(true);
        assert!(condition.should_retry("Some unknown error"));
    }

    #[test]
    fn test_retry_condition_custom() {
        let condition = RetryCondition::new()
            .add_retryable_error("database locked")
            .add_non_retryable_error("schema error");

        assert!(condition.should_retry("Database locked, try again"));
        assert!(!condition.should_retry("Schema error: invalid column"));
    }

    #[test]
    fn test_retry_history() {
        let mut history = RetryHistory::new();

        assert_eq!(history.total_retries, 0);
        assert!(!history.has_retries());
        assert!(!history.max_retries_reached);

        history.add_attempt(RetryAttempt::new(1, "Error 1".to_string(), 100));
        assert_eq!(history.total_retries, 1);
        assert!(history.has_retries());
        assert_eq!(history.last_error(), Some("Error 1"));

        history.add_attempt(RetryAttempt::new(2, "Error 2".to_string(), 200));
        assert_eq!(history.total_retries, 2);
        assert_eq!(history.last_error(), Some("Error 2"));

        history.mark_max_retries_reached();
        assert!(history.max_retries_reached);
    }
}
