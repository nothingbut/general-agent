//! LLM 工作流示例
//!
//! 展示如何创建和执行包含 LLM 调用的工作流
//!
//! 使用方法：
//! ```bash
//! export ANTHROPIC_API_KEY=sk-ant-xxx
//! cargo run --example llm_workflow
//! ```

use agent_workflow::workflow::*;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    // 检查 API Key
    if std::env::var("ANTHROPIC_API_KEY").is_err() {
        eprintln!("错误: 请设置 ANTHROPIC_API_KEY 环境变量");
        eprintln!("使用方法: export ANTHROPIC_API_KEY=sk-ant-xxx");
        std::process::exit(1);
    }

    println!("🚀 创建 LLM 工作流...");

    // 创建工作流
    let mut workflow = Workflow::new("creative-workflow", "创意写作工作流");

    // 任务 1: 生成主题
    println!("📝 添加任务 1: 生成主题");
    let task1 = Task::new(
        "topic",
        "生成主题",
        TaskType::LLMCall {
            prompt: "给我一个有趣的科幻故事主题，用一句话描述。".to_string(),
            model: Some("claude-3-5-sonnet-20241022".to_string()),
            temperature: Some(0.9),
            max_tokens: Some(100),
        },
    );

    // 任务 2: 生成角色
    println!("👤 添加任务 2: 生成角色");
    let task2 = Task::new(
        "character",
        "生成角色",
        TaskType::LLMCall {
            prompt: "创造一个有趣的科幻故事主角，包括姓名和一个特殊能力。".to_string(),
            model: Some("claude-3-5-sonnet-20241022".to_string()),
            temperature: Some(0.9),
            max_tokens: Some(100),
        },
    );

    // 任务 3: 汇总信息（依赖前两个任务）
    println!("📋 添加任务 3: 汇总信息");
    let task3 = Task::new(
        "summary",
        "汇总信息",
        TaskType::Custom("summary".to_string()),
    )
    .with_dependency("topic")
    .with_dependency("character");

    workflow.add_task(task1);
    workflow.add_task(task2);
    workflow.add_task(task3);

    println!("\n⚙️  创建编排器和执行器...");
    let orchestrator = WorkflowOrchestrator::new(workflow)?;
    let executor = TaskExecutor::new();

    println!("🎬 开始执行工作流...\n");
    let start = std::time::Instant::now();

    let result = orchestrator.execute(&executor).await?;

    let elapsed = start.elapsed();

    println!("\n✅ 工作流执行完成！");
    println!("⏱️  总耗时: {:.2}秒", elapsed.as_secs_f64());
    println!("📊 执行结果:\n");

    // 显示结果
    for task_id in ["topic", "character", "summary"] {
        if let Some(task_result) = result.task_results.get(task_id) {
            println!("🔹 {}", task_id);
            println!("   状态: {:?}", task_result.status);
            if let Some(output) = &task_result.output {
                println!("   输出: {}", output);
            }
            if let Some(error) = &task_result.error {
                println!("   错误: {}", error);
            }
            println!("   耗时: {}ms", task_result.execution_time_ms);
            println!();
        }
    }

    Ok(())
}
