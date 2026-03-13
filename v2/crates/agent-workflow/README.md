# Agent Workflow

Rust 实现的工作流编排系统，支持 DAG 依赖解析和并行任务执行。

## 功能

- ✅ DAG 工作流定义
- ✅ 依赖关系解析
- ✅ 并行任务执行
- ✅ 任务重试机制（指数退避）
- ✅ 超时控制
- ✅ **LLM 调用集成** (Week 2 Day 1 完成)
- ⏳ Skills 技能执行（计划中）
- ⏳ MCP 工具调用（计划中）
- ⏳ 取消和暂停支持（计划中）

## 使用示例

### 基础工作流

```rust
use agent_workflow::workflow::*;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    // 创建工作流
    let mut workflow = Workflow::new("my-workflow", "My Workflow");

    // 添加任务
    let task_a = Task::new("task-a", "Task A", TaskType::Custom("test".to_string()));
    let task_b = Task::new("task-b", "Task B", TaskType::Custom("test".to_string()))
        .with_dependency("task-a");

    workflow.add_task(task_a);
    workflow.add_task(task_b);

    // 执行工作流
    let orchestrator = WorkflowOrchestrator::new(workflow)?;
    let executor = TaskExecutor::new();
    let result = orchestrator.execute(&executor).await?;

    println!("Workflow completed in {}ms", result.execution_time_ms);
    Ok(())
}
```

### LLM 工作流

```rust
use agent_workflow::workflow::*;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    // 创建包含 LLM 调用的工作流
    let mut workflow = Workflow::new("llm-workflow", "LLM Workflow");

    // 任务 1: LLM 调用
    let task1 = Task::new(
        "question",
        "Ask Question",
        TaskType::LLMCall {
            prompt: "What is the capital of France?".to_string(),
            model: Some("claude-3-5-sonnet-20241022".to_string()),
            temperature: Some(0.7),
            max_tokens: Some(100),
        },
    );

    // 任务 2: 处理结果（依赖任务1）
    let task2 = Task::new(
        "process",
        "Process Result",
        TaskType::Custom("processing".to_string()),
    )
    .with_dependency("question");

    workflow.add_task(task1);
    workflow.add_task(task2);

    // 执行工作流
    let orchestrator = WorkflowOrchestrator::new(workflow)?;
    let executor = TaskExecutor::new();
    let result = orchestrator.execute(&executor).await?;

    // 查看结果
    let llm_result = &result.task_results["question"];
    println!("LLM Response: {:?}", llm_result.output);

    Ok(())
}
```

### 运行示例

```bash
# 设置 API Key
export ANTHROPIC_API_KEY=sk-ant-xxx

# 运行示例
cargo run --example llm_workflow
```

## 任务类型

### LLMCall - LLM 调用

调用大语言模型生成文本。

```rust
TaskType::LLMCall {
    prompt: "Your prompt here".to_string(),
    model: Some("claude-3-5-sonnet-20241022".to_string()),
    temperature: Some(0.7),
    max_tokens: Some(1000),
}
```

**参数**:
- `prompt` (必需): 提示词内容
- `model` (可选): 模型名称，默认 `claude-3-5-sonnet-20241022`
- `temperature` (可选): 温度参数 (0.0 - 2.0)，默认由模型决定
- `max_tokens` (可选): 最大输出 token 数，默认 4096

### SkillExecution - 技能执行

执行预定义的技能（计划中）。

```rust
TaskType::SkillExecution {
    skill_name: "my_skill".to_string(),
    params: Some(json!({"key": "value"})),
}
```

### MCPToolCall - MCP 工具调用

调用 MCP 服务器的工具（计划中）。

```rust
TaskType::MCPToolCall {
    server_name: "filesystem".to_string(),
    tool_name: "read_file".to_string(),
    params: Some(json!({"path": "/tmp/file.txt"})),
}
```

### Subworkflow - 子工作流

嵌套执行另一个工作流（计划中）。

```rust
TaskType::Subworkflow {
    workflow_id: "sub-workflow-id".to_string(),
}
```

### Custom - 自定义任务

自定义逻辑，主要用于测试。

```rust
TaskType::Custom("my-task".to_string())
```

## 任务配置

每个任务可以配置以下参数：

```rust
let task = Task::new("task-id", "Task Name", task_type)
    .with_config(TaskConfig {
        retry_count: 3,      // 重试次数，默认 3
        timeout_secs: 60,    // 超时时间（秒），默认 60
        priority: 0,         // 优先级，默认 0（暂未实现）
    });
```

## 测试

```bash
# 运行所有测试
cargo test -p agent-workflow

# 运行特定测试
cargo test -p agent-workflow test_simple_workflow_execution

# 运行 LLM 集成测试（需要 API Key）
export ANTHROPIC_API_KEY=sk-ant-xxx
cargo test -p agent-workflow --test llm_workflow_test -- --ignored
```

## 架构

```
agent-workflow/
├── src/
│   └── workflow/
│       ├── models.rs        # 数据模型定义
│       ├── orchestrator.rs  # DAG 编排器
│       ├── executor.rs      # 任务执行器
│       └── mod.rs
├── tests/
│   ├── workflow_integration.rs  # 集成测试
│   └── llm_workflow_test.rs     # LLM 测试
└── examples/
    └── llm_workflow.rs           # LLM 示例
```

## 开发进度

### Week 1 (已完成)
- ✅ 数据模型定义
- ✅ DAG 依赖解析
- ✅ 任务调度器
- ✅ 任务执行器
- ✅ 重试和超时机制
- ✅ 基础测试

### Week 2 (进行中)
- ✅ Day 1: LLM 调用集成
- ⏳ Day 2: Skills 技能执行
- ⏳ Day 3: MCP 工具调用
- ⏳ Day 4: 持久化支持
- ⏳ Day 5: 取消和暂停

## 许可证

MIT
