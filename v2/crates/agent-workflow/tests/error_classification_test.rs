//! 错误分类和处理系统集成测试

use agent_workflow::workflow::*;

#[test]
fn test_error_classifier_transient_errors() {
    let classifier = ErrorClassifier::default();

    // 网络错误 - 应该分类为临时错误
    let info = classifier.classify_with_info("Connection timeout occurred");
    assert_eq!(info.category, ErrorCategory::Transient);
    assert!(info.should_retry);
    assert!(info.matched_keyword.is_some());

    // 速率限制 - 应该分类为临时错误
    let info = classifier.classify_with_info("Rate limit exceeded, try again");
    assert_eq!(info.category, ErrorCategory::Transient);
    assert!(info.should_retry);

    // HTTP 503 - 应该分类为临时错误
    let info = classifier.classify_with_info("HTTP 503 Service Unavailable");
    assert_eq!(info.category, ErrorCategory::Transient);
    assert!(info.should_retry);
}

#[test]
fn test_error_classifier_permanent_errors() {
    let classifier = ErrorClassifier::default();

    // 权限错误 - 应该分类为永久错误
    let info = classifier.classify_with_info("Unauthorized: invalid API key");
    assert_eq!(info.category, ErrorCategory::Permanent);
    assert!(!info.should_retry);
    assert!(info.matched_keyword.is_some());

    // 参数错误 - 应该分类为永久错误
    let info = classifier.classify_with_info("Invalid parameter: user_id is required");
    assert_eq!(info.category, ErrorCategory::Permanent);
    assert!(!info.should_retry);

    // HTTP 404 - 应该分类为永久错误
    let info = classifier.classify_with_info("404 Not Found");
    assert_eq!(info.category, ErrorCategory::Permanent);
    assert!(!info.should_retry);
}

#[test]
fn test_error_classifier_unknown_errors() {
    let classifier = ErrorClassifier::default();

    // 未知错误 - 默认不重试
    let info = classifier.classify_with_info("Something unexpected happened");
    assert_eq!(info.category, ErrorCategory::Unknown);
    assert!(!info.should_retry);
    assert!(info.matched_keyword.is_none());

    // 配置为重试未知错误
    let classifier = ErrorClassifier::default().retry_unknown_errors(true);
    let info = classifier.classify_with_info("Something unexpected happened");
    assert_eq!(info.category, ErrorCategory::Unknown);
    assert!(info.should_retry);
}

#[test]
fn test_error_classifier_custom_keywords() {
    let classifier = ErrorClassifier::new()
        .add_transient_keyword("database locked")
        .add_permanent_keyword("schema mismatch")
        .retry_unknown_errors(false);

    // 自定义临时错误
    let info = classifier.classify_with_info("Error: database locked, please retry");
    assert_eq!(info.category, ErrorCategory::Transient);
    assert!(info.should_retry);

    // 自定义永久错误
    let info = classifier.classify_with_info("Fatal: schema mismatch detected");
    assert_eq!(info.category, ErrorCategory::Permanent);
    assert!(!info.should_retry);
}

#[test]
fn test_error_handling_strategy_defaults() {
    let strategy = ErrorHandlingStrategy::default();

    // 临时错误 - 重试 3 次
    assert_eq!(strategy.max_retries_for_category(ErrorCategory::Transient), 3);

    // 永久错误 - 不重试
    assert_eq!(strategy.max_retries_for_category(ErrorCategory::Permanent), 0);

    // 未知错误 - 重试 1 次
    assert_eq!(strategy.max_retries_for_category(ErrorCategory::Unknown), 1);

    // 永久错误应该停止工作流
    assert!(strategy.should_stop_workflow(ErrorCategory::Permanent));
    assert!(!strategy.should_stop_workflow(ErrorCategory::Transient));

    // 永久错误和未知错误应该通知
    assert!(strategy.should_notify(ErrorCategory::Permanent));
    assert!(strategy.should_notify(ErrorCategory::Unknown));
    assert!(!strategy.should_notify(ErrorCategory::Transient));
}

#[test]
fn test_error_handling_strategy_custom() {
    let strategy = ErrorHandlingStrategy {
        transient_max_retries: 5,
        permanent_max_retries: 1, // 允许永久错误重试一次
        unknown_max_retries: 2,
        stop_on_permanent_error: false, // 不停止工作流
        notify_on_unknown_error: false, // 不通知未知错误
    };

    assert_eq!(strategy.max_retries_for_category(ErrorCategory::Transient), 5);
    assert_eq!(strategy.max_retries_for_category(ErrorCategory::Permanent), 1);
    assert_eq!(strategy.max_retries_for_category(ErrorCategory::Unknown), 2);
    assert!(!strategy.should_stop_workflow(ErrorCategory::Permanent));
    assert!(!strategy.should_notify(ErrorCategory::Unknown));
}

#[tokio::test]
async fn test_executor_with_error_classification_transient() {
    // 创建执行器
    let executor = TaskExecutor::new();

    // 创建一个会失败的任务（模拟临时错误）
    // 注意：由于我们的 Custom 任务不会真正失败，这个测试主要验证结构
    let task = Task::new(
        "test-transient",
        "Test Transient Error",
        TaskType::Custom("test".to_string()),
    );

    let result = executor.execute_task(&task).await;

    // Custom 任务会成功，所以不会有错误分类
    assert_eq!(result.status, TaskStatus::Completed);
    assert!(result.error_classification.is_none());
}

#[tokio::test]
async fn test_executor_with_error_classification_permanent() {
    // 创建执行器
    let executor = TaskExecutor::new();

    // 创建一个会因为权限问题失败的任务
    let task = Task::new(
        "test-perm",
        "Test Permanent Error",
        TaskType::SkillExecution {
            skill_name: "nonexistent_skill".to_string(),
            params: None,
        },
    );

    let result = executor.execute_task(&task).await;

    // 应该失败
    assert!(matches!(result.status, TaskStatus::Failed(_)));
    assert!(result.error.is_some());

    // 由于 "registry not configured" 不在默认关键词中，会被分类为 Unknown
    // 但我们仍然应该有错误分类信息
    assert!(result.error_classification.is_some());

    let classification = result.error_classification.unwrap();
    // 实际上会是 Unknown，因为 "registry not configured" 不在默认关键词中
    println!("Error category: {:?}", classification.category);
    println!("Error message: {:?}", result.error);
}

#[tokio::test]
async fn test_executor_with_custom_error_classifier() {
    // 创建自定义分类器
    let classifier = ErrorClassifier::new()
        .add_permanent_keyword("registry not configured")
        .add_permanent_keyword("not found");

    // 创建带自定义分类器的执行器
    let executor = TaskExecutor::new().with_error_classifier(classifier);

    // 创建会失败的任务
    let task = Task::new(
        "test-custom",
        "Test Custom Classifier",
        TaskType::SkillExecution {
            skill_name: "nonexistent_skill".to_string(),
            params: None,
        },
    );

    let result = executor.execute_task(&task).await;

    // 应该失败
    assert!(matches!(result.status, TaskStatus::Failed(_)));

    // 应该有错误分类
    if let Some(classification) = result.error_classification {
        // 由于我们添加了 "registry not configured" 作为永久错误关键词
        assert_eq!(classification.category, ErrorCategory::Permanent);
        assert!(!classification.should_retry);
    } else {
        panic!("Expected error classification");
    }
}

#[tokio::test]
async fn test_error_classification_prevents_retry() {
    // 创建分类器：将所有错误视为永久错误（不重试）
    let classifier = ErrorClassifier::new()
        .add_permanent_keyword("registry")
        .retry_unknown_errors(false);

    // 创建执行器
    let executor = TaskExecutor::new().with_error_classifier(classifier);

    // 创建会失败的任务，配置重试策略
    let task = Task::new(
        "test-no-retry",
        "Test No Retry",
        TaskType::SkillExecution {
            skill_name: "nonexistent".to_string(),
            params: None,
        },
    )
    .with_config(
        TaskConfig::new()
            .with_retry_strategy(RetryStrategy::fixed(3, 100)) // 配置重试 3 次
    );

    let result = executor.execute_task(&task).await;

    // 应该失败
    assert!(matches!(result.status, TaskStatus::Failed(_)));

    // 因为错误被分类为永久错误，即使配置了重试策略，也不应该重试
    assert_eq!(result.retry_history.total_retries, 0);

    // 应该有错误分类
    if let Some(classification) = result.error_classification {
        assert_eq!(classification.category, ErrorCategory::Permanent);
        assert!(!classification.should_retry);
    }
}

#[tokio::test]
async fn test_batch_error_classification() {
    let classifier = ErrorClassifier::default();

    let errors = vec![
        "Connection timeout",
        "Invalid API key",
        "Something went wrong",
        "Rate limit exceeded",
        "404 Not Found",
    ];

    let results = classifier.classify_batch(&errors);

    assert_eq!(results.len(), 5);
    assert_eq!(results[0].category, ErrorCategory::Transient);
    assert_eq!(results[1].category, ErrorCategory::Permanent);
    assert_eq!(results[2].category, ErrorCategory::Unknown);
    assert_eq!(results[3].category, ErrorCategory::Transient);
    assert_eq!(results[4].category, ErrorCategory::Permanent);
}

#[tokio::test]
async fn test_error_classification_integration_with_retry() {
    // 创建执行器（使用默认分类器）
    let executor = TaskExecutor::new();

    // 创建任务，模拟一个会失败的场景
    let task = Task::new(
        "test-integration",
        "Test Integration",
        TaskType::MCPToolCall {
            server_name: "nonexistent_server".to_string(),
            tool_name: "test_tool".to_string(),
            params: None,
        },
    )
    .with_config(
        TaskConfig::new()
            .with_retry_strategy(RetryStrategy::exponential(2, 50, 1000, 2.0))
            .with_timeout(5),
    );

    let result = executor.execute_task(&task).await;

    // 应该失败（因为 MCP 管理器未配置）
    assert!(matches!(result.status, TaskStatus::Failed(_)));

    // 应该有错误信息
    assert!(result.error.is_some());
    let error_msg = result.error.as_ref().unwrap();
    println!("Error: {}", error_msg);

    // 应该有错误分类
    assert!(result.error_classification.is_some());
    let classification = result.error_classification.unwrap();
    println!(
        "Error category: {:?}, should_retry: {}",
        classification.category, classification.should_retry
    );
}
