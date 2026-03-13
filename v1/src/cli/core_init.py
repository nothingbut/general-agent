"""Core component initialization for CLI"""
import os
import logging
from pathlib import Path
from typing import Optional

from ..storage.database import Database
from ..core.executor import AgentExecutor
from ..core.router import SimpleRouter
from ..core.llm_client import MockLLMClient
from ..core.ollama_client import OllamaClient, OllamaConfig
from ..core.config_loader import load_config
from ..skills.loader import SkillLoader
from ..skills.registry import SkillRegistry
from ..skills.executor import SkillExecutor
from ..mcp.connection_manager import MCPConnectionManager
from ..mcp.security import MCPSecurityLayer
from ..mcp.tool_executor import MCPToolExecutor
from ..mcp.config import load_mcp_config

logger = logging.getLogger(__name__)


async def initialize_database(
    db_path: Optional[Path] = None,
) -> Database:
    """
    初始化数据库

    Args:
        db_path: 数据库文件路径，默认为 data/general_agent.db

    Returns:
        Database 实例
    """
    if db_path is None:
        db_path = Path("data/general_agent.db")

    # 确保目录存在
    db_path.parent.mkdir(parents=True, exist_ok=True)

    # 初始化数据库
    db = Database(db_path)
    await db.initialize()

    return db


async def initialize_executor(
    db: Database,
    verbose: bool = False,
) -> AgentExecutor:
    """
    初始化 AgentExecutor 及其依赖

    Args:
        db: Database 实例
        verbose: 是否显示详细日志

    Returns:
        AgentExecutor 实例
    """
    # 设置日志级别
    if verbose:
        logging.basicConfig(level=logging.DEBUG)
    else:
        logging.basicConfig(level=logging.INFO)

    # 初始化 Router
    router = SimpleRouter()

    # 初始化 LLM Client
    # 优先使用环境变量，如果没有则从配置文件加载
    use_ollama = os.getenv("USE_OLLAMA")
    if use_ollama is None:
        # 从配置文件加载
        config = load_config()
        llm_config = config.get("llm", {})
        provider = llm_config.get("provider", "mock")

        if provider == "ollama":
            ollama_cfg = llm_config.get("ollama", {})
            ollama_config = OllamaConfig(
                base_url=ollama_cfg.get("base_url", "http://localhost:11434"),
                model=ollama_cfg.get("model", "qwen2.5:7b"),
                temperature=ollama_cfg.get("temperature", 0.7),
                timeout=ollama_cfg.get("timeout", 120.0)
            )
            llm_client = OllamaClient(config=ollama_config)
            logger.info(f"Using Ollama client with model: {ollama_config.model}")
        else:
            llm_client = MockLLMClient()
            logger.info("Using Mock LLM client")
    else:
        # 从环境变量加载
        if use_ollama.lower() == "true":
            ollama_config = OllamaConfig(
                base_url=os.getenv("OLLAMA_BASE_URL", "http://localhost:11434"),
                model=os.getenv("OLLAMA_MODEL", "llama3.2:latest"),
                temperature=float(os.getenv("OLLAMA_TEMPERATURE", "0.7")),
                timeout=float(os.getenv("OLLAMA_TIMEOUT", "120.0"))
            )
            llm_client = OllamaClient(config=ollama_config)
            logger.info(f"Using Ollama client with model: {ollama_config.model}")
        else:
            llm_client = MockLLMClient()
            logger.info("Using Mock LLM client")

    # 初始化 Skill System
    skill_executor = None
    registry = None
    skills_dir = Path("skills")
    if skills_dir.exists():
        try:
            loader = SkillLoader(skills_dir)
            skills = loader.load_all()

            registry = SkillRegistry()
            for skill in skills:
                registry.register(skill)

            skill_executor = SkillExecutor(llm_client)
            logger.info(f"Loaded {len(skills)} skills")
        except Exception as e:
            logger.warning(f"Failed to load skills: {e}")

    # 初始化 MCP
    mcp_executor = None
    mcp_enabled = os.getenv("MCP_ENABLED", "false").lower() == "true"

    if mcp_enabled:
        try:
            config_path = Path("config/mcp_config.yaml")
            if config_path.exists():
                mcp_config = load_mcp_config(str(config_path))
                mcp_manager = MCPConnectionManager(str(config_path))

                fs_config = mcp_config.servers.get("filesystem")
                if fs_config:
                    mcp_security = MCPSecurityLayer(fs_config.security)
                    mcp_executor = MCPToolExecutor(mcp_manager, mcp_security, db)
                    logger.info("MCP integration initialized")
        except Exception as e:
            logger.warning(f"Failed to initialize MCP: {e}")

    # 创建 Executor
    executor = AgentExecutor(
        db,
        router,
        llm_client,
        skill_registry=registry if skill_executor else None,
        skill_executor=skill_executor,
        mcp_executor=mcp_executor
    )

    return executor
