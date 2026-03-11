"""监控面板 - Rich TUI"""

import asyncio
from typing import Optional
from rich.console import Console
from rich.layout import Layout
from rich.panel import Panel
from rich.text import Text

from .monitor import PerformanceMonitor
from .collector import WorkflowMetrics


class MonitoringDashboard:
    """监控面板（Rich TUI）"""

    def __init__(
        self, monitor: PerformanceMonitor, console: Optional[Console] = None
    ) -> None:
        """初始化

        Args:
            monitor: 性能监控器实例
            console: Rich Console（可选）
        """
        self.monitor = monitor
        self.console = console or Console()
        self._stop_event = asyncio.Event()

    async def display_realtime(
        self, workflow_id: str, refresh_interval: float = 1.0
    ) -> None:
        """实时监控模式（自动刷新）

        Args:
            workflow_id: 工作流 ID
            refresh_interval: 刷新间隔（秒）
        """
        from rich.live import Live

        self.console.print("[cyan]按 Ctrl+C 停止监控[/cyan]\n")
        try:
            with Live(
                self._build_empty_layout(), console=self.console, refresh_per_second=1
            ) as live:
                while not self._stop_event.is_set():
                    try:
                        metrics = self.monitor.get_current_metrics(workflow_id)
                        if metrics:
                            live.update(self._build_layout(metrics))
                            if metrics.completed_at is not None:
                                break
                        await asyncio.sleep(refresh_interval)
                    except KeyboardInterrupt:
                        break
        finally:
            self._stop_event.clear()

    def display_snapshot(self, workflow_id: str) -> None:
        """快照模式（显示一次）

        Args:
            workflow_id: 工作流 ID
        """
        metrics = self.monitor.get_current_metrics(workflow_id)
        if not metrics:
            self.console.print(f"[yellow]工作流 {workflow_id} 未找到[/yellow]")
            return

        layout = self._build_layout(metrics)
        self.console.print(layout)

    def display_summary(self, workflow_id: str) -> None:
        """显示执行摘要

        Args:
            workflow_id: 工作流 ID
        """
        from rich.markdown import Markdown
        import asyncio
        from .reporter import ReportGenerator

        # 创建报告生成器
        reporter = ReportGenerator(self.monitor.storage)

        # 生成 Markdown 报告（异步）
        try:
            # 检查是否已有运行中的事件循环
            try:
                asyncio.get_running_loop()
                # 如果已经在事件循环中，提示用户使用异步版本
                self.console.print(
                    "[yellow]注意: 请在异步上下文中使用 display_summary_async() 方法[/yellow]"
                )
                return
            except RuntimeError:
                # 没有运行中的事件循环，可以使用 asyncio.run()
                pass

            report = asyncio.run(
                reporter.generate_workflow_report(workflow_id, output_format="markdown")
            )
            # 使用 Rich Markdown 显示
            markdown = Markdown(report)
            self.console.print(markdown)
        except ValueError as e:
            self.console.print(f"[yellow]{e}[/yellow]")
        except Exception as e:
            self.console.print(f"[red]Error: {e}[/red]")

    async def display_summary_async(self, workflow_id: str) -> None:
        """显示执行摘要（异步版本）

        Args:
            workflow_id: 工作流 ID
        """
        from rich.markdown import Markdown
        from .reporter import ReportGenerator

        # 创建报告生成器
        reporter = ReportGenerator(self.monitor.storage)

        # 生成 Markdown 报告（异步）
        try:
            report = await reporter.generate_workflow_report(
                workflow_id, output_format="markdown"
            )
            # 使用 Rich Markdown 显示
            markdown = Markdown(report)
            self.console.print(markdown)
        except ValueError as e:
            self.console.print(f"[yellow]{e}[/yellow]")
        except Exception as e:
            self.console.print(f"[red]Error: {e}[/red]")

    def _build_layout(self, metrics: WorkflowMetrics) -> Layout:
        """构建面板布局（内部方法）

        Args:
            metrics: 工作流指标

        Returns:
            Rich Layout 对象
        """
        layout = Layout()

        # 创建 3 个面板
        overview_panel = self._build_overview_panel(metrics)
        performance_panel = self._build_performance_panel(metrics)
        status_panel = self._build_status_panel(metrics)

        # 组合布局（垂直排列）
        layout.split_column(overview_panel, performance_panel, status_panel)

        return layout

    def _build_overview_panel(self, metrics: WorkflowMetrics) -> Panel:
        """构建概览面板

        Args:
            metrics: 工作流指标

        Returns:
            概览 Panel
        """
        # 计算进度
        progress = 0.0
        if metrics.total_tasks > 0:
            completed = metrics.completed_tasks + metrics.failed_tasks
            progress = completed / metrics.total_tasks

        # 状态
        status = "RUNNING" if metrics.completed_at is None else "COMPLETED"
        status_color = "yellow" if status == "RUNNING" else "green"

        # 格式化时长
        duration_str = f"{int(metrics.total_duration // 60):02d}:{int(metrics.total_duration % 60):02d}"

        content = Text()
        content.append("状态: ", style="bold")
        content.append(f"{status}", style=f"bold {status_color}")
        content.append("  |  进度: ", style="bold")
        content.append(
            f"{metrics.completed_tasks + metrics.failed_tasks}/{metrics.total_tasks} ({progress:.0%})"
        )
        content.append("\n")
        content.append("执行时间: ", style="bold")
        content.append(f"{duration_str}")
        content.append("  |  吞吐量: ", style="bold")
        content.append(f"{metrics.throughput:.1f} tasks/s")

        return Panel(
            content,
            title=f"工作流监控: {metrics.workflow_id[:12]}...",
            border_style="cyan",
        )

    def _build_performance_panel(self, metrics: WorkflowMetrics) -> Panel:
        """构建性能指标面板

        Args:
            metrics: 工作流指标

        Returns:
            性能指标 Panel
        """

        # 格式化延迟
        def format_duration(seconds: float) -> str:
            if seconds < 1:
                return f"{int(seconds * 1000)}ms"
            else:
                return f"{seconds:.1f}s"

        content = Text()
        content.append("平均延迟: ", style="bold")
        content.append(f"{format_duration(metrics.avg_task_duration):<10}")
        content.append("P95: ", style="bold")
        content.append(f"{format_duration(metrics.p95_task_duration):<10}")
        content.append("P99: ", style="bold")
        content.append(f"{format_duration(metrics.p99_task_duration)}")
        content.append("\n")
        content.append("CPU 使用: ", style="bold")
        content.append(f"{metrics.avg_cpu_percent:.0f}%{' ' * 6}")
        content.append("内存: ", style="bold")
        content.append(f"{metrics.peak_memory_mb:.0f} MB")

        return Panel(content, title="性能指标", border_style="green")

    def _build_status_panel(self, metrics: WorkflowMetrics) -> Panel:
        """构建任务状态面板

        Args:
            metrics: 工作流指标

        Returns:
            任务状态 Panel
        """
        running = (
            metrics.total_tasks
            - metrics.completed_tasks
            - metrics.failed_tasks
            - metrics.cancelled_tasks
        )

        content = Text()
        content.append("✓ 完成: ", style="green")
        content.append(f"{metrics.completed_tasks:<5}")
        content.append("✗ 失败: ", style="red")
        content.append(f"{metrics.failed_tasks:<5}")
        content.append("⏸ 取消: ", style="yellow")
        content.append(f"{metrics.cancelled_tasks:<5}")
        content.append("⏳ 运行: ", style="cyan")
        content.append(f"{running}")

        return Panel(content, title="任务状态", border_style="blue")

    def _build_empty_layout(self) -> Panel:
        """构建空布局（初始状态）

        Returns:
            空布局 Panel
        """
        content = Text("正在加载监控数据...", style="yellow")
        return Panel(content, title="监控面板", border_style="cyan")
