//! 错误分类和处理系统
//!
//! 本模块实现了任务执行错误的分类和处理：
//! - 错误分类（Transient/Permanent/Unknown）
//! - 错误分析和建议
//! - 与重试机制集成
//! - 错误通知
//!
//! # 示例
//!
//! ```rust
//! use agent_workflow::workflow::errors::*;
//!
//! // 分类错误
//! let classifier = ErrorClassifier::default();
//! let category = classifier.classify("Connection timeout");
//! assert_eq!(category, ErrorCategory::Transient);
//!
//! // 获取处理建议
//! let info = classifier.classify_with_info("Invalid API key");
//! assert_eq!(info.category, ErrorCategory::Permanent);
//! assert!(info.should_retry == false);
//! ```

use serde::{Deserialize, Serialize};
use std::fmt;

/// 错误类别
///
/// 将错误分为三类，用于指导重试和处理策略。
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum ErrorCategory {
    /// 临时错误 - 通常是网络、超时等问题，可以重试
    ///
    /// 示例：
    /// - 网络超时
    /// - 连接失败
    /// - 服务暂时不可用
    /// - 速率限制
    Transient,

    /// 永久错误 - 通常是配置、权限、参数错误，重试无效
    ///
    /// 示例：
    /// - 无效的参数
    /// - 权限不足
    /// - 资源不存在
    /// - 身份验证失败
    Permanent,

    /// 未知错误 - 无法判断类别，根据配置决定是否重试
    Unknown,
}

impl fmt::Display for ErrorCategory {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Transient => write!(f, "Transient"),
            Self::Permanent => write!(f, "Permanent"),
            Self::Unknown => write!(f, "Unknown"),
        }
    }
}

/// 错误分类信息
///
/// 包含错误的详细分类结果和处理建议。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ErrorClassificationInfo {
    /// 错误类别
    pub category: ErrorCategory,
    /// 是否应该重试
    pub should_retry: bool,
    /// 建议的处理方式
    pub recommendation: String,
    /// 匹配的关键词（用于调试）
    pub matched_keyword: Option<String>,
}

impl ErrorClassificationInfo {
    /// 创建临时错误分类
    pub fn transient(keyword: Option<String>) -> Self {
        Self {
            category: ErrorCategory::Transient,
            should_retry: true,
            recommendation: "这是一个临时错误，建议重试。系统会自动使用指数退避策略重试。".to_string(),
            matched_keyword: keyword,
        }
    }

    /// 创建永久错误分类
    pub fn permanent(keyword: Option<String>) -> Self {
        Self {
            category: ErrorCategory::Permanent,
            should_retry: false,
            recommendation: "这是一个永久错误，重试无法解决。请检查配置、权限或参数。".to_string(),
            matched_keyword: keyword,
        }
    }

    /// 创建未知错误分类
    pub fn unknown(retry_unknown: bool) -> Self {
        Self {
            category: ErrorCategory::Unknown,
            should_retry: retry_unknown,
            recommendation: if retry_unknown {
                "未知错误类型。根据配置，系统会尝试重试。".to_string()
            } else {
                "未知错误类型。根据配置，系统不会重试。建议人工检查。".to_string()
            },
            matched_keyword: None,
        }
    }
}

/// 错误分类器
///
/// 根据错误消息判断错误类别，并提供处理建议。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ErrorClassifier {
    /// 临时错误关键词
    pub transient_keywords: Vec<String>,
    /// 永久错误关键词
    pub permanent_keywords: Vec<String>,
    /// 是否重试未知错误
    pub retry_unknown_errors: bool,
}

impl ErrorClassifier {
    /// 创建新的分类器
    pub fn new() -> Self {
        Self::default()
    }

    /// 添加临时错误关键词
    pub fn add_transient_keyword(mut self, keyword: impl Into<String>) -> Self {
        self.transient_keywords.push(keyword.into());
        self
    }

    /// 添加永久错误关键词
    pub fn add_permanent_keyword(mut self, keyword: impl Into<String>) -> Self {
        self.permanent_keywords.push(keyword.into());
        self
    }

    /// 设置是否重试未知错误
    pub fn retry_unknown_errors(mut self, retry: bool) -> Self {
        self.retry_unknown_errors = retry;
        self
    }

    /// 分类错误（简单版本）
    ///
    /// 只返回错误类别，不返回详细信息。
    ///
    /// # 参数
    ///
    /// - `error_message`: 错误消息
    ///
    /// # 返回
    ///
    /// 错误类别
    pub fn classify(&self, error_message: &str) -> ErrorCategory {
        self.classify_with_info(error_message).category
    }

    /// 分类错误（详细版本）
    ///
    /// 返回完整的分类信息，包括处理建议。
    ///
    /// # 参数
    ///
    /// - `error_message`: 错误消息
    ///
    /// # 返回
    ///
    /// 错误分类信息
    ///
    /// # 判断逻辑
    ///
    /// 1. 检查是否包含永久错误关键词 → Permanent
    /// 2. 检查是否包含临时错误关键词 → Transient
    /// 3. 否则返回 Unknown
    pub fn classify_with_info(&self, error_message: &str) -> ErrorClassificationInfo {
        let error_lower = error_message.to_lowercase();

        // 1. 检查永久错误（优先级最高）
        for keyword in &self.permanent_keywords {
            if error_lower.contains(&keyword.to_lowercase()) {
                return ErrorClassificationInfo::permanent(Some(keyword.clone()));
            }
        }

        // 2. 检查临时错误
        for keyword in &self.transient_keywords {
            if error_lower.contains(&keyword.to_lowercase()) {
                return ErrorClassificationInfo::transient(Some(keyword.clone()));
            }
        }

        // 3. 未知错误
        ErrorClassificationInfo::unknown(self.retry_unknown_errors)
    }

    /// 批量分类错误
    ///
    /// 分类多个错误消息。
    pub fn classify_batch(&self, error_messages: &[&str]) -> Vec<ErrorClassificationInfo> {
        error_messages
            .iter()
            .map(|msg| self.classify_with_info(msg))
            .collect()
    }
}

impl Default for ErrorClassifier {
    fn default() -> Self {
        Self {
            // 临时错误：通常与网络、服务可用性相关
            transient_keywords: vec![
                // 网络错误
                "timeout".to_string(),
                "timed out".to_string(),
                "connection".to_string(),
                "network".to_string(),
                "connect failed".to_string(),
                "connection refused".to_string(),
                "connection reset".to_string(),

                // 服务可用性
                "temporary".to_string(),
                "temporarily".to_string(),
                "unavailable".to_string(),
                "try again".to_string(),
                "retry".to_string(),

                // 速率限制
                "rate limit".to_string(),
                "too many requests".to_string(),
                "throttle".to_string(),
                "throttled".to_string(),

                // HTTP 状态码
                "429".to_string(), // Too Many Requests
                "500".to_string(), // Internal Server Error
                "502".to_string(), // Bad Gateway
                "503".to_string(), // Service Unavailable
                "504".to_string(), // Gateway Timeout

                // 资源暂时锁定
                "locked".to_string(),
                "busy".to_string(),
                "in use".to_string(),
            ],

            // 永久错误：通常与配置、权限、参数相关
            permanent_keywords: vec![
                // 权限和认证
                "unauthorized".to_string(),
                "forbidden".to_string(),
                "permission denied".to_string(),
                "access denied".to_string(),
                "authentication failed".to_string(),
                "invalid token".to_string(),
                "invalid api key".to_string(),
                "expired token".to_string(),

                // 资源不存在
                "not found".to_string(),
                "does not exist".to_string(),
                "no such".to_string(),

                // 参数错误
                "invalid".to_string(),
                "bad request".to_string(),
                "malformed".to_string(),
                "invalid parameter".to_string(),
                "invalid argument".to_string(),
                "validation failed".to_string(),
                "validation error".to_string(),

                // 配置错误
                "configuration error".to_string(),
                "misconfigured".to_string(),

                // HTTP 状态码
                "400".to_string(), // Bad Request
                "401".to_string(), // Unauthorized
                "403".to_string(), // Forbidden
                "404".to_string(), // Not Found
                "405".to_string(), // Method Not Allowed
                "422".to_string(), // Unprocessable Entity

                // 不可恢复的错误
                "fatal".to_string(),
                "critical".to_string(),
                "unrecoverable".to_string(),
            ],

            // 默认不重试未知错误（保守策略）
            retry_unknown_errors: false,
        }
    }
}

/// 错误处理策略
///
/// 定义针对不同错误类别的处理方式。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ErrorHandlingStrategy {
    /// 对临时错误的最大重试次数
    pub transient_max_retries: u32,
    /// 对永久错误的最大重试次数（通常为 0）
    pub permanent_max_retries: u32,
    /// 对未知错误的最大重试次数
    pub unknown_max_retries: u32,
    /// 是否在永久错误时立即停止工作流
    pub stop_on_permanent_error: bool,
    /// 是否在未知错误时发送通知
    pub notify_on_unknown_error: bool,
}

impl ErrorHandlingStrategy {
    /// 创建新的策略
    pub fn new() -> Self {
        Self::default()
    }

    /// 根据错误类别获取最大重试次数
    pub fn max_retries_for_category(&self, category: ErrorCategory) -> u32 {
        match category {
            ErrorCategory::Transient => self.transient_max_retries,
            ErrorCategory::Permanent => self.permanent_max_retries,
            ErrorCategory::Unknown => self.unknown_max_retries,
        }
    }

    /// 是否应该停止工作流
    pub fn should_stop_workflow(&self, category: ErrorCategory) -> bool {
        match category {
            ErrorCategory::Permanent => self.stop_on_permanent_error,
            _ => false,
        }
    }

    /// 是否应该发送通知
    pub fn should_notify(&self, category: ErrorCategory) -> bool {
        match category {
            ErrorCategory::Permanent => true, // 永久错误总是通知
            ErrorCategory::Unknown => self.notify_on_unknown_error,
            ErrorCategory::Transient => false, // 临时错误不通知（除非达到最大重试次数）
        }
    }
}

impl Default for ErrorHandlingStrategy {
    fn default() -> Self {
        Self {
            transient_max_retries: 3,    // 临时错误重试 3 次
            permanent_max_retries: 0,    // 永久错误不重试
            unknown_max_retries: 1,      // 未知错误重试 1 次
            stop_on_permanent_error: true, // 永久错误停止工作流
            notify_on_unknown_error: true, // 未知错误发送通知
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_classify_transient_errors() {
        let classifier = ErrorClassifier::default();

        // 网络相关
        assert_eq!(classifier.classify("Connection timeout"), ErrorCategory::Transient);
        assert_eq!(classifier.classify("Network error occurred"), ErrorCategory::Transient);
        assert_eq!(classifier.classify("Connection refused"), ErrorCategory::Transient);

        // 服务可用性
        assert_eq!(classifier.classify("Service temporarily unavailable"), ErrorCategory::Transient);
        assert_eq!(classifier.classify("Please try again later"), ErrorCategory::Transient);

        // 速率限制
        assert_eq!(classifier.classify("Rate limit exceeded"), ErrorCategory::Transient);
        assert_eq!(classifier.classify("Too many requests"), ErrorCategory::Transient);
        assert_eq!(classifier.classify("API throttled"), ErrorCategory::Transient);

        // HTTP 状态码
        assert_eq!(classifier.classify("HTTP 429 Error"), ErrorCategory::Transient);
        assert_eq!(classifier.classify("500 Internal Server Error"), ErrorCategory::Transient);
        assert_eq!(classifier.classify("502 Bad Gateway"), ErrorCategory::Transient);
        assert_eq!(classifier.classify("503 Service Unavailable"), ErrorCategory::Transient);
    }

    #[test]
    fn test_classify_permanent_errors() {
        let classifier = ErrorClassifier::default();

        // 权限和认证
        assert_eq!(classifier.classify("Unauthorized access"), ErrorCategory::Permanent);
        assert_eq!(classifier.classify("403 Forbidden"), ErrorCategory::Permanent);
        assert_eq!(classifier.classify("Invalid API key"), ErrorCategory::Permanent);
        assert_eq!(classifier.classify("Authentication failed"), ErrorCategory::Permanent);

        // 资源不存在
        assert_eq!(classifier.classify("Resource not found"), ErrorCategory::Permanent);
        assert_eq!(classifier.classify("404 Not Found"), ErrorCategory::Permanent);

        // 参数错误
        assert_eq!(classifier.classify("Invalid parameter: user_id"), ErrorCategory::Permanent);
        assert_eq!(classifier.classify("Bad request: missing field"), ErrorCategory::Permanent);
        assert_eq!(classifier.classify("Validation error"), ErrorCategory::Permanent);

        // HTTP 状态码
        assert_eq!(classifier.classify("400 Bad Request"), ErrorCategory::Permanent);
        assert_eq!(classifier.classify("401 Unauthorized"), ErrorCategory::Permanent);
    }

    #[test]
    fn test_classify_unknown_errors() {
        let classifier = ErrorClassifier::default();

        assert_eq!(classifier.classify("Something went wrong"), ErrorCategory::Unknown);
        assert_eq!(classifier.classify("Unexpected error"), ErrorCategory::Unknown);
        assert_eq!(classifier.classify("Error code: 9999"), ErrorCategory::Unknown);
    }

    #[test]
    fn test_classify_with_info() {
        let classifier = ErrorClassifier::default();

        // 临时错误
        let info = classifier.classify_with_info("Connection timeout");
        assert_eq!(info.category, ErrorCategory::Transient);
        assert!(info.should_retry);
        assert!(info.matched_keyword.is_some());

        // 永久错误
        let info = classifier.classify_with_info("Invalid API key");
        assert_eq!(info.category, ErrorCategory::Permanent);
        assert!(!info.should_retry);
        assert!(info.matched_keyword.is_some());

        // 未知错误
        let info = classifier.classify_with_info("Random error");
        assert_eq!(info.category, ErrorCategory::Unknown);
        assert!(!info.should_retry); // 默认不重试
        assert!(info.matched_keyword.is_none());
    }

    #[test]
    fn test_custom_classifier() {
        let classifier = ErrorClassifier::new()
            .add_transient_keyword("database locked")
            .add_permanent_keyword("schema mismatch")
            .retry_unknown_errors(true);

        assert_eq!(classifier.classify("Database locked, try again"), ErrorCategory::Transient);
        assert_eq!(classifier.classify("Schema mismatch detected"), ErrorCategory::Permanent);

        // 未知错误，但配置为重试
        let info = classifier.classify_with_info("Unknown error");
        assert_eq!(info.category, ErrorCategory::Unknown);
        assert!(info.should_retry);
    }

    #[test]
    fn test_classify_batch() {
        let classifier = ErrorClassifier::default();
        let errors = vec![
            "Connection timeout",
            "Invalid parameter",
            "Some random error",
        ];

        let results = classifier.classify_batch(&errors);
        assert_eq!(results.len(), 3);
        assert_eq!(results[0].category, ErrorCategory::Transient);
        assert_eq!(results[1].category, ErrorCategory::Permanent);
        assert_eq!(results[2].category, ErrorCategory::Unknown);
    }

    #[test]
    fn test_permanent_priority_over_transient() {
        let classifier = ErrorClassifier::new()
            .add_transient_keyword("error")
            .add_permanent_keyword("invalid");

        // "invalid" 是永久错误关键词，应该优先匹配
        assert_eq!(
            classifier.classify("Invalid error occurred"),
            ErrorCategory::Permanent
        );
    }

    #[test]
    fn test_error_handling_strategy() {
        let strategy = ErrorHandlingStrategy::default();

        assert_eq!(strategy.max_retries_for_category(ErrorCategory::Transient), 3);
        assert_eq!(strategy.max_retries_for_category(ErrorCategory::Permanent), 0);
        assert_eq!(strategy.max_retries_for_category(ErrorCategory::Unknown), 1);

        assert!(strategy.should_stop_workflow(ErrorCategory::Permanent));
        assert!(!strategy.should_stop_workflow(ErrorCategory::Transient));

        assert!(strategy.should_notify(ErrorCategory::Permanent));
        assert!(strategy.should_notify(ErrorCategory::Unknown));
        assert!(!strategy.should_notify(ErrorCategory::Transient));
    }

    #[test]
    fn test_custom_error_handling_strategy() {
        let strategy = ErrorHandlingStrategy {
            transient_max_retries: 5,
            permanent_max_retries: 1, // 允许永久错误重试一次
            unknown_max_retries: 2,
            stop_on_permanent_error: false, // 不停止工作流
            notify_on_unknown_error: false,
        };

        assert_eq!(strategy.max_retries_for_category(ErrorCategory::Transient), 5);
        assert_eq!(strategy.max_retries_for_category(ErrorCategory::Permanent), 1);
        assert!(!strategy.should_stop_workflow(ErrorCategory::Permanent));
        assert!(!strategy.should_notify(ErrorCategory::Unknown));
    }
}
