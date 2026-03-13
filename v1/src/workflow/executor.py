"""工作流执行引擎 - 调度和执行任务"""
import asyncio
import logging
import uuid
import random
from datetime import datetime
from typing import Dict, Any, List, Optional, Set

from .models import (
    Workflow,
    Task,
    TaskStatus,
    WorkflowStatus,
    ExecutionContext
)
from .orchestrator import ToolOrchestrator

logger = logging.getLogger(__name__)


class ExecutionError(Exception):
    """执行错误"""
    pass


class WorkflowExecutor:
    """工作流执行器 - 调度和执行工作流中的任务"""

    def __init__(
        self,
        orchestrator: ToolOrchestrator,
        database,
        max_parallel: int = 5,
        enable_retry: bool = True,
        base_backoff: float = 1.0,
        max_backoff: float = 60.0
    ):
        """初始化执行器

        Args:
            orchestrator: 工具编排器
            database: 数据库实例（用于持久化状态）
            max_parallel: 最大并行任务数
            enable_retry: 是否启用任务重试
            base_backoff: 基础退避时间（秒）
            max_backoff: 最大退避时间（秒）
        """
        self.orchestrator = orchestrator
        self.database = database
        self.max_parallel = max_parallel
        self.enable_retry = enable_retry
        self.base_backoff = base_backoff
        self.max_backoff = max_backoff
        self._paused = False
        self._stop_requested = False

    def pause(self) -> None:
        """暂停工作流执行"""
        logger.info("Pausing workflow execution")
        self._paused = True

    def resume(self) -> None:
        """恢复工作流执行"""
        logger.info("Resuming workflow execution")
        self._paused = False

    def stop(self) -> None:
        """停止工作流执行"""
        logger.info("Stopping workflow execution")
        self._stop_requested = True

    def _calculate_backoff(self, retry_count: int) -> float:
        """计算退避时间（指数退避 + 抖动）

        Args:
            retry_count: 当前重试次数

        Returns:
            退避时间（秒）
        """
        # 指数退避: base_backoff * 2^(retry_count - 1)
        backoff = self.base_backoff * (2 ** (retry_count - 1))
        # 限制最大值
        backoff = min(backoff, self.max_backoff)
        # 添加随机抖动 (±25%)
        jitter = backoff * 0.25 * (2 * random.random() - 1)
        return max(0.1, backoff + jitter)

    async def execute(
        self,
        workflow: Workflow,
        on_task_complete: Optional[callable] = None,
        on_approval_required: Optional[callable] = None
    ) -> Dict[str, Any]:
        """执行工作流

        Args:
            workflow: 要执行的工作流
            on_task_complete: 任务完成回调（可选）
            on_approval_required: 需要审批回调（可选）

        Returns:
            执行结果摘要

        Raises:
            ExecutionError: 执行失败
        """
        try:
            logger.info(f"Starting workflow execution: {workflow.id}")

            # 创建执行上下文
            context = ExecutionContext(
                workflow_id=workflow.id,
                session_id=workflow.session_id
            )

            # 更新工作流状态为 running
            workflow.status = WorkflowStatus.RUNNING
            workflow.started_at = datetime.now()
            await self._save_workflow_state(workflow)

            # 获取任务执行顺序（拓扑排序）
            execution_order = self._topological_sort(workflow)

            # 执行任务
            completed_tasks: Set[str] = set()
            failed_tasks: List[str] = []

            for task_batch in execution_order:
                # 检查是否请求停止
                if self._stop_requested:
                    logger.info("Stop requested, cancelling workflow")
                    workflow.status = WorkflowStatus.CANCELLED
                    break

                # 处理暂停
                while self._paused and not self._stop_requested:
                    logger.debug("Workflow paused, waiting...")
                    await asyncio.sleep(0.5)

                # 并行执行当前批次的任务
                batch_results = await self._execute_batch(
                    tasks=task_batch,
                    workflow=workflow,
                    context=context,
                    on_task_complete=on_task_complete,
                    on_approval_required=on_approval_required
                )

                # 处理结果
                for task_id, success in batch_results.items():
                    if success:
                        completed_tasks.add(task_id)
                    else:
                        failed_tasks.append(task_id)

                # 如果有任务失败，停止执行
                if failed_tasks:
                    logger.warning(f"Tasks failed: {failed_tasks}")
                    break

            # 更新最终状态
            # 计算所有已完成的任务（包括之前已完成的）
            all_completed_tasks = {
                t.id for t in workflow.tasks
                if t.status == TaskStatus.SUCCESS
            }
            all_completed_tasks.update(completed_tasks)

            # 计算取消的任务
            cancelled_tasks = [
                t.id for t in workflow.tasks
                if t.status == TaskStatus.CANCELLED
            ]

            if failed_tasks:
                workflow.status = WorkflowStatus.FAILED
            elif len(all_completed_tasks) == len(workflow.tasks):
                workflow.status = WorkflowStatus.COMPLETED
            elif cancelled_tasks and not failed_tasks:
                # 只有取消没有失败
                workflow.status = WorkflowStatus.CANCELLED
            else:
                workflow.status = WorkflowStatus.CANCELLED

            workflow.completed_at = datetime.now()
            await self._save_workflow_state(workflow)

            # 构建结果摘要
            result = {
                "workflow_id": workflow.id,
                "status": workflow.status.value,
                "completed_tasks": list(completed_tasks),
                "failed_tasks": failed_tasks,
                "total_tasks": len(workflow.tasks),
                "execution_time": (
                    workflow.completed_at - workflow.started_at
                ).total_seconds() if workflow.started_at and workflow.completed_at else 0
            }

            logger.info(f"Workflow execution complete: {result}")
            return result

        except Exception as e:
            logger.error(f"Workflow execution failed: {e}")
            workflow.status = WorkflowStatus.FAILED
            workflow.completed_at = datetime.now()
            await self._save_workflow_state(workflow)
            raise ExecutionError(f"Workflow execution failed: {e}") from e

    def _topological_sort(self, workflow: Workflow) -> List[List[Task]]:
        """拓扑排序 - 确定任务执行顺序

        Args:
            workflow: 工作流

        Returns:
            任务批次列表，每个批次中的任务可以并行执行
        """
        # 构建依赖图（只包含未完成的任务）
        in_degree: Dict[str, int] = {}
        graph: Dict[str, List[str]] = {}
        pending_tasks = [t for t in workflow.tasks if t.status != TaskStatus.SUCCESS]

        # 初始化
        for task in pending_tasks:
            # 计算未完成的依赖数量
            pending_deps = [
                dep_id for dep_id in task.dependencies
                if any(t.id == dep_id and t.status != TaskStatus.SUCCESS for t in workflow.tasks)
            ]
            in_degree[task.id] = len(pending_deps)
            graph[task.id] = []

        # 构建反向图（从依赖者指向被依赖者）
        for task in pending_tasks:
            for dep_id in task.dependencies:
                if dep_id in graph:  # 只考虑未完成的依赖
                    graph[dep_id].append(task.id)

        # 使用 BFS 进行分层
        result: List[List[Task]] = []
        task_map = {task.id: task for task in pending_tasks}

        while any(degree == 0 for degree in in_degree.values()):
            # 找出所有入度为 0 的任务（可以并行执行）
            current_batch = [
                task_map[task_id]
                for task_id, degree in in_degree.items()
                if degree == 0
            ]

            if not current_batch:
                break

            result.append(current_batch)

            # 更新入度
            for task in current_batch:
                in_degree[task.id] = -1  # 标记为已处理
                for next_task_id in graph[task.id]:
                    in_degree[next_task_id] -= 1

        return result

    async def _execute_batch(
        self,
        tasks: List[Task],
        workflow: Workflow,
        context: ExecutionContext,
        on_task_complete: Optional[callable] = None,
        on_approval_required: Optional[callable] = None
    ) -> Dict[str, bool]:
        """并行执行一批任务

        Args:
            tasks: 任务列表
            workflow: 工作流
            context: 执行上下文
            on_task_complete: 任务完成回调
            on_approval_required: 需要审批回调

        Returns:
            任务ID -> 是否成功的映射
        """
        # 限制并发数
        semaphore = asyncio.Semaphore(self.max_parallel)

        async def execute_with_semaphore(task: Task):
            async with semaphore:
                return await self._execute_task(
                    task=task,
                    workflow=workflow,
                    context=context,
                    on_task_complete=on_task_complete,
                    on_approval_required=on_approval_required
                )

        # 并行执行
        results = await asyncio.gather(
            *[execute_with_semaphore(task) for task in tasks],
            return_exceptions=True
        )

        # 处理结果
        result_map = {}
        for task, result in zip(tasks, results):
            if isinstance(result, Exception):
                logger.error(f"Task {task.id} raised exception: {result}")
                result_map[task.id] = False
            else:
                result_map[task.id] = result

        return result_map

    async def _execute_task(
        self,
        task: Task,
        workflow: Workflow,
        context: ExecutionContext,
        on_task_complete: Optional[callable] = None,
        on_approval_required: Optional[callable] = None
    ) -> bool:
        """执行单个任务

        Args:
            task: 要执行的任务
            workflow: 工作流
            context: 执行上下文
            on_task_complete: 任务完成回调
            on_approval_required: 需要审批回调

        Returns:
            是否执行成功
        """
        try:
            logger.info(f"Executing task: {task.id} - {task.name}")

            # 检查是否需要审批
            if task.requires_approval and on_approval_required:
                approved = await on_approval_required(task, workflow)
                if not approved:
                    logger.info(f"Task {task.id} rejected by user")
                    task.status = TaskStatus.CANCELLED
                    await self._save_task_state(task, workflow)
                    return False

            # 标记为运行中
            task.mark_running()
            await self._save_task_state(task, workflow, status="running")

            # 解析参数（处理变量引用）
            resolved_params = context.resolve_params(task.params)

            # 执行工具
            retry_count = 0
            max_retries = task.max_retries if self.enable_retry else 0

            while retry_count <= max_retries:
                try:
                    tool_result = await self.orchestrator.execute_tool(
                        tool_name=task.tool,
                        params=resolved_params,
                        context=context,
                        session_id=workflow.session_id
                    )

                    if tool_result.success:
                        # 执行成功
                        task.mark_success(tool_result.data)
                        context.set_result(task.id, tool_result.data)
                        await self._save_task_state(task, workflow, status="success", result=tool_result.data)

                        # 调用完成回调
                        if on_task_complete:
                            await on_task_complete(task, tool_result)

                        logger.info(f"Task {task.id} completed successfully")
                        return True

                    else:
                        # 工具执行失败
                        raise ExecutionError(tool_result.error or "Tool execution failed")

                except Exception as e:
                    retry_count += 1
                    task.retry_count = retry_count

                    if retry_count > max_retries:
                        # 达到最大重试次数
                        error_msg = f"Task failed after {max_retries} retries: {e}"
                        logger.error(f"Task {task.id}: {error_msg}")
                        task.mark_failed(error_msg)
                        await self._save_task_state(task, workflow, status="failed", error=error_msg)
                        return False
                    else:
                        # 重试 - 使用指数退避 + 抖动
                        backoff_time = self._calculate_backoff(retry_count)
                        logger.warning(
                            f"Task {task.id} failed, retrying ({retry_count}/{max_retries}) "
                            f"after {backoff_time:.2f}s: {e}"
                        )
                        await asyncio.sleep(backoff_time)

        except Exception as e:
            logger.error(f"Task {task.id} execution error: {e}")
            task.mark_failed(str(e))
            await self._save_task_state(task, workflow, status="failed", error=str(e))
            return False

        return False

    async def _save_workflow_state(self, workflow: Workflow) -> None:
        """保存工作流状态到数据库

        Args:
            workflow: 工作流对象
        """
        try:
            # 检查工作流是否已存在
            existing = await self.database.get_workflow(workflow.id)

            if existing:
                # 更新状态
                await self.database.update_workflow_status(
                    workflow_id=workflow.id,
                    status=workflow.status.value,
                    current_task_id=workflow.current_task_id,
                    completed_at=workflow.completed_at
                )
            else:
                # 创建新工作流
                await self.database.create_workflow(
                    workflow_id=workflow.id,
                    session_id=workflow.session_id,
                    goal=workflow.goal,
                    plan=workflow.get_plan_dict(),
                    status=workflow.status.value,
                    created_at=workflow.created_at or datetime.now(),
                    current_task_id=workflow.current_task_id
                )

            logger.debug(f"Workflow state saved: {workflow.id}")

        except Exception as e:
            logger.error(f"Failed to save workflow state: {e}")
            # 不抛出异常，避免中断执行

    async def _save_task_state(
        self,
        task: Task,
        workflow: Workflow,
        status: Optional[str] = None,
        result: Optional[Any] = None,
        error: Optional[str] = None
    ) -> None:
        """保存任务执行状态到数据库

        Args:
            task: 任务对象
            workflow: 工作流对象
            status: 状态（可选，默认使用 task.status）
            result: 执行结果（可选）
            error: 错误信息（可选）
        """
        try:
            execution_id = str(uuid.uuid4())

            await self.database.create_task_execution(
                execution_id=execution_id,
                workflow_id=workflow.id,
                task_id=task.id,
                task_name=task.name,
                tool_name=task.tool,
                params=task.params,
                status=status or task.status.value,
                started_at=datetime.now(),
                result=result,
                error=error,
                retry_count=task.retry_count
            )

            logger.debug(f"Task state saved: {task.id}")

        except Exception as e:
            logger.error(f"Failed to save task state: {e}")
            # 不抛出异常，避免中断执行

    async def get_execution_status(self, workflow_id: str) -> Dict[str, Any]:
        """获取工作流执行状态

        Args:
            workflow_id: 工作流ID

        Returns:
            状态信息字典
        """
        workflow_data = await self.database.get_workflow(workflow_id)
        if not workflow_data:
            raise ValueError(f"Workflow not found: {workflow_id}")

        executions = await self.database.get_workflow_executions(workflow_id)

        return {
            "workflow_id": workflow_id,
            "status": workflow_data["status"],
            "goal": workflow_data["goal"],
            "created_at": workflow_data["created_at"],
            "completed_at": workflow_data["completed_at"],
            "task_executions": executions
        }

    async def resume_workflow(
        self,
        workflow_id: str,
        on_task_complete: Optional[callable] = None,
        on_approval_required: Optional[callable] = None
    ) -> Dict[str, Any]:
        """从断点恢复工作流执行

        Args:
            workflow_id: 要恢复的工作流ID
            on_task_complete: 任务完成回调（可选）
            on_approval_required: 需要审批回调（可选）

        Returns:
            执行结果摘要

        Raises:
            ValueError: 工作流不存在或已完成
            ExecutionError: 执行失败
        """
        logger.info(f"Resuming workflow: {workflow_id}")

        # 从数据库加载工作流状态
        workflow_data = await self.database.get_workflow(workflow_id)
        if not workflow_data:
            raise ValueError(f"Workflow not found: {workflow_id}")

        # 检查状态
        if workflow_data["status"] in [WorkflowStatus.COMPLETED.value, WorkflowStatus.CANCELLED.value]:
            raise ValueError(f"Cannot resume {workflow_data['status']} workflow")

        # 重建 Workflow 对象
        from .models import Workflow
        workflow = Workflow(
            id=workflow_data["id"],
            session_id=workflow_data["session_id"],
            goal=workflow_data["goal"],
            tasks=[]  # 从 plan 中恢复
        )
        workflow.status = WorkflowStatus(workflow_data["status"])
        workflow.created_at = workflow_data["created_at"]
        workflow.started_at = workflow_data["started_at"]
        workflow.current_task_id = workflow_data.get("current_task_id")

        # 从 plan 恢复任务列表
        plan = workflow_data.get("plan", {})
        if "tasks" in plan:
            for task_data in plan["tasks"]:
                from .models import Task, TaskStatus
                task = Task(
                    id=task_data["id"],
                    name=task_data["name"],
                    tool=task_data["tool"],
                    params=task_data.get("params", {}),
                    dependencies=task_data.get("dependencies", []),
                    requires_approval=task_data.get("requires_approval", False),
                    max_retries=task_data.get("max_retries", 3)
                )
                workflow.tasks.append(task)

        # 加载已完成的任务执行记录
        executions = await self.database.get_workflow_executions(workflow_id)
        completed_task_ids = {
            exec["task_id"]
            for exec in executions
            if exec["status"] == TaskStatus.SUCCESS.value
        }

        # 标记已完成的任务
        for task in workflow.tasks:
            if task.id in completed_task_ids:
                task.status = TaskStatus.SUCCESS
                logger.info(f"Task {task.id} already completed, skipping")

        # 恢复执行上下文
        context = ExecutionContext(
            workflow_id=workflow.id,
            session_id=workflow.session_id
        )

        # 从成功的任务恢复结果到上下文
        for exec in executions:
            if exec["status"] == TaskStatus.SUCCESS.value and exec.get("result"):
                context.set_result(exec["task_id"], exec["result"])

        # 重置状态标志
        self._paused = False
        self._stop_requested = False

        # 继续执行（只执行未完成的任务）
        logger.info(f"Resuming from checkpoint, {len(completed_task_ids)} tasks already completed")
        return await self.execute(
            workflow=workflow,
            on_task_complete=on_task_complete,
            on_approval_required=on_approval_required
        )
