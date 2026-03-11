use agent_workflow::subagent::{SubagentState, SessionStatus};
use uuid::Uuid;

#[test]
fn test_subagent_state_creation() {
    let session_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();
    let stage_id = "test_stage".to_string();

    let state = SubagentState::new(session_id, parent_id, stage_id.clone());

    assert_eq!(state.session_id, session_id);
    assert_eq!(state.parent_id, parent_id);
    assert_eq!(state.stage_id, stage_id);
    assert_eq!(state.status, SessionStatus::Idle);
    assert_eq!(state.progress, 0.0);
    assert_eq!(state.message_count, 0);
    assert!(state.error.is_none());
    assert!(state.estimated_remaining.is_none());

    // Verify timestamps are set
    assert!(state.started_at <= chrono::Utc::now());
    assert_eq!(state.updated_at, state.started_at);
}

#[test]
fn test_subagent_state_progress_update() {
    let session_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();
    let stage_id = "test_stage".to_string();

    let mut state = SubagentState::new(session_id, parent_id, stage_id);

    let original_updated_at = state.updated_at;

    // Wait a tiny bit to ensure timestamp difference
    std::thread::sleep(std::time::Duration::from_millis(10));

    // Update progress and status
    state.progress = 0.5;
    state.status = SessionStatus::Running;
    state.message_count = 5;
    state.updated_at = chrono::Utc::now();

    assert_eq!(state.progress, 0.5);
    assert_eq!(state.status, SessionStatus::Running);
    assert_eq!(state.message_count, 5);
    assert!(state.updated_at > original_updated_at);
}

#[test]
fn test_subagent_state_with_error() {
    let session_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();
    let stage_id = "test_stage".to_string();

    let mut state = SubagentState::new(session_id, parent_id, stage_id);

    // Set error and failed status
    state.error = Some("Test error occurred".to_string());
    state.status = SessionStatus::Failed;

    assert!(state.error.is_some());
    assert_eq!(state.error.as_ref().unwrap(), "Test error occurred");
    assert_eq!(state.status, SessionStatus::Failed);
}
