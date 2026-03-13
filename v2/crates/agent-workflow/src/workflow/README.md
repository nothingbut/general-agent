# Workflow 系统

Rust 实现的 DAG 工作流编排系统。

## 功能

- ✅ DAG 依赖解析（基于 petgraph）
- ✅ 循环依赖检测
- ✅ 并行任务执行
- ✅ 超时控制
- ✅ 指数退避重试
- ⏳ 状态持久化（计划中）
- ⏳ 取消/暂停支持（计划中）

## 快速开始

```rust
use agent_workflow::workflow::*;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    // 创建工作流
    let mut workflow = Workflow::new("demo", "Demo Workflow");

    // 添加任务
    let task_a = Task::new("A", "Task A", TaskType::Custom("test".to_string()));
    let task_b = Task::new("B", "Task B", TaskType::Custom("test".to_string()))
        .with_dependency("A");

    workflow.add_task(task_a);
    workflow.add_task(task_b);

    // 执行工作流
    let orchestrator = WorkflowOrchestrator::new(workflow)?;
    let executor = TaskExecutor::new();
    let result = orchestrator.execute(&executor).await?;

    println!("完成 {} 个任务，耗时 {}ms",
        result.task_results.len(),
        result.execution_time_ms);

    Ok(())
}
```

## 架构

```
WorkflowOrchestrator  ──> TaskExecutor
        │                      │
        │                      │
    解析 DAG              执行单个任务
    调度批次              超时/重试
```

## 测试

```bash
# 单元测试
cargo test -p agent-workflow workflow

# 集成测试
cargo test -p agent-workflow --test workflow_integration
```

## 性能

- 并行任务执行：< 100ms
- 复杂 8 任务 DAG：46ms
