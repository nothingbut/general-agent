"""FastAPI application entry point"""
import logging
import sys
import os
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(levelname)s:     %(message)s',
    stream=sys.stdout
)

from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from pathlib import Path

from .api import routes
from .storage.database import Database
from .core.executor import AgentExecutor
from .core.router import SimpleRouter
from .core.llm_client import MockLLMClient
from .core.ollama_client import OllamaClient, OllamaConfig
import os
from .skills.loader import SkillLoader
from .skills.registry import SkillRegistry
from .skills.executor import SkillExecutor
from .mcp.connection_manager import MCPConnectionManager
from .mcp.security import MCPSecurityLayer
from .mcp.tool_executor import MCPToolExecutor
from .mcp.config import load_mcp_config

logger = logging.getLogger(__name__)

# Global singletons (MVP pattern)
_db: Database | None = None
_executor: AgentExecutor | None = None
_skill_registry: SkillRegistry | None = None
_mcp_manager: MCPConnectionManager | None = None


def get_db() -> Database:
    """Dependency to get database instance"""
    global _db
    if _db is None:
        raise RuntimeError("Database not initialized")
    return _db


def get_executor() -> AgentExecutor:
    """Dependency to get executor instance"""
    global _executor
    if _executor is None:
        raise RuntimeError("Executor not initialized")
    return _executor


async def startup() -> None:
    """Initialize database, skills, MCP, and executor on startup"""
    global _db, _executor, _skill_registry, _mcp_manager

    # Initialize database
    db_path = Path("data/general_agent.db")
    _db = Database(db_path)
    await _db.initialize()

    # Initialize skill system
    skills_dir = Path("skills")
    if skills_dir.exists():
        try:
            # Load skills from filesystem
            loader = SkillLoader(skills_dir)
            skills = loader.load_all()

            # Register skills
            _skill_registry = SkillRegistry()
            for skill in skills:
                _skill_registry.register(skill)

            logger.info(f"Loaded {len(skills)} skills from {skills_dir}")

        except Exception as e:
            logger.warning(f"Failed to load skills: {e}")
            _skill_registry = None
    else:
        logger.info(f"Skills directory not found: {skills_dir}")
        _skill_registry = None

    # Initialize executor
    router_instance = SimpleRouter()

    # Use Ollama if configured, otherwise fall back to Mock
    use_ollama = os.getenv("USE_OLLAMA", "false").lower() == "true"
    if use_ollama:
        ollama_config = OllamaConfig(
            base_url=os.getenv("OLLAMA_BASE_URL", "http://localhost:11434"),
            model=os.getenv("OLLAMA_MODEL", "llama3.2:latest"),
            temperature=float(os.getenv("OLLAMA_TEMPERATURE", "0.7")),
            timeout=float(os.getenv("OLLAMA_TIMEOUT", "120.0"))  # 支持环境变量配置超时
        )
        llm_client = OllamaClient(config=ollama_config)
        logger.info(f"Using Ollama client with model: {ollama_config.model}")
    else:
        llm_client = MockLLMClient()
        logger.info("Using Mock LLM client")

    # Create skill executor if skills are loaded
    skill_executor = None
    if _skill_registry:
        skill_executor = SkillExecutor(llm_client)

    # Initialize MCP if enabled
    mcp_enabled = os.getenv("MCP_ENABLED", "false").lower() == "true"
    mcp_executor = None

    if mcp_enabled:
        try:
            config_path = Path("config/mcp_config.yaml")
            if config_path.exists():
                mcp_config = load_mcp_config(str(config_path))
                _mcp_manager = MCPConnectionManager(str(config_path))

                # Get security config for filesystem server
                fs_config = mcp_config.servers.get("filesystem")
                if fs_config:
                    mcp_security = MCPSecurityLayer(fs_config.security)
                    mcp_executor = MCPToolExecutor(_mcp_manager, mcp_security, _db)
                    logger.info("MCP integration initialized")
                else:
                    logger.warning("No filesystem server configured in MCP config")
            else:
                logger.info(f"MCP config not found at {config_path}, MCP disabled")
        except Exception as e:
            logger.error(f"Failed to initialize MCP: {e}")
            logger.warning("Continuing without MCP support")

    _executor = AgentExecutor(
        _db,
        router_instance,
        llm_client,
        skill_registry=_skill_registry,
        skill_executor=skill_executor,
        mcp_executor=mcp_executor
    )

    # Set dependencies for routes
    routes.set_dependencies(get_db, get_executor)


async def shutdown() -> None:
    """Close database and MCP connections on shutdown"""
    global _db, _mcp_manager

    if _mcp_manager:
        try:
            await _mcp_manager.shutdown_all()
            logger.info("MCP connections closed")
        except Exception as e:
            logger.error(f"Error closing MCP connections: {e}")

    if _db:
        await _db.close()


# Create FastAPI app
app = FastAPI(
    title="General Agent API",
    description="REST API for the General Agent system",
    version="0.1.0"
)

# CORS middleware for development
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=False,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Mount static files
app.mount("/static", StaticFiles(directory="static"), name="static")

# Templates
templates = Jinja2Templates(directory="templates")


@app.get("/")
async def index(request: Request):
    """Serve the main page"""
    return templates.TemplateResponse("index.html", {"request": request})


# Register API routes
app.include_router(routes.router)

# Lifecycle events
app.add_event_handler("startup", startup)
app.add_event_handler("shutdown", shutdown)
