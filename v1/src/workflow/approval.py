"""工作流审批机制 - 人机协同"""
import asyncio
import logging
import uuid
from datetime import datetime
from typing import Optional, Dict, Any, List, Callable
from enum import Enum

from .models import (
    Task,
    Workflow,
    ApprovalRequest,
    ApprovalResult
)

logger = logging.getLogger(__name__)


class ApprovalStatus(Enum):
    """审批状态"""
    PENDING = "pending"
    APPROVED = "approved"
    REJECTED = "rejected"
    TIMEOUT = "timeout"


class ApprovalManager:
    """审批管理器 - 管理工作流中的审批流程"""

    def __init__(
        self,
        database,
        default_timeout: float = 300.0,  # 5 分钟
        enable_auto_approve: bool = False
    ):
        """初始化审批管理器

        Args:
            database: 数据库实例
            default_timeout: 默认审批超时时间（秒）
            enable_auto_approve: 是否启用自动批准（用于测试）
        """
        self.database = database
        self.default_timeout = default_timeout
        self.enable_auto_approve = enable_auto_approve

        # 审批回调函数
        self._approval_handlers: List[Callable] = []

        # 待处理的审批请求（内存缓存）
        self._pending_approvals: Dict[str, ApprovalRequest] = {}

        # 审批结果缓存
        self._approval_results: Dict[str, ApprovalResult] = {}

    def register_handler(self, handler: Callable) -> None:
        """注册审批处理函数

        Args:
            handler: 处理函数，签名为 async def handler(request: ApprovalRequest) -> ApprovalResult
        """
        self._approval_handlers.append(handler)
        logger.info(f"Registered approval handler: {handler.__name__}")

    def unregister_handler(self, handler: Callable) -> None:
        """取消注册审批处理函数

        Args:
            handler: 要取消的处理函数
        """
        if handler in self._approval_handlers:
            self._approval_handlers.remove(handler)
            logger.info(f"Unregistered approval handler: {handler.__name__}")

    async def request_approval(
        self,
        task: Task,
        workflow: Workflow,
        timeout: Optional[float] = None
    ) -> ApprovalResult:
        """请求审批

        Args:
            task: 需要审批的任务
            workflow: 所属工作流
            timeout: 超时时间（秒），None 使用默认值

        Returns:
            审批结果

        Raises:
            TimeoutError: 审批超时
        """
        # 创建审批请求
        request = ApprovalRequest(
            approval_id=str(uuid.uuid4()),
            workflow_id=workflow.id,
            task_id=task.id,
            task_name=task.name,
            tool_name=task.tool,
            params=task.params,
            reason=self._generate_approval_reason(task),
            created_at=datetime.now()
        )

        logger.info(f"Approval requested for task {task.id}: {task.name}")

        # 保存到数据库
        await self.database.create_approval(
            approval_id=request.approval_id,
            workflow_id=workflow.id,
            task_id=task.id,
            status=ApprovalStatus.PENDING.value,
            created_at=request.created_at,
            user_comment=request.reason
        )

        # 添加到待处理列表
        self._pending_approvals[request.approval_id] = request

        # 如果启用自动批准（测试模式）
        if self.enable_auto_approve:
            logger.warning("Auto-approve mode enabled, automatically approving")
            return await self._auto_approve(request)

        # 调用注册的处理函数
        if self._approval_handlers:
            try:
                result = await self._call_handlers(request, timeout or self.default_timeout)
                await self._save_approval_result(request, result)
                return result
            except asyncio.TimeoutError:
                logger.warning(f"Approval timeout for task {task.id}")
                result = ApprovalResult(
                approval_id=request.approval_id,
                approved=False,
                comment="审批超时",
                responded_at=datetime.now(
            )
                )
                await self._save_approval_result(request, result)
                raise TimeoutError(f"Approval timeout after {timeout or self.default_timeout}s")
        else:
            # 没有处理函数，默认拒绝
            logger.warning("No approval handlers registered, rejecting by default")
            result = ApprovalResult(
                approval_id=request.approval_id,
                approved=False,
                comment="无可用的审批处理器",
                responded_at=datetime.now(
            )
            )
            await self._save_approval_result(request, result)
            return result

    async def _call_handlers(
        self,
        request: ApprovalRequest,
        timeout: float
    ) -> ApprovalResult:
        """调用审批处理函数

        Args:
            request: 审批请求
            timeout: 超时时间

        Returns:
            审批结果

        Raises:
            asyncio.TimeoutError: 超时
        """
        # 并发调用所有处理函数，取第一个响应
        tasks = [
            asyncio.create_task(handler(request))
            for handler in self._approval_handlers
        ]

        try:
            # 等待第一个完成
            done, pending = await asyncio.wait(
                tasks,
                timeout=timeout,
                return_when=asyncio.FIRST_COMPLETED
            )

            # 取消其他任务
            for task in pending:
                task.cancel()

            if done:
                # 获取第一个结果
                result = done.pop().result()
                return result
            else:
                # 超时
                raise asyncio.TimeoutError()

        except Exception as e:
            logger.error(f"Error calling approval handlers: {e}")
            raise

    async def _auto_approve(self, request: ApprovalRequest) -> ApprovalResult:
        """自动批准（测试模式）

        Args:
            request: 审批请求

        Returns:
            审批结果
        """
        result = ApprovalResult(
                approval_id=request.approval_id,
                approved=True,
                comment="自动批准（测试模式）",
                responded_at=datetime.now(
            )
        )
        await self._save_approval_result(request, result)
        return result

    async def _save_approval_result(
        self,
        request: ApprovalRequest,
        result: ApprovalResult
    ) -> None:
        """保存审批结果

        Args:
            request: 审批请求
            result: 审批结果
        """
        # 更新数据库
        status = ApprovalStatus.APPROVED if result.approved else ApprovalStatus.REJECTED
        await self.database.update_approval(
            approval_id=request.approval_id,
            status=status.value,
            user_comment=result.comment,
            responded_at=result.responded_at
        )

        # 更新缓存
        self._approval_results[request.approval_id] = result

        # 从待处理列表移除
        if request.approval_id in self._pending_approvals:
            del self._pending_approvals[request.approval_id]

        logger.info(
            f"Approval {status.value} for task {request.task_id}: {result.comment}"
        )

    def _generate_approval_reason(self, task: Task) -> str:
        """生成审批原因描述

        Args:
            task: 任务

        Returns:
            审批原因
        """
        tool_type = task.tool.split(":")[0] if ":" in task.tool else task.tool

        # 根据工具类型生成原因
        reasons = {
            "mcp": "MCP 工具调用可能修改系统状态",
            "skill": "技能执行可能产生副作用",
            "rag": "RAG 操作可能访问敏感数据",
            "llm": "LLM 调用可能产生不可预测的结果"
        }

        base_reason = reasons.get(tool_type, "需要用户确认")

        # 添加具体信息
        if "delete" in task.tool.lower() or "remove" in task.tool.lower():
            return f"{base_reason}：执行删除操作"
        elif "write" in task.tool.lower() or "create" in task.tool.lower():
            return f"{base_reason}：执行写入操作"
        elif "execute" in task.tool.lower() or "run" in task.tool.lower():
            return f"{base_reason}：执行命令或脚本"
        else:
            return base_reason

    async def get_pending_approvals(
        self,
        workflow_id: Optional[str] = None
    ) -> List[ApprovalRequest]:
        """获取待处理的审批请求

        Args:
            workflow_id: 工作流 ID，None 返回所有

        Returns:
            审批请求列表
        """
        if workflow_id:
            return [
                req for req in self._pending_approvals.values()
                if req.workflow_id == workflow_id
            ]
        else:
            return list(self._pending_approvals.values())

    async def get_approval_history(
        self,
        workflow_id: Optional[str] = None,
        limit: int = 100
    ) -> List[Dict[str, Any]]:
        """获取审批历史

        Args:
            workflow_id: 工作流 ID，None 返回所有
            limit: 返回数量限制

        Returns:
            审批历史记录列表
        """
        if workflow_id:
            history = await self.database.get_workflow_approvals(workflow_id)
        else:
            history = await self.database.get_all_approvals(limit=limit)

        return history

    async def approve(
        self,
        approval_id: str,
        reason: Optional[str] = None
    ) -> ApprovalResult:
        """批准审批请求

        Args:
            approval_id: 审批请求 ID
            reason: 批准原因

        Returns:
            审批结果

        Raises:
            ValueError: 审批请求不存在
        """
        if approval_id not in self._pending_approvals:
            raise ValueError(f"Approval request not found: {approval_id}")

        request = self._pending_approvals[approval_id]
        result = ApprovalResult(
                approval_id=request.approval_id,
                approved=True,
                comment=reason or "用户批准",
                responded_at=datetime.now(
            )
        )

        await self._save_approval_result(request, result)
        return result

    async def reject(
        self,
        approval_id: str,
        reason: Optional[str] = None
    ) -> ApprovalResult:
        """拒绝审批请求

        Args:
            approval_id: 审批请求 ID
            reason: 拒绝原因

        Returns:
            审批结果

        Raises:
            ValueError: 审批请求不存在
        """
        if approval_id not in self._pending_approvals:
            raise ValueError(f"Approval request not found: {approval_id}")

        request = self._pending_approvals[approval_id]
        result = ApprovalResult(
                approval_id=request.approval_id,
                approved=False,
                comment=reason or "用户拒绝",
                responded_at=datetime.now(
            )
        )

        await self._save_approval_result(request, result)
        return result

    async def get_statistics(
        self,
        workflow_id: Optional[str] = None
    ) -> Dict[str, Any]:
        """获取审批统计信息

        Args:
            workflow_id: 工作流 ID，None 统计所有

        Returns:
            统计信息
        """
        history = await self.get_approval_history(workflow_id)

        total = len(history)
        approved = sum(1 for h in history if h.get("decision") is True)
        rejected = sum(1 for h in history if h.get("decision") is False)
        pending = len(self._pending_approvals)

        return {
            "total": total,
            "approved": approved,
            "rejected": rejected,
            "pending": pending,
            "approval_rate": approved / total if total > 0 else 0
        }
