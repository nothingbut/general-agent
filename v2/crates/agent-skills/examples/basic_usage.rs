//! 技能系统基本使用示例

use agent_skills::{SkillExecutor, SkillLoader, SkillRegistry};
use std::fs;
use tempfile::TempDir;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("=== Agent Skills 基本使用示例 ===\n");

    // 1. 创建临时目录和示例技能文件
    let temp_dir = TempDir::new()?;
    create_example_skills(temp_dir.path())?;

    // 2. 加载技能
    println!("📂 加载技能文件...");
    let loader = SkillLoader::new(temp_dir.path().to_path_buf())?;
    let skills = loader.load_all()?;
    println!("   加载了 {} 个技能\n", skills.len());

    // 3. 注册技能
    println!("📝 注册技能到注册表...");
    let mut registry = SkillRegistry::new();
    for skill in skills {
        println!("   注册: {}", skill.full_name());
        registry.register(skill);
    }
    println!();

    // 4. 创建执行器
    let executor = SkillExecutor::new();

    // 5. 示例 1: 基本调用
    println!("💬 示例 1: 基本问候");
    let invocation = "@greeting user_name='Alice'";
    println!("   调用: {}", invocation);

    let (skill_name, params) = executor.parse_invocation(invocation)?;
    let skill = registry.get(&skill_name)?;
    let result = executor.execute(skill, params)?;

    println!("   结果: {}\n", result);

    // 6. 示例 2: 使用命名空间
    println!("💼 示例 2: 工作邮件回复");
    let invocation = "@work:email:reply recipient='Bob' message='I agree with your proposal.'";
    println!("   调用: {}", invocation);

    let (skill_name, params) = executor.parse_invocation(invocation)?;
    let skill = registry.get(&skill_name)?;
    let result = executor.execute(skill, params)?;

    println!("   结果:\n{}\n", result);

    // 7. 示例 3: 使用默认值
    println!("📋 示例 3: 创建任务（使用默认优先级）");
    let invocation = "@task:create title='Review PR'";
    println!("   调用: {}", invocation);

    let (skill_name, params) = executor.parse_invocation(invocation)?;
    let skill = registry.get(&skill_name)?;
    let result = executor.execute(skill, params)?;

    println!("   结果: {}\n", result);

    // 8. 示例 4: 列出所有技能
    println!("📚 所有已注册的技能:");
    for skill in registry.list_all() {
        println!("   - {} ({})", skill.full_name(), skill.description);
    }
    println!();

    // 9. 示例 5: 按命名空间查询
    println!("🏢 工作相关技能:");
    for skill in registry.list_by_namespace("work") {
        println!("   - {}", skill.full_name());
    }

    Ok(())
}

fn create_example_skills(dir: &std::path::Path) -> std::io::Result<()> {
    // 创建问候技能
    let greeting = r#"---
name: greeting
description: Greet the user warmly
parameters:
  - name: user_name
    type: string
    required: true
    description: User's name
  - name: tone
    type: string
    required: false
    description: Greeting tone
    default: friendly
---

Hello {user_name}! I'm here to assist you in a {tone} manner.
"#;
    fs::create_dir_all(dir)?;
    fs::write(dir.join("greeting.md"), greeting)?;

    // 创建工作邮件回复技能
    let email_reply = r#"---
name: reply
description: Reply to work email
parameters:
  - name: recipient
    type: string
    required: true
    description: Email recipient
  - name: message
    type: string
    required: true
    description: Reply message
---

Dear {recipient},

{message}

Best regards,
Your Assistant
"#;
    fs::create_dir_all(dir.join("work/email"))?;
    fs::write(dir.join("work/email/reply.md"), email_reply)?;

    // 创建任务创建技能
    let task_create = r#"---
name: create
description: Create a new task
parameters:
  - name: title
    type: string
    required: true
    description: Task title
  - name: priority
    type: string
    required: false
    description: Task priority
    default: medium
---

Task created: {title} (Priority: {priority})
"#;
    fs::create_dir_all(dir.join("task"))?;
    fs::write(dir.join("task/create.md"), task_create)?;

    Ok(())
}
