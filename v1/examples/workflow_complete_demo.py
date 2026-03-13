"""Phase 7 完整功能演示 - 工作流、审批、监控、通知

运行方式:
    python examples/workflow_complete_demo.py
"""
import asyncio
import sys
from pathlib import Path
from datetime import datetime

# 添加项目根目录到 Python 路径
sys.path.insert(0, str(Path(__file__).parent.parent))

from src.workflow import (
    Orchestrator,
    Task,
    TaskStatus,
    ApprovalManager,
    ApprovalPolicy,
    ApprovalStrategy,
    NotificationManager,
    NotificationChannel,
)
from src.workflow.performance import (
    PerformanceMonitor,
    MonitoringDashboard,
    AlertManager,
    AlertRule,
)
from src.storage.database import Database


async def example_task_1(context: dict) -> dict:
    """示例任务 1: 数据准备"""
    print(f"[Task 1] 正在准备数据...")
    await asyncio.sleep(1)
    return {"data": [1, 2, 3, 4, 5], "status": "prepared"}


async def example_task_2(context: dict) -> dict:
    """示例任务 2: 数据处理"""
    data = context.get("data", [])
    print(f"[Task 2] 正在处理数据: {data}")
    await asyncio.sleep(1.5)
    processed = [x * 2 for x in data]
    return {"processed": processed, "count": len(processed)}


async def example_task_3(context: dict) -> dict:
    """示例任务 3: 数据保存"""
    processed = context.get("processed", [])
    print(f"[Task 3] 正在保存数据: {processed}")
    await asyncio.sleep(0.8)
    return {"saved": True, "file": "/tmp/result.json"}


async def demo_complete_workflow():
    """完整工作流演示"""
    print("\n" + "=" * 80)
    print("Phase 7 完整功能演示")
    print("=" * 80)

    # 1. 初始化数据库
    db_path = Path("/tmp/workflow_demo.db")
    if db_path.exists():
        db_path.unlink()

    db = Database(db_path)
    await db.initialize()

    try:
        # 2. 创建工作流编排器
        print("\n[1/6] 创建工作流编排器...")
        orchestrator = Orchestrator(db)

        # 3. 配置审批管理器
        print("[2/6] 配置审批管理器...")
        approval_manager = ApprovalManager(db)

        # 设置自动审批策略（演示用）
        policy = ApprovalPolicy(
            policy_id="demo-policy",
            name="演示策略",
            strategy=ApprovalStrategy.AUTO_APPROVE,
            workflow_patterns=["demo-*"],
        )
        approval_manager.add_policy(policy)

        # 4. 配置通知系统
        print("[3/6] 配置通知系统...")
        notification_manager = NotificationManager()
        notification_manager.add_channel(NotificationChannel.CONSOLE)
        notification_manager.add_channel(NotificationChannel.LOG)

        # 5. 启动性能监控
        print("[4/6] 启动性能监控...")
        monitor = PerformanceMonitor(db)
        await monitor.start()

        # 配置告警规则
        alert_manager = AlertManager(monitor)
        alert_manager.add_rule(
            AlertRule.task_duration_threshold(
                task_name="*",
                threshold_seconds=5.0,
                severity="warning"
            )
        )
        alert_manager.add_rule(
            AlertRule.failure_rate_threshold(
                threshold_percent=50.0,
                severity="critical"
            )
        )

        # 6. 创建并执行工作流
        print("[5/6] 创建并执行工作流...")

        # 创建任务
        task1 = Task(
            task_id="task-1",
            name="数据准备",
            function=example_task_1,
            dependencies=[],
            retry_policy={"max_retries": 3, "retry_delay": 1.0},
        )

        task2 = Task(
            task_id="task-2",
            name="数据处理",
            function=example_task_2,
            dependencies=["task-1"],
            timeout=10.0,
        )

        task3 = Task(
            task_id="task-3",
            name="数据保存",
            function=example_task_3,
            dependencies=["task-2"],
        )

        # 创建工作流
        workflow_id = f"demo-workflow-{datetime.now().strftime('%Y%m%d-%H%M%S')}"
        await orchestrator.create_workflow(
            workflow_id=workflow_id,
            tasks=[task1, task2, task3],
            metadata={
                "description": "完整功能演示工作流",
                "created_by": "demo",
            }
        )

        print(f"\n工作流已创建: {workflow_id}")
        print("正在执行任务...\n")

        # 执行工作流
        result = await orchestrator.execute_workflow(workflow_id)

        print(f"\n工作流执行{'成功' if result.success else '失败'}!")
        print(f"状态: {result.status}")
        print(f"执行时间: {result.duration:.2f}秒")
        print(f"完成任务: {result.completed_tasks}/{result.total_tasks}")

        # 7. 查看监控面板
        print("\n[6/6] 查看监控面板...")
        dashboard = MonitoringDashboard(monitor)

        # 显示快照
        print("\n--- 监控快照 ---")
        dashboard.display_snapshot(workflow_id)

        # 显示执行摘要
        print("\n--- 执行摘要 ---")
        dashboard.display_summary(workflow_id)

        # 检查告警
        alerts = alert_manager.check_alerts(workflow_id)
        if alerts:
            print(f"\n⚠️  触发 {len(alerts)} 个告警:")
            for alert in alerts:
                print(f"  - [{alert.severity.upper()}] {alert.message}")
        else:
            print("\n✓ 未触发任何告警")

        # 8. 生成性能报告
        print("\n--- 性能报告 ---")
        from src.workflow.performance.reporter import ReportGenerator
        reporter = ReportGenerator(db)

        # JSON 报告
        json_report = await reporter.generate_workflow_report(
            workflow_id,
            output_format="json"
        )
        print(f"JSON 报告已生成 ({len(json_report)} 字符)")

        # Markdown 报告
        md_report = await reporter.generate_workflow_report(
            workflow_id,
            output_format="markdown"
        )

        # 保存到文件
        report_file = Path(f"/tmp/workflow_report_{workflow_id}.md")
        report_file.write_text(md_report)
        print(f"Markdown 报告已保存: {report_file}")

    finally:
        # 清理
        await monitor.stop()
        await db.close()
        print("\n演示完成！")


async def demo_realtime_monitoring():
    """实时监控演示"""
    print("\n" + "=" * 80)
    print("实时监控演示")
    print("=" * 80)

    db_path = Path("/tmp/workflow_realtime.db")
    if db_path.exists():
        db_path.unlink()

    db = Database(db_path)
    await db.initialize()

    try:
        orchestrator = Orchestrator(db)
        monitor = PerformanceMonitor(db)
        await monitor.start()

        # 创建长时间运行的任务
        async def long_task(context: dict) -> dict:
            task_num = context.get("task_num", 0)
            print(f"[Task {task_num}] 开始执行...")
            await asyncio.sleep(3)
            return {"result": f"task-{task_num}-completed"}

        # 创建多个任务
        tasks = []
        for i in range(5):
            task = Task(
                task_id=f"task-{i}",
                name=f"长任务 {i}",
                function=long_task,
                dependencies=[f"task-{i-1}"] if i > 0 else [],
                context={"task_num": i},
            )
            tasks.append(task)

        workflow_id = "realtime-demo"
        await orchestrator.create_workflow(workflow_id, tasks)

        # 启动实时监控（在后台）
        dashboard = MonitoringDashboard(monitor)

        print("\n提示: 实时监控将在工作流执行期间显示更新")
        print("按 Ctrl+C 可以停止监控\n")

        # 同时执行工作流和监控
        monitor_task = asyncio.create_task(
            dashboard.display_realtime(workflow_id, refresh_interval=0.5)
        )
        execute_task = asyncio.create_task(orchestrator.execute_workflow(workflow_id))

        # 等待工作流完成
        result = await execute_task

        # 等待监控显示最终状态
        await asyncio.sleep(2)
        monitor_task.cancel()

        print(f"\n工作流执行完成: {result.status}")

    finally:
        await monitor.stop()
        await db.close()


async def main():
    """主函数"""
    print("请选择演示模式:")
    print("1. 完整功能演示（推荐）")
    print("2. 实时监控演示")

    try:
        choice = input("\n请输入选项 (1 或 2): ").strip()

        if choice == "1":
            await demo_complete_workflow()
        elif choice == "2":
            await demo_realtime_monitoring()
        else:
            print("无效选项，运行默认演示...")
            await demo_complete_workflow()
    except KeyboardInterrupt:
        print("\n\n演示已取消")
    except Exception as e:
        print(f"\n错误: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    asyncio.run(main())
