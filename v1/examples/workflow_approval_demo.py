"""工作流审批演示 - 展示 TUI 审批界面

运行方式:
    python examples/workflow_approval_demo.py
"""
import asyncio
import sys
from pathlib import Path

# 添加项目根目录到 Python 路径
sys.path.insert(0, str(Path(__file__).parent.parent))

from src.workflow import (
    ApprovalManager,
    ApprovalUI,
    ApprovalRequest,
    Workflow,
    Task,
    TaskStatus,
    WorkflowStatus
)
from src.storage.database import Database


async def demo_basic_approval():
    """演示基本审批流程"""
    print("\n" + "=" * 60)
    print("演示 1: 基本审批流程")
    print("=" * 60)

    # 创建审批请求
    request = ApprovalRequest(
        approval_id="demo-001",
        workflow_id="workflow-123",
        task_id="task-1",
        task_name="读取配置文件",
        tool_name="mcp:filesystem:read_file",
        params={
            "path": "/etc/config.yaml"
        },
        reason="读取系统配置"
    )

    # 创建 UI 并处理
    ui = ApprovalUI()
    result = await ui.handle_approval(request)

    print(f"\n审批结果: {'批准' if result.approved else '拒绝'}")
    if result.comment:
        print(f"评论: {result.comment}")


async def demo_dangerous_operation():
    """演示危险操作审批"""
    print("\n" + "=" * 60)
    print("演示 2: 危险操作审批")
    print("=" * 60)

    request = ApprovalRequest(
        approval_id="demo-002",
        workflow_id="workflow-123",
        task_id="task-2",
        task_name="删除临时文件",
        tool_name="mcp:filesystem:delete",
        params={
            "paths": [
                "/tmp/cache/file1.txt",
                "/tmp/cache/file2.txt",
                "/tmp/cache/file3.txt"
            ]
        },
        reason="执行删除操作"
    )

    ui = ApprovalUI()
    result = await ui.handle_approval(request)

    print(f"\n审批结果: {'批准' if result.approved else '拒绝'}")
    if result.comment:
        print(f"评论: {result.comment}")


async def demo_with_approval_manager():
    """演示与审批管理器集成"""
    print("\n" + "=" * 60)
    print("演示 3: 与审批管理器集成")
    print("=" * 60)

    # 创建数据库（内存模式）
    db = Database(":memory:")
    await db.initialize()

    # 创建审批管理器
    manager = ApprovalManager(database=db)

    # 注册 TUI 处理器
    ui = ApprovalUI()
    manager.register_handler(ui.handle_approval)

    # 创建测试工作流和任务
    workflow = Workflow(
        id="workflow-456",
        session_id="session-789",
        goal="执行文件操作",
        tasks=[],
        status=WorkflowStatus.RUNNING
    )

    task = Task(
        id="task-3",
        name="执行脚本",
        tool="mcp:python:execute",
        params={
            "script": "print('Hello World')",
            "timeout": 30
        },
        dependencies=[],
        requires_approval=True
    )

    print("\n正在请求审批...")
    result = await manager.request_approval(task, workflow)

    print(f"\n审批结果: {'批准' if result.approved else '拒绝'}")
    if result.comment:
        print(f"评论: {result.comment}")

    # 显示审批历史
    history = await manager.get_approval_history(workflow.id)
    if history:
        print("\n审批历史:")
        ui.display_approval_history(history)

    # 显示统计信息
    stats = await manager.get_statistics(workflow.id)
    print("\n审批统计:")
    print(f"  总数: {stats['total']}")
    print(f"  批准: {stats['approved']}")
    print(f"  拒绝: {stats['rejected']}")
    print(f"  批准率: {stats['approval_rate']:.0%}")


async def demo_approval_history():
    """演示审批历史显示"""
    print("\n" + "=" * 60)
    print("演示 4: 审批历史显示")
    print("=" * 60)

    # 创建模拟历史数据
    from datetime import datetime

    history = [
        {
            "created_at": datetime.now(),
            "task_name": "读取文件",
            "tool_name": "mcp:filesystem:read_file",
            "status": "approved",
            "user_comment": "安全操作"
        },
        {
            "created_at": datetime.now(),
            "task_name": "写入数据",
            "tool_name": "mcp:filesystem:write_file",
            "status": "approved",
            "user_comment": "已检查路径"
        },
        {
            "created_at": datetime.now(),
            "task_name": "删除文件",
            "tool_name": "mcp:filesystem:delete",
            "status": "rejected",
            "user_comment": "路径不安全"
        },
        {
            "created_at": datetime.now(),
            "task_name": "执行命令",
            "tool_name": "mcp:shell:execute",
            "status": "pending",
            "user_comment": None
        }
    ]

    ui = ApprovalUI()
    ui.display_approval_history(history)


async def main():
    """主函数"""
    print("\n" + "=" * 60)
    print("工作流审批系统 - TUI 演示")
    print("=" * 60)
    print("\n选择演示场景:")
    print("  1 - 基本审批流程")
    print("  2 - 危险操作审批")
    print("  3 - 与审批管理器集成")
    print("  4 - 审批历史显示")
    print("  5 - 运行所有演示")
    print("  0 - 退出")

    try:
        choice = input("\n请选择 (0-5): ").strip()

        if choice == "1":
            await demo_basic_approval()
        elif choice == "2":
            await demo_dangerous_operation()
        elif choice == "3":
            await demo_with_approval_manager()
        elif choice == "4":
            await demo_approval_history()
        elif choice == "5":
            await demo_basic_approval()
            await demo_dangerous_operation()
            await demo_with_approval_manager()
            await demo_approval_history()
        elif choice == "0":
            print("\n再见！")
            return
        else:
            print("\n无效选择！")
            return

        print("\n演示完成！")

    except KeyboardInterrupt:
        print("\n\n演示已取消")
    except Exception as e:
        print(f"\n错误: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    asyncio.run(main())
