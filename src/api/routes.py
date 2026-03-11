"""FastAPI routes for the agent API"""
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, field_validator
from typing import Optional, Callable
from datetime import datetime
import uuid

from ..storage.database import Database
from ..storage.models import Session
from ..core.executor import AgentExecutor

router = APIRouter()

# Dependency functions will be set by main.py
_get_db: Optional[Callable[[], Database]] = None
_get_executor: Optional[Callable[[], AgentExecutor]] = None


def set_dependencies(get_db: Callable[[], Database], get_executor: Callable[[], AgentExecutor]) -> None:
    """Set dependency injection functions (called from main.py)"""
    global _get_db, _get_executor
    _get_db = get_db
    _get_executor = get_executor


class ChatRequest(BaseModel):
    """Chat request model"""
    message: str
    session_id: Optional[str] = None

    @field_validator("message")
    @classmethod
    def validate_message(cls, v: str) -> str:
        """Validate message is not empty or whitespace"""
        if not v or not v.strip():
            raise ValueError("Message cannot be empty or whitespace")
        return v


class ChatResponse(BaseModel):
    """Chat response model"""
    response: str
    session_id: str
    plan_type: Optional[str] = None


@router.get("/health")
async def health_check() -> dict:
    """Health check endpoint"""
    return {
        "status": "healthy",
        "timestamp": datetime.now().isoformat()
    }


@router.post("/api/chat", response_model=ChatResponse)
async def chat(request: ChatRequest) -> ChatResponse:
    """Chat endpoint - processes user messages"""
    if _get_db is None or _get_executor is None:
        raise HTTPException(status_code=500, detail="Server not initialized")

    db = _get_db()
    executor = _get_executor()

    # Auto-generate session_id if not provided
    session_id = request.session_id
    if not session_id:
        session_id = f"session-{uuid.uuid4()}"
        # Create new session
        session = Session(
            id=session_id,
            title="New Conversation",
            created_at=datetime.now(),
            updated_at=datetime.now()
        )
        await db.create_session(session)

    # Execute the request
    try:
        result = await executor.execute(request.message, session_id)
        return ChatResponse(
            response=result["response"],
            session_id=result["session_id"],
            plan_type=result.get("plan_type")
        )
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    except ConnectionError as e:
        # Ollama连接错误
        raise HTTPException(
            status_code=503,
            detail=f"无法连接到Ollama服务: {str(e)}。请确保Ollama正在运行 (ollama serve)"
        )
    except TimeoutError as e:
        # 超时错误
        raise HTTPException(
            status_code=504,
            detail=f"请求超时: {str(e)}。模型响应时间过长，建议切换到更快的模型或增加超时时间"
        )
    except Exception as e:
        # 其他错误 - 提供更详细的信息
        error_msg = str(e)
        if "Ollama" in error_msg or "model" in error_msg.lower():
            detail = f"Ollama错误: {error_msg}"
        else:
            detail = f"处理请求时出错: {error_msg}"

        raise HTTPException(status_code=500, detail=detail)
