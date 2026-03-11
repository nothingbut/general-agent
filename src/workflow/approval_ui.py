"""TUI 审批界面 - 使用 Rich 库"""
import asyncio
import logging
from typing import Optional
from datetime import datetime

from rich.console import Console
from rich.panel import Panel
from rich.table import Table
from rich.prompt import Prompt
from rich.text import Text
from rich.markdown import Markdown

from .models import ApprovalRequest, ApprovalResult

logger = logging.getLogger(__name__)


class ApprovalUI:
    """TUI 审批界面"""

    def __init__(self, console: Optional[Console] = None):
        """初始化审批界面

        Args:
            console: Rich Console 实例，如果为 None 则创建新的
        """
        self.console = console or Console()
        self._running = False

    async def handle_approval(self, request: ApprovalRequest) -> ApprovalResult:
        """处理审批请求

        Args:
            request: 审批请求

        Returns:
            ApprovalResult: 审批结果
        """
        self._running = True

        try:
            # 显示审批请求
            self._display_approval_request(request)

            # 等待用户输入（在单独的线程中运行同步 I/O）
            loop = asyncio.get_event_loop()
            approved, comment = await loop.run_in_executor(
                None,
                self._get_user_decision
            )

            # 创建审批结果
            result = ApprovalResult(
                approval_id=request.approval_id,
                approved=approved,
                comment=comment
            )

            # 显示决策结果
            self._display_result(result)

            return result

        finally:
            self._running = False

    def _display_approval_request(self, request: ApprovalRequest) -> None:
        """显示审批请求

        Args:
            request: 审批请求
        """
        self.console.print()  # 空行

        # 创建标题
        title = Text("🔔 审批请求", style="bold cyan")
        if request.reason:
            title.append(f" - {request.reason}", style="yellow")

        # 创建信息表格
        table = Table(show_header=False, box=None, padding=(0, 2))
        table.add_column("字段", style="bold")
        table.add_column("值")

        table.add_row("审批 ID", request.approval_id[:8] + "...")
        table.add_row("工作流 ID", request.workflow_id[:8] + "...")
        table.add_row("任务名称", request.task_name)
        table.add_row("工具", request.tool_name)

        # 格式化参数
        if request.params:
            params_text = self._format_params(request.params)
            table.add_row("参数", params_text)

        # 创建面板
        panel = Panel(
            table,
            title=title,
            border_style="cyan",
            padding=(1, 2)
        )

        self.console.print(panel)

    def _format_params(self, params: dict) -> str:
        """格式化参数为可读字符串

        Args:
            params: 参数字典

        Returns:
            格式化后的字符串
        """
        lines = []
        for key, value in params.items():
            # 截断过长的值
            value_str = str(value)
            if len(value_str) > 60:
                value_str = value_str[:57] + "..."
            lines.append(f"  {key}: {value_str}")
        return "\n".join(lines)

    def _get_user_decision(self) -> tuple[bool, Optional[str]]:
        """获取用户决策（同步方法，用于 run_in_executor）

        Returns:
            tuple: (approved, comment)
        """
        self.console.print()

        # 显示操作提示
        prompt_text = Text()
        prompt_text.append("请选择操作：", style="bold")
        prompt_text.append(" [Y] 批准", style="green")
        prompt_text.append(" [N] 拒绝", style="red")
        prompt_text.append(" [I] 更多信息", style="cyan")
        self.console.print(prompt_text)

        while True:
            choice = Prompt.ask(
                "你的选择",
                choices=["y", "Y", "n", "N", "i", "I"],
                default="n"
            ).upper()

            if choice == "Y":
                # 批准
                comment = Prompt.ask(
                    "批准理由（可选，直接回车跳过）",
                    default=""
                )
                return True, comment if comment else None

            elif choice == "N":
                # 拒绝
                comment = Prompt.ask(
                    "拒绝理由（可选，直接回车跳过）",
                    default=""
                )
                return False, comment if comment else None

            elif choice == "I":
                # 显示更多信息
                self._display_help()
                continue

    def _display_help(self) -> None:
        """显示帮助信息"""
        help_text = """
### 审批说明

**批准 (Y)**: 允许执行此任务
- 任务将继续执行
- 工作流将继续进行

**拒绝 (N)**: 拒绝执行此任务
- 任务将被标记为失败
- 工作流可能停止（取决于错误处理策略）

**更多信息 (I)**: 显示此帮助信息
        """
        self.console.print(Panel(Markdown(help_text), border_style="blue"))

    def _display_result(self, result: ApprovalResult) -> None:
        """显示审批结果

        Args:
            result: 审批结果
        """
        if result.approved:
            style = "green"
            icon = "✓"
            status = "已批准"
        else:
            style = "red"
            icon = "✗"
            status = "已拒绝"

        message = Text()
        message.append(f"{icon} ", style=f"bold {style}")
        message.append(status, style=f"bold {style}")

        if result.comment:
            message.append(f"\n理由: {result.comment}")

        self.console.print()
        self.console.print(Panel(message, border_style=style))
        self.console.print()

    def display_approval_history(
        self,
        approvals: list[dict]
    ) -> None:
        """显示审批历史

        Args:
            approvals: 审批记录列表
        """
        if not approvals:
            self.console.print("[yellow]暂无审批历史[/yellow]")
            return

        # 创建历史表格
        table = Table(title="审批历史", show_lines=True)
        table.add_column("时间", style="cyan")
        table.add_column("任务", style="bold")
        table.add_column("工具")
        table.add_column("状态", justify="center")
        table.add_column("理由")

        for approval in approvals:
            # 格式化时间
            created_at = approval.get("created_at", "")
            if isinstance(created_at, datetime):
                time_str = created_at.strftime("%H:%M:%S")
            else:
                time_str = str(created_at)[:8]

            # 状态样式
            status = approval.get("status", "pending")
            if status == "approved":
                status_text = Text("✓ 批准", style="green")
            elif status == "rejected":
                status_text = Text("✗ 拒绝", style="red")
            else:
                status_text = Text("⏳ 待处理", style="yellow")

            table.add_row(
                time_str,
                approval.get("task_name", ""),
                approval.get("tool_name", ""),
                status_text,
                approval.get("user_comment", "") or "-"
            )

        self.console.print()
        self.console.print(table)
        self.console.print()


# 创建全局实例
_default_ui = ApprovalUI()


async def create_approval_handler(
    console: Optional[Console] = None
) -> callable:
    """创建审批处理函数

    Args:
        console: Rich Console 实例

    Returns:
        异步审批处理函数
    """
    ui = ApprovalUI(console)

    async def handler(request: ApprovalRequest) -> ApprovalResult:
        return await ui.handle_approval(request)

    return handler


# 默认处理函数
async def default_approval_handler(request: ApprovalRequest) -> ApprovalResult:
    """默认审批处理函数

    Args:
        request: 审批请求

    Returns:
        审批结果
    """
    return await _default_ui.handle_approval(request)
