use serde::{Deserialize, Serialize};
use utoipa::ToSchema;

#[derive(Serialize, ToSchema)]
pub struct AgentInfoDto {
    pub id: String,
    pub name: String,
    pub description: String,
    pub capabilities: Vec<String>,
    pub status: String,
    pub max_concurrent_tasks: usize,
    pub active_tasks: usize,
}

impl From<agent_multi_agent::AgentInfo> for AgentInfoDto {
    fn from(info: agent_multi_agent::AgentInfo) -> Self {
        Self {
            id: info.id,
            name: info.name,
            description: info.description,
            capabilities: info.capabilities.iter().map(|c| c.as_str().to_string()).collect(),
            status: match info.status {
                agent_multi_agent::AgentStatus::Idle => "idle".to_string(),
                agent_multi_agent::AgentStatus::Busy => "busy".to_string(),
                agent_multi_agent::AgentStatus::Offline => "offline".to_string(),
            },
            max_concurrent_tasks: info.max_concurrent_tasks,
            active_tasks: info.active_tasks,
        }
    }
}

#[derive(Deserialize, ToSchema)]
pub struct CollaborationRequest {
    pub title: String,
    pub description: String,
    pub strategy: String,
    #[serde(default)]
    pub min_votes: Option<usize>,
    pub subtasks: Vec<SubTaskRequest>,
}

#[derive(Deserialize, ToSchema)]
pub struct SubTaskRequest {
    pub agent_id: String,
    pub task: String,
    #[serde(default = "default_context")]
    pub context: serde_json::Value,
}

fn default_context() -> serde_json::Value {
    serde_json::json!({})
}

#[derive(Serialize, ToSchema)]
pub struct CollaborationResultDto {
    pub summary: String,
    pub agent_results: Vec<AgentResultDto>,
    pub metadata: serde_json::Value,
    pub execution_time_ms: u64,
}

#[derive(Serialize, ToSchema)]
pub struct AgentResultDto {
    pub agent_id: String,
    pub result: String,
    pub confidence: Option<f64>,
    pub execution_time_ms: u64,
}

impl From<agent_multi_agent::AggregatedResult> for CollaborationResultDto {
    fn from(result: agent_multi_agent::AggregatedResult) -> Self {
        Self {
            summary: result.summary,
            agent_results: result
                .agent_results
                .into_iter()
                .map(|r| AgentResultDto {
                    agent_id: r.agent_id,
                    result: r.result,
                    confidence: r.confidence,
                    execution_time_ms: r.execution_time.as_millis() as u64,
                })
                .collect(),
            metadata: result.metadata,
            execution_time_ms: result.total_execution_time.as_millis() as u64,
        }
    }
}
