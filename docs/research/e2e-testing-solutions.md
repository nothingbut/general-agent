# E2E自动化测试方案调研

**日期：** 2026-03-04
**项目：** General Agent
**目标：** 建立完整的端到端自动化测试体系

---

## 1. 调研概述

### 1.1 背景

General Agent项目包含多个层次的功能：
- **API层**：RESTful接口（FastAPI）
- **Web UI层**：前端界面（Jinja2 + JavaScript）
- **TUI层**（未来）：终端界面（Textual）
- **业务逻辑层**：Router、Skill System、Context管理

需要建立自动化E2E测试体系，覆盖完整用户场景。

### 1.2 测试需求

**核心场景：**
- ✅ 用户发送消息，获得AI回复
- ✅ 调用技能（@skill语法）
- ✅ 会话管理（创建、切换、删除）
- ✅ 会话历史保存和恢复
- ✅ 技能参数验证
- ✅ 错误处理和恢复

**非功能需求：**
- 自动化运行（CI/CD集成）
- 快速执行（< 5分钟）
- 易于维护
- 良好的报告和失败诊断

---

## 2. 主流E2E测试方案对比

### 2.1 方案对比矩阵

| 方案 | 适用场景 | 优点 | 缺点 | 推荐度 |
|------|---------|------|------|--------|
| **Playwright** | Web UI测试 | 现代化、快速、多浏览器 | 仅限浏览器 | ⭐⭐⭐⭐⭐ |
| **pytest-httpx** | API测试 | 轻量、与pytest集成好 | 仅API | ⭐⭐⭐⭐⭐ |
| **TestClient (FastAPI)** | API测试 | 无需启动服务器 | 不测试真实HTTP | ⭐⭐⭐⭐ |
| **Selenium** | Web UI测试 | 成熟、生态完整 | 较慢、维护成本高 | ⭐⭐⭐ |
| **Textual Testing** | TUI测试 | 专为Textual设计 | 新技术、文档少 | ⭐⭐⭐⭐ |

---

## 3. 推荐测试架构

### 3.1 三层测试策略

```
┌─────────────────────────────────────────────────────┐
│              E2E Test Pyramid                       │
│                                                     │
│  ┌───────────────────────────────────────────┐     │
│  │      UI E2E Tests (10%)                   │     │
│  │  - Playwright (Web UI)                    │     │
│  │  - Textual Testing (TUI)                  │     │
│  └───────────────────────────────────────────┘     │
│                     ▲                               │
│                     │                               │
│  ┌───────────────────────────────────────────┐     │
│  │      API E2E Tests (30%)                  │     │
│  │  - pytest + httpx                         │     │
│  │  - Full API workflows                     │     │
│  └───────────────────────────────────────────┘     │
│                     ▲                               │
│                     │                               │
│  ┌───────────────────────────────────────────┐     │
│  │      Integration Tests (60%)              │     │
│  │  - pytest + TestClient                    │     │
│  │  - Unit + Component tests                 │     │
│  └───────────────────────────────────────────┘     │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### 3.2 测试分层说明

#### Layer 1: 集成测试（已有）
- **工具**：pytest + FastAPI TestClient
- **覆盖**：单个API端点、Skill加载、Router逻辑
- **执行时间**：< 2分钟
- **频率**：每次代码提交

#### Layer 2: API E2E测试（新增）
- **工具**：pytest + httpx
- **覆盖**：完整用户场景的API调用链
- **执行时间**：2-3分钟
- **频率**：每次PR、每日构建

#### Layer 3: UI E2E测试（新增）
- **工具**：Playwright (Web) + Textual Testing (TUI)
- **覆盖**：关键用户流程的UI交互
- **执行时间**：3-5分钟
- **频率**：每次发布前、每周

---

## 4. API E2E测试方案

### 4.1 使用 pytest + httpx

**优点：**
- ✅ 测试真实HTTP请求
- ✅ 异步支持（与FastAPI一致）
- ✅ 与pytest完美集成
- ✅ 易于mock外部依赖

**目录结构：**
```
tests/
├── unit/              # 单元测试（已有）
├── integration/       # 集成测试（已有）
└── e2e/              # E2E测试（新增）
    ├── conftest.py   # 共享fixtures
    ├── test_chat_flow.py
    ├── test_skill_execution.py
    ├── test_session_management.py
    └── fixtures/
        └── test_data.json
```

### 4.2 实现示例

#### conftest.py - 共享Fixtures

```python
# tests/e2e/conftest.py
import pytest
import httpx
from typing import AsyncGenerator
import subprocess
import time
import signal
import os

@pytest.fixture(scope="session")
def test_server():
    """启动测试服务器"""
    # 启动FastAPI服务器
    env = os.environ.copy()
    env["DATABASE_URL"] = "sqlite:///./test_e2e.db"

    process = subprocess.Popen(
        ["uvicorn", "src.main:app", "--port", "8001"],
        env=env,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )

    # 等待服务器启动
    time.sleep(2)

    # 健康检查
    for _ in range(10):
        try:
            response = httpx.get("http://localhost:8001/health")
            if response.status_code == 200:
                break
        except:
            time.sleep(0.5)

    yield "http://localhost:8001"

    # 清理：停止服务器
    process.send_signal(signal.SIGTERM)
    process.wait(timeout=5)

    # 删除测试数据库
    if os.path.exists("test_e2e.db"):
        os.remove("test_e2e.db")


@pytest.fixture
async def api_client(test_server) -> AsyncGenerator[httpx.AsyncClient, None]:
    """创建API客户端"""
    async with httpx.AsyncClient(base_url=test_server, timeout=30.0) as client:
        yield client


@pytest.fixture
async def test_session(api_client: httpx.AsyncClient) -> str:
    """创建测试会话"""
    response = await api_client.post("/api/sessions/new")
    data = response.json()
    return data["session_id"]
```

#### test_chat_flow.py - 聊天流程测试

```python
# tests/e2e/test_chat_flow.py
import pytest
import httpx

class TestChatFlow:
    """测试完整的聊天流程"""

    @pytest.mark.asyncio
    async def test_basic_chat(self, api_client: httpx.AsyncClient, test_session: str):
        """测试基本聊天功能"""
        # 1. 发送消息
        response = await api_client.post(
            "/api/chat",
            json={
                "message": "Hello, how are you?",
                "session_id": test_session
            }
        )

        assert response.status_code == 200
        data = response.json()
        assert "response" in data
        assert len(data["response"]) > 0
        assert data["session_id"] == test_session

    @pytest.mark.asyncio
    async def test_multi_turn_conversation(self, api_client: httpx.AsyncClient, test_session: str):
        """测试多轮对话"""
        messages = [
            "My name is Alice",
            "What is my name?",  # 应该记住上一轮的名字
        ]

        responses = []
        for msg in messages:
            response = await api_client.post(
                "/api/chat",
                json={"message": msg, "session_id": test_session}
            )
            assert response.status_code == 200
            responses.append(response.json()["response"])

        # 验证AI记住了名字
        assert "alice" in responses[-1].lower()

    @pytest.mark.asyncio
    async def test_session_isolation(self, api_client: httpx.AsyncClient):
        """测试会话隔离"""
        # 创建两个独立会话
        session1 = (await api_client.post("/api/sessions/new")).json()["session_id"]
        session2 = (await api_client.post("/api/sessions/new")).json()["session_id"]

        # 在session1中设置信息
        await api_client.post(
            "/api/chat",
            json={"message": "Remember: code is 12345", "session_id": session1}
        )

        # 在session2中询问
        response = await api_client.post(
            "/api/chat",
            json={"message": "What is the code?", "session_id": session2}
        )

        # session2不应该知道session1的信息
        assert "12345" not in response.json()["response"]
```

#### test_skill_execution.py - 技能执行测试

```python
# tests/e2e/test_skill_execution.py
import pytest
import httpx

class TestSkillExecution:
    """测试技能调用完整流程"""

    @pytest.mark.asyncio
    async def test_simple_skill_call(self, api_client: httpx.AsyncClient, test_session: str):
        """测试简单技能调用"""
        response = await api_client.post(
            "/api/chat",
            json={
                "message": "@greeting",
                "session_id": test_session
            }
        )

        assert response.status_code == 200
        data = response.json()
        assert "response" in data
        # 验证使用了greeting技能
        assert any(word in data["response"].lower() for word in ["hello", "hi", "greetings"])

    @pytest.mark.asyncio
    async def test_skill_with_parameters(self, api_client: httpx.AsyncClient, test_session: str):
        """测试带参数的技能"""
        response = await api_client.post(
            "/api/chat",
            json={
                "message": '@reminder task="Buy milk" time="5pm"',
                "session_id": test_session
            }
        )

        assert response.status_code == 200
        data = response.json()
        assert "buy milk" in data["response"].lower()
        assert "5pm" in data["response"].lower()

    @pytest.mark.asyncio
    async def test_invalid_skill(self, api_client: httpx.AsyncClient, test_session: str):
        """测试调用不存在的技能"""
        response = await api_client.post(
            "/api/chat",
            json={
                "message": "@nonexistent_skill",
                "session_id": test_session
            }
        )

        assert response.status_code == 200
        data = response.json()
        # 应该返回友好的错误消息
        assert "not found" in data["response"].lower() or "unknown" in data["response"].lower()

    @pytest.mark.asyncio
    async def test_skill_parameter_validation(self, api_client: httpx.AsyncClient, test_session: str):
        """测试技能参数验证"""
        # 缺少必需参数
        response = await api_client.post(
            "/api/chat",
            json={
                "message": '@reminder time="5pm"',  # 缺少task参数
                "session_id": test_session
            }
        )

        assert response.status_code == 200
        data = response.json()
        # 应该提示缺少参数
        assert "required" in data["response"].lower() or "missing" in data["response"].lower()
```

#### test_session_management.py - 会话管理测试

```python
# tests/e2e/test_session_management.py
import pytest
import httpx

class TestSessionManagement:
    """测试会话管理功能"""

    @pytest.mark.asyncio
    async def test_create_session(self, api_client: httpx.AsyncClient):
        """测试创建会话"""
        response = await api_client.post("/api/sessions/new")

        assert response.status_code == 200
        data = response.json()
        assert "session_id" in data
        assert len(data["session_id"]) > 0

    @pytest.mark.asyncio
    async def test_list_sessions(self, api_client: httpx.AsyncClient):
        """测试获取会话列表"""
        # 创建几个会话
        session_ids = []
        for _ in range(3):
            response = await api_client.post("/api/sessions/new")
            session_ids.append(response.json()["session_id"])

        # 获取列表
        response = await api_client.get("/api/sessions")
        assert response.status_code == 200

        sessions = response.json()
        assert len(sessions) >= 3
        for sid in session_ids:
            assert any(s["session_id"] == sid for s in sessions)

    @pytest.mark.asyncio
    async def test_get_session_history(self, api_client: httpx.AsyncClient, test_session: str):
        """测试获取会话历史"""
        # 发送几条消息
        messages = ["Hello", "How are you?", "Goodbye"]
        for msg in messages:
            await api_client.post(
                "/api/chat",
                json={"message": msg, "session_id": test_session}
            )

        # 获取历史
        response = await api_client.get(f"/api/sessions/{test_session}/history")
        assert response.status_code == 200

        history = response.json()
        assert len(history) >= len(messages) * 2  # 用户消息 + AI回复

    @pytest.mark.asyncio
    async def test_delete_session(self, api_client: httpx.AsyncClient):
        """测试删除会话"""
        # 创建会话
        response = await api_client.post("/api/sessions/new")
        session_id = response.json()["session_id"]

        # 删除会话
        response = await api_client.delete(f"/api/sessions/{session_id}")
        assert response.status_code == 200

        # 验证已删除
        response = await api_client.get(f"/api/sessions/{session_id}/history")
        assert response.status_code == 404
```

### 4.3 运行API E2E测试

```bash
# 单独运行E2E测试
pytest tests/e2e/ -v

# 带覆盖率
pytest tests/e2e/ --cov=src --cov-report=html

# 并行运行（需要pytest-xdist）
pytest tests/e2e/ -n auto

# 只运行特定测试
pytest tests/e2e/test_chat_flow.py::TestChatFlow::test_basic_chat -v
```

---

## 5. Web UI E2E测试方案

### 5.1 使用 Playwright

**优点：**
- ✅ 现代化、快速
- ✅ 自动等待元素
- ✅ 多浏览器支持（Chromium、Firefox、WebKit）
- ✅ 截图和视频录制
- ✅ Trace viewer调试工具

### 5.2 安装配置

```bash
# 安装Playwright
pip install pytest-playwright

# 安装浏览器
playwright install
```

### 5.3 实现示例

#### conftest.py - Playwright配置

```python
# tests/e2e_ui/conftest.py
import pytest
from playwright.sync_api import Page, Browser

@pytest.fixture(scope="session")
def browser_context_args(browser_context_args):
    """配置浏览器上下文"""
    return {
        **browser_context_args,
        "viewport": {"width": 1280, "height": 720},
        "locale": "zh-CN",
    }

@pytest.fixture
def app_url():
    """应用URL"""
    return "http://localhost:8000"
```

#### test_web_ui.py - Web UI测试

```python
# tests/e2e_ui/test_web_ui.py
import pytest
from playwright.sync_api import Page, expect
import time

class TestWebUI:
    """测试Web界面交互"""

    def test_homepage_loads(self, page: Page, app_url: str):
        """测试首页加载"""
        page.goto(app_url)

        # 验证标题
        expect(page).to_have_title("General Agent")

        # 验证关键元素存在
        expect(page.locator("#message-input")).to_be_visible()
        expect(page.locator("#send-button")).to_be_visible()

    def test_send_message(self, page: Page, app_url: str):
        """测试发送消息"""
        page.goto(app_url)

        # 输入消息
        message_input = page.locator("#message-input")
        message_input.fill("Hello, AI!")

        # 点击发送
        page.locator("#send-button").click()

        # 等待响应出现
        response = page.locator(".ai-message").last
        expect(response).to_be_visible(timeout=10000)

        # 验证消息内容
        expect(response).to_contain_text("Hello")

    def test_skill_autocomplete(self, page: Page, app_url: str):
        """测试技能自动补全"""
        page.goto(app_url)

        # 输入@触发自动补全
        message_input = page.locator("#message-input")
        message_input.fill("@")

        # 等待自动补全菜单出现
        autocomplete_menu = page.locator(".autocomplete-menu")
        expect(autocomplete_menu).to_be_visible(timeout=2000)

        # 验证技能列表
        expect(autocomplete_menu).to_contain_text("greeting")
        expect(autocomplete_menu).to_contain_text("reminder")

    def test_session_management(self, page: Page, app_url: str):
        """测试会话管理"""
        page.goto(app_url)

        # 创建新会话
        page.locator("#new-session-button").click()

        # 验证会话列表更新
        sessions = page.locator(".session-item")
        expect(sessions).to_have_count_greater_than(0)

        # 切换会话
        first_session = sessions.first
        session_id = first_session.get_attribute("data-session-id")
        first_session.click()

        # 验证当前会话
        active_session = page.locator(".session-item.active")
        expect(active_session).to_have_attribute("data-session-id", session_id)

    def test_markdown_rendering(self, page: Page, app_url: str):
        """测试Markdown渲染"""
        page.goto(app_url)

        # 发送包含Markdown的消息
        message_input = page.locator("#message-input")
        message_input.fill("Show me markdown: **bold** and `code`")
        page.locator("#send-button").click()

        # 等待响应
        time.sleep(2)

        # 验证Markdown渲染
        response = page.locator(".ai-message").last
        expect(response.locator("strong")).to_be_visible()  # bold
        expect(response.locator("code")).to_be_visible()    # code

    @pytest.mark.slow
    def test_streaming_response(self, page: Page, app_url: str):
        """测试流式响应"""
        page.goto(app_url)

        # 发送消息
        message_input = page.locator("#message-input")
        message_input.fill("Tell me a long story")
        page.locator("#send-button").click()

        # 监控响应内容变化
        response = page.locator(".ai-message").last

        # 等待开始显示
        expect(response).to_be_visible(timeout=2000)

        # 记录内容长度变化
        lengths = []
        for _ in range(5):
            time.sleep(0.5)
            lengths.append(len(response.text_content()))

        # 验证内容逐渐增加（流式显示）
        assert lengths[-1] > lengths[0], "Response should grow over time"
```

#### pytest.ini - Playwright配置

```ini
# pytest.ini
[pytest]
markers =
    slow: marks tests as slow (deselect with '-m "not slow"')
    e2e: marks tests as e2e tests
    ui: marks tests as UI tests

# Playwright配置
playwright_browsers = chromium
playwright_headed = false
playwright_slow_mo = 0
```

### 5.4 运行Web UI测试

```bash
# 运行所有UI测试
pytest tests/e2e_ui/ -v

# 可视化模式（查看浏览器操作）
pytest tests/e2e_ui/ --headed --slowmo 500

# 生成Trace（用于调试）
pytest tests/e2e_ui/ --tracing on

# 查看Trace
playwright show-trace trace.zip
```

---

## 6. TUI E2E测试方案

### 6.1 使用 Textual Testing API

Textual提供了内置的测试工具，可以模拟用户交互。

### 6.2 实现示例

```python
# tests/e2e_tui/test_tui.py
import pytest
from textual.pilot import Pilot
from src.tui.app import GeneralAgentTUI

class TestTUI:
    """测试TUI界面"""

    @pytest.mark.asyncio
    async def test_app_starts(self):
        """测试应用启动"""
        app = GeneralAgentTUI()
        async with app.run_test() as pilot:
            # 验证应用启动成功
            assert app.is_running

            # 验证关键组件存在
            assert pilot.app.query_one("#messages")
            assert pilot.app.query_one("#input-container")

    @pytest.mark.asyncio
    async def test_send_message(self):
        """测试发送消息"""
        app = GeneralAgentTUI()
        async with app.run_test() as pilot:
            # 找到输入框
            input_box = pilot.app.query_one("Input")

            # 模拟输入
            input_box.value = "Hello, TUI!"

            # 模拟回车
            await pilot.press("enter")

            # 等待响应
            await pilot.pause(2)

            # 验证消息显示
            messages = pilot.app.query("#messages ChatMessage")
            assert len(messages) > 0

    @pytest.mark.asyncio
    async def test_keyboard_shortcuts(self):
        """测试键盘快捷键"""
        app = GeneralAgentTUI()
        async with app.run_test() as pilot:
            # 测试创建新会话 (n键)
            await pilot.press("n")
            await pilot.pause(0.5)

            # 验证会话列表更新
            sessions = pilot.app.query("#sessions ListItem")
            assert len(sessions) > 0

            # 测试清空消息 (ctrl+l)
            await pilot.press("ctrl+l")
            await pilot.pause(0.5)

            # 验证消息被清空
            messages = pilot.app.query("#messages ChatMessage")
            assert len(messages) == 0
```

---

## 7. CI/CD集成

### 7.1 GitHub Actions配置

```yaml
# .github/workflows/e2e-tests.yml
name: E2E Tests

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  api-e2e:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v3

    - name: Set up Python
      uses: actions/setup-python@v4
      with:
        python-version: '3.12'

    - name: Install dependencies
      run: |
        pip install -e ".[dev]"

    - name: Run API E2E tests
      run: |
        pytest tests/e2e/ -v --cov=src --cov-report=xml

    - name: Upload coverage
      uses: codecov/codecov-action@v3
      with:
        file: ./coverage.xml

  ui-e2e:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v3

    - name: Set up Python
      uses: actions/setup-python@v4
      with:
        python-version: '3.12'

    - name: Install dependencies
      run: |
        pip install -e ".[dev]"
        playwright install --with-deps chromium

    - name: Start test server
      run: |
        uvicorn src.main:app --port 8000 &
        sleep 5

    - name: Run UI E2E tests
      run: |
        pytest tests/e2e_ui/ -v --tracing retain-on-failure

    - name: Upload Playwright traces
      if: failure()
      uses: actions/upload-artifact@v3
      with:
        name: playwright-traces
        path: test-results/
```

### 7.2 Pre-commit Hook

```bash
# .git/hooks/pre-commit
#!/bin/bash

echo "Running E2E tests before commit..."

# 运行快速E2E测试
pytest tests/e2e/ -v -m "not slow"

if [ $? -ne 0 ]; then
    echo "E2E tests failed. Commit aborted."
    exit 1
fi

echo "E2E tests passed!"
```

---

## 8. 测试数据管理

### 8.1 Fixtures数据

```python
# tests/e2e/fixtures/test_data.py
TEST_MESSAGES = [
    "Hello, how are you?",
    "What's the weather today?",
    "@greeting",
    '@reminder task="Meeting" time="3pm"',
]

TEST_SKILLS = [
    {"name": "greeting", "params": {}},
    {"name": "reminder", "params": {"task": "Test", "time": "5pm"}},
    {"name": "note", "params": {"content": "Test note", "category": "work"}},
]

TEST_SESSIONS = [
    {"title": "Session 1", "messages": 5},
    {"title": "Session 2", "messages": 10},
]
```

### 8.2 Mock LLM响应

```python
# tests/e2e/mocks.py
from unittest.mock import AsyncMock

def mock_llm_response(message: str) -> str:
    """Mock LLM响应"""
    if "@greeting" in message:
        return "Hello! How can I help you today?"
    elif "@reminder" in message:
        return "I've set a reminder for you."
    else:
        return f"You said: {message}"

@pytest.fixture
def mock_llm_client(monkeypatch):
    """Mock LLM客户端"""
    mock = AsyncMock()
    mock.chat.return_value = mock_llm_response
    monkeypatch.setattr("src.core.llm_client.LLMClient", lambda: mock)
    return mock
```

---

## 9. 最佳实践

### 9.1 测试原则

1. **独立性**：每个测试独立运行，不依赖其他测试
2. **可重复性**：测试结果稳定，多次运行结果一致
3. **清晰性**：测试意图明确，失败时易于诊断
4. **快速性**：E2E测试控制在5分钟内
5. **覆盖关键路径**：优先测试核心用户场景

### 9.2 测试命名规范

```python
# 好的命名
def test_user_can_send_message_and_receive_response():
    pass

def test_skill_with_missing_required_parameter_shows_error():
    pass

# 不好的命名
def test_1():
    pass

def test_api():
    pass
```

### 9.3 测试组织

```python
# 使用类组织相关测试
class TestChatFlow:
    """聊天流程相关测试"""

    def test_basic_chat(self):
        pass

    def test_multi_turn_chat(self):
        pass

class TestSkillExecution:
    """技能执行相关测试"""

    def test_simple_skill(self):
        pass

    def test_skill_with_params(self):
        pass
```

---

## 10. 工具和依赖

### 10.1 pyproject.toml配置

```toml
[project.optional-dependencies]
e2e = [
    "playwright>=1.40.0",
    "pytest-playwright>=0.4.0",
    "pytest-asyncio>=0.21.0",
    "pytest-xdist>=3.5.0",  # 并行测试
    "httpx>=0.25.0",
]

[tool.pytest.ini_options]
testpaths = ["tests"]
python_files = ["test_*.py"]
python_classes = ["Test*"]
python_functions = ["test_*"]
asyncio_mode = "auto"
markers = [
    "e2e: End-to-end tests",
    "ui: UI tests",
    "slow: Slow tests",
]
```

### 10.2 安装命令

```bash
# 安装E2E测试依赖
pip install -e ".[e2e]"

# 或使用uv
uv pip install -e ".[e2e]"

# 安装浏览器
playwright install
```

---

## 11. 性能基准

### 11.1 测试执行时间目标

| 测试类型 | 测试数量 | 目标时间 | 频率 |
|---------|---------|---------|------|
| 单元测试 | ~100 | < 30秒 | 每次提交 |
| 集成测试 | ~50 | < 2分钟 | 每次提交 |
| API E2E | ~20 | < 3分钟 | 每次PR |
| UI E2E | ~10 | < 5分钟 | 每次发布 |
| **总计** | ~180 | **< 10分钟** | 完整测试套件 |

### 11.2 优化建议

1. **并行运行**：使用pytest-xdist并行执行
2. **测试分级**：快速测试每次提交，慢速测试定期运行
3. **数据库复用**：使用in-memory SQLite或fixtures
4. **Mock外部服务**：避免真实API调用
5. **增量测试**：只测试变更相关的模块

---

## 12. 故障排查

### 12.1 常见问题

**问题1：测试超时**
- **原因**：服务器启动慢或网络问题
- **解决**：增加timeout，检查服务器状态

**问题2：测试不稳定（flaky）**
- **原因**：异步时序问题、共享状态
- **解决**：使用proper等待、隔离测试数据

**问题3：UI元素找不到**
- **原因**：页面加载慢、选择器错误
- **解决**：使用Playwright的auto-wait、检查选择器

### 12.2 调试技巧

```python
# 1. 使用 --pdb 在失败时进入调试器
pytest tests/e2e/test_chat.py --pdb

# 2. 使用 -s 查看print输出
pytest tests/e2e/test_chat.py -s

# 3. 使用 --lf 只运行上次失败的测试
pytest --lf

# 4. UI测试：使用headed模式查看浏览器
pytest tests/e2e_ui/ --headed --slowmo 1000

# 5. UI测试：生成trace用于回放
pytest tests/e2e_ui/ --tracing on
playwright show-trace trace.zip
```

---

## 13. 总结

### 13.1 推荐方案

**General Agent项目E2E测试架构：**

1. **API E2E测试**（优先级最高）
   - 工具：pytest + httpx
   - 覆盖：完整用户场景的API调用
   - 执行时间：2-3分钟

2. **Web UI E2E测试**（中等优先级）
   - 工具：Playwright
   - 覆盖：关键UI交互流程
   - 执行时间：3-5分钟

3. **TUI E2E测试**（未来）
   - 工具：Textual Testing API
   - 覆盖：TUI核心功能
   - 执行时间：1-2分钟

### 13.2 实施路线图

**Week 1：基础设施**
- ✅ 设置E2E测试目录结构
- ✅ 配置pytest fixtures
- ✅ 实现测试服务器启动/清理

**Week 2：API E2E测试**
- ✅ 实现聊天流程测试
- ✅ 实现技能执行测试
- ✅ 实现会话管理测试

**Week 3：UI E2E测试**
- ✅ 配置Playwright
- ✅ 实现Web UI基础测试
- ✅ 实现UI交互测试

**Week 4：CI/CD集成**
- ✅ 配置GitHub Actions
- ✅ 设置测试报告
- ✅ 优化测试性能

### 13.3 预期收益

- ✅ **质量保证**：自动验证核心功能正常工作
- ✅ **快速反馈**：每次提交立即发现回归问题
- ✅ **文档价值**：测试即文档，展示系统用法
- ✅ **重构信心**：安全重构代码，测试保护
- ✅ **减少手工测试**：节省90%手工测试时间

---

## 参考资源

- **Playwright Python文档**：https://playwright.dev/python/
- **pytest文档**：https://docs.pytest.org/
- **pytest-httpx**：https://github.com/colin-b/pytest_httpx
- **Textual Testing**：https://textual.textualize.io/guide/testing/
- **FastAPI Testing**：https://fastapi.tiangolo.com/tutorial/testing/
