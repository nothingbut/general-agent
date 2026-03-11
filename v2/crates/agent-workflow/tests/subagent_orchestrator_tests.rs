//! Tests for SubagentOrchestrator

use agent_workflow::subagent::orchestrator::SubagentOrchestrator;
use agent_workflow::subagent::*;
use std::sync::Arc;
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
    let result = orchestrator.check_concurrent_limit(3);
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
    let result = orchestrator.check_concurrent_limit(3);
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

#[tokio::test]
async fn test_try_reserve_slots_atomic() {
    let config = OrchestratorConfig {
        max_concurrent_subagents: 5,
        max_stages: 5,
        default_timeout_secs: 300,
    };
    let orchestrator = Arc::new(SubagentOrchestrator::new(config));

    // Successfully reserve 3 slots
    assert!(orchestrator.try_reserve_slots(3).is_ok());
    assert_eq!(orchestrator.active_count(), 3);

    // Cannot reserve 3 more (would exceed 5)
    assert!(orchestrator.try_reserve_slots(3).is_err());
    assert_eq!(orchestrator.active_count(), 3); // Count unchanged

    // Release 2 slots
    orchestrator.release_slots(2);
    assert_eq!(orchestrator.active_count(), 1);

    // Now can reserve 3
    assert!(orchestrator.try_reserve_slots(3).is_ok());
    assert_eq!(orchestrator.active_count(), 4);
}

#[tokio::test]
async fn test_overflow_protection() {
    let config = OrchestratorConfig {
        max_concurrent_subagents: 100,
        max_stages: 5,
        default_timeout_secs: 300,
    };
    let orchestrator = SubagentOrchestrator::new(config);

    // First reserve some slots
    assert!(orchestrator.try_reserve_slots(50).is_ok());
    assert_eq!(orchestrator.active_count(), 50);

    // Try to reserve usize::MAX tasks (should catch overflow when adding to 50)
    let result = orchestrator.try_reserve_slots(usize::MAX);
    assert!(result.is_err());

    // Count should be unchanged after failed reservation
    assert_eq!(orchestrator.active_count(), 50);
}

#[tokio::test]
async fn test_check_concurrent_limit_overflow_protection() {
    let config = OrchestratorConfig {
        max_concurrent_subagents: 100,
        max_stages: 5,
        default_timeout_secs: 300,
    };
    let orchestrator = SubagentOrchestrator::new(config);

    // Reserve some slots first
    assert!(orchestrator.try_reserve_slots(50).is_ok());

    // Try to check with usize::MAX (should catch overflow when adding to 50)
    let result = orchestrator.check_concurrent_limit(usize::MAX);
    assert!(result.is_err());
}

#[tokio::test]
async fn test_channel_accessor_methods() {
    let config = OrchestratorConfig::default();
    let mut orchestrator = SubagentOrchestrator::new(config);

    // Test command receiver can be taken
    let command_rx = orchestrator.take_command_rx();
    assert!(command_rx.is_some());

    // Can only take once
    let command_rx2 = orchestrator.take_command_rx();
    assert!(command_rx2.is_none());

    // Test result receiver can be taken
    let result_rx = orchestrator.take_result_rx();
    assert!(result_rx.is_some());

    // Can only take once
    let result_rx2 = orchestrator.take_result_rx();
    assert!(result_rx2.is_none());

    // Test command sender can be cloned
    let _cmd_tx1 = orchestrator.command_tx();
    let _cmd_tx2 = orchestrator.command_tx();

    // Test result sender can be cloned
    let _res_tx1 = orchestrator.result_tx();
    let _res_tx2 = orchestrator.result_tx();

    // Test shutdown subscription
    let _shutdown_rx1 = orchestrator.subscribe_shutdown();
    let _shutdown_rx2 = orchestrator.subscribe_shutdown();
}

#[tokio::test]
async fn test_concurrent_reservation_race() {
    use tokio::task;

    let config = OrchestratorConfig {
        max_concurrent_subagents: 10,
        max_stages: 5,
        default_timeout_secs: 300,
    };
    let orchestrator = Arc::new(SubagentOrchestrator::new(config));

    // Spawn multiple tasks trying to reserve slots concurrently
    let mut handles = vec![];
    for _ in 0..20 {
        let orch = Arc::clone(&orchestrator);
        let handle = task::spawn(async move {
            orch.try_reserve_slots(1)
        });
        handles.push(handle);
    }

    // Collect results
    let mut successes = 0;
    let mut failures = 0;
    for handle in handles {
        match handle.await.unwrap() {
            Ok(_) => successes += 1,
            Err(_) => failures += 1,
        }
    }

    // Exactly 10 should succeed (the limit), 10 should fail
    assert_eq!(successes, 10);
    assert_eq!(failures, 10);
    assert_eq!(orchestrator.active_count(), 10);
}
