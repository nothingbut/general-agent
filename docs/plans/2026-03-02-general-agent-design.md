# General Agent 系统设计文档

**日期：** 2026-03-02
**版本：** 1.0
**状态：** 已批准

---

## 1. 项目概述

### 1.1 目标

构建一个通用的个人助理Agent系统，能够：
- 兼容Claude Code的Skill协议
- 集成Model Context Protocol (MCP)服务器生态
- 支持RAG（检索增强生成）能力
- 提供混合交互模式（问答、对话、任务执行）

### 1.2 应用场景

- **个人助理**：任务管理、日程安排、笔记整理
- **知识管理**：文档总结、问答检索、研究辅助
- **生产力工具**：番茄钟、时间追踪、提醒系统

### 1.3 部署方式

- **云端部署**：Web应用，FastAPI后端
- **轻量级**：单机部署，适合个人/小团队使用
- **可扩展**：插件化架构，支持后续功能扩展

---

## 2. 整体架构

### 2.1 架构图

```
┌─────────────────────────────────────────────────────────┐
│                    Web Interface                        │
│                  (FastAPI + Jinja2)                     │
└──────────────────┬──────────────────────────────────────┘
                   │ HTTP/WebSocket
┌──────────────────▼──────────────────────────────────────┐
│                  Agent Core (协调层)                     │
│  ┌─────────────┬─────────────┬──────────────┐          │
│  │   Router    │   Context   │   Executor   │          │
│  │   (路由)    │  (上下文)   │   (执行器)   │          │
│  └─────────────┴─────────────┴──────────────┘          │
└───────┬──────────────┬──────────────┬──────────────────┘
        │              │              │
┌───────▼──────┐ ┌────▼─────┐ ┌──────▼──────┐
│   Skill      │ │   MCP    │ │    RAG      │
│   System     │ │  Client  │ │   Engine    │
└───────┬──────┘ └────┬─────┘ └──────┬──────┘
        │              │              │
┌───────▼──────────────▼──────────────▼──────────────────┐
│              Storage Layer (存储层)                     │
│  ┌──────────────────┐  ┌──────────────────┐           │
│  │   SQLite         │  │  Vector DB       │           │
│  │  (结构化数据)    │  │  (Chroma/Qdrant) │           │
│  │  - 会话历史      │  │  - 文档向量      │           │
│  │  - 任务状态      │  │  - 语义检索      │           │
│  │  - 用户配置      │  │                  │           │
│  └──────────────────┘  └──────────────────┘           │
└─────────────────────────────────────────────────────────┘
```

### 2.2 设计原则

- **插件化单体架构**：单个应用，内部模块化
- **分层清晰**：Web层、协调层、插件层、存储层各司其职
- **松耦合**：Skill、MCP、RAG独立可替换
- **渐进增强**：MVP先行，后续平滑扩展

---

## 3. 核心组件设计

### 3.1 Agent Core（协调层）

Agent Core是系统大脑，负责协调所有能力。

#### 3.1.1 Router（路由器）

**职责：** 智能路由用户请求到合适的处理器

**核心逻辑：**
```python
class AgentRouter:
    def route(self, user_input: str, context: Context) -> ExecutionPlan:
        """
        分析用户输入，决定执行策略：
        - 简单问答 → 直接LLM
        - 需要知识 → RAG检索
        - 需要工具 → Skill/MCP调用
        - 复杂任务 → 多步骤规划
        """
        intent = self._analyze_intent(user_input)
        capabilities = self._select_capabilities(intent)
        return self._build_plan(capabilities, context)
```

**路由策略：**
- 关键词匹配：快速识别常见意图
- 语义分析：使用LLM判断工具需求
- 上下文感知：基于会话历史优化决策

#### 3.1.2 Context（上下文管理器）

**职责：** 管理多轮对话上下文

**核心能力：**
```python
class ContextManager:
    def add_message(self, role: str, content: str)
    def get_recent_history(self, limit: int = 10)
    def search_relevant_history(self, query: str)
    def summarize_and_store()
```

**上下文策略：**
- **滑动窗口**：保留最近N轮对话
- **语义检索**：从历史中检索相关内容（通过RAG）
- **自动总结**：长会话自动压缩

#### 3.1.3 Executor（执行器）

**职责：** 执行计划，协调Skill、MCP、RAG

**执行模式：**
- **直接模式**：单个Skill/MCP调用
- **链式模式**：A的输出作为B的输入
- **并行模式**：多个独立任务同时执行
- **循环模式**：ReAct风格（推理-行动-观察循环）

```python
class AgentExecutor:
    def execute(self, plan: ExecutionPlan) -> Result:
        if plan.is_simple():
            return self._execute_single(plan)
        else:
            return self._execute_workflow(plan)
```

---

### 3.2 Skill System（技能系统）

**目标：** 兼容Claude Code的Skill协议，支持动态加载技能插件

#### 3.2.1 Skill协议定义

```markdown
---
name: example-skill
description: 技能的简短描述
parameters:
  - name: param1
    type: string
    required: true
    description: 参数说明
---

# Skill Content
技能的详细说明和执行逻辑...
```

#### 3.2.2 目录结构与管理

```
skills/
├── .ignore                    # 忽略规则文件（支持glob模式）
├── personal/                  # 个人助理相关
│   ├── calendar.md
│   ├── reminder.md
│   └── note-taking.md
├── productivity/              # 生产力工具
│   ├── task-management.md
│   ├── pomodoro.md
│   └── time-tracking.md
├── knowledge/                 # 知识管理
│   ├── summarize.md
│   ├── qa.md
│   └── research.md
└── experimental/              # 实验性功能（可被忽略）
    └── voice-assistant.md
```

**.ignore 文件格式：**
```gitignore
# 忽略整个目录
experimental/*
deprecated/*

# 忽略特定文件
personal/draft-*.md

# 忽略所有test文件
**/test-*.md
```

#### 3.2.3 核心实现

```python
class SkillLoader:
    def __init__(self, skills_dir: str):
        self.skills_dir = skills_dir
        self.skills = {}
        self.ignore_patterns = self._load_ignore_file()

    def load_all(self):
        """递归扫描目录，加载所有未被忽略的skills"""
        for skill_file in glob(f"{self.skills_dir}/**/*.md", recursive=True):
            if self._should_ignore(skill_file):
                continue

            skill = self._parse_skill(skill_file)
            namespace = self._extract_namespace(skill_file)
            skill.full_name = f"{namespace}:{skill.name}" if namespace else skill.name
            self.skills[skill.full_name] = skill
```

**命名空间访问：**
```python
# 完整路径
executor.execute("personal:reminder", params)

# 简写（如果无冲突）
executor.execute("reminder", params)
```

#### 3.2.4 关键特性

- ✅ 兼容Claude Code格式
- ✅ 分类组织（按功能域分目录）
- ✅ 命名空间隔离（避免名称冲突）
- ✅ 灵活过滤（通过.ignore控制加载）
- ✅ 热加载（无需重启添加新skill）
- ✅ 沙箱执行（脚本类skill隔离运行）

---

### 3.3 MCP Client（MCP客户端）

**目标：** 集成MCP服务器生态，调用外部工具和资源

#### 3.3.1 MCP服务器配置

```json
{
  "servers": {
    "filesystem": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/Users/user"],
      "description": "文件系统访问"
    },
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github"],
      "env": {
        "GITHUB_TOKEN": "${GITHUB_TOKEN}"
      },
      "description": "GitHub API集成"
    }
  }
}
```

#### 3.3.2 核心组件

**MCPConnectionManager：** 管理服务器连接
```python
class MCPConnectionManager:
    async def start_server(self, server_name: str) -> MCPConnection
    async def stop_server(self, server_name: str)
```

**MCPToolExecutor：** 执行MCP工具
```python
class MCPToolExecutor:
    async def discover_tools()  # 发现所有工具
    async def call_tool(tool_name: str, arguments: dict) -> Any
```

**MCPResourceAccessor：** 访问MCP资源
```python
class MCPResourceAccessor:
    async def list_resources(server_name: str) -> List[dict]
    async def read_resource(server_name: str, uri: str) -> str
```

#### 3.3.3 关键特性

- ✅ 动态发现：自动发现MCP服务器提供的工具
- ✅ 命名空间隔离：避免不同服务器工具名冲突（如 `filesystem:read`）
- ✅ 配置驱动：通过JSON管理MCP服务器
- ✅ 异步通信：支持stdio协议的异步通信
- ✅ 环境变量：安全处理敏感信息

---

### 3.4 RAG Engine（检索增强生成）

**目标：** 实现混合检索（向量检索 + 关键词检索 + 重排序）

#### 3.4.1 RAG架构

```
User Query
    ↓
Query Processing (查询处理)
    ├→ 向量化 (Embedding)
    └→ 关键词提取 (Keywords)
    ↓
Parallel Retrieval (并行检索)
    ├→ Vector Search (Chroma/Qdrant)
    └→ Keyword Search (SQLite FTS5)
    ↓
Result Fusion (结果融合 - RRF)
    ↓
Re-ranking (重排序)
    ↓
Context Augmentation (上下文增强)
```

#### 3.4.2 核心组件

**DocumentIndexer：** 文档索引器
```python
class DocumentIndexer:
    def index_document(self, doc: Document):
        # 1. 文档分块（智能分块，尊重语义边界）
        # 2. 生成向量并存储到向量数据库
        # 3. 全文索引到SQLite FTS5
```

**HybridRetriever：** 混合检索器
```python
class HybridRetriever:
    async def retrieve(self, query: str, top_k: int = 10) -> List[Document]:
        # 并行执行向量检索和关键词检索
        # 使用RRF算法融合结果
        # 重排序（Cross-Encoder或LLM）
```

**RAGContextBuilder：** 上下文构建器
```python
class RAGContextBuilder:
    async def build_context(self, query: str, max_tokens: int = 2000) -> str:
        # 检索相关文档
        # 过滤和截断以适应token限制
        # 格式化为prompt
```

**KnowledgeBase：** 知识库管理
```python
class KnowledgeBase:
    def add_from_file(self, file_path: str)
    def add_from_conversation(self, conversation_id: str)
    def add_from_url(self, url: str)
    def delete(self, doc_id: str)
```

#### 3.4.3 关键特性

- ✅ 混合检索：向量+关键词，结果更全面
- ✅ 智能分块：尊重语义边界，保留上下文重叠
- ✅ RRF融合：Reciprocal Rank Fusion算法融合结果
- ✅ 重排序：二次精排提升准确性
- ✅ 多源导入：文件、会话、网页等
- ✅ Token管理：自动截断适应LLM限制

---

### 3.5 API层与Web界面

#### 3.5.1 API设计

**REST API：**
```python
POST   /api/chat              # 单轮对话
POST   /api/task              # 创建复杂任务
GET    /api/task/{task_id}    # 查询任务状态
POST   /api/knowledge         # 添加知识
GET    /api/skills            # 列出所有技能
GET    /api/mcp/tools         # 列出MCP工具
```

**WebSocket：**
```python
WS     /ws/chat               # 实时对话，支持流式响应
```

**消息格式：**
```json
// 请求
{
  "message": "用户输入",
  "session_id": "会话ID"
}

// 响应（流式）
{
  "type": "thought|tool_call|response",
  "content": "...",
  "metadata": {...}
}
```

#### 3.5.2 Web界面

**布局：** 三栏布局
- **左侧边栏**：会话历史列表
- **中间区域**：聊天消息区 + 输入框
- **右侧面板**：技能列表、知识库、设置

**交互特性：**
- 实时消息流
- 状态可视化（显示思考过程、工具调用）
- 会话管理（新建、切换、删除）
- 知识库管理（上传文档、查看列表）

---

## 4. 数据存储

### 4.1 SQLite（结构化数据）

**表结构：**

```sql
-- 会话表
CREATE TABLE sessions (
    id TEXT PRIMARY KEY,
    created_at TIMESTAMP,
    updated_at TIMESTAMP,
    title TEXT,
    metadata JSON
);

-- 消息表
CREATE TABLE messages (
    id TEXT PRIMARY KEY,
    session_id TEXT,
    role TEXT,  -- 'user' | 'assistant' | 'system'
    content TEXT,
    timestamp TIMESTAMP,
    metadata JSON,
    FOREIGN KEY (session_id) REFERENCES sessions(id)
);

-- 任务表
CREATE TABLE tasks (
    id TEXT PRIMARY KEY,
    description TEXT,
    status TEXT,  -- 'pending' | 'in_progress' | 'completed'
    plan JSON,
    result JSON,
    created_at TIMESTAMP,
    updated_at TIMESTAMP
);

-- 全文检索（FTS5）
CREATE VIRTUAL TABLE documents_fts USING fts5(
    doc_id,
    content,
    metadata
);

-- 用户配置
CREATE TABLE config (
    key TEXT PRIMARY KEY,
    value JSON
);
```

### 4.2 Vector Database（向量数据库）

**选项：**
- **Chroma**（推荐）：轻量级，Python原生，易于集成
- **Qdrant**：高性能，支持复杂过滤

**存储内容：**
- 文档向量（文档分块后的embedding）
- 元数据（文档来源、时间戳等）
- 会话总结向量（用于长期记忆检索）

---

## 5. 技术栈

### 5.1 后端

- **框架**：FastAPI（异步、高性能、自动API文档）
- **数据库**：SQLite（结构化数据）+ Chroma/Qdrant（向量）
- **AI模型**：
  - LLM：OpenAI/Claude API（或本地模型）
  - Embedding：OpenAI text-embedding-3 或本地模型
- **异步**：asyncio + aiohttp

### 5.2 前端

- **模板引擎**：Jinja2（服务端渲染）
- **交互**：原生JavaScript（WebSocket、Fetch API）
- **样式**：简洁CSS（无框架依赖）

### 5.3 开发工具

- **包管理**：uv（快速Python包管理器）
- **测试**：pytest（单元测试 + 集成测试）
- **代码质量**：ruff（linting）、mypy（类型检查）

---

## 6. 项目结构

```
general-agent/
├── docs/
│   └── plans/
│       └── 2026-03-02-general-agent-design.md
├── src/
│   ├── core/
│   │   ├── agent.py           # Agent主类
│   │   ├── router.py          # 路由器
│   │   ├── context.py         # 上下文管理
│   │   └── executor.py        # 执行器
│   ├── skills/
│   │   ├── loader.py          # Skill加载器
│   │   ├── executor.py        # Skill执行器
│   │   └── registry.py        # Skill注册表
│   ├── mcp/
│   │   ├── connection.py      # MCP连接管理
│   │   ├── executor.py        # MCP工具执行
│   │   └── resource.py        # MCP资源访问
│   ├── rag/
│   │   ├── indexer.py         # 文档索引
│   │   ├── retriever.py       # 混合检索
│   │   ├── context_builder.py # 上下文构建
│   │   └── knowledge_base.py  # 知识库管理
│   ├── storage/
│   │   ├── sqlite.py          # SQLite操作
│   │   └── vector.py          # 向量数据库
│   ├── api/
│   │   ├── routes.py          # API路由
│   │   └── websocket.py       # WebSocket处理
│   └── main.py                # 应用入口
├── skills/                     # Skill插件目录
│   ├── .ignore
│   ├── personal/
│   ├── productivity/
│   └── knowledge/
├── templates/                  # Jinja2模板
│   └── index.html
├── static/                     # 静态资源
│   ├── style.css
│   └── app.js
├── data/                       # 数据目录
│   ├── agent.db               # SQLite数据库
│   └── vector/                # 向量数据库
├── config/
│   └── mcp_config.json        # MCP服务器配置
├── tests/                      # 测试
│   ├── test_core/
│   ├── test_skills/
│   ├── test_mcp/
│   └── test_rag/
├── pyproject.toml             # 项目配置
└── README.md
```

---

## 7. 开发阶段

### Phase 1: 基础框架（Week 1-2）
- ✅ 项目初始化
- ✅ Agent Core基础实现（Router、Context、Executor）
- ✅ SQLite存储层
- ✅ 基础API（chat接口）
- ✅ 简单Web界面

### Phase 2: Skill System（Week 3）
- ✅ Skill加载器（支持.ignore）
- ✅ Skill解析器（YAML + Markdown）
- ✅ Skill执行器（prompt模式）
- ✅ 示例skills（reminder、note-taking）

### Phase 3: MCP集成（Week 4）
- ✅ MCP连接管理
- ✅ MCP工具发现和调用
- ✅ MCP资源访问
- ✅ 集成示例MCP服务器（filesystem、github）

### Phase 4: RAG Engine（Week 5-6）
- ✅ 文档索引器（分块、embedding）
- ✅ 向量数据库集成（Chroma）
- ✅ 混合检索器（向量+关键词）
- ✅ 上下文构建器
- ✅ 知识库管理接口

### Phase 5: TUI终端界面（Week 7）
- 🚧 基于Textual框架的TUI应用
- 🚧 聊天对话界面（消息列表 + 输入框）
- 🚧 实时流式响应显示
- 🚧 会话历史侧边栏
- 🚧 技能快速调用（自动补全@skill）
- 🚧 Markdown渲染和代码高亮
- 🚧 键盘快捷键支持

### Phase 6: 增强与优化（Week 8-9）
- ✅ WebSocket流式响应
- ✅ 任务管理（复杂任务编排）
- ✅ 会话总结和长期记忆
- ✅ 性能优化
- ✅ 测试覆盖（80%+）

### Phase 7: 部署与文档（Week 10）
- ✅ Docker容器化
- ✅ 部署指南
- ✅ API文档
- ✅ 用户手册

---

## 8. 非功能性需求

### 8.1 性能

- **响应时间**：简单问答 < 1s，复杂任务 < 5s
- **并发**：支持50+并发会话
- **检索速度**：RAG检索 < 500ms

### 8.2 可靠性

- **错误处理**：优雅降级，错误信息用户友好
- **数据持久化**：所有会话、任务、知识库持久化
- **幂等性**：任务执行支持重试

### 8.3 安全性

- **输入验证**：所有用户输入严格验证
- **沙箱执行**：Skill脚本隔离执行
- **秘密管理**：敏感信息使用环境变量

### 8.4 可维护性

- **代码质量**：遵循PEP 8，类型注解，80%+测试覆盖
- **文档**：代码注释、API文档、架构文档
- **日志**：结构化日志，便于调试

---

## 9. 风险与挑战

### 9.1 技术风险

**风险：** MCP协议兼容性问题
- **缓解**：优先测试主流MCP服务器，提供降级方案

**风险：** RAG检索质量不稳定
- **缓解**：混合检索+重排序，持续优化检索策略

**风险：** LLM API成本
- **缓解**：支持本地模型，实现请求缓存

### 9.2 业务风险

**风险：** 用户需求不明确
- **缓解**：MVP快速迭代，收集用户反馈

**风险：** Skill生态构建缓慢
- **缓解**：兼容Claude Code格式，复用现有skills

---

## 10. 后续演进

### 10.1 短期（3个月内）

- **TUI终端界面**：基于Textual的本地命令行交互（Phase 5）
  - 聊天对话界面（消息列表 + 输入框）
  - 实时流式响应显示
  - 会话历史管理
  - 技能快速调用（自动补全）
  - Markdown渲染和代码高亮
- 多用户支持（认证、权限）
- 更多预置Skills（邮件、日历、天气等）

### 10.2 中期（6个月内）

- 语音交互
- 本地模型支持（Llama、Mistral等）
- 高级任务编排（工作流引擎）

### 10.3 长期（1年内）

- 多Agent协作
- 插件市场（Skill/MCP共享）
- 微服务架构演进（高并发场景）

---

## 11. 总结

本设计文档描述了一个插件化、可扩展的通用Agent系统，核心特性包括：

✅ **Skill System**：兼容Claude Code，分目录管理，灵活过滤
✅ **MCP Client**：完整MCP生态集成，动态工具发现
✅ **RAG Engine**：混合检索，智能分块，重排序优化
✅ **统一协调**：Agent Core统一调度，支持多种执行模式
✅ **轻量部署**：SQLite + FastAPI，单机可运行
✅ **渐进增强**：MVP先行，后续平滑扩展到微服务

该架构在满足当前需求的同时，为未来演进预留了充足空间。
