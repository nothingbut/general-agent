# Phase 1 & 2 人工验收指南

**日期：** 2026-03-04
**版本：** v0.2
**验收人：** _______
**验收状态：** 🔲 待验收

---

## 📋 验收概述

本文档提供Phase 1（基础框架）和Phase 2（技能系统）的完整验收流程，包括：
- 环境准备和启动步骤
- 功能验收清单（每项带操作步骤和预期结果）
- 性能和质量验证
- 已知问题记录

**预计验收时间：** 30-45分钟

---

## 1️⃣ 环境准备

### 1.1 安装依赖

```bash
# 进入项目目录
cd /Users/shichang/Workspace/program/python/general-agent

# 安装依赖（使用uv或pip）
uv pip install -e ".[dev]"
# 或
pip install -e ".[dev]"
```

**验证：**
```bash
# 检查Python版本
python --version  # 应该是 3.12+

# 检查依赖安装
pip list | grep -E "fastapi|uvicorn|pydantic"
```

### 1.2 启动服务

```bash
# 启动FastAPI服务器
uvicorn src.main:app --reload --port 8000
```

**验证：**
- 终端输出应显示：`Application startup complete`
- 访问 http://localhost:8000 应该看到Web界面
- 访问 http://localhost:8000/docs 应该看到API文档

### 1.3 打开新终端（用于API测试）

保持服务器运行，打开新终端用于执行验收命令。

---

## 2️⃣ Phase 1 验收：基础框架

### ✅ 2.1 项目初始化

**验收项：** 项目结构完整，依赖配置正确

**检查清单：**
- [ ] `pyproject.toml` 存在且配置完整
- [ ] `src/` 目录结构清晰
- [ ] `tests/` 目录包含测试用例
- [ ] `.gitignore` 配置正确

**操作：**
```bash
# 检查项目结构
tree -L 2 src/
tree -L 2 tests/

# 查看配置文件
cat pyproject.toml | grep -A 5 "\[project\]"
```

**预期结果：**
```
src/
├── core/          # 核心模块
├── skills/        # 技能系统
├── storage/       # 存储层
├── api/           # API路由
└── main.py        # 应用入口

tests/
├── unit/          # 单元测试
└── integration/   # 集成测试
```

---

### ✅ 2.2 Agent Core基础实现

#### 2.2.1 Router（路由器）

**验收项：** 能够正确路由用户请求

**操作：**
```bash
# 测试简单查询
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello", "session_id": "test-1"}'
```

**预期结果：**
```json
{
  "response": "我收到了你的消息：「Hello」...",
  "session_id": "test-1",
  "plan_type": "simple_query"
}
```

**验证点：**
- [ ] 返回状态码 200
- [ ] 包含 `response` 字段
- [ ] 包含 `session_id` 字段
- [ ] 包含 `plan_type` 字段

#### 2.2.2 Context（上下文管理器）

**验收项：** 正确管理多轮对话上下文

**操作：**
```bash
# 第1轮：告诉名字
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "My name is Alice", "session_id": "test-context"}'

# 第2轮：询问名字（应该记住）
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What is my name?", "session_id": "test-context"}'
```

**预期结果：**
- 第2轮响应应该包含 "Alice"
- 显示系统记住了之前的上下文

**验证点：**
- [ ] 第2轮响应提到了名字 "Alice"
- [ ] 会话ID保持一致
- [ ] 上下文在同一会话内保持

#### 2.2.3 Executor（执行器）

**验收项：** 能够执行基本的LLM调用

**操作：**
```bash
# 测试执行器响应
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Tell me a joke", "session_id": "test-executor"}'
```

**预期结果：**
- 返回AI生成的内容
- 响应时间 < 5秒

**验证点：**
- [ ] 返回有意义的AI回复
- [ ] 响应时间合理
- [ ] 无错误或异常

---

### ✅ 2.3 SQLite存储层

**验收项：** 数据正确持久化到SQLite

**操作：**
```bash
# 发送几条消息
for i in {1..3}; do
  curl -X POST http://localhost:8000/api/chat \
    -H "Content-Type: application/json" \
    -d "{\"message\": \"Test message $i\", \"session_id\": \"test-storage\"}"
  sleep 1
done

# 检查数据库文件
ls -lh data/agent.db

# 查询数据库内容
sqlite3 data/agent.db "SELECT COUNT(*) FROM messages;"
sqlite3 data/agent.db "SELECT role, substr(content, 1, 50) FROM messages LIMIT 5;"
```

**预期结果：**
- `data/agent.db` 文件存在
- 至少有6条消息（3条用户 + 3条助手）
- 消息内容正确存储

**验证点：**
- [ ] 数据库文件存在
- [ ] 消息数量正确
- [ ] 消息内容完整
- [ ] 时间戳正确

---

### ✅ 2.4 基础API（chat接口）

**验收项：** Chat API功能完整且符合规范

#### 测试1：基本聊天

**操作：**
```bash
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello", "session_id": "api-test-1"}' \
  | jq '.'
```

**验证点：**
- [ ] 返回 200 状态码
- [ ] JSON格式正确
- [ ] 包含 response、session_id、plan_type 字段

#### 测试2：会话ID自动生成

**操作：**
```bash
# 不提供session_id
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Test auto session"}' \
  | jq '.session_id'
```

**验证点：**
- [ ] 返回自动生成的session_id
- [ ] session_id格式正确（如：sess-xxx）

#### 测试3：错误处理

**操作：**
```bash
# 空消息
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "", "session_id": "error-test"}'

# 无效JSON
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d 'invalid json'
```

**验证点：**
- [ ] 返回适当的错误码（400/422）
- [ ] 错误消息友好且清晰
- [ ] 不会导致服务器崩溃

---

### ✅ 2.5 简单Web界面

**验收项：** Web界面可用且功能正常

#### 测试1：访问首页

**操作：**
1. 打开浏览器访问 http://localhost:8000
2. 检查页面元素

**验证点：**
- [ ] 页面正常加载
- [ ] 看到消息输入框
- [ ] 看到发送按钮
- [ ] 页面布局清晰美观

#### 测试2：发送消息

**操作：**
1. 在输入框输入 "Hello"
2. 点击发送按钮或按Enter
3. 等待响应

**验证点：**
- [ ] 消息出现在聊天区域
- [ ] AI回复出现在聊天区域
- [ ] 消息格式正确（用户消息和AI消息区分明显）
- [ ] 滚动条自动滚动到底部

#### 测试3：多轮对话

**操作：**
1. 连续发送3-5条消息
2. 观察聊天历史

**验证点：**
- [ ] 所有消息正确显示
- [ ] 消息顺序正确
- [ ] 页面不卡顿
- [ ] 输入框在发送后自动清空

#### 测试4：API文档

**操作：**
1. 访问 http://localhost:8000/docs (Swagger UI)
2. 访问 http://localhost:8000/redoc (ReDoc)

**验证点：**
- [ ] Swagger UI正常显示
- [ ] ReDoc正常显示
- [ ] 可以看到所有API端点
- [ ] 可以直接在Swagger UI测试API

---

## 3️⃣ Phase 2 验收：技能系统

### ✅ 3.1 Skill加载器（支持.ignore）

**验收项：** 正确加载技能，支持.ignore过滤

#### 测试1：查看可用技能

**操作：**
```bash
# 查看技能目录
ls -R skills/

# 查看.ignore文件
cat skills/.ignore
```

**验证点：**
- [ ] `skills/personal/` 目录存在
- [ ] `skills/productivity/` 目录存在
- [ ] `.ignore` 文件存在且配置合理

#### 测试2：验证技能加载

**操作：**
```bash
# 启动服务器后查看日志
# 应该看到类似 "Loaded X skills" 的信息

# 或通过API查询（如果有技能列表端点）
curl http://localhost:8000/api/skills
```

**预期结果：**
- 加载至少5个技能
- 被.ignore的技能未被加载

**验证点：**
- [ ] 成功加载 greeting 技能
- [ ] 成功加载 reminder 技能
- [ ] 成功加载 note 技能
- [ ] 成功加载 task 技能
- [ ] 成功加载 brainstorm 技能

---

### ✅ 3.2 Skill解析器（YAML + Markdown）

**验收项：** 正确解析技能文件格式

**操作：**
```bash
# 查看技能文件格式
cat skills/personal/greeting.md
cat skills/personal/reminder.md
```

**验证点：**
- [ ] YAML frontmatter正确
- [ ] 包含 name、description 字段
- [ ] 包含 parameters 定义（如果有参数）
- [ ] Markdown内容清晰

---

### ✅ 3.3 Skill执行器（prompt模式）

**验收项：** 能够正确执行各种技能

#### 测试1：无参数技能（greeting）

**操作：**
```bash
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@greeting", "session_id": "skill-test-1"}' \
  | jq '.response'
```

**预期结果：**
- 返回友好的问候消息
- 内容包含问候语（如 "Hello", "Hi", "你好" 等）

**验证点：**
- [ ] 返回状态码 200
- [ ] 响应包含问候语
- [ ] 响应内容符合greeting技能定义

#### 测试2：带参数技能（reminder）

**操作：**
```bash
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@reminder task=\"Buy milk\" time=\"5pm\"", "session_id": "skill-test-2"}' \
  | jq '.response'
```

**预期结果：**
- 返回设置提醒的确认消息
- 包含任务内容 "Buy milk"
- 包含时间 "5pm"

**验证点：**
- [ ] 返回状态码 200
- [ ] 响应包含任务内容
- [ ] 响应包含时间信息
- [ ] 参数正确解析和传递

#### 测试3：复杂参数技能（note）

**操作：**
```bash
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@note content=\"Meeting notes for today\" category=\"work\"", "session_id": "skill-test-3"}' \
  | jq '.response'
```

**预期结果：**
- 返回笔记创建确认
- 包含笔记内容和分类

**验证点：**
- [ ] 返回状态码 200
- [ ] 响应包含笔记内容
- [ ] 响应包含分类信息
- [ ] 多个参数正确处理

#### 测试4：技能链式调用

**操作：**
```bash
# 先设置提醒
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@reminder task=\"Call John\" time=\"3pm\"", "session_id": "skill-chain"}'

# 然后记笔记
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@note content=\"Remember to call John at 3pm\" category=\"personal\"", "session_id": "skill-chain"}'
```

**验证点：**
- [ ] 两个技能都成功执行
- [ ] 在同一会话中保持上下文
- [ ] 无冲突或错误

---

### ✅ 3.4 示例Skills验收

#### Skill 1: greeting（问候）

**操作：**
```bash
# Web界面测试
# 1. 访问 http://localhost:8000
# 2. 输入 "@greeting" 并发送

# API测试
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@greeting", "session_id": "greeting-test"}' \
  | jq '.response'
```

**验证点：**
- [ ] ✅ 返回友好问候
- [ ] ✅ 响应时间 < 2秒
- [ ] ✅ 内容符合预期

---

#### Skill 2: reminder（提醒）

**操作：**
```bash
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@reminder task=\"Team meeting\" time=\"2pm\"", "session_id": "reminder-test"}' \
  | jq '.response'
```

**验证点：**
- [ ] ✅ 成功创建提醒
- [ ] ✅ 包含任务和时间信息
- [ ] ✅ 确认消息清晰

---

#### Skill 3: note（笔记）

**操作：**
```bash
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@note content=\"Project ideas: AI chatbot\" category=\"work\"", "session_id": "note-test"}' \
  | jq '.response'
```

**验证点：**
- [ ] ✅ 成功创建笔记
- [ ] ✅ 包含内容和分类
- [ ] ✅ 确认消息清晰

---

#### Skill 4: task（任务管理）

**操作：**
```bash
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@task title=\"Review PR\" priority=\"high\"", "session_id": "task-test"}' \
  | jq '.response'
```

**验证点：**
- [ ] ✅ 成功创建任务
- [ ] ✅ 包含标题和优先级
- [ ] ✅ 确认消息清晰

---

#### Skill 5: brainstorm（头脑风暴）

**操作：**
```bash
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@brainstorm topic=\"New features for chatbot\"", "session_id": "brainstorm-test"}' \
  | jq '.response'
```

**验证点：**
- [ ] ✅ 生成头脑风暴内容
- [ ] ✅ 包含多个想法
- [ ] ✅ 格式清晰易读

---

### ✅ 3.5 Router集成（@skill 和 /skill 语法）

**验收项：** Router能正确识别和路由技能调用

#### 测试1：@skill 语法

**操作：**
```bash
# 使用 @ 语法
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@greeting", "session_id": "syntax-test-1"}' \
  | jq '.plan_type'
```

**验证点：**
- [ ] plan_type 为 "skill_execution"
- [ ] 技能正确执行

#### 测试2：/skill 语法（如果支持）

**操作：**
```bash
# 使用 / 语法
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "/greeting", "session_id": "syntax-test-2"}' \
  | jq '.plan_type'
```

**验证点：**
- [ ] 如果支持，plan_type 为 "skill_execution"
- [ ] 如果不支持，返回友好提示

#### 测试3：混合使用

**操作：**
```bash
# 普通消息 + 技能调用
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Please @greeting and then help me", "session_id": "mixed-test"}' \
  | jq '.response'
```

**验证点：**
- [ ] Router正确识别技能
- [ ] 执行流程正确
- [ ] 响应合理

---

## 4️⃣ 性能和质量验证

### ✅ 4.1 运行测试套件

**操作：**
```bash
# 运行所有测试
pytest -v

# 运行带覆盖率的测试
pytest --cov=src --cov-report=html --cov-report=term

# 查看覆盖率报告
open htmlcov/index.html  # macOS
# 或
xdg-open htmlcov/index.html  # Linux
```

**验证点：**
- [ ] 所有测试通过
- [ ] 测试覆盖率 ≥ 80%
- [ ] 无测试失败或错误

---

### ✅ 4.2 代码质量检查

**操作：**
```bash
# Ruff检查
ruff check src/

# Mypy类型检查
mypy src/
```

**验证点：**
- [ ] 无严重的代码质量问题
- [ ] 类型注解完整
- [ ] 遵循PEP 8规范

---

### ✅ 4.3 性能基准测试

**操作：**
```bash
# 测试响应时间
time curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello", "session_id": "perf-test"}'

# 连续测试10次
for i in {1..10}; do
  time curl -s -X POST http://localhost:8000/api/chat \
    -H "Content-Type: application/json" \
    -d '{"message": "Test '$i'", "session_id": "perf-test"}' > /dev/null
done
```

**验证点：**
- [ ] 平均响应时间 < 2秒（简单查询）
- [ ] 平均响应时间 < 3秒（技能调用）
- [ ] 无明显性能衰减

---

## 5️⃣ 验收总结

### ✅ 5.1 验收清单总览

#### Phase 1: 基础框架
- [ ] 2.1 项目初始化 ✅
- [ ] 2.2.1 Router（路由器）✅
- [ ] 2.2.2 Context（上下文管理）✅
- [ ] 2.2.3 Executor（执行器）✅
- [ ] 2.3 SQLite存储层 ✅
- [ ] 2.4 基础API（chat接口）✅
- [ ] 2.5 简单Web界面 ✅

#### Phase 2: 技能系统
- [ ] 3.1 Skill加载器（支持.ignore）✅
- [ ] 3.2 Skill解析器（YAML + Markdown）✅
- [ ] 3.3 Skill执行器（prompt模式）✅
- [ ] 3.4.1 greeting 技能 ✅
- [ ] 3.4.2 reminder 技能 ✅
- [ ] 3.4.3 note 技能 ✅
- [ ] 3.4.4 task 技能 ✅
- [ ] 3.4.5 brainstorm 技能 ✅
- [ ] 3.5 Router集成（@skill语法）✅

#### 质量验证
- [ ] 4.1 测试套件通过 ✅
- [ ] 4.2 代码质量合格 ✅
- [ ] 4.3 性能基准达标 ✅

---

### ✅ 5.2 已知问题记录

| 问题编号 | 严重性 | 描述 | 发现时间 | 状态 |
|---------|--------|------|---------|------|
| - | - | - | - | - |

**问题分类：**
- 🔴 Critical：阻塞性问题，必须修复
- 🟡 High：重要问题，应尽快修复
- 🟢 Medium：一般问题，可延后修复
- ⚪ Low：轻微问题，可忽略

---

### ✅ 5.3 验收结论

**验收日期：** __________
**验收人：** __________

**总体评价：**
- [ ] ✅ 通过验收（所有核心功能正常）
- [ ] ⚠️ 有条件通过（存在非关键问题）
- [ ] ❌ 未通过验收（存在阻塞性问题）

**签字：** __________

---

## 6️⃣ 快速验收脚本

为了方便验收，这里提供一键验收脚本：

### 快速验收脚本

```bash
#!/bin/bash
# acceptance_test.sh - Phase 1&2 快速验收脚本

set -e

echo "🚀 开始 Phase 1 & 2 验收测试..."
echo ""

# 颜色定义
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 检查服务器是否运行
echo "📡 检查服务器状态..."
if curl -s http://localhost:8000/health > /dev/null 2>&1; then
    echo -e "${GREEN}✅ 服务器运行中${NC}"
else
    echo -e "${RED}❌ 服务器未运行，请先启动: uvicorn src.main:app --reload${NC}"
    exit 1
fi

echo ""
echo "🧪 测试 Phase 1: 基础框架"
echo "================================"

# 测试1：基本聊天
echo -n "测试基本聊天... "
RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello", "session_id": "acceptance-test-1"}')

if echo $RESPONSE | jq -e '.response' > /dev/null 2>&1; then
    echo -e "${GREEN}✅ 通过${NC}"
else
    echo -e "${RED}❌ 失败${NC}"
fi

# 测试2：上下文管理
echo -n "测试上下文管理... "
curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "My name is Bob", "session_id": "context-test"}' > /dev/null

RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What is my name?", "session_id": "context-test"}')

if echo $RESPONSE | grep -iq "bob"; then
    echo -e "${GREEN}✅ 通过${NC}"
else
    echo -e "${YELLOW}⚠️  可能失败（未检测到名字）${NC}"
fi

echo ""
echo "🎯 测试 Phase 2: 技能系统"
echo "================================"

# 测试3：greeting技能
echo -n "测试 greeting 技能... "
RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@greeting", "session_id": "skill-test-1"}')

if echo $RESPONSE | jq -e '.response' > /dev/null 2>&1; then
    echo -e "${GREEN}✅ 通过${NC}"
else
    echo -e "${RED}❌ 失败${NC}"
fi

# 测试4：reminder技能（带参数）
echo -n "测试 reminder 技能... "
RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@reminder task=\"Test\" time=\"5pm\"", "session_id": "skill-test-2"}')

if echo $RESPONSE | jq -e '.response' > /dev/null 2>&1; then
    echo -e "${GREEN}✅ 通过${NC}"
else
    echo -e "${RED}❌ 失败${NC}"
fi

# 测试5：note技能
echo -n "测试 note 技能... "
RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@note content=\"Test note\" category=\"test\"", "session_id": "skill-test-3"}')

if echo $RESPONSE | jq -e '.response' > /dev/null 2>&1; then
    echo -e "${GREEN}✅ 通过${NC}"
else
    echo -e "${RED}❌ 失败${NC}"
fi

echo ""
echo "📊 运行测试套件..."
pytest tests/ -v --tb=short

echo ""
echo "✨ 验收测试完成！"
echo ""
echo "📝 下一步："
echo "  1. 检查上述测试结果"
echo "  2. 手动验证Web界面功能"
echo "  3. 填写验收清单"
echo "  4. 记录任何发现的问题"
```

### 使用脚本

```bash
# 赋予执行权限
chmod +x acceptance_test.sh

# 运行验收脚本
./acceptance_test.sh
```

---

## 7️⃣ 附录

### A. 常见问题

**Q1: 服务器启动失败？**
- 检查端口8000是否被占用：`lsof -i :8000`
- 检查依赖是否安装完整：`pip list`

**Q2: 数据库文件不存在？**
- 确保 `data/` 目录存在
- 服务器首次启动会自动创建数据库

**Q3: 技能未加载？**
- 检查 `skills/` 目录结构
- 查看服务器启动日志
- 验证技能文件格式正确

**Q4: API返回500错误？**
- 查看服务器终端的错误日志
- 检查LLM配置（API密钥等）

### B. 联系方式

如有问题，请联系：
- GitHub Issues: [项目地址]
- Email: [邮箱地址]

---

**验收完成日期：** __________
**文档版本：** v1.0
