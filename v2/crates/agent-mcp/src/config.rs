//! MCP 服务器配置

use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::time::Duration;

/// MCP 服务器配置
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MCPServerConfig {
    /// 服务器名称
    pub name: String,
    /// 启动命令
    pub command: String,
    /// 命令参数
    pub args: Vec<String>,
    /// 环境变量
    pub env: HashMap<String, String>,

    /// 超时时间
    #[serde(default = "default_timeout", with = "humantime_serde")]
    pub timeout: Duration,
}

fn default_timeout() -> Duration {
    Duration::from_secs(30)
}

impl MCPServerConfig {
    /// 创建新的服务器配置
    pub fn new(name: String, command: String) -> Self {
        Self {
            name,
            command,
            args: vec![],
            env: HashMap::new(),
            timeout: default_timeout(),
        }
    }
}
