"""工作流通知系统演示

运行方式:
    python examples/workflow_notification_demo.py
"""
import asyncio
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

from src.workflow import (
    Notification,
    NotificationPriority,
    NotificationManager,
    TerminalChannel,
    DesktopChannel
)


async def demo_1_terminal_notification():
    """演示 1: 终端通知"""
    print("\n" + "=" * 60)
    print("演示 1: 终端通知")
    print("=" * 60)

    manager = NotificationManager()

    # 普通通知
    notif1 = Notification(
        title="普通通知",
        message="这是一个普通优先级的通知",
        priority=NotificationPriority.NORMAL
    )

    await manager.send(notif1)
    await asyncio.sleep(1)

    # 高优先级通知
    notif2 = Notification(
        title="重要通知",
        message="这是一个高优先级的通知",
        priority=NotificationPriority.HIGH
    )

    await manager.send(notif2)
    await asyncio.sleep(1)

    # 关键通知
    notif3 = Notification(
        title="关键通知",
        message="这是一个关键优先级的通知",
        priority=NotificationPriority.CRITICAL
    )

    await manager.send(notif3)
    await asyncio.sleep(1)

    print("✅ 终端通知演示完成")


async def demo_2_desktop_notification():
    """演示 2: 桌面通知"""
    print("\n" + "=" * 60)
    print("演示 2: 桌面通知")
    print("=" * 60)

    manager = NotificationManager()

    # 注册桌面通知渠道
    desktop = DesktopChannel()
    if desktop.is_available():
        manager.register_channel("desktop", desktop)
        print("✅ 桌面通知渠道已注册")
    else:
        print("⚠️  桌面通知在此系统上不可用")
        return

    # 发送桌面通知
    notif = Notification(
        title="General Agent",
        message="工作流任务需要审批",
        priority=NotificationPriority.HIGH,
        channels=["desktop"]
    )

    results = await manager.send(notif)

    if results.get("desktop"):
        print("✅ 桌面通知已发送")
    else:
        print("❌ 桌面通知发送失败")


async def demo_3_multi_channel():
    """演示 3: 多渠道通知"""
    print("\n" + "=" * 60)
    print("演示 3: 多渠道通知")
    print("=" * 60)

    manager = NotificationManager()

    # 注册桌面渠道
    desktop = DesktopChannel()
    if desktop.is_available():
        manager.register_channel("desktop", desktop)

    # 发送到多个渠道
    notif = Notification(
        title="多渠道通知",
        message="此通知将发送到所有可用渠道",
        priority=NotificationPriority.HIGH,
        channels=manager.get_available_channels()
    )

    print(f"可用渠道: {', '.join(manager.get_available_channels())}")

    results = await manager.send(notif)

    print("\n发送结果:")
    for channel, success in results.items():
        status = "✅" if success else "❌"
        print(f"  {status} {channel}")


async def demo_4_priority_inference():
    """演示 4: 优先级推断"""
    print("\n" + "=" * 60)
    print("演示 4: 从工具名推断优先级")
    print("=" * 60)

    manager = NotificationManager()

    # 测试各种工具的优先级推断
    tools = [
        "mcp:filesystem:delete",
        "mcp:filesystem:write",
        "mcp:filesystem:read",
        "mcp:shell:execute",
        "mcp:database:query"
    ]

    for tool_name in tools:
        priority = NotificationPriority.from_tool(tool_name)

        notif = Notification(
            title=f"工具: {tool_name}",
            message=f"优先级: {priority.value}",
            priority=priority
        )

        await manager.send(notif)
        await asyncio.sleep(0.5)

    print("\n✅ 优先级推断演示完成")


async def demo_5_approval_notification():
    """演示 5: 审批通知场景"""
    print("\n" + "=" * 60)
    print("演示 5: 审批通知场景")
    print("=" * 60)

    manager = NotificationManager()

    # 注册桌面渠道
    desktop = DesktopChannel()
    if desktop.is_available():
        manager.register_channel("desktop", desktop)

    # 模拟审批请求通知
    notif = Notification(
        workflow_id="workflow-123",
        task_id="task-delete-files",
        title="审批请求",
        message="任务「删除临时文件」需要您的批准",
        priority=NotificationPriority.CRITICAL,
        channels=manager.get_available_channels()
    )

    print("发送审批请求通知...")
    results = await manager.send(notif)

    print("\n发送结果:")
    for channel, success in results.items():
        status = "✅" if success else "❌"
        print(f"  {status} {channel}")

    # 模拟审批完成通知
    await asyncio.sleep(2)

    notif2 = Notification(
        workflow_id="workflow-123",
        task_id="task-delete-files",
        title="审批已完成",
        message="任务「删除临时文件」已被批准",
        priority=NotificationPriority.NORMAL,
        channels=["terminal"]
    )

    await manager.send(notif2)


async def main():
    """主函数"""
    print("\n" + "=" * 60)
    print("工作流通知系统演示")
    print("=" * 60)
    print("\n选择演示场景:")
    print("  1 - 终端通知（不同优先级）")
    print("  2 - 桌面通知")
    print("  3 - 多渠道通知")
    print("  4 - 优先级推断")
    print("  5 - 审批通知场景")
    print("  6 - 运行所有演示")
    print("  0 - 退出")

    try:
        choice = input("\n请选择 (0-6): ").strip()

        if choice == "1":
            await demo_1_terminal_notification()
        elif choice == "2":
            await demo_2_desktop_notification()
        elif choice == "3":
            await demo_3_multi_channel()
        elif choice == "4":
            await demo_4_priority_inference()
        elif choice == "5":
            await demo_5_approval_notification()
        elif choice == "6":
            await demo_1_terminal_notification()
            await demo_2_desktop_notification()
            await demo_3_multi_channel()
            await demo_4_priority_inference()
            await demo_5_approval_notification()
        elif choice == "0":
            print("\n再见！")
            return
        else:
            print("\n无效选择！")
            return

        print("\n✅ 演示完成！")

    except KeyboardInterrupt:
        print("\n\n演示已取消")
    except Exception as e:
        print(f"\n错误: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    asyncio.run(main())
