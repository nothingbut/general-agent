use async_trait::async_trait;

use crate::error::Result;
use crate::models::{AgentInfo, AgentMessage};

#[async_trait]
pub trait Agent: Send + Sync {
    fn info(&self) -> &AgentInfo;

    async fn handle_message(&self, message: AgentMessage) -> Result<AgentMessage>;

    async fn execute_task(&self, task: &str, context: serde_json::Value) -> Result<String>;
}
