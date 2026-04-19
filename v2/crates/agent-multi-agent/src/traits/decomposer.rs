use async_trait::async_trait;

use crate::error::Result;
use crate::models::SubTask;

#[async_trait]
pub trait TaskDecomposer: Send + Sync {
    async fn decompose(
        &self,
        task: &str,
        available_agents: &[String],
    ) -> Result<Vec<SubTask>>;
}

#[async_trait]
pub trait ResultAggregator: Send + Sync {
    async fn aggregate(
        &self,
        results: Vec<(String, String)>,
    ) -> Result<String>;
}
