//! LLM 工作流集成测试
//!
//! 本测试展示如何在工作流中集成 LLM 调用

use agent_workflow::workflow::*;

#[tokio::test]
#[ignore] // 需要真实的 API Key，在 CI 中跳过
async fn test_llm_workflow() {
    // 只有在设置了 ANTHROPIC_API_KEY 时才运行
    if std::env::var("ANTHROPIC_API_KEY").is_err() {
        println!("Skipping LLM test: ANTHROPIC_API_KEY not set");
        return;
    }

    // 创建一个包含 LLM 调用的工作流
    let mut workflow = Workflow::new("llm-workflow", "LLM Workflow Example");

    // 任务 1: LLM 调用 - 回答问题
    let task1 = Task::new(
        "math-question",
        "Math Question",
        TaskType::LLMCall {
            prompt: "What is 5 + 7? Reply with just the number.".to_string(),
            model: Some("claude-3-5-sonnet-20241022".to_string()),
            temperature: Some(0.0),
            max_tokens: Some(10),
        },
    );

    // 任务 2: 自定义任务 - 验证结果（依赖任务1）
    let task2 = Task::new(
        "verify-result",
        "Verify Result",
        TaskType::Custom("verification".to_string()),
    )
    .with_dependency("math-question");

    workflow.add_task(task1);
    workflow.add_task(task2);

    // 执行工作流
    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let executor = TaskExecutor::new();

    let result = orchestrator.execute(&executor).await.unwrap();

    // 验证结果
    assert_eq!(result.task_results.len(), 2);

    let task1_result = &result.task_results["math-question"];
    assert_eq!(task1_result.status, TaskStatus::Completed);
    assert!(task1_result.output.is_some());

    let output = task1_result.output.as_ref().unwrap();
    println!("LLM Response: {}", output);
    assert!(
        output.contains("12"),
        "Expected '12' in output, got: {}",
        output
    );

    let task2_result = &result.task_results["verify-result"];
    assert_eq!(task2_result.status, TaskStatus::Completed);
}

#[tokio::test]
#[ignore] // 需要真实的 API Key
async fn test_multi_llm_workflow() {
    if std::env::var("ANTHROPIC_API_KEY").is_err() {
        println!("Skipping LLM test: ANTHROPIC_API_KEY not set");
        return;
    }

    let mut workflow = Workflow::new("multi-llm", "Multi-LLM Workflow");

    // 任务 1: 生成一个随机数字
    let task1 = Task::new(
        "generate-number",
        "Generate Number",
        TaskType::LLMCall {
            prompt: "Pick a random number between 1 and 10. Reply with just the number."
                .to_string(),
            model: Some("claude-3-5-sonnet-20241022".to_string()),
            temperature: Some(0.8), // 更高的温度以增加随机性
            max_tokens: Some(5),
        },
    );

    // 任务 2 & 3: 并行执行两个独立的 LLM 调用
    let task2 = Task::new(
        "color-question",
        "Ask Color",
        TaskType::LLMCall {
            prompt: "What is your favorite color? Reply in one word.".to_string(),
            model: None, // 使用默认模型
            temperature: Some(0.5),
            max_tokens: Some(10),
        },
    );

    let task3 = Task::new(
        "animal-question",
        "Ask Animal",
        TaskType::LLMCall {
            prompt: "Name a common pet animal. Reply in one word.".to_string(),
            model: None,
            temperature: Some(0.5),
            max_tokens: Some(10),
        },
    );

    // 任务 4: 汇总所有结果（依赖前面3个任务）
    let task4 = Task::new(
        "summarize",
        "Summarize",
        TaskType::Custom("summary".to_string()),
    )
    .with_dependency("generate-number")
    .with_dependency("color-question")
    .with_dependency("animal-question");

    workflow.add_task(task1);
    workflow.add_task(task2);
    workflow.add_task(task3);
    workflow.add_task(task4);

    // 执行工作流
    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let executor = TaskExecutor::new();

    let result = orchestrator.execute(&executor).await.unwrap();

    // 验证所有任务完成
    assert_eq!(result.task_results.len(), 4);
    for (task_id, task_result) in &result.task_results {
        println!(
            "Task {}: {:?} - {:?}",
            task_id, task_result.status, task_result.output
        );
        assert_eq!(task_result.status, TaskStatus::Completed);
    }

    println!("Total execution time: {}ms", result.execution_time_ms);
}
