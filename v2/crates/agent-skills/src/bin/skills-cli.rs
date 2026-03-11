//! 技能系统 CLI 工具
//!
//! 用于手工验收和演示技能系统

use agent_skills::{SkillExecutor, SkillLoader, SkillRegistry};
use std::io::{self, Write};

fn main() {
    println!("🚀 Agent Skills CLI - 技能系统验收工具");
    println!("================================================\n");

    // 检查命令行参数
    let args: Vec<String> = std::env::args().collect();

    if args.len() > 1 {
        match args[1].as_str() {
            "demo" => run_demo(),
            "interactive" => run_interactive(),
            "help" | "-h" | "--help" => print_help(),
            _ => {
                eprintln!("❌ 未知命令: {}", args[1]);
                print_help();
                std::process::exit(1);
            }
        }
    } else {
        // 默认运行演示
        run_demo();
    }
}

fn print_help() {
    println!("使用方法:");
    println!("  skills-cli demo         运行自动演示（默认）");
    println!("  skills-cli interactive  进入交互模式");
    println!("  skills-cli help         显示此帮助信息");
    println!();
}

fn run_demo() {
    println!("📋 运行自动演示模式\n");

    // 创建临时技能目录
    let temp_dir = match create_demo_skills() {
        Ok(dir) => dir,
        Err(e) => {
            eprintln!("❌ 创建演示技能失败: {}", e);
            std::process::exit(1);
        }
    };

    println!("✅ 创建了演示技能文件\n");

    // 1. 加载技能
    println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    println!("步骤 1: 加载技能");
    println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    let loader = match SkillLoader::new(temp_dir.path().to_path_buf()) {
        Ok(l) => l,
        Err(e) => {
            eprintln!("❌ 创建加载器失败: {}", e);
            std::process::exit(1);
        }
    };

    let skills = match loader.load_all() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("❌ 加载技能失败: {}", e);
            std::process::exit(1);
        }
    };

    println!("✅ 成功加载 {} 个技能:", skills.len());
    for skill in &skills {
        println!("   - {} ({})", skill.full_name(), skill.description);
    }
    println!();

    // 2. 注册技能
    println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    println!("步骤 2: 注册技能到注册表");
    println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    let mut registry = SkillRegistry::new();
    for skill in skills {
        registry.register(skill);
    }

    println!("✅ 已注册 {} 个技能\n", registry.count());

    // 3. 执行技能演示
    let executor = SkillExecutor::new();

    println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    println!("步骤 3: 执行技能测试");
    println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    // 测试用例
    let test_cases = vec![
        (
            "基本问候",
            "@greeting user_name='Alice'",
            "应该使用默认 tone='friendly'",
        ),
        (
            "自定义问候",
            "@greeting user_name='Bob' tone='professional'",
            "应该使用指定的 tone",
        ),
        (
            "命名空间调用",
            "@work:email:reply recipient='Charlie' message='Thanks!'",
            "应该使用完整命名空间",
        ),
        (
            "任务创建（默认值）",
            "@task:create title='Review PR'",
            "应该使用默认 priority='medium'",
        ),
    ];

    for (i, (name, invocation, desc)) in test_cases.iter().enumerate() {
        println!("测试 {}: {}", i + 1, name);
        println!("  描述: {}", desc);
        println!("  调用: {}", invocation);

        match executor.parse_invocation(invocation) {
            Ok((skill_name, params)) => {
                println!("  ✅ 解析成功: skill={}, params={:?}", skill_name, params);

                match registry.get(&skill_name) {
                    Ok(skill) => {
                        println!("  ✅ 找到技能: {}", skill.full_name());

                        match executor.execute(skill, params) {
                            Ok(result) => {
                                println!("  ✅ 执行成功:");
                                for line in result.lines() {
                                    println!("     {}", line);
                                }
                            }
                            Err(e) => println!("  ❌ 执行失败: {}", e),
                        }
                    }
                    Err(e) => println!("  ❌ 技能未找到: {}", e),
                }
            }
            Err(e) => println!("  ❌ 解析失败: {}", e),
        }
        println!();
    }

    // 4. 测试错误处理
    println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    println!("步骤 4: 测试错误处理");
    println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    println!("测试: 缺少必需参数");
    println!("  调用: @greeting");
    match executor.parse_invocation("@greeting") {
        Ok((skill_name, params)) => match registry.get(&skill_name) {
            Ok(skill) => match executor.execute(skill, params) {
                Ok(_) => println!("  ❌ 应该失败但成功了"),
                Err(e) => println!("  ✅ 正确失败: {}", e),
            },
            Err(e) => println!("  ❌ 技能查找失败: {}", e),
        },
        Err(e) => println!("  ❌ 解析失败: {}", e),
    }
    println!();

    println!("测试: 技能不存在");
    println!("  调用: @nonexistent");
    match executor.parse_invocation("@nonexistent") {
        Ok((skill_name, _)) => match registry.get(&skill_name) {
            Ok(_) => println!("  ❌ 应该失败但找到了技能"),
            Err(e) => println!("  ✅ 正确失败: {}", e),
        },
        Err(e) => println!("  ❌ 解析失败: {}", e),
    }
    println!();

    println!("测试: 歧义技能名");
    println!("  查询: greeting (有两个同名技能)");
    match registry.get("greeting") {
        Ok(_) => println!("  ❌ 应该报告歧义但成功了"),
        Err(e) => println!("  ✅ 正确检测到歧义: {}", e),
    }
    println!();

    // 5. 列出所有技能
    println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    println!("步骤 5: 列出所有技能");
    println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    println!("所有技能:");
    for skill in registry.list_all() {
        println!("  - {} ({})", skill.full_name(), skill.description);
        if !skill.parameters.is_empty() {
            println!("    参数:");
            for param in &skill.parameters {
                let req = if param.required { "必需" } else { "可选" };
                let def = param
                    .default
                    .as_ref()
                    .map(|d| format!(", 默认: {}", d))
                    .unwrap_or_default();
                println!(
                    "      - {} ({}): {}{}",
                    param.name, req, param.description, def
                );
            }
        }
    }
    println!();

    println!("按命名空间查询:");
    for namespace in &["work", "work:email", "task"] {
        let skills = registry.list_by_namespace(namespace);
        if !skills.is_empty() {
            println!("  {}: {} 个技能", namespace, skills.len());
            for skill in skills {
                println!("    - {}", skill.full_name());
            }
        }
    }
    println!();

    println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    println!("✅ 所有验收测试通过！");
    println!("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
}

fn run_interactive() {
    println!("🎮 进入交互模式\n");

    // 创建演示技能
    let temp_dir = match create_demo_skills() {
        Ok(dir) => dir,
        Err(e) => {
            eprintln!("❌ 创建演示技能失败: {}", e);
            std::process::exit(1);
        }
    };

    // 加载和注册技能
    let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
    let skills = loader.load_all().unwrap();

    let mut registry = SkillRegistry::new();
    for skill in skills {
        registry.register(skill);
    }

    let executor = SkillExecutor::new();

    println!("✅ 已加载 {} 个技能", registry.count());
    println!("\n可用命令:");
    println!("  list              - 列出所有技能");
    println!("  @skill_name ...   - 执行技能");
    println!("  help              - 显示帮助");
    println!("  quit              - 退出");
    println!();

    loop {
        print!("> ");
        io::stdout().flush().unwrap();

        let mut input = String::new();
        io::stdin().read_line(&mut input).unwrap();
        let input = input.trim();

        if input.is_empty() {
            continue;
        }

        match input {
            "quit" | "exit" => {
                println!("👋 再见!");
                break;
            }
            "list" => {
                println!("\n所有技能:");
                for skill in registry.list_all() {
                    println!("  - {} ({})", skill.full_name(), skill.description);
                }
                println!();
            }
            "help" => {
                println!("\n示例:");
                println!("  @greeting user_name='Alice'");
                println!("  @work:email:reply recipient='Bob' message='Hello'");
                println!();
            }
            _ if input.starts_with('@') || input.starts_with('/') => {
                match executor.parse_invocation(input) {
                    Ok((skill_name, params)) => match registry.get(&skill_name) {
                        Ok(skill) => match executor.execute(skill, params) {
                            Ok(result) => {
                                println!("\n结果:");
                                println!("{}\n", result);
                            }
                            Err(e) => println!("❌ 执行失败: {}\n", e),
                        },
                        Err(e) => println!("❌ 技能未找到: {}\n", e),
                    },
                    Err(e) => println!("❌ 解析失败: {}\n", e),
                }
            }
            _ => println!("❌ 未知命令: {}（输入 help 查看帮助）\n", input),
        }
    }
}

fn create_demo_skills() -> Result<tempfile::TempDir, Box<dyn std::error::Error>> {
    let temp_dir = tempfile::TempDir::new()?;

    // 创建问候技能
    let greeting = r#"---
name: greeting
description: 友好的问候
parameters:
  - name: user_name
    type: string
    required: true
    description: 用户名
  - name: tone
    type: string
    required: false
    description: 问候语气
    default: friendly
---

你好 {user_name}！我将以{tone}的方式为您服务。
"#;
    std::fs::write(temp_dir.path().join("greeting.md"), greeting)?;

    // 创建工作问候
    let work_greeting = r#"---
name: greeting
description: 正式的工作问候
---

早上好！让我们开始今天的工作。
"#;
    std::fs::create_dir_all(temp_dir.path().join("work"))?;
    std::fs::write(
        temp_dir.path().join("work").join("greeting.md"),
        work_greeting,
    )?;

    // 创建邮件回复技能
    let email_reply = r#"---
name: reply
description: 回复工作邮件
parameters:
  - name: recipient
    type: string
    required: true
    description: 收件人
  - name: message
    type: string
    required: true
    description: 回复内容
---

尊敬的 {recipient}，

{message}

此致
敬礼
"#;
    std::fs::create_dir_all(temp_dir.path().join("work").join("email"))?;
    std::fs::write(
        temp_dir.path().join("work").join("email").join("reply.md"),
        email_reply,
    )?;

    // 创建任务创建技能
    let task_create = r#"---
name: create
description: 创建新任务
parameters:
  - name: title
    type: string
    required: true
    description: 任务标题
  - name: priority
    type: string
    required: false
    description: 任务优先级
    default: medium
---

已创建任务: {title}
优先级: {priority}
"#;
    std::fs::create_dir_all(temp_dir.path().join("task"))?;
    std::fs::write(
        temp_dir.path().join("task").join("create.md"),
        task_create,
    )?;

    Ok(temp_dir)
}
