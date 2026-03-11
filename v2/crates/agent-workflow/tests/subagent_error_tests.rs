//! Tests for subagent error types

use agent_workflow::subagent::error::{SubagentError, SubagentResult};

#[test]
fn test_subagent_error_display() {
    let error = SubagentError::StageFailed {
        stage: 2,
        reason: "Task execution failed".to_string(),
    };

    let display_str = format!("{}", error);
    assert!(display_str.contains("Stage 2"));
    assert!(display_str.contains("Task execution failed"));
}

#[test]
fn test_timeout_error() {
    let error = SubagentError::Timeout {
        stage: 1,
        duration_ms: 5000,
    };

    let display_str = format!("{}", error);
    assert!(display_str.contains("timed out"));
    assert!(display_str.contains("Stage 1"));
}

#[test]
fn test_error_conversion_from_sqlx() {
    // Create a sqlx error by attempting to parse an invalid connection string
    let sqlx_error = sqlx::Error::Configuration("test error".into());
    let subagent_error: SubagentError = sqlx_error.into();

    match subagent_error {
        SubagentError::DatabaseError(_) => {
            // Expected conversion
        }
        _ => panic!("Expected DatabaseError variant"),
    }
}

#[test]
fn test_subagent_result_ok() {
    let result: SubagentResult<i32> = Ok(42);
    assert_eq!(result.unwrap(), 42);
}

#[test]
fn test_subagent_result_err() {
    let result: SubagentResult<i32> = Err(SubagentError::ShutdownRequested);
    assert!(result.is_err());
}
