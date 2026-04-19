//! # agent-context-compression
//!
//! 上下文压缩系统，提供 Token 计数和多种压缩策略。
//!
//! ## 功能
//!
//! - **Token 计数**: 支持多种 LLM 模型的 Token 计数
//! - **滑动窗口**: 保留最近 N 条消息
//! - **语义压缩**: LLM 生成摘要
//! - **分层压缩**: 智能选择压缩策略
//!
//! ## 示例
//!
//! ```rust,ignore
//! use agent_context_compression::{TokenCounter, CompressionService};
//!
//! // Token 计数
//! let counter = TokenCounter::new_for_claude()?;
//! let count = counter.count("Hello, world!");
//!
//! // 压缩消息
//! let service = CompressionService::new(config);
//! let compressed = service.compress(messages, StrategyType::SlidingWindow).await?;
//! ```

pub mod cache;
pub mod config;
pub mod error;
pub mod models;
pub mod service;
pub mod strategies;
pub mod token_counter;

pub use cache::{Cache, CacheStats};
pub use config::ConfigFile;
pub use error::{CompressionError, Result};
pub use models::{CompressionConfig, CompressionRecord, CompressionResult};
pub use service::CompressionService;
pub use strategies::{CompressionStrategy, StrategyType};
pub use token_counter::TokenCounter;
