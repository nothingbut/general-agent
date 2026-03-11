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
