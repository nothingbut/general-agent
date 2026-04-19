use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use utoipa::ToSchema;
use uuid::Uuid;

#[derive(Serialize, ToSchema)]
pub struct SessionDto {
    pub id: Uuid,
    pub title: Option<String>,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
}

impl From<agent_core::models::session::Session> for SessionDto {
    fn from(s: agent_core::models::session::Session) -> Self {
        Self {
            id: s.id,
            title: s.title,
            created_at: s.created_at,
            updated_at: s.updated_at,
        }
    }
}

#[derive(Deserialize, ToSchema)]
pub struct CreateSessionRequest {
    pub title: Option<String>,
}

#[derive(Deserialize, ToSchema)]
pub struct UpdateSessionRequest {
    pub title: String,
}

#[derive(Serialize, ToSchema)]
pub struct SessionStatsDto {
    pub session: SessionDto,
    pub message_count: u64,
}
