"""上下文管理器"""
import uuid
from datetime import datetime
from typing import List, Dict, Any, Optional
from ..storage.database import Database
from ..storage.models import Message


class ContextManager:
    """管理会话上下文"""

    VALID_ROLES = {'user', 'assistant', 'system'}
    DEFAULT_LLM_LIMIT = 10

    def __init__(self, db: Database, session_id: str) -> None:
        """初始化上下文管理器

        Args:
            db: 数据库实例
            session_id: 会话ID

        Raises:
            ValueError: 如果session_id为空
        """
        if not session_id or not session_id.strip():
            raise ValueError("Session ID cannot be empty")

        self.db = db
        self.session_id = session_id

    async def add_message(
        self,
        role: str,
        content: str,
        metadata: Optional[Dict[str, Any]] = None
    ) -> Message:
        """添加消息到上下文

        Args:
            role: 消息角色 (user/assistant/system)
            content: 消息内容
            metadata: 可选的元数据

        Returns:
            添加的消息对象

        Raises:
            ValueError: 如果role无效或content为空
        """
        if role not in self.VALID_ROLES:
            raise ValueError(
                f"Invalid role: {role}. Must be one of {self.VALID_ROLES}"
            )

        if not content or not content.strip():
            raise ValueError("Content cannot be empty")

        message = Message(
            id=f"msg-{uuid.uuid4().hex[:8]}",
            session_id=self.session_id,
            role=role,
            content=content,
            timestamp=datetime.now(),
            metadata=metadata
        )
        await self.db.add_message(message)
        return message

    async def get_history(self, limit: Optional[int] = None) -> List[Message]:
        """获取会话历史

        Args:
            limit: 可选的消息数量限制

        Returns:
            消息列表，按时间排序
        """
        if limit is not None:
            return await self.db.get_recent_messages(self.session_id, limit)
        return await self.db.get_messages(self.session_id)

    async def format_for_llm(
        self,
        limit: int = DEFAULT_LLM_LIMIT
    ) -> List[Dict[str, Any]]:
        """格式化为LLM输入格式

        Args:
            limit: 消息数量限制，默认10条

        Returns:
            格式化的消息列表，包含role和content字段
        """
        messages = await self.get_history(limit)
        return [
            {
                "role": msg.role,
                "content": msg.content
            }
            for msg in messages
        ]
