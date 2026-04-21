//! 集中配置默认值
//!
//! 所有与 LLM、服务地址相关的默认值统一在此定义，
//! 避免在各 crate 中硬编码。

pub mod defaults {
    pub const OLLAMA_MODEL: &str = "qwen2.5:7b-instruct";
    pub const OLLAMA_BASE_URL: &str = "http://localhost:11434";
    pub const OLLAMA_PROVIDER: &str = "ollama";

    pub const ANTHROPIC_PROVIDER: &str = "anthropic";
    pub const ANTHROPIC_MODEL: &str = "claude-3-5-sonnet-20241022";

    pub const LLM_MAX_TOKENS: usize = 2048;
    pub const LLM_TEMPERATURE: f32 = 0.5;

    pub mod subagent {
        pub const SIMPLE_MODEL: &str = super::OLLAMA_MODEL;
        pub const SIMPLE_MAX_TOKENS: usize = 1024;
        pub const SIMPLE_TEMPERATURE: f32 = 0.3;

        pub const MEDIUM_MODEL: &str = super::OLLAMA_MODEL;
        pub const MEDIUM_MAX_TOKENS: usize = 2048;
        pub const MEDIUM_TEMPERATURE: f32 = 0.5;

        pub const COMPLEX_MODEL: &str = super::ANTHROPIC_MODEL;
        pub const COMPLEX_MAX_TOKENS: usize = 4096;
        pub const COMPLEX_TEMPERATURE: f32 = 0.7;
    }
}
