# V2 技能系统设计文档

**日期:** 2026-03-09
**状态:** 设计中
**目标:** 使用 Rust 实现可复用的技能系统

---

## 📋 项目目标

基于 V1 的 Python 技能系统，使用 Rust 重新实现一个高性能、类型安全的技能系统，支持：
- Markdown 技能定义
- YAML frontmatter 参数
- 命名空间管理
- 参数验证和默认值
- 与 workflow 集成

---

## 🎯 核心功能

### 1. 技能文件格式

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

Hello {user_name}!

I'm here to assist you in a {tone} manner.
```

### 2. 调用语法

```bash
# 基本调用
@greeting user_name='Alice'

# 带可选参数
@greeting user_name='Bob' tone='professional'

# 命名空间调用
@personal:greeting user_name='Alice'
```

### 3. 核心组件

```
┌────────────────┐
│  SkillLoader   │  加载 .md 文件，应用 .ignore
└────────┬───────┘
         │
         ▼
┌────────────────┐
│  SkillParser   │  解析 YAML + Markdown
└────────┬───────┘
         │
         ▼
┌────────────────┐
│ SkillRegistry  │  存储和检索技能
└────────┬───────┘
         │
         ▼
┌────────────────┐
│ SkillExecutor  │  验证参数，替换占位符
└────────────────┘
```

---

## 🏗️ 架构设计

### 模块结构

```
crates/agent-skills/
├── Cargo.toml
├── src/
│   ├── lib.rs              # 公共导出
│   ├── models.rs           # 数据模型
│   ├── parser.rs           # YAML + Markdown 解析
│   ├── loader.rs           # 文件加载
│   ├── registry.rs         # 技能注册表
│   ├── executor.rs         # 技能执行
│   └── error.rs            # 错误类型
└── tests/
    ├── parser_tests.rs
    ├── loader_tests.rs
    ├── registry_tests.rs
    └── integration_tests.rs
```

### 依赖库

```toml
[dependencies]
# YAML 解析
serde = { version = "1.0", features = ["derive"] }
serde_yaml = "0.9"

# Markdown 解析
pulldown-cmark = "0.9"

# 文件操作
walkdir = "2.4"
ignore = "0.4"  # gitignore 风格的 .ignore 支持

# 正则表达式
regex = "1.10"

# 异步
tokio = { version = "1.35", features = ["full"] }
async-trait = "0.1"

# 错误处理
thiserror = "1.0"
anyhow = "1.0"
```

---

## 📊 数据模型

### 1. SkillParameter

```rust
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SkillParameter {
    pub name: String,
    pub param_type: String,  // "string", "number", "boolean", etc.
    pub required: bool,
    pub description: String,
    pub default: Option<String>,
}
```

### 2. SkillDefinition

```rust
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SkillDefinition {
    pub name: String,
    pub description: String,
    pub namespace: String,  // 从文件路径提取
    pub parameters: Vec<SkillParameter>,
    pub content: String,  // Markdown 内容
    pub file_path: PathBuf,
}

impl SkillDefinition {
    /// 获取完整名称 (namespace:name)
    pub fn full_name(&self) -> String {
        format!("{}:{}", self.namespace, self.name)
    }
}
```

### 3. SkillExecutionContext

```rust
pub struct SkillExecutionContext {
    pub skill: SkillDefinition,
    pub parameters: HashMap<String, String>,
}

impl SkillExecutionContext {
    /// 验证参数
    pub fn validate(&self) -> Result<()> {
        // 检查必需参数
        // 应用默认值
    }

    /// 构建提示词（替换占位符）
    pub fn build_prompt(&self) -> String {
        // 替换 {param} 为实际值
    }
}
```

---

## 🔧 核心组件实现

### 1. SkillParser

**职责:**
- 解析 YAML frontmatter
- 提取 Markdown 内容
- 验证必需字段

```rust
pub struct SkillParser;

impl SkillParser {
    /// 解析技能文件
    pub fn parse(content: &str, file_path: &Path) -> Result<SkillDefinition> {
        // 1. 分离 YAML frontmatter 和 Markdown
        let (yaml_str, markdown_str) = Self::split_frontmatter(content)?;

        // 2. 解析 YAML
        let mut skill: SkillDefinition = serde_yaml::from_str(&yaml_str)?;

        // 3. 设置内容和路径
        skill.content = markdown_str.to_string();
        skill.file_path = file_path.to_path_buf();

        // 4. 验证
        Self::validate(&skill)?;

        Ok(skill)
    }

    fn split_frontmatter(content: &str) -> Result<(&str, &str)> {
        // 查找 "---" 分隔符
        // 返回 (YAML部分, Markdown部分)
    }

    fn validate(skill: &SkillDefinition) -> Result<()> {
        // 验证 name 和 description 非空
        // 验证参数定义
    }
}
```

### 2. SkillLoader

**职责:**
- 扫描技能目录
- 应用 .ignore 规则
- 提取命名空间
- 批量加载技能

```rust
pub struct SkillLoader {
    skills_dir: PathBuf,
    ignore_patterns: ignore::gitignore::Gitignore,
}

impl SkillLoader {
    pub fn new(skills_dir: PathBuf) -> Self {
        let ignore_patterns = Self::load_ignore_patterns(&skills_dir);
        Self { skills_dir, ignore_patterns }
    }

    /// 加载所有技能
    pub fn load_all(&self) -> Result<Vec<SkillDefinition>> {
        let mut skills = Vec::new();

        for entry in WalkDir::new(&self.skills_dir) {
            let entry = entry?;
            let path = entry.path();

            // 跳过目录和被忽略的文件
            if !path.is_file() || self.should_ignore(path) {
                continue;
            }

            // 只处理 .md 文件
            if path.extension().and_then(|s| s.to_str()) != Some("md") {
                continue;
            }

            // 解析技能
            let content = std::fs::read_to_string(path)?;
            let mut skill = SkillParser::parse(&content, path)?;

            // 提取命名空间
            skill.namespace = self.extract_namespace(path)?;

            skills.push(skill);
        }

        Ok(skills)
    }

    fn extract_namespace(&self, path: &Path) -> Result<String> {
        // 从相对路径提取命名空间
        // skills/personal/greeting.md -> "personal"
        let relative = path.strip_prefix(&self.skills_dir)?;
        let parent = relative.parent().unwrap_or(Path::new(""));
        Ok(parent.to_string_lossy().replace("/", ":"))
    }

    fn should_ignore(&self, path: &Path) -> bool {
        self.ignore_patterns.matched(path, false).is_ignore()
    }

    fn load_ignore_patterns(skills_dir: &Path) -> ignore::gitignore::Gitignore {
        // 加载 skills/.ignore 文件
    }
}
```

### 3. SkillRegistry

**职责:**
- 存储技能定义
- 提供查询接口
- 处理命名空间和歧义

```rust
pub struct SkillRegistry {
    // 完整名称 -> 技能定义
    skills: HashMap<String, SkillDefinition>,

    // 短名称 -> 完整名称列表（用于检测歧义）
    short_name_index: HashMap<String, Vec<String>>,
}

impl SkillRegistry {
    pub fn new() -> Self {
        Self {
            skills: HashMap::new(),
            short_name_index: HashMap::new(),
        }
    }

    /// 注册技能
    pub fn register(&mut self, skill: SkillDefinition) {
        let full_name = skill.full_name();
        let short_name = skill.name.clone();

        // 存储完整定义
        self.skills.insert(full_name.clone(), skill);

        // 更新短名称索引
        self.short_name_index
            .entry(short_name)
            .or_insert_with(Vec::new)
            .push(full_name);
    }

    /// 获取技能（支持短名称和完整名称）
    pub fn get(&self, name: &str) -> Result<&SkillDefinition> {
        // 1. 尝试作为完整名称查找
        if let Some(skill) = self.skills.get(name) {
            return Ok(skill);
        }

        // 2. 尝试作为短名称查找
        if let Some(full_names) = self.short_name_index.get(name) {
            match full_names.len() {
                0 => Err(Error::SkillNotFound(name.to_string())),
                1 => Ok(&self.skills[&full_names[0]]),
                _ => Err(Error::AmbiguousSkillName {
                    name: name.to_string(),
                    candidates: full_names.clone(),
                }),
            }
        } else {
            Err(Error::SkillNotFound(name.to_string()))
        }
    }

    /// 列出所有技能
    pub fn list_all(&self) -> Vec<&SkillDefinition> {
        self.skills.values().collect()
    }

    /// 按命名空间查询
    pub fn list_by_namespace(&self, namespace: &str) -> Vec<&SkillDefinition> {
        self.skills
            .values()
            .filter(|s| s.namespace == namespace)
            .collect()
    }
}
```

### 4. SkillExecutor

**职责:**
- 解析调用语法
- 验证参数
- 替换占位符
- 构建最终提示词

```rust
pub struct SkillExecutor;

impl SkillExecutor {
    /// 解析技能调用
    /// 输入: "@greeting user_name='Alice' tone='friendly'"
    /// 输出: (skill_name, parameters)
    pub fn parse_invocation(input: &str) -> Result<(String, HashMap<String, String>)> {
        // 正则表达式匹配
        // SKILL_PATTERN: ^[@/](\S+)
        // PARAM_PATTERN: (\w+)=['"]([^'"]+)['"]
    }

    /// 执行技能
    pub fn execute(
        &self,
        skill: &SkillDefinition,
        parameters: HashMap<String, String>,
    ) -> Result<String> {
        // 1. 创建执行上下文
        let context = SkillExecutionContext {
            skill: skill.clone(),
            parameters,
        };

        // 2. 验证参数
        context.validate()?;

        // 3. 构建提示词
        let prompt = context.build_prompt();

        Ok(prompt)
    }
}

impl SkillExecutionContext {
    pub fn validate(&self) -> Result<()> {
        for param in &self.skill.parameters {
            if param.required && !self.parameters.contains_key(&param.name) {
                return Err(Error::MissingRequiredParameter(param.name.clone()));
            }

            if let Some(value) = self.parameters.get(&param.name) {
                if value.is_empty() {
                    return Err(Error::EmptyParameter(param.name.clone()));
                }
            }
        }
        Ok(())
    }

    pub fn build_prompt(&self) -> String {
        let mut prompt = self.skill.content.clone();

        // 应用默认值
        let mut params = self.parameters.clone();
        for param in &self.skill.parameters {
            if !params.contains_key(&param.name) {
                if let Some(default) = &param.default {
                    params.insert(param.name.clone(), default.clone());
                }
            }
        }

        // 替换占位符 {param}
        for (key, value) in params {
            let placeholder = format!("{{{}}}", key);
            prompt = prompt.replace(&placeholder, &value);
        }

        prompt
    }
}
```

---

## 🔗 与 Workflow 集成

### ConversationFlow 集成

```rust
// 在 ConversationFlow 中添加技能支持
impl ConversationFlow {
    pub async fn send_message_with_skills(
        &self,
        session_id: Uuid,
        user_input: String,
    ) -> Result<String> {
        // 1. 检测是否为技能调用
        if user_input.starts_with('@') || user_input.starts_with('/') {
            // 2. 解析技能调用
            let (skill_name, params) = SkillExecutor::parse_invocation(&user_input)?;

            // 3. 获取技能
            let skill = self.skill_registry.get(&skill_name)?;

            // 4. 构建提示词
            let prompt = SkillExecutor::execute(skill, params)?;

            // 5. 作为普通消息发送给 LLM
            return self.send_message(session_id, prompt).await;
        }

        // 普通消息处理
        self.send_message(session_id, user_input).await
    }
}
```

---

## 🧪 测试计划

### 测试覆盖目标

- **单元测试:** 每个组件独立测试
- **集成测试:** 端到端流程测试
- **覆盖率目标:** ≥ 80%

### 测试用例

#### 1. Parser Tests (10+)
- ✅ 解析基本技能文件
- ✅ 解析带参数的技能
- ✅ 验证必需字段
- ✅ 处理缺失 frontmatter
- ✅ 处理无效 YAML
- ✅ 处理空 Markdown

#### 2. Loader Tests (8+)
- ✅ 加载单个技能
- ✅ 递归加载目录
- ✅ 应用 .ignore 规则
- ✅ 提取命名空间
- ✅ 跳过非 .md 文件
- ✅ 处理空目录

#### 3. Registry Tests (10+)
- ✅ 注册和检索技能
- ✅ 短名称查找
- ✅ 完整名称查找
- ✅ 歧义检测
- ✅ 命名空间查询
- ✅ 列出所有技能

#### 4. Executor Tests (12+)
- ✅ 解析调用语法
- ✅ 参数验证（必需）
- ✅ 参数验证（空值）
- ✅ 应用默认值
- ✅ 替换占位符
- ✅ 构建提示词

#### 5. Integration Tests (5+)
- ✅ 加载 → 注册 → 执行
- ✅ 命名空间调用
- ✅ 错误处理

---

## 📅 实施计划

### Week 5 (3-4天)

**Day 1-2: 数据模型和解析器**
- [ ] 创建 agent-skills crate
- [ ] 实现数据模型 (models.rs)
- [ ] 实现 SkillParser (parser.rs)
- [ ] 编写 Parser 测试 (10+ tests)

**Day 3-4: 加载器和注册表**
- [ ] 实现 SkillLoader (loader.rs)
- [ ] 实现 SkillRegistry (registry.rs)
- [ ] 编写 Loader + Registry 测试 (18+ tests)

### Week 6 (3-4天)

**Day 1-2: 执行器**
- [ ] 实现 SkillExecutor (executor.rs)
- [ ] 实现参数验证和提示词构建
- [ ] 编写 Executor 测试 (12+ tests)

**Day 3-4: 集成和测试**
- [ ] 与 ConversationFlow 集成
- [ ] 编写集成测试 (5+ tests)
- [ ] 验证覆盖率 ≥ 80%
- [ ] 更新文档

---

## ✅ 验收标准

### 功能完整性
- [ ] 支持 YAML frontmatter 解析
- [ ] 支持参数定义（required, default）
- [ ] 支持命名空间
- [ ] 支持 .ignore 文件
- [ ] 支持调用语法解析 (@skill, /skill)
- [ ] 支持参数验证
- [ ] 支持占位符替换

### 测试要求
- [ ] 45+ 单元测试
- [ ] 5+ 集成测试
- [ ] 测试覆盖率 ≥ 80%
- [ ] 所有测试通过

### 代码质量
- [ ] 100% 类型注解
- [ ] 通过 cargo clippy
- [ ] 通过 cargo fmt --check
- [ ] 无 unsafe 代码（除非必要）

### 文档要求
- [ ] API 文档（rustdoc）
- [ ] 使用示例
- [ ] 集成指南

---

## 🎓 学习参考

### Rust 库文档
- [serde_yaml](https://docs.rs/serde_yaml/)
- [pulldown-cmark](https://docs.rs/pulldown-cmark/)
- [walkdir](https://docs.rs/walkdir/)
- [ignore](https://docs.rs/ignore/)

### V1 参考
- `skills/README.md` - 技能格式和调用语法
- `docs/skills.md` - 完整技能系统文档
- `src/skills/` - Python 实现（可参考但不直接移植）

---

**设计日期:** 2026-03-09
**预计完成:** 2026-03-16 (Week 5-6)
**状态:** 准备开始实施 ✅
