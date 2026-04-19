use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use utoipa::ToSchema;
use uuid::Uuid;

#[derive(Serialize, ToSchema)]
pub struct MemoryDto {
    pub id: Uuid,
    pub memory_type: String,
    pub content: String,
    pub source: Option<String>,
    pub session_id: Option<Uuid>,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
}

impl From<agent_memory::Memory> for MemoryDto {
    fn from(m: agent_memory::Memory) -> Self {
        Self {
            id: m.id,
            memory_type: m.memory_type.as_str().to_string(),
            content: m.content,
            source: m.source,
            session_id: m.session_id,
            created_at: m.created_at,
            updated_at: m.updated_at,
        }
    }
}

#[derive(Deserialize, ToSchema)]
pub struct CreateMemoryRequest {
    pub memory_type: String,
    pub content: String,
    pub source: Option<String>,
    pub session_id: Option<Uuid>,
}

#[derive(Deserialize, ToSchema)]
pub struct UpdateMemoryRequest {
    pub content: Option<String>,
    pub source: Option<String>,
}

#[derive(Deserialize, ToSchema)]
pub struct SearchMemoryRequest {
    pub query: String,
    pub mode: Option<String>,
    pub limit: Option<u32>,
}

#[derive(Serialize, ToSchema)]
pub struct MemoryStatsDto {
    pub total_memories: u64,
    pub type_counts: Vec<(String, u64)>,
    pub vector_available: bool,
}
