use agent_workflow::subagent::{
    OrchestratorConfig, SubagentOrchestrator, SubagentConfig, LLMConfig, SharedContext,
};
use agent_storage::Database;
use std::time::Duration;
use uuid::Uuid;

async fn setup_test_environment() -> (Database, SubagentOrchestrator) {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let orchestrator = SubagentOrchestrator::new(OrchestratorConfig::default());

    (db, orchestrator)
}

#[tokio::test]
async fn test_create_and_execute_stage() {
    let (_db, orchestrator) = setup_test_environment().await;
    let parent_session_id = Uuid::new_v4();

    let tasks = vec![
        "任务1".to_string(),
        "任务2".to_string(),
        "任务3".to_string(),
    ];

    let config = SubagentConfig {
        title: "Test Stage".to_string(),
        initial_prompt: "Test prompt".to_string(),
        shared_context: SharedContext::default(),
        llm_config: LLMConfig::default(),
        keep_alive: false,
        timeout: Some(Duration::from_secs(10)),
    };

    let stage_id = orchestrator
        .create_and_execute_stage(parent_session_id, tasks, Some(config))
        .await
        .unwrap();

    assert_ne!(stage_id, Uuid::nil());
    assert_eq!(orchestrator.active_count(), 3);

    tokio::time::sleep(Duration::from_millis(100)).await;

    let states = orchestrator.get_subagent_states(parent_session_id);
    assert_eq!(states.len(), 3);
}

#[tokio::test]
async fn test_concurrent_limit_enforcement() {
    let (_db, orchestrator) = setup_test_environment().await;
    let parent_session_id = Uuid::new_v4();

    let tasks: Vec<String> = (0..15)
        .map(|i| format!("任务{}", i))
        .collect();

    let result = orchestrator
        .create_and_execute_stage(parent_session_id, tasks, None)
        .await;

    assert!(result.is_err());
}

#[tokio::test]
async fn test_slot_release_after_completion() {
    let (_db, orchestrator) = setup_test_environment().await;
    let parent_session_id = Uuid::new_v4();

    let tasks = vec!["任务1".to_string(), "任务2".to_string()];

    // Create and execute stage
    let _stage_id = orchestrator
        .create_and_execute_stage(parent_session_id, tasks, None)
        .await
        .unwrap();

    // Immediately after spawning, active_count should be 2
    assert_eq!(orchestrator.active_count(), 2);

    // Wait for tasks to complete (SubagentTask::run is very fast - just inserts state)
    tokio::time::sleep(Duration::from_millis(200)).await;

    // After completion, slots should be released (active_count back to 0)
    assert_eq!(orchestrator.active_count(), 0);
}
