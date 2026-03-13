"""审批界面 Smoke 测试 - 快速验证基本功能

运行方式:
    python tests/smoke_test_approval_ui.py
"""
import asyncio
import sys
from pathlib import Path

# 添加项目根目录到 Python 路径
sys.path.insert(0, str(Path(__file__).parent.parent))

from src.workflow import (
    ApprovalUI,
    ApprovalRequest,
    ApprovalManager,
    Workflow,
    Task,
    WorkflowStatus,
    TaskStatus
)
from src.storage.database import Database


async def smoke_test_1_basic_ui():
    """测试 1: 基本 UI 显示"""
    print("\n" + "=" * 60)
    print("🧪 Smoke Test 1: 基本 UI 显示")
    print("=" * 60)
    print("\n预期: 显示审批界面，选择 Y 批准")

    request = ApprovalRequest(
        approval_id="smoke-001",
        workflow_id="workflow-test",
        task_id="task-1",
        task_name="读取文件（测试）",
        tool_name="mcp:filesystem:read_file",
        params={"path": "/tmp/test.txt"},
        reason="Smoke 测试"
    )

    ui = ApprovalUI()
    result = await ui.handle_approval(request)

    print(f"\n✅ 结果: {'批准' if result.approved else '拒绝'}")
    if result.comment:
        print(f"   评论: {result.comment}")

    return result.approved


async def smoke_test_2_rejection():
    """测试 2: 拒绝流程"""
    print("\n" + "=" * 60)
    print("🧪 Smoke Test 2: 拒绝流程")
    print("=" * 60)
    print("\n预期: 显示危险操作，选择 N 拒绝")

    request = ApprovalRequest(
        approval_id="smoke-002",
        workflow_id="workflow-test",
        task_id="task-2",
        task_name="删除文件（测试）",
        tool_name="mcp:filesystem:delete",
        params={
            "paths": ["/tmp/file1.txt", "/tmp/file2.txt", "/tmp/file3.txt"]
        },
        reason="执行删除操作"
    )

    ui = ApprovalUI()
    result = await ui.handle_approval(request)

    print(f"\n✅ 结果: {'批准' if result.approved else '拒绝'}")
    if result.comment:
        print(f"   评论: {result.comment}")

    return not result.approved  # 期望拒绝


async def smoke_test_3_help():
    """测试 3: 帮助功能"""
    print("\n" + "=" * 60)
    print("🧪 Smoke Test 3: 帮助功能")
    print("=" * 60)
    print("\n预期: 先选择 I 查看帮助，再选择 Y 批准")

    request = ApprovalRequest(
        approval_id="smoke-003",
        workflow_id="workflow-test",
        task_id="task-3",
        task_name="写入配置（测试）",
        tool_name="mcp:filesystem:write_file",
        params={"path": "/tmp/config.yaml", "content": "test: true"},
        reason="写入配置文件"
    )

    ui = ApprovalUI()
    result = await ui.handle_approval(request)

    print(f"\n✅ 结果: {'批准' if result.approved else '拒绝'}")
    if result.comment:
        print(f"   评论: {result.comment}")

    return result.approved


async def smoke_test_4_with_manager():
    """测试 4: 与审批管理器集成"""
    print("\n" + "=" * 60)
    print("🧪 Smoke Test 4: 与审批管理器集成")
    print("=" * 60)
    print("\n预期: 通过审批管理器触发审批，选择 Y 批准")

    # 创建内存数据库
    db = Database(":memory:")
    await db.initialize()

    # 创建审批管理器
    manager = ApprovalManager(database=db)

    # 注册 TUI 处理器
    ui = ApprovalUI()
    manager.register_handler(ui.handle_approval)

    # 创建测试工作流和任务
    workflow = Workflow(
        id="workflow-smoke",
        session_id="session-smoke",
        goal="Smoke 测试",
        tasks=[],
        status=WorkflowStatus.RUNNING
    )

    task = Task(
        id="task-4",
        name="执行命令（测试）",
        tool="mcp:shell:execute",
        params={"command": "echo 'Hello'", "timeout": 10},
        dependencies=[],
        requires_approval=True
    )

    # 请求审批
    result = await manager.request_approval(task, workflow)

    print(f"\n✅ 结果: {'批准' if result.approved else '拒绝'}")
    if result.comment:
        print(f"   评论: {result.comment}")

    # 显示审批历史
    print("\n📋 审批历史:")
    history = await manager.get_approval_history(workflow.id)
    ui.display_approval_history(history)

    # 显示统计
    stats = await manager.get_statistics(workflow.id)
    print("\n📊 统计信息:")
    print(f"   总数: {stats['total']}")
    print(f"   批准: {stats['approved']}")
    print(f"   拒绝: {stats['rejected']}")
    print(f"   批准率: {stats['approval_rate']:.0%}")

    return result.approved


async def smoke_test_5_history():
    """测试 5: 审批历史显示"""
    print("\n" + "=" * 60)
    print("🧪 Smoke Test 5: 审批历史显示")
    print("=" * 60)
    print("\n预期: 显示模拟的审批历史表格")

    from datetime import datetime

    # 创建模拟历史数据
    history = [
        {
            "created_at": datetime.now(),
            "task_name": "读取日志",
            "tool_name": "mcp:filesystem:read_file",
            "status": "approved",
            "user_comment": "安全操作"
        },
        {
            "created_at": datetime.now(),
            "task_name": "删除临时文件",
            "tool_name": "mcp:filesystem:delete",
            "status": "rejected",
            "user_comment": "路径不安全"
        },
        {
            "created_at": datetime.now(),
            "task_name": "备份数据库",
            "tool_name": "mcp:database:backup",
            "status": "approved",
            "user_comment": None
        }
    ]

    ui = ApprovalUI()
    ui.display_approval_history(history)

    print("\n✅ 历史显示完成")

    return True


async def main():
    """主函数 - 运行所有 smoke 测试"""
    print("\n" + "=" * 60)
    print("🚀 审批界面 Smoke 测试套件")
    print("=" * 60)
    print("\n提示:")
    print("  - 每个测试会显示一个审批界面")
    print("  - 按照提示选择 Y（批准）或 N（拒绝）")
    print("  - 测试 3 可以先选择 I 查看帮助")
    print("  - 可以添加可选评论")
    print()

    input("按 Enter 开始测试...")

    results = []

    try:
        # 测试 1: 基本 UI
        result1 = await smoke_test_1_basic_ui()
        results.append(("基本 UI 显示", result1))

        # 测试 2: 拒绝流程
        result2 = await smoke_test_2_rejection()
        results.append(("拒绝流程", result2))

        # 测试 3: 帮助功能
        result3 = await smoke_test_3_help()
        results.append(("帮助功能", result3))

        # 测试 4: 与管理器集成
        result4 = await smoke_test_4_with_manager()
        results.append(("审批管理器集成", result4))

        # 测试 5: 审批历史
        result5 = await smoke_test_5_history()
        results.append(("审批历史显示", result5))

        # 显示测试结果总结
        print("\n" + "=" * 60)
        print("📊 Smoke 测试结果总结")
        print("=" * 60)

        passed = 0
        failed = 0

        for test_name, passed_test in results:
            status = "✅ PASS" if passed_test else "❌ FAIL"
            print(f"{status}  {test_name}")
            if passed_test:
                passed += 1
            else:
                failed += 1

        print(f"\n总计: {passed} 通过, {failed} 失败")

        if failed == 0:
            print("\n🎉 所有 Smoke 测试通过！")
            return 0
        else:
            print(f"\n⚠️  {failed} 个测试失败")
            return 1

    except KeyboardInterrupt:
        print("\n\n⚠️  测试已取消")
        return 1
    except Exception as e:
        print(f"\n❌ 测试失败: {e}")
        import traceback
        traceback.print_exc()
        return 1


if __name__ == "__main__":
    exit_code = asyncio.run(main())
    sys.exit(exit_code)
