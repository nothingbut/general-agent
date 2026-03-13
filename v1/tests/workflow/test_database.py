"""测试 workflow 数据库操作"""
import asyncio
import pytest
import json
from datetime import datetime
from pathlib import Path
from src.storage.database import Database
from src.storage.models import Session


@pytest.mark.asyncio
async def test_workflow_tables_created(test_db_path: Path):
    """测试 workflow 相关表是否正确创建"""
    db = Database(test_db_path)
    await db.initialize()

    # 检查表是否存在
    async with db.conn.execute(
        "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('workflows', 'task_executions', 'workflow_approvals')"
    ) as cursor:
        tables = await cursor.fetchall()
        table_names = [row[0] for row in tables]

    assert "workflows" in table_names
    assert "task_executions" in table_names
    assert "workflow_approvals" in table_names

    await db.close()


@pytest.mark.asyncio
async def test_create_workflow(test_db_path: Path):
    """测试创建工作流"""
    db = Database(test_db_path)
    await db.initialize()

    # 先创建会话
    session = Session(
        id="sess-1",
        title="Test Session",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    # 创建工作流
    workflow_id = "workflow-1"
    goal = "测试工作流"
    plan = {
        "tasks": [
            {
                "id": "task-1",
                "name": "Task 1",
                "tool": "mcp:filesystem",
                "params": {"action": "read"}
            }
        ]
    }

    await db.create_workflow(
        workflow_id=workflow_id,
        session_id="sess-1",
        goal=goal,
        plan=plan,
        status="pending",
        created_at=datetime.now()
    )

    # 获取工作流并验证
    workflow = await db.get_workflow(workflow_id)
    assert workflow is not None
    assert workflow["id"] == workflow_id
    assert workflow["session_id"] == "sess-1"
    assert workflow["goal"] == goal
    assert workflow["status"] == "pending"
    assert workflow["plan"] == plan

    await db.close()


@pytest.mark.asyncio
async def test_get_nonexistent_workflow(test_db_path: Path):
    """测试获取不存在的工作流"""
    db = Database(test_db_path)
    await db.initialize()

    workflow = await db.get_workflow("nonexistent-workflow")
    assert workflow is None

    await db.close()


@pytest.mark.asyncio
async def test_update_workflow_status(test_db_path: Path):
    """测试更新工作流状态"""
    db = Database(test_db_path)
    await db.initialize()

    # 创建会话和工作流
    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    workflow_id = "workflow-1"
    await db.create_workflow(
        workflow_id=workflow_id,
        session_id="sess-1",
        goal="测试",
        plan={"tasks": []},
        status="pending",
        created_at=datetime.now()
    )

    # 更新状态为 running
    await db.update_workflow_status(
        workflow_id=workflow_id,
        status="running",
        current_task_id="task-1"
    )

    workflow = await db.get_workflow(workflow_id)
    assert workflow["status"] == "running"
    assert workflow["current_task_id"] == "task-1"

    # 更新状态为 completed
    completed_at = datetime.now()
    await db.update_workflow_status(
        workflow_id=workflow_id,
        status="completed",
        completed_at=completed_at
    )

    workflow = await db.get_workflow(workflow_id)
    assert workflow["status"] == "completed"
    assert workflow["completed_at"] is not None

    await db.close()


@pytest.mark.asyncio
async def test_create_task_execution(test_db_path: Path):
    """测试创建任务执行记录"""
    db = Database(test_db_path)
    await db.initialize()

    # 创建会话和工作流
    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    workflow_id = "workflow-1"
    await db.create_workflow(
        workflow_id=workflow_id,
        session_id="sess-1",
        goal="测试",
        plan={"tasks": []},
        status="pending",
        created_at=datetime.now()
    )

    # 创建任务执行记录
    execution_id = "exec-1"
    await db.create_task_execution(
        execution_id=execution_id,
        workflow_id=workflow_id,
        task_id="task-1",
        task_name="Test Task",
        tool_name="mcp:filesystem",
        params={"action": "read", "path": "/test"},
        status="pending",
        started_at=datetime.now()
    )

    # 获取任务执行记录
    execution = await db.get_task_execution(execution_id)
    assert execution is not None
    assert execution["id"] == execution_id
    assert execution["workflow_id"] == workflow_id
    assert execution["task_id"] == "task-1"
    assert execution["task_name"] == "Test Task"
    assert execution["tool_name"] == "mcp:filesystem"
    assert execution["status"] == "pending"
    assert execution["retry_count"] == 0

    await db.close()


@pytest.mark.asyncio
async def test_update_task_execution(test_db_path: Path):
    """测试更新任务执行状态"""
    db = Database(test_db_path)
    await db.initialize()

    # 创建会话、工作流和任务执行
    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    workflow_id = "workflow-1"
    await db.create_workflow(
        workflow_id=workflow_id,
        session_id="sess-1",
        goal="测试",
        plan={"tasks": []},
        status="pending",
        created_at=datetime.now()
    )

    execution_id = "exec-1"
    await db.create_task_execution(
        execution_id=execution_id,
        workflow_id=workflow_id,
        task_id="task-1",
        task_name="Test Task",
        tool_name="mcp:filesystem",
        params={"action": "read"},
        status="pending",
        started_at=datetime.now()
    )

    # 更新为 completed 状态
    result = {"content": "test result"}
    completed_at = datetime.now()
    await db.update_task_execution(
        execution_id=execution_id,
        status="completed",
        result=result,
        completed_at=completed_at
    )

    execution = await db.get_task_execution(execution_id)
    assert execution["status"] == "completed"
    assert execution["result"] == result
    assert execution["completed_at"] is not None

    # 更新为 failed 状态
    execution_id2 = "exec-2"
    await db.create_task_execution(
        execution_id=execution_id2,
        workflow_id=workflow_id,
        task_id="task-2",
        task_name="Test Task 2",
        tool_name="skill:test",
        params={},
        status="pending",
        started_at=datetime.now()
    )

    await db.update_task_execution(
        execution_id=execution_id2,
        status="failed",
        error="Test error message",
        retry_count=3,
        completed_at=datetime.now()
    )

    execution2 = await db.get_task_execution(execution_id2)
    assert execution2["status"] == "failed"
    assert execution2["error"] == "Test error message"
    assert execution2["retry_count"] == 3

    await db.close()


@pytest.mark.asyncio
async def test_get_workflow_executions(test_db_path: Path):
    """测试获取工作流的所有任务执行记录"""
    db = Database(test_db_path)
    await db.initialize()

    # 创建会话和工作流
    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    workflow_id = "workflow-1"
    await db.create_workflow(
        workflow_id=workflow_id,
        session_id="sess-1",
        goal="测试",
        plan={"tasks": []},
        status="pending",
        created_at=datetime.now()
    )

    # 创建多个任务执行记录
    for i in range(3):
        await db.create_task_execution(
            execution_id=f"exec-{i}",
            workflow_id=workflow_id,
            task_id=f"task-{i}",
            task_name=f"Task {i}",
            tool_name="mcp:test",
            params={},
            status="pending",
            started_at=datetime.now()
        )
        await asyncio.sleep(0.01)  # 确保时间戳不同

    # 获取所有执行记录
    executions = await db.get_workflow_executions(workflow_id)
    assert len(executions) == 3
    assert all(e["workflow_id"] == workflow_id for e in executions)

    await db.close()


@pytest.mark.asyncio
async def test_create_workflow_approval(test_db_path: Path):
    """测试创建工作流审批记录"""
    db = Database(test_db_path)
    await db.initialize()

    # 创建会话和工作流
    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    workflow_id = "workflow-1"
    await db.create_workflow(
        workflow_id=workflow_id,
        session_id="sess-1",
        goal="测试",
        plan={"tasks": []},
        status="pending",
        created_at=datetime.now()
    )

    # 创建审批记录
    approval_id = "approval-1"
    await db.create_approval(
        approval_id=approval_id,
        workflow_id=workflow_id,
        task_id="task-1",
        status="pending",
        created_at=datetime.now()
    )

    # 获取审批记录
    approval = await db.get_approval(approval_id)
    assert approval is not None
    assert approval["id"] == approval_id
    assert approval["workflow_id"] == workflow_id
    assert approval["task_id"] == "task-1"
    assert approval["status"] == "pending"

    await db.close()


@pytest.mark.asyncio
async def test_update_approval_status(test_db_path: Path):
    """测试更新审批状态"""
    db = Database(test_db_path)
    await db.initialize()

    # 创建会话、工作流和审批
    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    workflow_id = "workflow-1"
    await db.create_workflow(
        workflow_id=workflow_id,
        session_id="sess-1",
        goal="测试",
        plan={"tasks": []},
        status="pending",
        created_at=datetime.now()
    )

    approval_id = "approval-1"
    await db.create_approval(
        approval_id=approval_id,
        workflow_id=workflow_id,
        task_id="task-1",
        status="pending",
        created_at=datetime.now()
    )

    # 更新为 approved 状态
    responded_at = datetime.now()
    await db.update_approval(
        approval_id=approval_id,
        status="approved",
        user_comment="Looks good!",
        responded_at=responded_at
    )

    approval = await db.get_approval(approval_id)
    assert approval["status"] == "approved"
    assert approval["user_comment"] == "Looks good!"
    assert approval["responded_at"] is not None

    await db.close()


@pytest.mark.asyncio
async def test_get_pending_approvals(test_db_path: Path):
    """测试获取待审批的记录"""
    db = Database(test_db_path)
    await db.initialize()

    # 创建会话和工作流
    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    workflow_id = "workflow-1"
    await db.create_workflow(
        workflow_id=workflow_id,
        session_id="sess-1",
        goal="测试",
        plan={"tasks": []},
        status="pending",
        created_at=datetime.now()
    )

    # 创建多个审批记录
    await db.create_approval(
        approval_id="approval-1",
        workflow_id=workflow_id,
        task_id="task-1",
        status="pending",
        created_at=datetime.now()
    )
    await db.create_approval(
        approval_id="approval-2",
        workflow_id=workflow_id,
        task_id="task-2",
        status="pending",
        created_at=datetime.now()
    )
    await db.create_approval(
        approval_id="approval-3",
        workflow_id=workflow_id,
        task_id="task-3",
        status="approved",
        created_at=datetime.now()
    )

    # 获取待审批记录
    pending = await db.get_pending_approvals(workflow_id)
    assert len(pending) == 2
    assert all(a["status"] == "pending" for a in pending)

    await db.close()


@pytest.mark.asyncio
async def test_workflow_foreign_key_constraint(test_db_path: Path):
    """测试工作流外键约束"""
    db = Database(test_db_path)
    await db.initialize()

    # 尝试创建没有对应会话的工作流
    with pytest.raises(Exception):
        await db.create_workflow(
            workflow_id="workflow-1",
            session_id="nonexistent-session",
            goal="测试",
            plan={"tasks": []},
            status="pending",
            created_at=datetime.now()
        )

    await db.close()


@pytest.mark.asyncio
async def test_task_execution_foreign_key_constraint(test_db_path: Path):
    """测试任务执行外键约束"""
    db = Database(test_db_path)
    await db.initialize()

    # 尝试创建没有对应工作流的任务执行
    with pytest.raises(Exception):
        await db.create_task_execution(
            execution_id="exec-1",
            workflow_id="nonexistent-workflow",
            task_id="task-1",
            task_name="Test",
            tool_name="mcp:test",
            params={},
            status="pending",
            started_at=datetime.now()
        )

    await db.close()


@pytest.mark.asyncio
async def test_workflow_json_plan_storage(test_db_path: Path):
    """测试工作流 JSON 计划的存储和检索"""
    db = Database(test_db_path)
    await db.initialize()

    # 创建会话
    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    # 创建复杂的计划
    complex_plan = {
        "tasks": [
            {
                "id": "task-1",
                "name": "Read file",
                "tool": "mcp:filesystem",
                "params": {"path": "/test.txt"},
                "dependencies": []
            },
            {
                "id": "task-2",
                "name": "Process data",
                "tool": "skill:process",
                "params": {"input": "data"},
                "dependencies": ["task-1"]
            }
        ]
    }

    await db.create_workflow(
        workflow_id="workflow-1",
        session_id="sess-1",
        goal="测试复杂计划",
        plan=complex_plan,
        status="pending",
        created_at=datetime.now()
    )

    # 验证 JSON 正确存储和检索
    workflow = await db.get_workflow("workflow-1")
    assert workflow["plan"] == complex_plan
    assert len(workflow["plan"]["tasks"]) == 2
    assert workflow["plan"]["tasks"][0]["id"] == "task-1"
    assert workflow["plan"]["tasks"][1]["dependencies"] == ["task-1"]

    await db.close()


@pytest.mark.asyncio
async def test_workflow_operations_without_initialize(test_db_path: Path):
    """测试未初始化数据库时的 workflow 操作"""
    db = Database(test_db_path)
    # 不调用 initialize()

    with pytest.raises(RuntimeError) as exc_info:
        await db.create_workflow(
            workflow_id="workflow-1",
            session_id="sess-1",
            goal="测试",
            plan={},
            status="pending",
            created_at=datetime.now()
        )
    assert "Database not initialized" in str(exc_info.value)

    with pytest.raises(RuntimeError) as exc_info:
        await db.get_workflow("workflow-1")
    assert "Database not initialized" in str(exc_info.value)


@pytest.mark.asyncio
async def test_get_session_workflows(test_db_path: Path):
    """测试获取会话的所有工作流"""
    db = Database(test_db_path)
    await db.initialize()

    # 创建会话
    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    # 创建多个工作流
    for i in range(3):
        await db.create_workflow(
            workflow_id=f"workflow-{i}",
            session_id="sess-1",
            goal=f"目标 {i}",
            plan={"tasks": []},
            status="pending",
            created_at=datetime.now()
        )
        await asyncio.sleep(0.01)  # 确保时间戳不同

    # 获取会话的所有工作流
    workflows = await db.get_session_workflows("sess-1")
    assert len(workflows) == 3
    assert all(w["session_id"] == "sess-1" for w in workflows)

    await db.close()
