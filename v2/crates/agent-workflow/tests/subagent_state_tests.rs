use agent_workflow::subagent::{SubagentState, SessionStatus};
use uuid::Uuid;

#[test]
fn test_subagent_state_creation() {
    let session_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();
    let stage_id = "test_stage".to_string();

    let state = SubagentState::new(session_id, parent_id, stage_id.clone())
        .expect("should create state with valid inputs");

    assert_eq!(state.session_id(), session_id);
    assert_eq!(state.parent_id(), parent_id);
    assert_eq!(state.stage_id(), stage_id);
    assert_eq!(state.status(), SessionStatus::Idle);
    assert_eq!(state.progress(), 0.0);
    assert_eq!(state.message_count(), 0);
    assert!(state.error().is_none());
    assert!(state.estimated_remaining().is_none());

    // Verify timestamps are set
    assert!(state.started_at() <= chrono::Utc::now());
    assert_eq!(state.updated_at(), state.started_at());
}

#[test]
fn test_subagent_state_progress_update() {
    let session_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();
    let stage_id = "test_stage".to_string();

    let state = SubagentState::new(session_id, parent_id, stage_id)
        .expect("should create state");

    let original_updated_at = state.updated_at();

    // Wait a tiny bit to ensure timestamp difference
    std::thread::sleep(std::time::Duration::from_millis(10));

    // Update progress and status using immutable methods
    let state = state
        .with_progress(0.5)
        .expect("should update progress")
        .with_status(SessionStatus::Running)
        .increment_message_count()
        .increment_message_count()
        .increment_message_count()
        .increment_message_count()
        .increment_message_count();

    assert_eq!(state.progress(), 0.5);
    assert_eq!(state.status(), SessionStatus::Running);
    assert_eq!(state.message_count(), 5);
    assert!(state.updated_at() > original_updated_at);
}

#[test]
fn test_subagent_state_with_error() {
    let session_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();
    let stage_id = "test_stage".to_string();

    let state = SubagentState::new(session_id, parent_id, stage_id)
        .expect("should create state");

    // Set error and failed status using immutable methods
    let state = state
        .with_error("Test error occurred".to_string())
        .with_status(SessionStatus::Failed);

    assert!(state.error().is_some());
    assert_eq!(state.error().unwrap(), "Test error occurred");
    assert_eq!(state.status(), SessionStatus::Failed);
}

#[test]
fn test_subagent_state_empty_stage_id() {
    let session_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();

    let result = SubagentState::new(session_id, parent_id, "".to_string());
    assert!(result.is_err());
    assert!(result.unwrap_err().to_string().contains("stage_id cannot be empty"));

    let result = SubagentState::new(session_id, parent_id, "   ".to_string());
    assert!(result.is_err());
    assert!(result.unwrap_err().to_string().contains("stage_id cannot be empty"));
}

#[test]
fn test_subagent_state_progress_bounds_validation() {
    let session_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();
    let stage_id = "test_stage".to_string();

    let state = SubagentState::new(session_id, parent_id, stage_id)
        .expect("should create state");

    // Valid progress values
    let state = state.with_progress(0.0).expect("0.0 should be valid");
    assert_eq!(state.progress(), 0.0);

    let state = state.with_progress(0.5).expect("0.5 should be valid");
    assert_eq!(state.progress(), 0.5);

    let state = state.with_progress(1.0).expect("1.0 should be valid");
    assert_eq!(state.progress(), 1.0);

    // Invalid progress values (clone to test multiple error cases)
    let result = state.clone().with_progress(-0.1);
    assert!(result.is_err());
    assert!(result.unwrap_err().to_string().contains("progress must be"));

    let result = state.clone().with_progress(1.1);
    assert!(result.is_err());
    assert!(result.unwrap_err().to_string().contains("progress must be"));

    let result = state.clone().with_progress(f32::NAN);
    assert!(result.is_err());
    assert!(result.unwrap_err().to_string().contains("progress must be"));
}

#[test]
fn test_subagent_state_timestamp_invariants() {
    let session_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();
    let stage_id = "test_stage".to_string();

    let state = SubagentState::new(session_id, parent_id, stage_id)
        .expect("should create state");

    let started_at = state.started_at();

    std::thread::sleep(std::time::Duration::from_millis(10));

    let state = state.with_status(SessionStatus::Running);

    // updated_at should be >= started_at
    assert!(state.updated_at() >= started_at);

    // started_at should not change
    assert_eq!(state.started_at(), started_at);
}

#[test]
fn test_subagent_state_serialization() {
    let session_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();
    let stage_id = "test_stage".to_string();

    let state = SubagentState::new(session_id, parent_id, stage_id)
        .expect("should create state")
        .with_progress(0.75)
        .expect("should update progress")
        .with_status(SessionStatus::Running);

    // Serialize
    let json = serde_json::to_string(&state).expect("should serialize");

    // Deserialize
    let deserialized: SubagentState = serde_json::from_str(&json).expect("should deserialize");

    assert_eq!(deserialized.session_id(), state.session_id());
    assert_eq!(deserialized.parent_id(), state.parent_id());
    assert_eq!(deserialized.stage_id(), state.stage_id());
    assert_eq!(deserialized.status(), state.status());
    assert_eq!(deserialized.progress(), state.progress());
}
