# General Agent V2 - 部署和使用指南

## 🎉 已完成组件

### ✅ 技能系统 (agent-skills)
- **状态**: 完整实现并测试通过
- **测试**: 66 个测试全部通过（60 单元 + 6 集成）
- **CLI**: `skills-cli` (1.7M)

### ✅ 主 Agent CLI (agent-cli)
- **状态**: 完整实现
- **二进制**: `agent` (6.2M)
- **功能**: 会话管理、对话、LLM 集成

## 📦 编译好的可执行文件

```bash
v2/target/release/
├── agent          # 6.2M - 主 Agent CLI
└── skills-cli     # 1.7M - 技能系统 CLI
```

## 🚀 快速开始

### 1. 主 Agent CLI

#### 创建新会话
```bash
./target/release/agent new --title "我的对话"
# 输出: Session ID
```

#### 列出会话
```bash
./target/release/agent list
```

#### 开始对话
```bash
./target/release/agent chat <session-id>
# 交互式对话，输入 'exit' 退出
```

#### 流式对话
```bash
./target/release/agent chat <session-id> --stream
```

#### 删除会话
```bash
./target/release/agent delete <session-id>
```

#### 搜索会话
```bash
./target/release/agent search "关键词"
```

### 2. 技能系统 CLI

#### 运行自动演示
```bash
./target/release/skills-cli demo
```

#### 交互模式
```bash
./target/release/skills-cli interactive
```

## ⚙️ 配置

### 环境变量

#### Agent CLI
```bash
# 数据库路径
export AGENT_DB="agent.db"

# LLM 提供商 (anthropic/ollama)
export AGENT_PROVIDER="ollama"

# Anthropic API Key (如使用 anthropic)
export ANTHROPIC_API_KEY="sk-..."

# Ollama 配置
export OLLAMA_MODEL="qwen2.5:0.5b"
export OLLAMA_BASE_URL="http://localhost:11434"
```

#### 或使用命令行参数
```bash
./target/release/agent \
  --db-path agent.db \
  --provider ollama \
  --ollama-model qwen2.5:0.5b \
  --ollama-url http://localhost:11434 \
  chat <session-id>
```

## 📁 项目结构

```
v2/
├── crates/
│   ├── agent-cli/         ✅ 主 CLI（已完成）
│   ├── agent-core/        ✅ 核心接口
│   ├── agent-llm/         ✅ LLM 集成（Anthropic, Ollama）
│   ├── agent-storage/     ✅ 数据存储
│   ├── agent-workflow/    ✅ 对话流程
│   ├── agent-skills/      ✅ 技能系统（已完成）
│   ├── agent-api/         📋 待实现
│   └── agent-tui/         📋 待实现
└── target/release/
    ├── agent              ✅ 编译完成 (6.2M)
    └── skills-cli         ✅ 编译完成 (1.7M)
```

## 🧪 测试验收

### 技能系统验收
```bash
cd v2/crates/agent-skills

# 运行所有测试
cargo test

# 运行演示
../../target/release/skills-cli demo

# 查看文档
cat ACCEPTANCE.md
```

### Agent CLI 测试
```bash
cd v2

# 创建测试会话
./target/release/agent new --title "测试"

# 列出会话
./target/release/agent list

# 开始对话（需要 Ollama 运行）
# 确保 Ollama 已启动: ollama serve
./target/release/agent chat <session-id>
```

## 📊 技能系统功能清单

### 核心功能
- [x] YAML frontmatter 解析
- [x] Markdown 内容提取
- [x] 参数定义（必需/可选/默认值）
- [x] 命名空间管理（支持嵌套）
- [x] .ignore 文件支持
- [x] 技能注册和查询
- [x] 短名称/完整名称查询
- [x] 歧义检测
- [x] 调用语法解析（@/@）
- [x] 参数验证
- [x] 占位符替换

### 测试覆盖
- [x] 单元测试: 60 个
- [x] 集成测试: 6 个
- [x] 代码质量: clippy + fmt 通过

## 🛠️ 重新编译

```bash
cd v2

# 编译主 Agent
cargo build --release --bin agent

# 编译技能 CLI
cargo build --release --bin skills-cli

# 编译所有
cargo build --release
```

## 📝 使用示例

### 完整对话流程

```bash
# 1. 启动 Ollama（如使用本地模型）
ollama serve

# 2. 创建会话
SESSION_ID=$(./target/release/agent new --title "编程助手" | grep "ID:" | awk '{print $2}')

# 3. 开始对话
./target/release/agent chat $SESSION_ID

# 4. 在对话中输入
You: 帮我写一个 Python 排序函数
AI: [生成响应...]

You: exit
```

### 技能系统示例

```bash
# 交互式使用
./target/release/skills-cli interactive

> list
所有技能:
  - greeting (友好的问候)
  - work:email:reply (回复工作邮件)
  ...

> @greeting user_name='Alice'
结果:
你好 Alice！我将以friendly的方式为您服务。

> quit
```

## 🔍 故障排查

### Ollama 连接失败
```bash
# 检查 Ollama 是否运行
curl http://localhost:11434/api/tags

# 启动 Ollama
ollama serve
```

### 数据库错误
```bash
# 删除并重新创建数据库
rm agent.db
./target/release/agent new --title "测试"
```

### 编译错误
```bash
# 清理并重新编译
cargo clean
cargo build --release
```

## 📚 更多文档

- **技能系统**: `crates/agent-skills/README.md`
- **验收指南**: `crates/agent-skills/ACCEPTANCE.md`
- **设计文档**: `docs/plans/skills-system-design.md`

## 🎯 下一步

### 可选扩展
1. **技能系统集成**: 将 skills 集成到 agent-workflow
2. **TUI 界面**: 实现 agent-tui 交互界面
3. **API 服务**: 实现 agent-api REST API

## 📊 Git 提交历史

```bash
# 查看技能系统开发历史
git log --oneline --grep="feat(v2)" | head -10

4a0c4ae feat(v2): 添加 CLI 工具用于手工验收
8bb288d feat(v2): 添加集成测试和完整文档 - Week 6 完成
8ff0b1f feat(v2): 实现 SkillExecutor - Week 6 Day 1-2 完成
c38d85f feat(v2): 实现 SkillRegistry - Day 3-4 完成
7d71765 feat(v2): 实现 SkillLoader - Day 3
5d7c9bc feat(v2): 实现 SkillParser - Day 2
539b5f9 feat(v2): 实现技能系统数据模型 - Day 1
```

---

**部署完成！** 🎉

现在您可以使用编译好的二进制文件进行验收和测试。
