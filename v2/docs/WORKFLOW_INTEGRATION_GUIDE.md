# Workflow 集成指南

本文档介绍如何在 Rust V2 版本中使用 Workflow、审批和通知系统。

---

## 快速开始

### 1. 基本工作流

```rust
use agent_workflow::workflow::*;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // 创建工作流
    let mut workflow = Workflow::new("wf-1", "数据处理流程");

    // 添加任务
    workflow.add_task(Task::new("load", "数据加载",
        TaskType::Custom("load".to_string())));
    workflow.add_task(Task::new("process", "数据处理",
        TaskType::Custom("process".to_string()))
        .with_dependency("load"));

    // 创建编排器并执行
    let orchestrator = WorkflowOrchestrator::new(workflow)?;
    let executor = TaskExecutor::new();
    let result = orchestrator.execute(&executor).await?;

    println!("完成 {} 个任务", result.task_results.len());
    Ok(())
}
```

---

## 通知系统集成

### 1. 注册通知渠道

```rust
use agent_workflow::notification::*;
use std::sync::Arc;

// 创建通知管理器
let notification_mgr = Arc::new(NotificationManager::new());

// 注册终端通知
notification_mgr.register_channel(Arc::new(TerminalChannel::new())).await;

// 注册日志通知
notification_mgr.register_channel(Arc::new(LogChannel::new())).await;

// 桌面通知（可选，需要系统支持）
let desktop = DesktopChannel::new();
if desktop.is_available() {
    notification_mgr.register_channel(Arc::new(desktop)).await;
}
```

### 2. 集成到工作流

```rust
let orchestrator = WorkflowOrchestrator::new(workflow)?
    .with_notification_manager(notification_mgr);

// 执行时自动发送通知
let result = orchestrator.execute(&executor).await?;
```

### 3. 通知事件

工作流会自动发送以下通知：
- 🟢 **工作流开始**: Normal 优先级
- 🟢 **工作流完成**: Normal 优先级
- 🟡 **工作流取消**: High 优先级
- 🟢 **工作流暂停**: Normal 优先级
- 🟢 **任务开始**: 根据任务名称智能推断
- 🟢 **任务完成**: Normal 优先级
- 🔴 **任务失败**: Critical 优先级
- 🟡 **审批拒绝**: High 优先级

---

## 审批系统集成

### 1. 创建审批管理器

```rust
use agent_workflow::approval::*;

let approval_mgr = Arc::new(ApprovalManager::new());
```

### 2. 集成到工作流

```rust
let orchestrator = WorkflowOrchestrator::new(workflow)?
    .with_approval_manager(approval_mgr);
```

### 3. 审批策略

当前支持三种策略：

#### Auto（自动批准）
```rust
ApprovalStrategy::Auto
```

#### Manual（手动审批）
```rust
ApprovalStrategy::Manual {
    prompt: "是否执行删除操作？".to_string(),
    options: Some(vec!["是".to_string(), "否".to_string()]),
}
```

#### Threshold（条件审批）
```rust
ApprovalStrategy::Threshold {
    condition: "cost < 100".to_string(),
    on_pass: Box::new(ApprovalStrategy::Auto),
    on_fail: Box::new(ApprovalStrategy::Manual {
        prompt: "成本超标，是否继续？".to_string(),
        options: None,
    }),
}
```

---

## 完整集成示例

```rust
use agent_workflow::approval::*;
use agent_workflow::notification::*;
use agent_workflow::workflow::*;
use std::sync::Arc;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // 1. 创建管理器
    let approval_mgr = Arc::new(ApprovalManager::new());
    let notification_mgr = Arc::new(NotificationManager::new());
    notification_mgr.register_channel(Arc::new(TerminalChannel::new())).await;
    notification_mgr.register_channel(Arc::new(LogChannel::new())).await;

    // 2. 创建工作流
    let mut workflow = Workflow::new("etl", "ETL 数据流程");
    workflow.add_task(Task::new("extract", "数据提取",
        TaskType::Custom("extract".to_string())));
    workflow.add_task(Task::new("transform", "数据转换",
        TaskType::Custom("transform".to_string()))
        .with_dependency("extract"));
    workflow.add_task(Task::new("load", "数据加载",
        TaskType::Custom("load".to_string()))
        .with_dependency("transform"));

    // 3. 创建编排器（集成所有功能）
    let orchestrator = WorkflowOrchestrator::new(workflow)?
        .with_approval_manager(approval_mgr)
        .with_notification_manager(notification_mgr);

    // 4. 执行
    let executor = TaskExecutor::new();
    let result = orchestrator.execute(&executor).await?;

    println!("✓ 工作流完成，耗时: {}ms", result.execution_time_ms);
    Ok(())
}
```

---

## 工作流控制

### 取消工作流

```rust
// 在另一个任务中
orchestrator.cancel().await;

// 当前执行会收到取消通知
```

### 暂停和恢复

```rust
// 暂停
orchestrator.pause().await;

// 恢复
orchestrator.resume().await;

// 继续执行
let result = orchestrator.execute(&executor).await?;
```

### 检查状态

```rust
let state = orchestrator.get_control_state().await;
match state {
    ControlState::Running => println!("运行中"),
    ControlState::Paused => println!("已暂停"),
    ControlState::CancelRequested => println!("取消中"),
    _ => {}
}
```

---

## 任务类型

### LLM 调用
```rust
TaskType::LLMCall {
    provider: "anthropic".to_string(),
    model: Some("claude-3-sonnet".to_string()),
    prompt: "分析数据".to_string(),
    max_tokens: Some(1000),
}
```

### 技能执行
```rust
TaskType::SkillExecution {
    skill_name: "data_analysis".to_string(),
    params: Some(serde_json::json!({
        "dataset": "sales.csv"
    })),
}
```

### MCP 工具调用
```rust
TaskType::MCPToolCall {
    server_name: "filesystem".to_string(),
    tool_name: "read_file".to_string(),
    params: Some(serde_json::json!({
        "path": "/data/input.txt"
    })),
}
```

---

## 性能优化

### 1. 并行任务

工作流自动识别可并行执行的任务：
```rust
// task-2 和 task-3 会并行执行
workflow.add_task(task1);
workflow.add_task(task2.with_dependency("task-1"));
workflow.add_task(task3.with_dependency("task-1"));
```

### 2. 任务配置

```rust
let task = Task::new("task", "任务", task_type)
    .with_config(TaskConfig {
        retry_count: 3,        // 失败重试3次
        timeout_secs: 60,      // 超时60秒
        priority: 1,           // 优先级（暂未使用）
    });
```

---

## 错误处理

### 任务失败

```rust
match orchestrator.execute(&executor).await {
    Ok(result) => {
        println!("成功: {:?}", result);
    }
    Err(e) => {
        if e.to_string().contains("Task") {
            println!("任务失败: {}", e);
        } else if e.to_string().contains("cancelled") {
            println!("工作流已取消");
        } else if e.to_string().contains("paused") {
            println!("工作流已暂停");
        }
    }
}
```

---

## 示例程序

### 运行演示

```bash
# 通知系统演示
cargo run -p agent-workflow --example notification_demo

# 集成工作流演示
cargo run -p agent-workflow --example integrated_workflow_demo

# LLM 工作流演示
cargo run -p agent-workflow --example llm_workflow

# 技能工作流演示
cargo run -p agent-workflow --example skill_workflow

# MCP 工作流演示
cargo run -p agent-workflow --example mcp_workflow
```

---

## 最佳实践

### 1. 通知渠道选择

- **开发环境**: Terminal + Log
- **生产环境**: Log + Desktop（可选）
- **CI/CD**: 仅 Log

### 2. 审批策略选择

- **低风险操作**: Auto
- **高风险操作**: Manual
- **成本敏感**: Threshold

### 3. 错误处理

- 始终检查 execute() 返回值
- 使用 TaskConfig 配置重试
- 记录失败原因到日志

### 4. 性能优化

- 合理设计任务依赖（最大化并行）
- 设置合理的超时时间
- 避免循环依赖

---

## 故障排查

### 问题 1: 通知未显示

**检查**:
- 是否注册了通知渠道？
- 渠道是否可用（`is_available()`）？

**解决**:
```rust
let channels = notification_mgr.get_available_channels().await;
println!("可用渠道: {:?}", channels);
```

### 问题 2: 工作流卡住

**检查**:
- 是否存在循环依赖？
- 是否有未完成的依赖？

**解决**:
```rust
// 工作流创建时会自动检测循环依赖
match WorkflowOrchestrator::new(workflow) {
    Err(e) if e.to_string().contains("Cyclic") => {
        println!("检测到循环依赖");
    }
    _ => {}
}
```

### 问题 3: 审批未触发

**检查**:
- 是否设置了 ApprovalManager？
- 任务配置是否需要审批？

**当前**: 所有任务默认使用 Auto 策略

---

## API 参考

完整 API 文档：
```bash
cargo doc --open -p agent-workflow
```

---

**更新日期**: 2026-03-13
**版本**: 0.1.0
