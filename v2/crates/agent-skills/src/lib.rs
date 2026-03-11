//! Agent Skills - 技能系统
//!
//! 提供可复用的提示词模板系统，支持：
//! - Markdown + YAML frontmatter 格式
//! - 参数化和默认值
//! - 命名空间管理
//! - 参数验证

pub mod executor;
pub mod loader;
pub mod models;
pub mod parser;
pub mod registry;

pub use executor::{ExecutorError, SkillExecutor};
pub use loader::{LoadError, SkillLoader};
pub use models::{SkillDefinition, SkillExecutionContext, SkillParameter};
pub use parser::{ParseError, SkillParser};
pub use registry::{RegistryError, SkillRegistry};
