use agent_workflow::subagent::orchestrator::SubagentOrchestrator;
use agent_workflow::subagent::*;

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

    // Try to spawn 3 tasks when max is 2
    let result = orchestrator.check_concurrent_limit(3).await;
    assert!(result.is_err());

    // Should allow 2 tasks
    let result = orchestrator.check_concurrent_limit(2).await;
    assert!(result.is_ok());
}
