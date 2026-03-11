use agent_workflow::subagent::channels::*;
use agent_workflow::subagent::SessionStatus;
use std::time::Duration;
use uuid::Uuid;

#[tokio::test]
async fn test_result_metadata_creation() {
    let metadata = ResultMetadata {
        execution_time: Duration::from_secs(10),
        token_count: 1000,
        model_used: "qwen2.5:7b".to_string(),
        error_count: 0,
        tool_calls: vec!["read_file".to_string()],
        memory_used: 50_000_000,
    };

    assert_eq!(metadata.token_count, 1000);
    assert_eq!(metadata.error_count, 0);
}

#[tokio::test]
async fn test_task_result_structure() {
    let result = TaskResult {
        session_id: Uuid::new_v4(),
        status: SessionStatus::Completed,
        output: "Task completed".to_string(),
        metadata: ResultMetadata {
            execution_time: Duration::from_secs(5),
            token_count: 500,
            model_used: "test".to_string(),
            error_count: 0,
            tool_calls: vec![],
            memory_used: 10_000_000,
        },
    };

    assert_eq!(result.status, SessionStatus::Completed);
    assert_eq!(result.output, "Task completed");
}

#[tokio::test]
async fn test_subagent_command_types() {
    let config = agent_workflow::subagent::SubagentTaskConfig {
        id: Uuid::new_v4(),
        config: agent_workflow::subagent::SubagentConfig {
            title: "Test".to_string(),
            initial_prompt: "Test".to_string(),
            shared_context: Default::default(),
            llm_config: Default::default(),
            keep_alive: false,
            timeout: None,
        },
        parent_id: Uuid::new_v4(),
        stage_id: "test".to_string(),
        priority: 0,
        task_type: agent_workflow::subagent::TaskType::Testing,
    };

    let cmd = SubagentCommand::Start(config);
    assert!(matches!(cmd, SubagentCommand::Start(_)));
}

#[tokio::test]
async fn test_task_result_serialization() {
    let result = TaskResult {
        session_id: Uuid::new_v4(),
        status: SessionStatus::Completed,
        output: "Test output".to_string(),
        metadata: ResultMetadata {
            execution_time: Duration::from_secs(5),
            token_count: 500,
            model_used: "test-model".to_string(),
            error_count: 0,
            tool_calls: vec!["read".to_string(), "write".to_string()],
            memory_used: 1024,
        },
    };

    // Serialize to JSON
    let json = serde_json::to_string(&result).expect("Failed to serialize");

    // Deserialize back
    let deserialized: TaskResult = serde_json::from_str(&json).expect("Failed to deserialize");

    // Verify round-trip
    assert_eq!(result.session_id, deserialized.session_id);
    assert_eq!(result.status, deserialized.status);
    assert_eq!(result.output, deserialized.output);
    assert_eq!(result.metadata.token_count, deserialized.metadata.token_count);
}

#[tokio::test]
async fn test_result_metadata_serialization() {
    let metadata = ResultMetadata {
        execution_time: Duration::from_secs(10),
        token_count: 1000,
        model_used: "qwen2.5:7b".to_string(),
        error_count: 2,
        tool_calls: vec!["read_file".to_string()],
        memory_used: 50_000_000,
    };

    // Serialize to JSON
    let json = serde_json::to_string(&metadata).expect("Failed to serialize");

    // Deserialize back
    let deserialized: ResultMetadata = serde_json::from_str(&json).expect("Failed to deserialize");

    // Verify round-trip
    assert_eq!(metadata.token_count, deserialized.token_count);
    assert_eq!(metadata.error_count, deserialized.error_count);
    assert_eq!(metadata.model_used, deserialized.model_used);
}

#[tokio::test]
async fn test_subagent_command_clone() {
    let uuid = Uuid::new_v4();
    let cmd = SubagentCommand::Cancel(uuid);

    // Clone the command
    let cloned = cmd.clone();

    // Verify both match
    match (cmd, cloned) {
        (SubagentCommand::Cancel(id1), SubagentCommand::Cancel(id2)) => {
            assert_eq!(id1, id2);
        }
        _ => panic!("Clone didn't preserve command type"),
    }
}

#[tokio::test]
async fn test_subagent_command_serialization() {
    let uuid = Uuid::new_v4();
    let cmd = SubagentCommand::Cancel(uuid);

    // Serialize to JSON
    let json = serde_json::to_string(&cmd).expect("Failed to serialize");

    // Deserialize back
    let deserialized: SubagentCommand = serde_json::from_str(&json).expect("Failed to deserialize");

    // Verify round-trip
    match (cmd, deserialized) {
        (SubagentCommand::Cancel(id1), SubagentCommand::Cancel(id2)) => {
            assert_eq!(id1, id2);
        }
        _ => panic!("Deserialization didn't preserve command type"),
    }
}
