"""Startup checks for CLI"""
import os
import logging
import asyncio
from pathlib import Path
import aiohttp

logger = logging.getLogger(__name__)


async def check_ollama_connection() -> bool:
    """
    检查 Ollama 连接

    Returns:
        True 如果连接成功，False 否则
    """
    ollama_url = os.getenv("OLLAMA_BASE_URL", "http://localhost:11434")

    try:
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{ollama_url}/api/tags", timeout=aiohttp.ClientTimeout(total=5)) as resp:
                return resp.status == 200
    except (aiohttp.ClientError, asyncio.TimeoutError, OSError) as e:
        logger.debug(f"Ollama connection check failed: {e}")
        return False


async def startup_checks(verbose: bool = False) -> None:
    """
    启动前的系统检查

    Args:
        verbose: 是否显示详细信息

    Raises:
        RuntimeError: 如果关键检查失败

    Note:
        This function assumes the CLI is run from the project root directory.
        Paths are relative to the current working directory:
        - data/ (database storage)
        - .env (environment configuration)
    """
    # 1. 检查数据目录
    # NOTE: Assumes CLI is run from project root where 'data/' should exist
    data_dir = Path("data")
    if not data_dir.exists():
        if verbose:
            print("📁 创建数据目录...")
        data_dir.mkdir(parents=True, exist_ok=True)

    # 2. 检查 .env 文件
    # NOTE: Assumes CLI is run from project root where '.env' should exist
    env_file = Path(".env")
    if not env_file.exists():
        if verbose:
            print("⚠️  .env 文件不存在，使用默认配置")

    # 3. 检查 Ollama（如果配置了）
    use_ollama = os.getenv("USE_OLLAMA", "false").lower() == "true"
    if use_ollama:
        if verbose:
            print("🔍 检查 Ollama 连接...")

        if not await check_ollama_connection():
            ollama_url = os.getenv("OLLAMA_BASE_URL", "http://localhost:11434")
            raise RuntimeError(
                f"❌ 无法连接到 Ollama 服务 ({ollama_url})\n"
                f"💡 提示：\n"
                f"  1. 运行 'ollama serve' 启动服务\n"
                f"  2. 或在 .env 中设置 USE_OLLAMA=false 使用 Mock 模式"
            )

        if verbose:
            print("✅ Ollama 连接正常")

    if verbose:
        print("✅ 启动检查完成")
