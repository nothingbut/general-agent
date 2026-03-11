use agent_workflow::subagent::config::*;
use std::collections::HashMap;
use std::time::Duration;
use uuid::Uuid;

#[test]
fn test_task_type_complexity_mapping() {
    let complexity = TaskComplexity::from_task_type(&TaskType::CodeReview);
    assert_eq!(complexity, TaskComplexity::Medium);

    let complexity = TaskComplexity::from_task_type(&TaskType::Research);
    assert_eq!(complexity, TaskComplexity::Complex);

    let complexity = TaskComplexity::from_task_type(&TaskType::Documentation);
    assert_eq!(complexity, TaskComplexity::Simple);

    let complexity = TaskComplexity::from_task_type(&TaskType::Testing);
    assert_eq!(complexity, TaskComplexity::Simple);

    let complexity = TaskComplexity::from_task_type(&TaskType::Analysis);
    assert_eq!(complexity, TaskComplexity::Medium);

    let complexity = TaskComplexity::from_task_type(&TaskType::Custom);
    assert_eq!(complexity, TaskComplexity::Medium);
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
    assert_eq!(parallel, StageStrategy::Parallel);

    let limited = StageStrategy::ParallelWithLimit(5);
    match limited {
        StageStrategy::ParallelWithLimit(n) => assert_eq!(n, 5),
        _ => panic!("Expected ParallelWithLimit"),
    }
}

// NEW TESTS FOR IMPROVED COVERAGE

#[test]
fn test_task_complexity_select_llm_config_simple() {
    let complexity = TaskComplexity::Simple;
    let config = complexity.select_llm_config();

    assert_eq!(config.provider, "ollama");
    assert_eq!(config.model, "qwen2.5:0.5b");
    assert_eq!(config.max_tokens, 1024);
    assert_eq!(config.temperature, 0.3);
}

#[test]
fn test_task_complexity_select_llm_config_medium() {
    let complexity = TaskComplexity::Medium;
    let config = complexity.select_llm_config();

    assert_eq!(config.provider, "ollama");
    assert_eq!(config.model, "qwen2.5:7b");
    assert_eq!(config.max_tokens, 2048);
    assert_eq!(config.temperature, 0.5);
}

#[test]
fn test_task_complexity_select_llm_config_complex() {
    let complexity = TaskComplexity::Complex;
    let config = complexity.select_llm_config();

    assert_eq!(config.provider, "anthropic");
    assert_eq!(config.model, "claude-3-5-sonnet-20241022");
    assert_eq!(config.max_tokens, 4096);
    assert_eq!(config.temperature, 0.7);
}

#[test]
fn test_task_complexity_custom_variant() {
    let custom_config = LLMConfig {
        provider: "custom".to_string(),
        model: "custom-model".to_string(),
        max_tokens: 8192,
        temperature: 0.9,
    };
    let complexity = TaskComplexity::Custom(custom_config.clone());
    let config = complexity.select_llm_config();

    assert_eq!(config.provider, "custom");
    assert_eq!(config.model, "custom-model");
    assert_eq!(config.max_tokens, 8192);
    assert_eq!(config.temperature, 0.9);
}

#[test]
fn test_llm_config_validate_success() {
    let config = LLMConfig {
        provider: "ollama".to_string(),
        model: "qwen2.5:7b".to_string(),
        max_tokens: 2048,
        temperature: 0.7,
    };

    assert!(config.validate().is_ok());
}

#[test]
fn test_llm_config_validate_temperature_too_low() {
    let config = LLMConfig {
        provider: "ollama".to_string(),
        model: "qwen2.5:7b".to_string(),
        max_tokens: 2048,
        temperature: -0.1,
    };

    assert!(config.validate().is_err());
}

#[test]
fn test_llm_config_validate_temperature_too_high() {
    let config = LLMConfig {
        provider: "ollama".to_string(),
        model: "qwen2.5:7b".to_string(),
        max_tokens: 2048,
        temperature: 2.1,
    };

    assert!(config.validate().is_err());
}

#[test]
fn test_llm_config_validate_max_tokens_zero() {
    let config = LLMConfig {
        provider: "ollama".to_string(),
        model: "qwen2.5:7b".to_string(),
        max_tokens: 0,
        temperature: 0.7,
    };

    assert!(config.validate().is_err());
}

#[test]
fn test_llm_config_validate_empty_provider() {
    let config = LLMConfig {
        provider: "".to_string(),
        model: "qwen2.5:7b".to_string(),
        max_tokens: 2048,
        temperature: 0.7,
    };

    assert!(config.validate().is_err());
}

#[test]
fn test_llm_config_validate_empty_model() {
    let config = LLMConfig {
        provider: "ollama".to_string(),
        model: "".to_string(),
        max_tokens: 2048,
        temperature: 0.7,
    };

    assert!(config.validate().is_err());
}

#[test]
fn test_llm_config_validate_edge_cases() {
    // Temperature at minimum boundary
    let config = LLMConfig {
        provider: "ollama".to_string(),
        model: "qwen2.5:7b".to_string(),
        max_tokens: 1,
        temperature: 0.0,
    };
    assert!(config.validate().is_ok());

    // Temperature at maximum boundary
    let config = LLMConfig {
        provider: "ollama".to_string(),
        model: "qwen2.5:7b".to_string(),
        max_tokens: 1,
        temperature: 2.0,
    };
    assert!(config.validate().is_ok());
}

#[test]
fn test_subagent_task_config_creation() {
    let task_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();

    let task_config = SubagentTaskConfig {
        id: task_id,
        config: SubagentConfig {
            title: "Test Task".to_string(),
            initial_prompt: "Do something".to_string(),
            shared_context: SharedContext::default(),
            llm_config: LLMConfig::default(),
            keep_alive: false,
            timeout: None,
        },
        parent_id,
        stage_id: "stage-1".to_string(),
        priority: 5,
        task_type: TaskType::CodeReview,
    };

    assert_eq!(task_config.id, task_id);
    assert_eq!(task_config.parent_id, parent_id);
    assert_eq!(task_config.stage_id, "stage-1");
    assert_eq!(task_config.priority, 5);
    assert_eq!(task_config.task_type, TaskType::CodeReview);
}

#[test]
fn test_stage_creation() {
    let stage = Stage {
        id: "stage-1".to_string(),
        name: "Initial Stage".to_string(),
        tasks: vec![],
        strategy: StageStrategy::Sequential,
        failure_strategy: FailureStrategy::FailStage,
    };

    assert_eq!(stage.id, "stage-1");
    assert_eq!(stage.name, "Initial Stage");
    assert_eq!(stage.strategy, StageStrategy::Sequential);
    assert_eq!(stage.failure_strategy, FailureStrategy::FailStage);
}

#[test]
fn test_failure_strategy_variants() {
    let ignore = FailureStrategy::IgnoreAndContinue;
    assert_eq!(ignore, FailureStrategy::IgnoreAndContinue);

    let fail = FailureStrategy::FailStage;
    assert_eq!(fail, FailureStrategy::FailStage);

    let retry = FailureStrategy::RetryOnce;
    assert_eq!(retry, FailureStrategy::RetryOnce);

    let ask = FailureStrategy::AskUser;
    assert_eq!(ask, FailureStrategy::AskUser);
}

#[test]
fn test_stage_strategy_sequential() {
    let strategy = StageStrategy::Sequential;
    assert_eq!(strategy, StageStrategy::Sequential);
}

#[test]
fn test_subagent_config_serialization() {
    let config = SubagentConfig {
        title: "Test".to_string(),
        initial_prompt: "Test prompt".to_string(),
        shared_context: SharedContext::default(),
        llm_config: LLMConfig::default(),
        keep_alive: true,
        timeout: Some(Duration::from_secs(60)),
    };

    let json = serde_json::to_string(&config).unwrap();
    let deserialized: SubagentConfig = serde_json::from_str(&json).unwrap();

    assert_eq!(config.title, deserialized.title);
    assert_eq!(config.initial_prompt, deserialized.initial_prompt);
}

#[test]
fn test_subagent_task_config_serialization() {
    let task_config = SubagentTaskConfig {
        id: Uuid::new_v4(),
        config: SubagentConfig {
            title: "Test".to_string(),
            initial_prompt: "Test prompt".to_string(),
            shared_context: SharedContext::default(),
            llm_config: LLMConfig::default(),
            keep_alive: false,
            timeout: None,
        },
        parent_id: Uuid::new_v4(),
        stage_id: "stage-1".to_string(),
        priority: 3,
        task_type: TaskType::Research,
    };

    let json = serde_json::to_string(&task_config).unwrap();
    let deserialized: SubagentTaskConfig = serde_json::from_str(&json).unwrap();

    assert_eq!(task_config.id, deserialized.id);
    assert_eq!(task_config.stage_id, deserialized.stage_id);
}

#[test]
fn test_stage_serialization() {
    let stage = Stage {
        id: "stage-1".to_string(),
        name: "Test Stage".to_string(),
        tasks: vec![],
        strategy: StageStrategy::Parallel,
        failure_strategy: FailureStrategy::RetryOnce,
    };

    let json = serde_json::to_string(&stage).unwrap();
    let deserialized: Stage = serde_json::from_str(&json).unwrap();

    assert_eq!(stage.id, deserialized.id);
    assert_eq!(stage.name, deserialized.name);
    assert_eq!(stage.strategy, deserialized.strategy);
}
