//! Workflow 集成测试 - 测试完整的工作流执行生命周期

use agent_workflow::workflow::*;

/// 辅助函数：创建带依赖的任务
fn task_with_deps(id: &str, deps: Vec<&str>) -> Task {
    let mut task = Task::new(id, format!("Task {}", id), TaskType::Custom("test".to_string()));
    for dep in deps {
        task.dependencies.push(dep.to_string());
    }
    task
}

#[tokio::test]
async fn test_simple_workflow_execution() {
    // 创建工作流：A -> B -> C
    let mut workflow = Workflow::new("test-workflow", "Test Workflow");
    workflow.add_task(task_with_deps("A", vec![]));
    workflow.add_task(task_with_deps("B", vec!["A"]));
    workflow.add_task(task_with_deps("C", vec!["B"]));

    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let executor = TaskExecutor::new();

    let result = orchestrator.execute(&executor).await.unwrap();

    // 验证所有任务完成
    assert_eq!(result.task_results.len(), 3);
    assert_eq!(
        result.task_results["A"].status,
        TaskStatus::Completed
    );
    assert_eq!(
        result.task_results["B"].status,
        TaskStatus::Completed
    );
    assert_eq!(
        result.task_results["C"].status,
        TaskStatus::Completed
    );

    // 验证输出存在
    assert!(result.task_results["A"].output.is_some());
    assert!(result.task_results["B"].output.is_some());
    assert!(result.task_results["C"].output.is_some());
}

#[tokio::test]
async fn test_parallel_task_execution() {
    // 创建工作流：A, B, C 并行 -> D 依赖所有
    let mut workflow = Workflow::new("parallel-wf", "Parallel Workflow");
    workflow.add_task(task_with_deps("A", vec![]));
    workflow.add_task(task_with_deps("B", vec![]));
    workflow.add_task(task_with_deps("C", vec![]));
    workflow.add_task(task_with_deps("D", vec!["A", "B", "C"]));

    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let executor = TaskExecutor::new();

    let start = std::time::Instant::now();
    let result = orchestrator.execute(&executor).await.unwrap();
    let duration = start.elapsed();

    // 验证所有任务完成
    assert_eq!(result.task_results.len(), 4);
    for (task_id, task_result) in &result.task_results {
        assert_eq!(
            task_result.status,
            TaskStatus::Completed,
            "Task {} should be completed",
            task_id
        );
    }

    // 验证并行执行（总时间应该小于串行执行）
    // A, B, C 应该并行执行（各约 10ms），D 等待后执行（约 10ms）
    // 总时间应该约 20-30ms（而串行需要 40ms+）
    assert!(
        duration.as_millis() < 100,
        "Parallel execution took too long: {}ms",
        duration.as_millis()
    );
}

#[tokio::test]
async fn test_diamond_dag_workflow() {
    // 创建钻石形 DAG：
    //     A
    //    / \
    //   B   C
    //    \ /
    //     D
    let mut workflow = Workflow::new("diamond-wf", "Diamond Workflow");
    workflow.add_task(task_with_deps("A", vec![]));
    workflow.add_task(task_with_deps("B", vec!["A"]));
    workflow.add_task(task_with_deps("C", vec!["A"]));
    workflow.add_task(task_with_deps("D", vec!["B", "C"]));

    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let executor = TaskExecutor::new();

    let result = orchestrator.execute(&executor).await.unwrap();

    // 验证所有任务完成
    assert_eq!(result.task_results.len(), 4);
    assert!(result
        .task_results
        .values()
        .all(|r| r.status == TaskStatus::Completed));

    // 验证执行顺序正确（通过验证所有任务都有输出）
    for task_id in ["A", "B", "C", "D"] {
        assert!(
            result.task_results[task_id].output.is_some(),
            "Task {} should have output",
            task_id
        );
    }
}

#[tokio::test]
async fn test_complex_multi_layer_dag() {
    // 创建多层 DAG：
    //       A
    //      /|\
    //     B C D
    //     |X| |
    //     E F G
    //      \|/
    //       H
    let mut workflow = Workflow::new("complex-wf", "Complex Workflow");

    // 第一层
    workflow.add_task(task_with_deps("A", vec![]));

    // 第二层
    workflow.add_task(task_with_deps("B", vec!["A"]));
    workflow.add_task(task_with_deps("C", vec!["A"]));
    workflow.add_task(task_with_deps("D", vec!["A"]));

    // 第三层（交叉依赖）
    workflow.add_task(task_with_deps("E", vec!["B", "C"]));
    workflow.add_task(task_with_deps("F", vec!["B", "C", "D"]));
    workflow.add_task(task_with_deps("G", vec!["D"]));

    // 第四层
    workflow.add_task(task_with_deps("H", vec!["E", "F", "G"]));

    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let executor = TaskExecutor::new();

    let result = orchestrator.execute(&executor).await.unwrap();

    // 验证所有任务完成
    assert_eq!(result.task_results.len(), 8);
    assert!(result
        .task_results
        .values()
        .all(|r| r.status == TaskStatus::Completed));

    println!("Complex workflow completed in {}ms", result.execution_time_ms);
}

#[tokio::test]
async fn test_single_task_workflow() {
    // 最简单的工作流：只有一个任务
    let mut workflow = Workflow::new("single-wf", "Single Task Workflow");
    workflow.add_task(task_with_deps("OnlyTask", vec![]));

    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let executor = TaskExecutor::new();

    let result = orchestrator.execute(&executor).await.unwrap();

    assert_eq!(result.task_results.len(), 1);
    assert_eq!(
        result.task_results["OnlyTask"].status,
        TaskStatus::Completed
    );
}

#[tokio::test]
async fn test_independent_tasks_workflow() {
    // 所有任务都独立（无依赖）
    let mut workflow = Workflow::new("independent-wf", "Independent Tasks Workflow");
    for i in 1..=5 {
        workflow.add_task(task_with_deps(&format!("Task{}", i), vec![]));
    }

    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let executor = TaskExecutor::new();

    let start = std::time::Instant::now();
    let result = orchestrator.execute(&executor).await.unwrap();
    let duration = start.elapsed();

    // 验证所有任务完成
    assert_eq!(result.task_results.len(), 5);
    assert!(result
        .task_results
        .values()
        .all(|r| r.status == TaskStatus::Completed));

    // 所有任务应该并行执行，总时间应该接近单个任务的执行时间
    assert!(
        duration.as_millis() < 50,
        "Independent tasks should execute in parallel: {}ms",
        duration.as_millis()
    );
}
