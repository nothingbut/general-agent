"""CLI entry point for General Agent"""
import asyncio
from typing import Optional
import typer

# Create Typer app
cli = typer.Typer(
    name="agent",
    help="General Agent CLI - 智能助理命令行工具",
    add_completion=False,
)


def version_callback(value: bool) -> None:
    """Display version information"""
    if value:
        from . import __version__
        typer.echo(f"General Agent CLI v{__version__}")
        raise typer.Exit()


@cli.command()
def main(
    query: Optional[str] = typer.Argument(
        None,
        help="查询内容（命令行模式）"
    ),
    tui: bool = typer.Option(
        False,
        "--tui",
        help="启动交互式 TUI 界面"
    ),
    session: Optional[str] = typer.Option(
        None,
        "--session",
        help="指定会话 ID"
    ),
    verbose: bool = typer.Option(
        False,
        "--verbose",
        "-v",
        help="显示详细日志"
    ),
    version: Optional[bool] = typer.Option(
        None,
        "--version",
        callback=version_callback,
        is_eager=True,
        help="显示版本信息"
    ),
) -> None:
    """General Agent - 智能助理命令行工具"""

    if tui:
        # TUI mode
        from .app import run_tui
        run_tui(session, verbose)
    elif query:
        # Quick query mode
        from .quick import run_quick_query
        try:
            result = asyncio.run(run_quick_query(query, session, verbose))
            typer.echo(result)
        except KeyboardInterrupt:
            typer.echo("\n操作已取消")
            raise typer.Exit(1)
        except Exception as e:
            typer.echo(f"❌ 错误: {e}", err=True)
            raise typer.Exit(1)
    else:
        # No arguments, show usage hint
        typer.echo("用法：")
        typer.echo("  agent '你的问题'        # 快速查询")
        typer.echo("  agent --tui             # 交互式界面")
        typer.echo("  agent --help            # 查看帮助")


if __name__ == "__main__":
    cli()
