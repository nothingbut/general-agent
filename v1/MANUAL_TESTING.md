# General Agent 手工验收指南

**分支:** main
**包含 Phase:** 1-6
**日期:** 2026-03-08

---

## 快速启动

### 1. 环境准备
```bash
# 确认在 main 分支
git branch

# 安装依赖（如需要）
pip install -r requirements.txt

# 运行测试确保一切正常
python -m pytest tests/ -v
```

---

## Phase 1: 基础聊天功能

### 功能验收
```bash
# 启动 Web API
python -m uvicorn src.api.main:app --reload

# 测试聊天（新终端）
curl -X POST http://localhost:8000/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "你好", "session_id": "test-session"}'
```

**验证点:**
- ✅ API 正常响应
- ✅ 返回 JSON 包含 response 字段
- ✅ 会话持久化工作

---

## Phase 2: Skills 技能系统

### 功能验收
```bash
# 测试技能列表
python -c "from src.skills.manager import SkillManager; m = SkillManager(); print(m.list_skills())"

# 测试技能执行
python -c "
from src.skills.executor import SkillExecutor
executor = SkillExecutor()
# 测试内置技能
"
```

**验证点:**
- ✅ 技能发现正常
- ✅ 技能执行成功
- ✅ 错误处理正确

---

## Phase 3-4: MCP 集成

### 功能验收
```bash
# 检查 MCP 配置
cat ~/.config/general-agent/mcp-servers.json

# 测试 MCP 连接
python -c "
from src.mcp.connection import MCPConnectionManager
import asyncio

async def test():
    manager = MCPConnectionManager()
    servers = await manager.list_servers()
    print(f'可用 MCP 服务器: {servers}')

asyncio.run(test())
"
```

**验证点:**
- ✅ MCP 配置正确加载
- ✅ 服务器连接正常
- ✅ 工具调用成功

---

## Phase 5: RAG 知识库

### 功能验收
```bash
# 测试 RAG 索引
python -c "
from src.rag.indexer import RAGIndexer
indexer = RAGIndexer()
# 索引测试文档
indexer.index_directory('docs/')
print('索引完成')
"

# 测试检索
python -c "
from src.rag.retrieval import RAGRetriever
retriever = RAGRetriever()
results = retriever.search('如何使用 Agent')
print(f'检索结果: {len(results)} 条')
"
```

**验证点:**
- ✅ 文档索引成功
- ✅ 检索结果相关
- ✅ 向量数据库正常

---

## Phase 6: TUI 终端界面

### 功能验收
```bash
# 启动 TUI
python -m src.cli.app

# 或使用快速查询
python -m src.cli.main quick "你好，测试一下"
```

**验证点:**
- ✅ TUI 界面正常显示
- ✅ 输入框可用
- ✅ 消息历史显示
- ✅ 快捷键工作（Ctrl+C 退出）
- ✅ 会话管理正常

**TUI 功能测试:**
- 输入消息并发送
- 查看历史记录
- 切换会话
- 退出应用

---

## 集成测试

### 完整流程验收
```bash
# 1. 启动 TUI
python -m src.cli.app

# 2. 在 TUI 中测试：
#    - 基础对话
#    - 调用技能
#    - 使用 MCP 工具
#    - 查询 RAG 知识库

# 3. 验证数据持久化
python -c "
from src.storage.database import Database
import asyncio

async def check():
    db = Database()
    await db.initialize()
    sessions = await db.get_recent_sessions(limit=10)
    print(f'最近会话数: {len(sessions)}')

asyncio.run(check())
"
```

---

## 性能测试

### 基准测试
```bash
# 运行性能测试
python -m pytest tests/ -k "performance" -v

# 检查响应时间
python -m pytest tests/api/test_routes.py::test_chat_endpoint -v --durations=10
```

---

## 常见问题排查

### 1. 数据库初始化失败
```bash
# 删除旧数据库
rm -f ~/.local/share/general-agent/agent.db

# 重新初始化
python -c "
from src.storage.database import Database
import asyncio
asyncio.run(Database().initialize())
"
```

### 2. MCP 服务器连接失败
```bash
# 检查配置
cat ~/.config/general-agent/mcp-servers.json

# 测试连接
python -m src.mcp.connection test
```

### 3. RAG 向量库问题
```bash
# 重建索引
python -m src.rag.indexer rebuild

# 检查向量库
python -c "
from src.rag.storage import get_vector_store
store = get_vector_store()
print(f'向量数量: {store.count()}')
"
```

---

## 验收清单

**Phase 1 - 基础聊天:**
- [ ] Web API 正常运行
- [ ] 聊天接口响应正确
- [ ] 会话管理工作
- [ ] 数据库持久化

**Phase 2 - Skills:**
- [ ] 技能发现正常
- [ ] 技能执行成功
- [ ] 错误处理正确

**Phase 3-4 - MCP:**
- [ ] 配置加载正确
- [ ] 服务器连接成功
- [ ] 工具调用正常

**Phase 5 - RAG:**
- [ ] 文档索引成功
- [ ] 检索结果准确
- [ ] 向量库稳定

**Phase 6 - TUI:**
- [ ] 界面正常显示
- [ ] 交互流畅
- [ ] 快捷键工作
- [ ] 会话切换正常

**集成测试:**
- [ ] 所有模块协同工作
- [ ] 端到端流程通畅
- [ ] 性能满足要求

---

## 返回开发分支

验收完成后，切回开发分支：
```bash
git checkout feature/phase7-agent-workflow
```

---

**验收完成标准:** 所有验收清单项目打勾 ✅
