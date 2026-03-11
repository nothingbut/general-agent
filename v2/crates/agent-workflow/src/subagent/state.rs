use super::SessionStatus;
use uuid::Uuid;

#[derive(Debug, Clone)]
pub struct SubagentState {
    pub session_id: Uuid,
    pub parent_id: Uuid,
    pub stage_id: String,
    pub status: SessionStatus,
}
