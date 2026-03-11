# Agent Skills

一个高性能、类型安全的 Rust 技能系统，支持 Markdown + YAML frontmatter 格式的可复用提示词模板。

## 功能特性

- ✅ **Markdown + YAML Frontmatter** - 简洁直观的技能定义格式
- ✅ **参数化支持** - 必需参数、可选参数、默认值
- ✅ **命名空间管理** - 支持嵌套命名空间，避免命名冲突
- ✅ **智能检索** - 短名称和完整名称查询，自动歧义检测
- ✅ **.ignore 支持** - 灵活的文件过滤规则
- ✅ **类型安全** - 100% Rust 类型安全，零 unsafe 代码
- ✅ **完整测试** - 66 个测试覆盖所有场景

## 快速开始

### 1. 定义技能文件

创建 `skills/greeting.md`:

```markdown
---
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

# Greeting Skill

Hello {user_name}! I'm here to assist you in a {tone} manner.
```

### 2. 加载和注册技能

```rust
use agent_skills::{SkillLoader, SkillRegistry, SkillExecutor};

// 加载技能文件
let loader = SkillLoader::new("./skills".into())?;
let skills = loader.load_all()?;

// 注册到注册表
let mut registry = SkillRegistry::new();
for skill in skills {
    registry.register(skill);
}
```

### 3. 解析和执行调用

```rust
let executor = SkillExecutor::new();

// 解析调用语法
let (skill_name, params) = executor
    .parse_invocation("@greeting user_name='Alice' tone='professional'")?;

// 执行技能
let skill = registry.get(&skill_name)?;
let prompt = executor.execute(skill, params)?;

println!("{}", prompt);
// 输出: Hello Alice! I'm here to assist you in a professional manner.
```

## 技能文件格式

### YAML Frontmatter

```yaml
---
name: skill_name          # 必需：技能名称
description: Description  # 必需：技能描述
parameters:               # 可选：参数列表
  - name: param1
    type: string          # 参数类型
    required: true        # 是否必需
    description: Desc     # 参数描述
  - name: param2
    type: string
    required: false
    default: value        # 默认值
---
```

### Markdown 内容

使用 `{param_name}` 占位符引用参数：

```markdown
Hello {user_name}!

Your message: {message}
Tone: {tone}
```

## 调用语法

### 基本调用

```bash
@greeting user_name='Alice'
/greeting user_name='Bob'
```

### 多个参数

```bash
@greeting user_name='Alice' tone='friendly' lang='en'
```

### 使用命名空间

```bash
@personal:greeting user_name='Alice'
@work:email:reply message='Thanks!'
```

## 命名空间

技能系统自动从文件路径提取命名空间：

```
skills/
├── greeting.md              → greeting
├── personal/
│   └── greeting.md          → personal:greeting
└── work/
    └── email/
        └── reply.md         → work:email:reply
```

### 歧义处理

当多个技能同名时，必须使用完整名称：

```rust
// 有两个 greeting 技能：personal:greeting 和 work:greeting
registry.get("greeting");           // Error: 歧义
registry.get("personal:greeting");  // OK
registry.get("work:greeting");      // OK
```

## .ignore 文件

在技能目录创建 `.ignore` 文件来排除特定文件或目录：

```
# 忽略草稿
drafts/

# 忽略临时文件
*.tmp
temp*.md

# 忽略特定文件
README.md
```

## 错误处理

```rust
use agent_skills::{ParseError, LoadError, RegistryError, ExecutorError};

// 解析错误
match SkillParser::parse(content, path) {
    Ok(skill) => { /* ... */ },
    Err(ParseError::MissingFrontmatter) => { /* 缺少 frontmatter */ },
    Err(ParseError::YamlError(e)) => { /* YAML 语法错误 */ },
    Err(ParseError::MissingField(field)) => { /* 缺少必需字段 */ },
}

// 注册表错误
match registry.get("skill_name") {
    Ok(skill) => { /* ... */ },
    Err(RegistryError::SkillNotFound(name)) => { /* 技能不存在 */ },
    Err(RegistryError::AmbiguousSkillName { name, candidates }) => {
        // 多个同名技能，需要使用完整名称
        println!("Ambiguous: {}. Use one of: {:?}", name, candidates);
    },
}

// 执行错误
match executor.execute(skill, params) {
    Ok(prompt) => { /* ... */ },
    Err(ExecutorError::ValidationError(msg)) => { /* 参数验证失败 */ },
    Err(ExecutorError::InvalidSyntax(msg)) => { /* 调用语法错误 */ },
}
```

## 高级用法

### 动态参数验证

```rust
use agent_skills::SkillExecutionContext;

let context = SkillExecutionContext::new(skill.clone(), params);

// 验证参数
if let Err(e) = context.validate() {
    eprintln!("Validation error: {}", e);
    return;
}

// 构建提示词
let prompt = context.build_prompt();
```

### 按命名空间查询

```rust
// 获取所有个人技能
let personal_skills = registry.list_by_namespace("personal");

// 获取所有工作相关技能
let work_skills = registry.list_by_namespace("work");
```

### 自定义文件过滤

```rust
let loader = SkillLoader::new("./skills".into())?;
let mut skills = loader.load_all()?;

// 手动过滤
skills.retain(|skill| {
    skill.name.starts_with("ai_") // 只保留 AI 相关技能
});
```

## 测试

```bash
# 运行所有测试
cargo test

# 运行单元测试
cargo test --lib

# 运行集成测试
cargo test --test integration_tests

# 查看测试覆盖率
cargo tarpaulin --out Html
```

## 性能

- **解析速度:** ~1ms/文件（中等大小）
- **内存占用:** 每个技能 ~1KB
- **查询性能:** O(1) HashMap 查找

## 架构设计

```
┌──────────────┐
│ SkillLoader  │  扫描和加载 .md 文件
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ SkillParser  │  解析 YAML + Markdown
└──────┬───────┘
       │
       ▼
┌──────────────┐
│SkillRegistry │  存储和检索技能
└──────┬───────┘
       │
       ▼
┌──────────────┐
│SkillExecutor │  解析调用并执行
└──────────────┘
```

## 依赖

```toml
[dependencies]
serde = { version = "1.0", features = ["derive"] }
serde_yaml = "0.9"
walkdir = "2.4"
ignore = "0.4"
regex = "1.10"
thiserror = "1.0"
anyhow = "1.0"
```

## 许可证

本项目使用 MIT 许可证。

## 贡献

欢迎提交 Issue 和 Pull Request！

---

**相关文档:**
- [设计文档](../../docs/plans/skills-system-design.md)
- [API 文档](https://docs.rs/agent-skills)
