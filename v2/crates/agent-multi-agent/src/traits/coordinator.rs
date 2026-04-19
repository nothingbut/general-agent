use async_trait::async_trait;

use crate::error::Result;
use crate::models::{AggregatedResult, CollaborationTask};

#[async_trait]
pub trait Coordinator: Send + Sync {
    async fn execute(&self, task: &mut CollaborationTask) -> Result<AggregatedResult>;
}
