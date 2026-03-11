"""报告生成器"""

import json
from datetime import datetime
from typing import List, Literal, Dict, Any
from .storage import MetricsStorage
from .collector import WorkflowMetrics, TaskMetrics


class ReportGenerator:
    """报告生成器 - 生成 Markdown/JSON 格式的性能报告"""

    def __init__(self, storage: MetricsStorage) -> None:
        """初始化

        Args:
            storage: 指标存储实例
        """
        self.storage = storage

    async def generate_workflow_report(
        self, workflow_id: str, output_format: Literal["markdown", "json"] = "markdown"
    ) -> str:
        """生成单个工作流报告

        Args:
            workflow_id: 工作流 ID
            output_format: 输出格式（"markdown" 或 "json"）

        Returns:
            报告字符串

        Raises:
            RuntimeError: 数据库未初始化
            ValueError: 工作流不存在
        """
        # 检查数据库是否已初始化
        if self.storage.db is None:
            raise RuntimeError(
                "Database not initialized. Call storage.initialize() first."
            )

        # 查询工作流指标（异步）
        cursor = await self.storage.db.execute(
            "SELECT * FROM workflow_metrics WHERE workflow_id = ?", (workflow_id,)
        )
        row = await cursor.fetchone()
        if not row:
            raise ValueError(f"Workflow {workflow_id} not found")

        # 解析工作流指标
        workflow_metrics = WorkflowMetrics(
            workflow_id=row[0],
            total_tasks=row[1],
            completed_tasks=row[2],
            failed_tasks=row[3],
            cancelled_tasks=row[4],
            started_at=datetime.fromisoformat(row[5]),
            completed_at=datetime.fromisoformat(row[6]) if row[6] else None,
            total_duration=row[7],
            throughput=row[8],
            avg_task_duration=row[9],
            p50_task_duration=row[10],
            p95_task_duration=row[11],
            p99_task_duration=row[12],
            peak_memory_mb=row[13],
            avg_cpu_percent=row[14],
            db_query_count=row[15],
            db_total_time=row[16],
            db_avg_query_time=row[17],
        )

        # 查询任务指标
        task_metrics = await self._async_get_task_metrics(workflow_id)

        # 计算派生指标
        derived_metrics = self._calculate_derived_metrics(
            workflow_metrics, task_metrics
        )

        # 生成报告
        if output_format == "markdown":
            return self._generate_markdown_report(
                workflow_metrics, task_metrics, derived_metrics
            )
        else:
            return self._generate_json_report(
                workflow_metrics, task_metrics, derived_metrics
            )

    async def generate_comparison_report(
        self,
        workflow_ids: List[str],
        output_format: Literal["markdown", "json"] = "markdown",
    ) -> str:
        """生成对比报告

        Args:
            workflow_ids: 工作流 ID 列表
            output_format: 输出格式

        Returns:
            对比报告字符串

        Raises:
            RuntimeError: 数据库未初始化
            ValueError: 任一工作流不存在
        """
        # 检查数据库是否已初始化
        if self.storage.db is None:
            raise RuntimeError(
                "Database not initialized. Call storage.initialize() first."
            )

        # 查询所有工作流指标
        workflows_data = []
        for workflow_id in workflow_ids:
            cursor = await self.storage.db.execute(
                "SELECT * FROM workflow_metrics WHERE workflow_id = ?", (workflow_id,)
            )
            row = await cursor.fetchone()
            if not row:
                raise ValueError(f"Workflow {workflow_id} not found")

            # 解析工作流指标
            workflow_metrics = WorkflowMetrics(
                workflow_id=row[0],
                total_tasks=row[1],
                completed_tasks=row[2],
                failed_tasks=row[3],
                cancelled_tasks=row[4],
                started_at=datetime.fromisoformat(row[5]),
                completed_at=datetime.fromisoformat(row[6]) if row[6] else None,
                total_duration=row[7],
                throughput=row[8],
                avg_task_duration=row[9],
                p50_task_duration=row[10],
                p95_task_duration=row[11],
                p99_task_duration=row[12],
                peak_memory_mb=row[13],
                avg_cpu_percent=row[14],
                db_query_count=row[15],
                db_total_time=row[16],
                db_avg_query_time=row[17],
            )

            # 计算成功率
            success_rate = 0.0
            if workflow_metrics.total_tasks > 0:
                success_rate = (
                    workflow_metrics.completed_tasks
                    / workflow_metrics.total_tasks
                    * 100
                )

            workflows_data.append(
                {
                    "metrics": workflow_metrics,
                    "success_rate": success_rate,
                }
            )

        # 生成报告
        if output_format == "markdown":
            return self._generate_comparison_markdown(workflows_data)
        else:
            return self._generate_comparison_json(workflows_data)

    def _generate_comparison_markdown(
        self, workflows_data: List[Dict[str, Any]]
    ) -> str:
        """生成 Markdown 格式的对比报告

        Args:
            workflows_data: 工作流数据列表

        Returns:
            Markdown 格式的对比报告字符串
        """
        lines = []

        # 标题
        lines.append("# 工作流对比报告")
        lines.append("")

        # 对比表格
        lines.append("## 性能对比")
        lines.append("")
        lines.append("| 工作流 ID | 任务数 | 成功率 | 吞吐量 (tasks/s) | P95 延迟 |")
        lines.append("|----------|--------|--------|-----------------|---------|")

        for data in workflows_data:
            metrics = data["metrics"]
            success_rate = data["success_rate"]
            p95_latency_ms = metrics.p95_task_duration * 1000

            lines.append(
                f"| {metrics.workflow_id} | {metrics.total_tasks} | "
                f"{success_rate:.1f}% | {metrics.throughput:.1f} | {p95_latency_ms:.0f}ms |"
            )

        lines.append("")

        # 对比亮点
        lines.append("## 对比亮点")
        lines.append("")

        # 找出最优值
        best_throughput = max(workflows_data, key=lambda x: x["metrics"].throughput)
        best_latency = min(workflows_data, key=lambda x: x["metrics"].p95_task_duration)
        best_success_rate = max(workflows_data, key=lambda x: x["success_rate"])

        lines.append(
            f"- **最高吞吐量**: {best_throughput['metrics'].workflow_id} "
            f"({best_throughput['metrics'].throughput:.1f} tasks/s)"
        )
        lines.append(
            f"- **最低 P95 延迟**: {best_latency['metrics'].workflow_id} "
            f"({best_latency['metrics'].p95_task_duration * 1000:.0f}ms)"
        )
        lines.append(
            f"- **最高成功率**: {best_success_rate['metrics'].workflow_id} "
            f"({best_success_rate['success_rate']:.1f}%)"
        )
        lines.append("")

        return "\n".join(lines)

    def _generate_comparison_json(self, workflows_data: List[Dict[str, Any]]) -> str:
        """生成 JSON 格式的对比报告

        Args:
            workflows_data: 工作流数据列表

        Returns:
            JSON 格式的对比报告字符串
        """
        # 构建对比数据
        comparison_data: Dict[str, Any] = {
            "workflows": [
                {
                    "workflow_id": data["metrics"].workflow_id,
                    "total_tasks": data["metrics"].total_tasks,
                    "success_rate": data["success_rate"],
                    "throughput": data["metrics"].throughput,
                    "p95_latency": data["metrics"].p95_task_duration,
                }
                for data in workflows_data
            ]
        }

        # 计算最优值
        best_throughput = max(workflows_data, key=lambda x: x["metrics"].throughput)
        best_latency = min(workflows_data, key=lambda x: x["metrics"].p95_task_duration)
        best_success_rate = max(workflows_data, key=lambda x: x["success_rate"])

        comparison_data["highlights"] = {
            "best_throughput": {
                "workflow_id": best_throughput["metrics"].workflow_id,
                "value": best_throughput["metrics"].throughput,
            },
            "best_latency": {
                "workflow_id": best_latency["metrics"].workflow_id,
                "value": best_latency["metrics"].p95_task_duration,
            },
            "best_success_rate": {
                "workflow_id": best_success_rate["metrics"].workflow_id,
                "value": best_success_rate["success_rate"],
            },
        }

        return json.dumps(comparison_data, indent=2)

    async def export_metrics(
        self, workflow_id: str, output_format: Literal["json"] = "json"
    ) -> str:
        """导出原始指标

        Args:
            workflow_id: 工作流 ID
            output_format: 输出格式（目前只支持 "json"）

        Returns:
            指标数据字符串

        Raises:
            ValueError: 不支持的格式或工作流不存在
            RuntimeError: 数据库未初始化
        """
        if output_format != "json":
            raise ValueError(f"Unsupported format: {output_format}")

        # 检查数据库是否已初始化
        if self.storage.db is None:
            raise RuntimeError(
                "Database not initialized. Call storage.initialize() first."
            )

        # 查询工作流指标
        cursor = await self.storage.db.execute(
            "SELECT * FROM workflow_metrics WHERE workflow_id = ?", (workflow_id,)
        )
        row = await cursor.fetchone()
        if not row:
            raise ValueError(f"Workflow {workflow_id} not found")

        # 解析工作流指标
        workflow_metrics = WorkflowMetrics(
            workflow_id=row[0],
            total_tasks=row[1],
            completed_tasks=row[2],
            failed_tasks=row[3],
            cancelled_tasks=row[4],
            started_at=datetime.fromisoformat(row[5]),
            completed_at=datetime.fromisoformat(row[6]) if row[6] else None,
            total_duration=row[7],
            throughput=row[8],
            avg_task_duration=row[9],
            p50_task_duration=row[10],
            p95_task_duration=row[11],
            p99_task_duration=row[12],
            peak_memory_mb=row[13],
            avg_cpu_percent=row[14],
            db_query_count=row[15],
            db_total_time=row[16],
            db_avg_query_time=row[17],
        )

        # 查询任务指标
        task_metrics = await self._async_get_task_metrics(workflow_id)

        # 查询追踪数据（简化：返回空列表）
        traces: List[Dict[str, Any]] = []

        # 构建导出数据
        export_data = {
            "workflow_metrics": workflow_metrics.to_dict(),
            "task_metrics": [tm.to_dict() for tm in task_metrics],
            "traces": traces,
        }

        return json.dumps(export_data, indent=2)

    async def _async_get_task_metrics(self, workflow_id: str) -> List[TaskMetrics]:
        """异步查询工作流的所有任务指标

        Args:
            workflow_id: 工作流 ID

        Returns:
            任务指标列表

        Raises:
            RuntimeError: 数据库未初始化
        """
        if self.storage.db is None:
            raise RuntimeError(
                "Database not initialized. Call storage.initialize() first."
            )

        cursor = await self.storage.db.execute(
            "SELECT * FROM task_metrics WHERE workflow_id = ?", (workflow_id,)
        )
        rows = await cursor.fetchall()

        tasks = []
        for row in rows:
            tasks.append(
                TaskMetrics(
                    task_id=row[0],
                    task_name=row[1],
                    tool_name=row[2],
                    workflow_id=row[3],
                    started_at=datetime.fromisoformat(row[4]),
                    completed_at=datetime.fromisoformat(row[5]) if row[5] else None,
                    duration=row[6],
                    status=row[7],
                    retry_count=row[8],
                    memory_used=row[9],
                    cpu_time=row[10],
                )
            )
        return tasks

    def _calculate_derived_metrics(
        self, workflow_metrics: WorkflowMetrics, task_metrics: List[TaskMetrics]
    ) -> Dict[str, Any]:
        """计算派生指标

        Args:
            workflow_metrics: 工作流指标
            task_metrics: 任务指标列表

        Returns:
            派生指标字典
        """
        # 计算成功率
        success_rate = 0.0
        if workflow_metrics.total_tasks > 0:
            success_rate = (
                workflow_metrics.completed_tasks / workflow_metrics.total_tasks * 100
            )

        # 找出慢任务（耗时超过 P95 的任务）
        slow_tasks = [
            task
            for task in task_metrics
            if task.duration > workflow_metrics.p95_task_duration
            and task.status == "completed"
        ]
        slow_tasks.sort(key=lambda t: t.duration, reverse=True)

        # 找出失败任务
        failed_tasks = [task for task in task_metrics if task.status == "failed"]

        # 按工具名称统计
        tool_stats: Dict[str, Dict[str, Any]] = {}
        for task in task_metrics:
            if task.tool_name not in tool_stats:
                tool_stats[task.tool_name] = {
                    "count": 0,
                    "total_duration": 0.0,
                    "failed_count": 0,
                }
            tool_stats[task.tool_name]["count"] += 1
            tool_stats[task.tool_name]["total_duration"] += task.duration
            if task.status == "failed":
                tool_stats[task.tool_name]["failed_count"] += 1

        # 计算每个工具的平均耗时
        for tool_name in tool_stats:
            count = tool_stats[tool_name]["count"]
            if count > 0:
                tool_stats[tool_name]["avg_duration"] = (
                    tool_stats[tool_name]["total_duration"] / count
                )

        return {
            "success_rate": success_rate,
            "slow_tasks": slow_tasks[:10],  # 只取前 10 个最慢的
            "failed_tasks": failed_tasks[:10],  # 只取前 10 个失败任务
            "tool_stats": tool_stats,
        }

    def _generate_markdown_report(
        self,
        workflow_metrics: WorkflowMetrics,
        task_metrics: List[TaskMetrics],
        derived_metrics: Dict[str, Any],
    ) -> str:
        """生成 Markdown 格式报告

        Args:
            workflow_metrics: 工作流指标
            task_metrics: 任务指标列表
            derived_metrics: 派生指标

        Returns:
            Markdown 格式的报告字符串
        """
        lines = []

        # 标题
        lines.append("# 工作流性能报告")
        lines.append("")
        lines.append(f"**工作流 ID**: {workflow_metrics.workflow_id}")
        lines.append(
            f"**执行时间**: {workflow_metrics.started_at.strftime('%Y-%m-%d %H:%M:%S')}"
        )
        if workflow_metrics.completed_at:
            lines.append(
                f"**完成时间**: {workflow_metrics.completed_at.strftime('%Y-%m-%d %H:%M:%S')}"
            )
        lines.append("")

        # 概览
        lines.append("## 概览")
        lines.append("")
        lines.append(f"- **总任务数**: {workflow_metrics.total_tasks}")
        lines.append(f"- **已完成**: {workflow_metrics.completed_tasks}")
        lines.append(f"- **失败**: {workflow_metrics.failed_tasks}")
        lines.append(f"- **取消**: {workflow_metrics.cancelled_tasks}")
        lines.append(f"- **成功率**: {derived_metrics['success_rate']:.1f}%")
        lines.append(f"- **总耗时**: {workflow_metrics.total_duration:.2f}s")
        lines.append(f"- **吞吐量**: {workflow_metrics.throughput:.1f} tasks/s")
        lines.append("")

        # 性能指标
        lines.append("## 性能指标")
        lines.append("")
        lines.append(
            f"- **平均任务耗时**: {workflow_metrics.avg_task_duration * 1000:.0f}ms"
        )
        lines.append(
            f"- **P50 延迟**: {workflow_metrics.p50_task_duration * 1000:.0f}ms"
        )
        lines.append(
            f"- **P95 延迟**: {workflow_metrics.p95_task_duration * 1000:.0f}ms"
        )
        lines.append(
            f"- **P99 延迟**: {workflow_metrics.p99_task_duration * 1000:.0f}ms"
        )
        lines.append(f"- **峰值内存**: {workflow_metrics.peak_memory_mb:.1f} MB")
        lines.append(f"- **平均 CPU**: {workflow_metrics.avg_cpu_percent:.1f}%")
        lines.append("")

        # 慢任务
        if derived_metrics["slow_tasks"]:
            lines.append("## 慢任务 (Top 10)")
            lines.append("")
            lines.append("| 任务名称 | 工具 | 耗时 | 重试次数 |")
            lines.append("|---------|------|------|---------|")
            for task in derived_metrics["slow_tasks"]:
                lines.append(
                    f"| {task.task_name} | {task.tool_name} | "
                    f"{task.duration:.2f}s | {task.retry_count} |"
                )
            lines.append("")

        # 失败任务
        if derived_metrics["failed_tasks"]:
            lines.append("## 失败任务")
            lines.append("")
            lines.append("| 任务名称 | 工具 | 耗时 | 重试次数 |")
            lines.append("|---------|------|------|---------|")
            for task in derived_metrics["failed_tasks"]:
                lines.append(
                    f"| {task.task_name} | {task.tool_name} | "
                    f"{task.duration:.2f}s | {task.retry_count} |"
                )
            lines.append("")

        # 工具统计
        if derived_metrics["tool_stats"]:
            lines.append("## 工具统计")
            lines.append("")
            lines.append("| 工具名称 | 调用次数 | 平均耗时 | 失败次数 |")
            lines.append("|---------|---------|---------|---------|")
            for tool_name, stats in derived_metrics["tool_stats"].items():
                avg_duration = stats.get("avg_duration", 0.0)
                lines.append(
                    f"| {tool_name} | {stats['count']} | "
                    f"{avg_duration * 1000:.0f}ms | {stats['failed_count']} |"
                )
            lines.append("")

        return "\n".join(lines)

    def _generate_json_report(
        self,
        workflow_metrics: WorkflowMetrics,
        task_metrics: List[TaskMetrics],
        derived_metrics: Dict[str, Any],
    ) -> str:
        """生成 JSON 格式报告

        Args:
            workflow_metrics: 工作流指标
            task_metrics: 任务指标列表
            derived_metrics: 派生指标

        Returns:
            JSON 格式的报告字符串
        """
        report_data = {
            "workflow_id": workflow_metrics.workflow_id,
            "execution": {
                "started_at": workflow_metrics.started_at.isoformat(),
                "completed_at": (
                    workflow_metrics.completed_at.isoformat()
                    if workflow_metrics.completed_at
                    else None
                ),
                "total_duration": workflow_metrics.total_duration,
            },
            "summary": {
                "total_tasks": workflow_metrics.total_tasks,
                "completed_tasks": workflow_metrics.completed_tasks,
                "failed_tasks": workflow_metrics.failed_tasks,
                "cancelled_tasks": workflow_metrics.cancelled_tasks,
                "success_rate": derived_metrics["success_rate"],
            },
            "performance": {
                "throughput": workflow_metrics.throughput,
                "avg_latency": workflow_metrics.avg_task_duration,
                "p50_latency": workflow_metrics.p50_task_duration,
                "p95_latency": workflow_metrics.p95_task_duration,
                "p99_latency": workflow_metrics.p99_task_duration,
            },
            "resources": {
                "peak_memory_mb": workflow_metrics.peak_memory_mb,
                "avg_cpu_percent": workflow_metrics.avg_cpu_percent,
            },
            "slow_tasks": [
                {
                    "task_id": t.task_id,
                    "task_name": t.task_name,
                    "tool_name": t.tool_name,
                    "duration": t.duration,
                    "retry_count": t.retry_count,
                }
                for t in derived_metrics["slow_tasks"]
            ],
            "failed_tasks": [
                {
                    "task_id": t.task_id,
                    "task_name": t.task_name,
                    "tool_name": t.tool_name,
                    "duration": t.duration,
                    "status": t.status,
                    "retry_count": t.retry_count,
                }
                for t in derived_metrics["failed_tasks"]
            ],
            "tool_stats": derived_metrics["tool_stats"],
        }

        return json.dumps(report_data, indent=2)
