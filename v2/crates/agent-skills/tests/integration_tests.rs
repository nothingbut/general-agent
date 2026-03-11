//! 技能系统集成测试
//!
//! 测试完整的技能系统流程：加载 -> 注册 -> 解析调用 -> 执行

use agent_skills::{SkillExecutor, SkillLoader, SkillRegistry};
use std::fs;
use tempfile::TempDir;

// 测试辅助函数：创建测试技能文件
fn create_skill_file(dir: &std::path::Path, relative_path: &str, content: &str) {
    let file_path = dir.join(relative_path);
    if let Some(parent) = file_path.parent() {
        fs::create_dir_all(parent).unwrap();
    }
    fs::write(&file_path, content).unwrap();
}

#[test]
fn test_end_to_end_skill_workflow() {
    // 1. 准备：创建测试目录和技能文件
    let temp_dir = TempDir::new().unwrap();

    let greeting_skill = r#"---
name: greeting
description: Greet the user with custom name
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

    create_skill_file(temp_dir.path(), "greeting.md", greeting_skill);

    // 2. 加载技能
    let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
    let skills = loader.load_all().unwrap();

    assert_eq!(skills.len(), 1);
    assert_eq!(skills[0].name, "greeting");

    // 3. 注册技能
    let mut registry = SkillRegistry::new();
    for skill in skills {
        registry.register(skill);
    }

    assert_eq!(registry.count(), 1);

    // 4. 解析调用
    let executor = SkillExecutor::new();
    let invocation = "@greeting user_name='Alice' tone='professional'";
    let (skill_name, params) = executor.parse_invocation(invocation).unwrap();

    assert_eq!(skill_name, "greeting");
    assert_eq!(params.get("user_name"), Some(&"Alice".to_string()));
    assert_eq!(params.get("tone"), Some(&"professional".to_string()));

    // 5. 执行技能
    let skill = registry.get(&skill_name).unwrap();
    let result = executor.execute(skill, params).unwrap();

    assert_eq!(
        result,
        "Hello Alice! I'm here to assist you in a professional manner."
    );
}

#[test]
fn test_multiple_skills_with_namespaces() {
    let temp_dir = TempDir::new().unwrap();

    // 创建多个技能文件
    let personal_greeting = r#"---
name: greeting
description: Personal greeting
---
Hi {name}! How are you doing?
"#;

    let work_greeting = r#"---
name: greeting
description: Work greeting
---
Good morning {name}. Let's start the meeting.
"#;

    create_skill_file(temp_dir.path(), "personal/greeting.md", personal_greeting);
    create_skill_file(temp_dir.path(), "work/greeting.md", work_greeting);

    // 加载和注册
    let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
    let skills = loader.load_all().unwrap();

    let mut registry = SkillRegistry::new();
    for skill in skills {
        registry.register(skill);
    }

    assert_eq!(registry.count(), 2);

    // 通过命名空间区分同名技能
    let executor = SkillExecutor::new();

    // 使用完整名称调用
    let (skill_name, _) = executor
        .parse_invocation("@personal:greeting name='Alice'")
        .unwrap();
    let skill = registry.get(&skill_name).unwrap();
    assert_eq!(skill.namespace, "personal");

    let (skill_name, _) = executor
        .parse_invocation("@work:greeting name='Bob'")
        .unwrap();
    let skill = registry.get(&skill_name).unwrap();
    assert_eq!(skill.namespace, "work");

    // 短名称应该产生歧义
    let result = registry.get("greeting");
    assert!(result.is_err());
}

#[test]
fn test_skill_with_ignore_patterns() {
    let temp_dir = TempDir::new().unwrap();

    // 创建技能文件
    let active_skill = r#"---
name: active
description: Active skill
---
This skill is active.
"#;

    let draft_skill = r#"---
name: draft
description: Draft skill
---
This is a draft.
"#;

    create_skill_file(temp_dir.path(), "active.md", active_skill);
    create_skill_file(temp_dir.path(), "drafts/draft.md", draft_skill);

    // 创建 .ignore 文件
    fs::write(temp_dir.path().join(".ignore"), "drafts/\n").unwrap();

    // 加载技能（应该忽略 drafts 目录）
    let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
    let skills = loader.load_all().unwrap();

    assert_eq!(skills.len(), 1);
    assert_eq!(skills[0].name, "active");
}

#[test]
fn test_skill_execution_with_default_values() {
    let temp_dir = TempDir::new().unwrap();

    let skill_content = r#"---
name: email
description: Send email
parameters:
  - name: to
    type: string
    required: true
    description: Recipient email
  - name: subject
    type: string
    required: false
    description: Email subject
    default: No Subject
  - name: priority
    type: string
    required: false
    description: Email priority
    default: normal
---

To: {to}
Subject: {subject}
Priority: {priority}
"#;

    create_skill_file(temp_dir.path(), "email.md", skill_content);

    // 完整流程
    let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
    let skills = loader.load_all().unwrap();

    let mut registry = SkillRegistry::new();
    for skill in skills {
        registry.register(skill);
    }

    let executor = SkillExecutor::new();

    // 只提供必需参数，其他使用默认值
    let (skill_name, params) = executor
        .parse_invocation("@email to='alice@example.com'")
        .unwrap();

    let skill = registry.get(&skill_name).unwrap();
    let result = executor.execute(skill, params).unwrap();

    assert!(result.contains("To: alice@example.com"));
    assert!(result.contains("Subject: No Subject"));
    assert!(result.contains("Priority: normal"));
}

#[test]
fn test_skill_execution_validation_error() {
    let temp_dir = TempDir::new().unwrap();

    let skill_content = r#"---
name: task
description: Create task
parameters:
  - name: title
    type: string
    required: true
    description: Task title
---

Task: {title}
"#;

    create_skill_file(temp_dir.path(), "task.md", skill_content);

    let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
    let skills = loader.load_all().unwrap();

    let mut registry = SkillRegistry::new();
    for skill in skills {
        registry.register(skill);
    }

    let executor = SkillExecutor::new();

    // 解析调用（不提供必需参数）
    let (skill_name, params) = executor.parse_invocation("@task").unwrap();

    // 执行应该失败（缺少必需参数）
    let skill = registry.get(&skill_name).unwrap();
    let result = executor.execute(skill, params);

    assert!(result.is_err());
}

#[test]
fn test_nested_namespace_skills() {
    let temp_dir = TempDir::new().unwrap();

    let skill_content = r#"---
name: reply
description: Reply to email
parameters:
  - name: message
    type: string
    required: true
    description: Reply message
---

Reply: {message}
"#;

    create_skill_file(temp_dir.path(), "work/email/reply.md", skill_content);

    let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
    let skills = loader.load_all().unwrap();

    assert_eq!(skills.len(), 1);
    assert_eq!(skills[0].namespace, "work:email");
    assert_eq!(skills[0].full_name(), "work:email:reply");

    let mut registry = SkillRegistry::new();
    for skill in skills {
        registry.register(skill);
    }

    // 使用完整名称调用
    let executor = SkillExecutor::new();
    let (skill_name, params) = executor
        .parse_invocation("@work:email:reply message='Thanks!'")
        .unwrap();

    assert_eq!(skill_name, "work:email:reply");

    let skill = registry.get(&skill_name).unwrap();
    let result = executor.execute(skill, params).unwrap();

    assert_eq!(result, "Reply: Thanks!");
}
