//! Tests for SubagentOrchestrator

use agent_workflow::subagent::orchestrator::SubagentOrchestrator;
use agent_workflow::subagent::*;
use uuid::Uuid;

fn create_test_task_config(num: u8) -> SubagentTaskConfig {
    SubagentTaskConfig {
        id: Uuid::new_v4(),
        parent_id: Uuid::new_v4(),
        stage_id: format!("stage-{}", num),
        config: SubagentConfig {
            title: format!("Test Task {}", num),
            initial_prompt: "Test prompt".to_string(),
            shared_context: SharedContext::default(),
            llm_config: LLMConfig::default(),
            keep_alive: false,
            timeout: None,
        },
        priority: 0,
        task_type: TaskType::Testing,
    }
}

#[tokio::test]
async fn test_orchestrator_creation() {
    let config = OrchestratorConfig {
        max_concurrent_subagents: 10,
        max_stages: 5,
        default_timeout_secs: 300,
    };

    let orchestrator = SubagentOrchestrator::new(config);
    assert_eq!(orchestrator.active_count(), 0);
}

#[tokio::test]
async fn test_orchestrator_concurrent_limit() {
    let config = OrchestratorConfig {
        max_concurrent_subagents: 2,
        max_stages: 5,
        default_timeout_secs: 300,
    };

    let orchestrator = SubagentOrchestrator::new(config);

    // Create 3 tasks but max is 2
    let _stage = Stage {
        id: "test".to_string(),
        name: "Test".to_string(),
        tasks: vec![
            create_test_task_config(1),
            create_test_task_config(2),
            create_test_task_config(3),
        ],
        strategy: StageStrategy::Parallel,
        failure_strategy: FailureStrategy::IgnoreAndContinue,
    };

    // Should respect concurrent limit
    let result = orchestrator.check_concurrent_limit(3).await;
    assert!(result.is_err());
}

#[tokio::test]
async fn test_orchestrator_concurrent_limit_success() {
    let config = OrchestratorConfig {
        max_concurrent_subagents: 5,
        max_stages: 5,
        default_timeout_secs: 300,
    };

    let orchestrator = SubagentOrchestrator::new(config);

    // 3 tasks with limit of 5 should succeed
    let result = orchestrator.check_concurrent_limit(3).await;
    assert!(result.is_ok());
}

#[tokio::test]
async fn test_orchestrator_default_config() {
    let config = OrchestratorConfig::default();
    assert_eq!(config.max_concurrent_subagents, 10);
    assert_eq!(config.max_stages, 5);
    assert_eq!(config.default_timeout_secs, 300);
}

#[tokio::test]
async fn test_orchestrator_state_map_access() {
    let config = OrchestratorConfig::default();
    let orchestrator = SubagentOrchestrator::new(config);

    // Should be able to access state map
    let state_map = orchestrator.state_map();
    assert_eq!(state_map.len(), 0);
}
