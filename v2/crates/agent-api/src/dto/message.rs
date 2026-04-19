use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use utoipa::ToSchema;
use uuid::Uuid;

#[derive(Serialize, ToSchema)]
pub struct MessageDto {
    pub id: Uuid,
    pub session_id: Uuid,
    pub role: String,
    pub content: String,
    pub created_at: DateTime<Utc>,
}

impl From<agent_core::models::message::Message> for MessageDto {
    fn from(m: agent_core::models::message::Message) -> Self {
        Self {
            id: m.id,
            session_id: m.session_id,
            role: m.role.to_string(),
            content: m.content,
            created_at: m.created_at,
        }
    }
}

#[derive(Deserialize, ToSchema)]
pub struct SendMessageRequest {
    pub content: String,
    #[serde(default)]
    pub stream: bool,
}
