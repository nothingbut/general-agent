//! General Agent Core - 核心领域模型和业务逻辑
//!
//! 这个 crate 包含核心领域实体、trait 定义和错误类型。

pub mod error;
pub mod models;
pub mod traits;

pub use error::{Error, Result};
