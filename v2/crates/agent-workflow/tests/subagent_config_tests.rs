use agent_workflow::subagent::config::*;
use std::collections::HashMap;
use std::time::Duration;

#[test]
fn test_task_type_complexity_mapping() {
    let complexity = TaskComplexity::from_task_type(&TaskType::CodeReview);
    assert!(matches!(complexity, TaskComplexity::Medium));

    let complexity = TaskComplexity::from_task_type(&TaskType::Research);
    assert!(matches!(complexity, TaskComplexity::Complex));
}

#[test]
fn test_shared_context_creation() {
    let mut variables = HashMap::new();
    variables.insert("key".to_string(), "value".to_string());

    let context = SharedContext {
        recent_messages: Some(5),
        variables,
        system_prompt: Some("Test prompt".to_string()),
    };

    assert_eq!(context.recent_messages, Some(5));
    assert_eq!(context.variables.get("key"), Some(&"value".to_string()));
}

#[test]
fn test_subagent_config_with_timeout() {
    let config = SubagentConfig {
        title: "Test Task".to_string(),
        initial_prompt: "Do something".to_string(),
        shared_context: SharedContext::default(),
        llm_config: LLMConfig::default(),
        keep_alive: false,
        timeout: Some(Duration::from_secs(300)),
    };

    assert_eq!(config.title, "Test Task");
    assert_eq!(config.timeout, Some(Duration::from_secs(300)));
}

#[test]
fn test_stage_strategy_types() {
    let parallel = StageStrategy::Parallel;
    assert!(matches!(parallel, StageStrategy::Parallel));

    let limited = StageStrategy::ParallelWithLimit(5);
    match limited {
        StageStrategy::ParallelWithLimit(n) => assert_eq!(n, 5),
        _ => panic!("Expected ParallelWithLimit"),
    }
}
