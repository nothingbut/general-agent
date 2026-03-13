//! Skills 工作流示例
//!
//! 展示如何创建和执行包含 Skills 技能的工作流
//!
//! 使用方法：
//! ```bash
//! cargo run --example skill_workflow
//! ```

use agent_workflow::workflow::*;
use agent_skills::{SkillDefinition, SkillParameter, SkillRegistry};
use std::sync::Arc;

/// 创建示例技能注册表
fn create_skill_registry() -> SkillRegistry {
    let mut registry = SkillRegistry::new();

    // 技能 1: 电子邮件模板
    let mut email = SkillDefinition::new(
        "email_template".to_string(),
        "Generate an email template".to_string(),
    );
    email.content = r#"To: {recipient}
From: {sender}
Subject: {subject}

Dear {recipient},

{message}

Best regards,
{sender}"#
        .to_string();

    email.parameters.push(SkillParameter::new(
        "recipient".to_string(),
        "string".to_string(),
        true,
        "Recipient's name".to_string(),
    ));
    email.parameters.push(SkillParameter::new(
        "sender".to_string(),
        "string".to_string(),
        true,
        "Sender's name".to_string(),
    ));
    email.parameters.push(SkillParameter::new(
        "subject".to_string(),
        "string".to_string(),
        true,
        "Email subject".to_string(),
    ));
    email.parameters.push(SkillParameter::new(
        "message".to_string(),
        "string".to_string(),
        true,
        "Email message body".to_string(),
    ));
    registry.register(email);

    // 技能 2: 项目状态报告
    let mut status_report = SkillDefinition::new(
        "status_report".to_string(),
        "Generate a project status report".to_string(),
    );
    status_report.content = r#"# Project Status Report

**Project:** {project_name}
**Date:** {date}
**Status:** {status}

## Progress
{progress}

## Next Steps
{next_steps}"#
        .to_string();

    status_report.parameters.push(SkillParameter::new(
        "project_name".to_string(),
        "string".to_string(),
        true,
        "Project name".to_string(),
    ));
    status_report.parameters.push(SkillParameter::new(
        "date".to_string(),
        "string".to_string(),
        true,
        "Report date".to_string(),
    ));
    status_report.parameters.push(SkillParameter::new(
        "status".to_string(),
        "string".to_string(),
        true,
        "Project status".to_string(),
    ));
    status_report.parameters.push(SkillParameter::new(
        "progress".to_string(),
        "string".to_string(),
        true,
        "Progress description".to_string(),
    ));
    status_report
        .parameters
        .push(SkillParameter::new(
            "next_steps".to_string(),
            "string".to_string(),
            true,
            "Next steps description".to_string(),
        ));
    registry.register(status_report);

    // 技能 3: 会议纪要
    let mut meeting_notes = SkillDefinition::new(
        "meeting_notes".to_string(),
        "Generate meeting notes".to_string(),
    );
    meeting_notes.content = r#"# Meeting Notes - {title}

**Date:** {date}
**Attendees:** {attendees}

## Discussion
{discussion}

## Action Items
{action_items}

## Next Meeting
{next_meeting}"#
        .to_string();

    meeting_notes.parameters.push(SkillParameter::new(
        "title".to_string(),
        "string".to_string(),
        true,
        "Meeting title".to_string(),
    ));
    meeting_notes.parameters.push(SkillParameter::new(
        "date".to_string(),
        "string".to_string(),
        true,
        "Meeting date".to_string(),
    ));
    meeting_notes.parameters.push(SkillParameter::new(
        "attendees".to_string(),
        "string".to_string(),
        true,
        "List of attendees".to_string(),
    ));
    meeting_notes.parameters.push(SkillParameter::new(
        "discussion".to_string(),
        "string".to_string(),
        true,
        "Discussion summary".to_string(),
    ));
    meeting_notes.parameters.push(
        SkillParameter::new(
            "action_items".to_string(),
            "string".to_string(),
            false,
            "Action items".to_string(),
        )
        .with_default("None".to_string()),
    );
    meeting_notes.parameters.push(
        SkillParameter::new(
            "next_meeting".to_string(),
            "string".to_string(),
            false,
            "Next meeting date".to_string(),
        )
        .with_default("TBD".to_string()),
    );
    registry.register(meeting_notes);

    registry
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    println!("🚀 创建 Skills 工作流示例...\n");

    // 创建技能注册表
    let registry = create_skill_registry();
    println!("✅ 已注册 {} 个技能", registry.count());
    for skill in registry.list_all() {
        println!("   - {}: {}", skill.name, skill.description);
    }
    println!();

    // 创建执行器
    let executor = TaskExecutor::with_skill_registry(Arc::new(registry));

    // 创建工作流
    let mut workflow = Workflow::new("document-generation", "文档生成工作流");

    // 任务 1: 生成项目状态报告
    println!("📊 添加任务 1: 生成项目状态报告");
    let task1 = Task::new(
        "status-report",
        "Generate Status Report",
        TaskType::SkillExecution {
            skill_name: "status_report".to_string(),
            params: Some(serde_json::json!({
                "project_name": "Workflow Migration",
                "date": "2026-03-13",
                "status": "In Progress - Week 2 Day 2",
                "progress": "Completed LLM integration, implementing Skills support",
                "next_steps": "Complete MCP integration, add persistence layer"
            })),
        },
    );

    // 任务 2: 生成会议纪要（并行于任务1）
    println!("📝 添加任务 2: 生成会议纪要");
    let task2 = Task::new(
        "meeting-notes",
        "Generate Meeting Notes",
        TaskType::SkillExecution {
            skill_name: "meeting_notes".to_string(),
            params: Some(serde_json::json!({
                "title": "Weekly Sync",
                "date": "2026-03-13",
                "attendees": "Team Lead, Developer, QA",
                "discussion": "Discussed workflow migration progress and technical challenges",
                "action_items": "1. Complete Skills integration\n2. Start MCP integration\n3. Write tests",
                "next_meeting": "2026-03-20"
            })),
        },
    );

    // 任务 3: 生成总结邮件（依赖前两个任务）
    println!("✉️  添加任务 3: 生成总结邮件");
    let task3 = Task::new(
        "summary-email",
        "Generate Summary Email",
        TaskType::SkillExecution {
            skill_name: "email_template".to_string(),
            params: Some(serde_json::json!({
                "recipient": "Project Manager",
                "sender": "Development Team",
                "subject": "Weekly Update - Workflow Migration Project",
                "message": "Please find attached the weekly status report and meeting notes. We've made significant progress on the workflow migration project this week."
            })),
        },
    )
    .with_dependency("status-report")
    .with_dependency("meeting-notes");

    workflow.add_task(task1);
    workflow.add_task(task2);
    workflow.add_task(task3);

    println!("\n⚙️  创建编排器...");
    let orchestrator = WorkflowOrchestrator::new(workflow)?;

    println!("🎬 开始执行工作流...\n");
    let start = std::time::Instant::now();

    let result = orchestrator.execute(&executor).await?;

    let elapsed = start.elapsed();

    println!("✅ 工作流执行完成！");
    println!("⏱️  总耗时: {:.2}秒", elapsed.as_secs_f64());
    println!("📊 执行结果:\n");

    // 显示结果
    let task_order = ["status-report", "meeting-notes", "summary-email"];
    for task_id in task_order {
        if let Some(task_result) = result.task_results.get(task_id) {
            println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            println!("🔹 {} ({:?})", task_id, task_result.status);
            println!("⏱  耗时: {}ms", task_result.execution_time_ms);
            if let Some(output) = &task_result.output {
                println!("\n📄 输出:\n{}\n", output);
            }
            if let Some(error) = &task_result.error {
                println!("❌ 错误: {}\n", error);
            }
        }
    }

    println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    println!("\n✨ 示例完成！");

    Ok(())
}
