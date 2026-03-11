"""Integration tests for skill system with core components"""
import pytest
from datetime import datetime
from src.core.router import SimpleRouter
from src.core.executor import AgentExecutor
from src.core.llm_client import MockLLMClient
from src.storage.database import Database
from src.storage.models import Session
from src.skills.loader import SkillLoader
from src.skills.registry import SkillRegistry
from src.skills.executor import SkillExecutor


@pytest.fixture
async def test_db(tmp_path):
    """Create test database"""
    db_path = tmp_path / "test.db"
    db = Database(db_path)
    await db.initialize()
    yield db
    await db.close()


@pytest.fixture
def test_skills_dir(tmp_path):
    """Create test skills directory with sample skills"""
    skills_dir = tmp_path / "skills"
    skills_dir.mkdir()

    # Create a simple greeting skill
    greeting = skills_dir / "greeting.md"
    greeting.write_text("""---
name: greeting
description: Greet the user
---
Please greet the user warmly and professionally.
""")

    # Create a parameterized reminder skill
    personal_dir = skills_dir / "personal"
    personal_dir.mkdir()
    reminder = personal_dir / "reminder.md"
    reminder.write_text("""---
name: reminder
description: Create a reminder
parameters:
  - name: task
    type: string
    required: true
    description: Task to remind about
  - name: time
    type: string
    required: true
    description: When to remind
---
Create a reminder for {task} at {time}.
""")

    return skills_dir


@pytest.fixture
def skill_registry(test_skills_dir):
    """Create and populate skill registry"""
    loader = SkillLoader(test_skills_dir)
    registry = SkillRegistry()

    # Load all skills
    skills = loader.load_all()
    for skill in skills:
        registry.register(skill)

    return registry


def test_router_detects_skill_invocation():
    """Test that router detects @skill syntax"""
    router = SimpleRouter()

    # Test @skill syntax
    plan = router.route("@greeting")
    assert plan.type == "skill"
    assert plan.metadata is not None
    assert plan.metadata["skill_name"] == "greeting"
    assert plan.metadata["parameters"] == {}


def test_router_detects_skill_with_slash():
    """Test that router detects /skill syntax"""
    router = SimpleRouter()

    plan = router.route("/greeting")
    assert plan.type == "skill"
    assert plan.metadata["skill_name"] == "greeting"


def test_router_parses_skill_parameters():
    """Test that router parses skill parameters"""
    router = SimpleRouter()

    # Parameters in query string style
    plan = router.route("@reminder task='Buy milk' time='5pm'")

    assert plan.type == "skill"
    assert plan.metadata["skill_name"] == "reminder"
    assert "task" in plan.metadata["parameters"]
    assert "time" in plan.metadata["parameters"]
    assert plan.metadata["parameters"]["task"] == "Buy milk"
    assert plan.metadata["parameters"]["time"] == "5pm"


def test_router_handles_quoted_parameters():
    """Test that router handles both single and double quotes"""
    router = SimpleRouter()

    # Double quotes
    plan = router.route('@reminder task="Buy groceries" time="3pm"')
    assert plan.metadata["parameters"]["task"] == "Buy groceries"

    # Single quotes
    plan = router.route("@reminder task='Call mom' time='evening'")
    assert plan.metadata["parameters"]["task"] == "Call mom"


def test_router_non_skill_input():
    """Test that router handles non-skill input normally"""
    router = SimpleRouter()

    plan = router.route("What is the weather today?")
    assert plan.type == "simple_query"
    assert plan.requires_llm is True


@pytest.mark.asyncio
async def test_executor_executes_skill(test_db, skill_registry):
    """Test that executor can execute a skill"""
    router = SimpleRouter()
    llm_client = MockLLMClient()
    skill_executor = SkillExecutor(llm_client)

    executor = AgentExecutor(
        db=test_db,
        router=router,
        llm_client=llm_client,
        skill_registry=skill_registry,
        skill_executor=skill_executor
    )

    # Create session
    session = Session(
        id="test-session",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await test_db.create_session(session)

    # Execute skill
    result = await executor.execute("@greeting", "test-session")

    # Should succeed
    assert result["session_id"] == "test-session"
    assert result["plan_type"] == "skill"
    assert "response" in result
    assert len(result["response"]) > 0


@pytest.mark.asyncio
async def test_executor_executes_parameterized_skill(test_db, skill_registry):
    """Test that executor can execute skill with parameters"""
    router = SimpleRouter()
    llm_client = MockLLMClient()
    skill_executor = SkillExecutor(llm_client)

    executor = AgentExecutor(
        db=test_db,
        router=router,
        llm_client=llm_client,
        skill_registry=skill_registry,
        skill_executor=skill_executor
    )

    session = Session(
        id="test-session",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await test_db.create_session(session)

    # Execute parameterized skill
    result = await executor.execute(
        "@reminder task='Meeting' time='2pm'",
        "test-session"
    )

    assert result["plan_type"] == "skill"
    assert "Meeting" in result["response"]
    assert "2pm" in result["response"]


@pytest.mark.asyncio
async def test_executor_handles_missing_skill(test_db, skill_registry):
    """Test error handling when skill not found"""
    router = SimpleRouter()
    llm_client = MockLLMClient()
    skill_executor = SkillExecutor(llm_client)

    executor = AgentExecutor(
        db=test_db,
        router=router,
        llm_client=llm_client,
        skill_registry=skill_registry,
        skill_executor=skill_executor
    )

    session = Session(
        id="test-session",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await test_db.create_session(session)

    # Try to execute non-existent skill
    result = await executor.execute("@nonexistent", "test-session")

    # Should return error response
    assert result["session_id"] == "test-session"
    assert "error" in result or "not found" in result["response"].lower()


@pytest.mark.asyncio
async def test_executor_handles_missing_parameters(test_db, skill_registry):
    """Test error handling when required parameters are missing"""
    router = SimpleRouter()
    llm_client = MockLLMClient()
    skill_executor = SkillExecutor(llm_client)

    executor = AgentExecutor(
        db=test_db,
        router=router,
        llm_client=llm_client,
        skill_registry=skill_registry,
        skill_executor=skill_executor
    )

    session = Session(
        id="test-session",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await test_db.create_session(session)

    # Execute skill without required parameters
    result = await executor.execute("@reminder", "test-session")

    # Should return error response
    assert "error" in result or "required" in result["response"].lower()


@pytest.mark.asyncio
async def test_conversation_context_preserved(test_db, skill_registry):
    """Test that conversation context is preserved with skill execution"""
    router = SimpleRouter()
    llm_client = MockLLMClient()
    skill_executor = SkillExecutor(llm_client)

    executor = AgentExecutor(
        db=test_db,
        router=router,
        llm_client=llm_client,
        skill_registry=skill_registry,
        skill_executor=skill_executor
    )

    session = Session(
        id="test-session",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await test_db.create_session(session)

    # Normal query
    await executor.execute("Hello", "test-session")

    # Skill execution
    await executor.execute("@greeting", "test-session")

    # Another normal query
    result = await executor.execute("Thanks", "test-session")

    # Context should be preserved
    assert result["session_id"] == "test-session"

    # Check message history
    messages = await test_db.get_messages("test-session")
    assert len(messages) >= 6  # 3 user + 3 assistant messages


@pytest.mark.asyncio
async def test_skill_system_end_to_end(test_db, test_skills_dir):
    """Full E2E test: load skills, route, execute"""
    # 1. Load skills
    loader = SkillLoader(test_skills_dir)
    skills = loader.load_all()
    assert len(skills) >= 2  # greeting + reminder

    # 2. Register skills
    registry = SkillRegistry()
    for skill in skills:
        registry.register(skill)

    # 3. Setup executor
    router = SimpleRouter()
    llm_client = MockLLMClient()
    skill_executor = SkillExecutor(llm_client)

    executor = AgentExecutor(
        db=test_db,
        router=router,
        llm_client=llm_client,
        skill_registry=registry,
        skill_executor=skill_executor
    )

    # 4. Create session
    session = Session(
        id="e2e-session",
        title="E2E Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await test_db.create_session(session)

    # 5. Execute skill
    result = await executor.execute("@greeting", "e2e-session")

    # 6. Verify result
    assert result["success"] is True or "response" in result
    assert result["session_id"] == "e2e-session"
