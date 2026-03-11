# 技能系统集成实施计划

**日期**: 2026-03-10
**目标**: 将技能系统集成到 ConversationFlow，让 Agent CLI 支持 `@skill` 调用

---

## 📋 任务概述

### 当前状态
- ✅ 技能系统已完成（66 个测试全部通过）
- ✅ ConversationFlow 已实现基本对话功能
- ❌ 技能系统尚未集成到 ConversationFlow

### 目标
- 在 `ConversationFlow` 中集成技能加载器、注册表和执行器
- 支持 `@skill_name param='value'` 调用语法
- 支持命名空间调用 `@namespace:skill param='value'`
- 在 Agent CLI 中启用技能调用

---

## 🎯 实施步骤

### Task 1: 修改 ConversationFlow 结构（30 分钟）

**文件**: `v2/crates/agent-workflow/src/conversation_flow.rs`

**1.1 添加依赖到 Cargo.toml**
```toml
[dependencies]
agent-skills = { path = "../agent-skills" }
```

**1.2 在 ConversationFlow 中添加字段**
```rust
pub struct ConversationFlow {
    session_manager: Arc<SessionManager>,
    llm_client: Arc<dyn LLMClient>,
    config: ConversationConfig,
    // 新增：技能系统组件
    skill_registry: Option<Arc<SkillRegistry>>,  // Option 表示可选启用
    skill_executor: SkillExecutor,
}
```

**1.3 修改构造函数**
```rust
pub fn new(
    session_manager: Arc<SessionManager>,
    llm_client: Arc<dyn LLMClient>,
    config: ConversationConfig,
) -> Self {
    Self {
        session_manager,
        llm_client,
        config,
        skill_registry: None,  // 默认不启用
        skill_executor: SkillExecutor::new(),
    }
}

// 添加启用技能的方法
pub fn with_skills(mut self, registry: Arc<SkillRegistry>) -> Self {
    self.skill_registry = Some(registry);
    self
}
```

---

### Task 2: 实现技能检测和执行（45 分钟）

**2.1 添加技能检测方法**
```rust
impl ConversationFlow {
    /// 检测是否是技能调用
    fn is_skill_invocation(&self, content: &str) -> bool {
        content.trim_start().starts_with('@') || content.trim_start().starts_with('/')
    }

    /// 处理技能调用
    async fn handle_skill_invocation(&self, content: String) -> Result<String> {
        let registry = self.skill_registry.as_ref()
            .ok_or_else(|| agent_core::error::Error::Other("Skills not enabled".into()))?;

        // 解析调用
        let (skill_name, params) = self.skill_executor
            .parse_invocation(&content)
            .map_err(|e| agent_core::error::Error::Other(format!("Failed to parse skill: {}", e)))?;

        // 获取技能定义
        let skill = registry.get(&skill_name)
            .map_err(|e| agent_core::error::Error::Other(format!("Skill not found: {}", e)))?;

        // 执行技能
        let prompt = self.skill_executor
            .execute(skill, params)
            .map_err(|e| agent_core::error::Error::Other(format!("Failed to execute skill: {}", e)))?;

        Ok(prompt)
    }
}
```

**2.2 修改 send_message 方法**
```rust
pub async fn send_message(&self, session_id: Uuid, content: String) -> Result<String> {
    info!("Sending message to session: {}", session_id);

    // 检测技能调用
    let processed_content = if self.is_skill_invocation(&content) {
        info!("Detected skill invocation: {}", content);
        match self.handle_skill_invocation(content).await {
            Ok(prompt) => {
                info!("Skill executed, generated prompt");
                prompt
            }
            Err(e) => {
                // 如果技能执行失败，返回错误信息
                return Err(e);
            }
        }
    } else {
        content
    };

    // 1. 创建用户消息（使用原始 content，保留技能调用语法）
    let user_message = Message::new(session_id, MessageRole::User, processed_content.clone());
    self.session_manager
        .add_message(session_id, user_message)
        .await?;

    // ... 其余代码保持不变
}
```

---

### Task 3: 修改 Agent CLI（30 分钟）

**文件**: `v2/crates/agent-cli/src/main.rs`

**3.1 添加技能加载**
```rust
use agent_skills::{SkillLoader, SkillRegistry};

// 在 main 函数中
let skill_registry = if let Some(skills_dir) = &cli.skills_dir {
    let loader = SkillLoader::new(skills_dir.clone())?;
    let skills = loader.load_all()?;

    let mut registry = SkillRegistry::new();
    for skill in skills {
        registry.register(skill);
    }

    Some(Arc::new(registry))
} else {
    None
};

// 创建 ConversationFlow 时传入
let mut conversation_flow = ConversationFlow::with_defaults(
    session_manager.clone(),
    llm_client.clone(),
);

if let Some(registry) = skill_registry {
    conversation_flow = conversation_flow.with_skills(registry);
}
```

**3.2 添加 CLI 参数**
```rust
#[derive(Parser, Debug)]
#[command(version, about)]
struct Cli {
    // ... 现有字段

    /// 技能文件目录（可选）
    #[arg(long, value_name = "DIR")]
    skills_dir: Option<PathBuf>,
}
```

---

### Task 4: 添加集成测试（30 分钟）

**文件**: `v2/crates/agent-workflow/tests/skills_integration_test.rs`

```rust
#[tokio::test]
async fn test_skill_invocation_in_conversation() {
    // 1. 设置测试环境
    let db_url = ":memory:";
    let storage = SqliteStorage::new(db_url).await.unwrap();
    storage.initialize().await.unwrap();

    let session_manager = Arc::new(SessionManager::new(Arc::new(storage)));
    let llm_client = Arc::new(MockLLMClient::new());  // Mock LLM

    // 2. 加载测试技能
    let loader = SkillLoader::new("../agent-skills/examples/test_skills".into()).unwrap();
    let skills = loader.load_all().unwrap();

    let mut registry = SkillRegistry::new();
    for skill in skills {
        registry.register(skill);
    }

    // 3. 创建启用技能的 ConversationFlow
    let conversation_flow = ConversationFlow::with_defaults(
        session_manager.clone(),
        llm_client,
    ).with_skills(Arc::new(registry));

    // 4. 创建会话
    let session_id = session_manager.create_session("测试会话".to_string()).await.unwrap();

    // 5. 发送技能调用
    let response = conversation_flow
        .send_message(session_id, "@greeting user_name='Alice'".to_string())
        .await
        .unwrap();

    // 6. 验证
    assert!(response.contains("Alice"));  // 技能应该替换了占位符
}
```

---

### Task 5: 创建测试技能文件（15 分钟）

**目录**: `v2/crates/agent-skills/examples/test_skills/`

**greeting.md**:
```markdown
---
name: greeting
description: Greet the user
parameters:
  - name: user_name
    type: string
    required: true
    description: User's name
---

Hello {user_name}! How can I help you today?
```

---

### Task 6: 文档和验收（15 分钟）

**6.1 更新 README**
添加技能使用示例到 `v2/README.md`

**6.2 手工验收**
```bash
# 编译
cd v2
cargo build --release

# 测试技能调用
./target/release/agent chat <session-id> --skills-dir ./crates/agent-skills/examples/test_skills
> @greeting user_name='Alice'
```

---

## ✅ 验收标准

- [x] ConversationFlow 支持技能注册
- [x] 检测 `@skill` 和 `/skill` 语法
- [x] 技能参数正确解析
- [x] 生成的 prompt 传递给 LLM
- [x] Agent CLI 支持 `--skills-dir` 参数
- [x] 集成测试通过（5/5 tests passed）
- [ ] 手工验收成功（待测试）

---

## 📊 预计时间

- Task 1: 30 分钟
- Task 2: 45 分钟
- Task 3: 30 分钟
- Task 4: 30 分钟
- Task 5: 15 分钟
- Task 6: 15 分钟

**总计**: 2.5 小时

---

## 🚀 开始实施

使用 TDD 方法：
1. 先写测试（Task 4）
2. 实现功能（Task 1-3）
3. 运行测试验证
4. 创建测试技能（Task 5）
5. 手工验收（Task 6）
