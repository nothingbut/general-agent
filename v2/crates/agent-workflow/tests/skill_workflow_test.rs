//! Skills 工作流集成测试
//!
//! 本测试展示如何在工作流中集成 Skills 技能系统

use agent_workflow::workflow::*;
use agent_skills::{SkillDefinition, SkillParameter, SkillRegistry};
use std::sync::Arc;

/// 创建测试技能注册表
fn create_test_registry() -> SkillRegistry {
    let mut registry = SkillRegistry::new();

    // 技能 1: 欢迎消息
    let mut greeting = SkillDefinition::new(
        "greeting".to_string(),
        "Generate a greeting message".to_string(),
    );
    greeting.content = "Hello, {name}! Welcome to {place}.".to_string();
    greeting.parameters.push(SkillParameter::new(
        "name".to_string(),
        "string".to_string(),
        true,
        "User's name".to_string(),
    ));
    greeting.parameters.push(
        SkillParameter::new(
            "place".to_string(),
            "string".to_string(),
            false,
            "Place name".to_string(),
        )
        .with_default("our community".to_string()),
    );
    registry.register(greeting);

    // 技能 2: 总结信息
    let mut summary = SkillDefinition::new(
        "summary".to_string(),
        "Create a summary".to_string(),
    );
    summary.content = "Summary: {title}\n\nDetails: {content}".to_string();
    summary.parameters.push(SkillParameter::new(
        "title".to_string(),
        "string".to_string(),
        true,
        "Summary title".to_string(),
    ));
    summary.parameters.push(SkillParameter::new(
        "content".to_string(),
        "string".to_string(),
        true,
        "Summary content".to_string(),
    ));
    registry.register(summary);

    // 技能 3: 格式化日期
    let mut format_date = SkillDefinition::new(
        "format_date".to_string(),
        "Format a date string".to_string(),
    );
    format_date.content = "Formatted date: {date} in {format} format".to_string();
    format_date.parameters.push(SkillParameter::new(
        "date".to_string(),
        "string".to_string(),
        true,
        "Date to format".to_string(),
    ));
    format_date.parameters.push(
        SkillParameter::new(
            "format".to_string(),
            "string".to_string(),
            false,
            "Date format".to_string(),
        )
        .with_default("ISO8601".to_string()),
    );
    registry.register(format_date);

    registry
}

#[tokio::test]
async fn test_simple_skill_workflow() {
    let registry = create_test_registry();
    let executor = TaskExecutor::with_skill_registry(Arc::new(registry));

    // 创建工作流
    let mut workflow = Workflow::new("skill-workflow", "Skills Workflow Example");

    // 任务 1: 生成欢迎消息
    let task1 = Task::new(
        "greeting",
        "Generate Greeting",
        TaskType::SkillExecution {
            skill_name: "greeting".to_string(),
            params: Some(serde_json::json!({
                "name": "Alice",
                "place": "the team"
            })),
        },
    );

    workflow.add_task(task1);

    // 执行工作流
    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let result = orchestrator.execute(&executor).await.unwrap();

    // 验证结果
    assert_eq!(result.task_results.len(), 1);
    let task_result = &result.task_results["greeting"];
    assert_eq!(task_result.status, TaskStatus::Completed);
    assert!(task_result.output.is_some());
    assert_eq!(
        task_result.output.as_ref().unwrap(),
        "Hello, Alice! Welcome to the team."
    );
}

#[tokio::test]
async fn test_multi_skill_workflow() {
    let registry = create_test_registry();
    let executor = TaskExecutor::with_skill_registry(Arc::new(registry));

    // 创建工作流
    let mut workflow = Workflow::new("multi-skill", "Multi-Skills Workflow");

    // 任务 1: 欢迎消息
    let task1 = Task::new(
        "greeting",
        "Generate Greeting",
        TaskType::SkillExecution {
            skill_name: "greeting".to_string(),
            params: Some(serde_json::json!({
                "name": "Bob"
                // 使用默认的 place
            })),
        },
    );

    // 任务 2: 格式化日期（并行于任务1）
    let task2 = Task::new(
        "date",
        "Format Date",
        TaskType::SkillExecution {
            skill_name: "format_date".to_string(),
            params: Some(serde_json::json!({
                "date": "2026-03-13",
                "format": "YYYY-MM-DD"
            })),
        },
    );

    // 任务 3: 创建总结（依赖任务1和2）
    let task3 = Task::new(
        "summary",
        "Create Summary",
        TaskType::SkillExecution {
            skill_name: "summary".to_string(),
            params: Some(serde_json::json!({
                "title": "Daily Report",
                "content": "Tasks completed successfully"
            })),
        },
    )
    .with_dependency("greeting")
    .with_dependency("date");

    workflow.add_task(task1);
    workflow.add_task(task2);
    workflow.add_task(task3);

    // 执行工作流
    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let result = orchestrator.execute(&executor).await.unwrap();

    // 验证结果
    assert_eq!(result.task_results.len(), 3);

    // 任务1 - 欢迎消息
    let greeting_result = &result.task_results["greeting"];
    assert_eq!(greeting_result.status, TaskStatus::Completed);
    assert_eq!(
        greeting_result.output.as_ref().unwrap(),
        "Hello, Bob! Welcome to our community."
    );

    // 任务2 - 日期格式化
    let date_result = &result.task_results["date"];
    assert_eq!(date_result.status, TaskStatus::Completed);
    assert_eq!(
        date_result.output.as_ref().unwrap(),
        "Formatted date: 2026-03-13 in YYYY-MM-DD format"
    );

    // 任务3 - 总结
    let summary_result = &result.task_results["summary"];
    assert_eq!(summary_result.status, TaskStatus::Completed);
    assert_eq!(
        summary_result.output.as_ref().unwrap(),
        "Summary: Daily Report\n\nDetails: Tasks completed successfully"
    );
}

#[tokio::test]
async fn test_skill_with_default_parameter() {
    let registry = create_test_registry();
    let executor = TaskExecutor::with_skill_registry(Arc::new(registry));

    // 创建任务（不提供可选参数，使用默认值）
    let task = Task::new(
        "greeting",
        "Generate Greeting",
        TaskType::SkillExecution {
            skill_name: "greeting".to_string(),
            params: Some(serde_json::json!({
                "name": "Charlie"
                // 不提供 place，将使用默认值 "our community"
            })),
        },
    );

    let result = executor.execute_task(&task).await;

    assert_eq!(result.status, TaskStatus::Completed);
    assert_eq!(
        result.output.as_ref().unwrap(),
        "Hello, Charlie! Welcome to our community."
    );
}

#[tokio::test]
async fn test_skill_missing_required_parameter() {
    let registry = create_test_registry();
    let executor = TaskExecutor::with_skill_registry(Arc::new(registry));

    // 创建任务（缺少必需参数）
    let task = Task::new(
        "summary",
        "Create Summary",
        TaskType::SkillExecution {
            skill_name: "summary".to_string(),
            params: Some(serde_json::json!({
                "title": "Report"
                // 缺少必需参数 "content"
            })),
        },
    );

    let result = executor.execute_task(&task).await;

    assert!(matches!(result.status, TaskStatus::Failed(_)));
    assert!(result.error.is_some());
    let error = result.error.unwrap();
    assert!(
        error.contains("missing") || error.contains("content"),
        "Expected parameter error, got: {}",
        error
    );
}

#[tokio::test]
async fn test_skill_not_found() {
    let registry = create_test_registry();
    let executor = TaskExecutor::with_skill_registry(Arc::new(registry));

    // 创建任务（技能不存在）
    let task = Task::new(
        "unknown",
        "Unknown Skill",
        TaskType::SkillExecution {
            skill_name: "nonexistent_skill".to_string(),
            params: None,
        },
    );

    let result = executor.execute_task(&task).await;

    assert!(matches!(result.status, TaskStatus::Failed(_)));
    assert!(result.error.is_some());
    let error = result.error.unwrap();
    assert!(
        error.contains("not found") || error.contains("nonexistent"),
        "Expected skill not found error, got: {}",
        error
    );
}
