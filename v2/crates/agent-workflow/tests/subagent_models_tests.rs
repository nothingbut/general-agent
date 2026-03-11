//! Tests for subagent session models

use agent_workflow::subagent::models::{SessionStatus, SessionType};

#[test]
fn test_session_status_transitions() {
    // Test all SessionStatus variants exist
    let idle = SessionStatus::Idle;
    let running = SessionStatus::Running;
    let completed = SessionStatus::Completed;
    let failed = SessionStatus::Failed;
    let cancelled = SessionStatus::Cancelled;

    // Test they are distinct
    assert_ne!(idle, running);
    assert_ne!(running, completed);
    assert_ne!(completed, failed);
    assert_ne!(failed, cancelled);

    // Test Default implementation
    let default_status = SessionStatus::default();
    assert_eq!(default_status, SessionStatus::Idle);
}

#[test]
fn test_session_type_distinction() {
    // Test both SessionType variants exist
    let main_session = SessionType::Main;
    let subagent_session = SessionType::Subagent;

    // Test they are distinct
    assert_ne!(main_session, subagent_session);

    // Test Copy trait
    let main_copy = main_session;
    assert_eq!(main_session, main_copy);
}

#[test]
fn test_session_status_serialization() {
    // Test SessionStatus can be serialized and deserialized
    let completed = SessionStatus::Completed;
    let serialized = serde_json::to_string(&completed).unwrap();

    assert!(serialized.contains("Completed"));

    let deserialized: SessionStatus = serde_json::from_str(&serialized).unwrap();
    assert_eq!(deserialized, completed);

    // Test multiple variants
    let statuses = vec![
        SessionStatus::Idle,
        SessionStatus::Running,
        SessionStatus::Completed,
        SessionStatus::Failed,
        SessionStatus::Cancelled,
    ];

    for status in statuses {
        let json = serde_json::to_string(&status).unwrap();
        let recovered: SessionStatus = serde_json::from_str(&json).unwrap();
        assert_eq!(recovered, status);
    }
}

#[test]
fn test_session_type_serialization() {
    // Test SessionType can be serialized and deserialized
    let main = SessionType::Main;
    let serialized = serde_json::to_string(&main).unwrap();

    assert!(serialized.contains("Main"));

    let deserialized: SessionType = serde_json::from_str(&serialized).unwrap();
    assert_eq!(deserialized, main);

    // Test both variants
    let types = vec![SessionType::Main, SessionType::Subagent];

    for session_type in types {
        let json = serde_json::to_string(&session_type).unwrap();
        let recovered: SessionType = serde_json::from_str(&json).unwrap();
        assert_eq!(recovered, session_type);
    }
}

#[test]
fn test_session_models_are_send_sync() {
    // Compile-time assertion that our types implement Send + Sync
    fn assert_send_sync<T: Send + Sync>() {}
    assert_send_sync::<SessionStatus>();
    assert_send_sync::<SessionType>();
}
