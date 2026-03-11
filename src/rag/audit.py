"""
RAG 审计日志模块
"""
import json
import logging
from pathlib import Path
from typing import Dict, Any, List, Optional
from datetime import datetime

logger = logging.getLogger(__name__)


class AuditLogger:
    """RAG 审计日志记录器"""

    def __init__(self, enabled: bool = True, log_dir: str = "data/rag/audit"):
        """
        初始化审计日志记录器

        Args:
            enabled: 是否启用审计日志
            log_dir: 日志目录
        """
        self.enabled = enabled
        self.log_dir = Path(log_dir)

        if self.enabled:
            self.log_dir.mkdir(parents=True, exist_ok=True)
            logger.info(f"Audit logging enabled: {self.log_dir}")

    def log_query(
        self,
        query: str,
        results: List[Dict[str, Any]],
        stats: Dict[str, Any],
        metadata: Optional[Dict[str, Any]] = None
    ) -> None:
        """
        记录 RAG 查询

        Args:
            query: 查询文本
            results: 检索结果
            stats: 统计信息
            metadata: 额外元数据
        """
        if not self.enabled:
            return

        try:
            log_entry = {
                "timestamp": datetime.now().isoformat(),
                "type": "query",
                "query": query,
                "result_count": len(results),
                "stats": stats,
                "metadata": metadata or {}
            }

            # 写入日志文件（按日期分文件）
            log_file = self.log_dir / f"queries_{datetime.now().strftime('%Y%m%d')}.jsonl"
            with open(log_file, 'a', encoding='utf-8') as f:
                f.write(json.dumps(log_entry, ensure_ascii=False) + '\n')

        except Exception as e:
            logger.error(f"Failed to log query: {e}")

    def get_recent_queries(self, limit: int = 10) -> List[Dict[str, Any]]:
        """
        获取最近的查询记录

        Args:
            limit: 返回数量

        Returns:
            查询记录列表
        """
        if not self.enabled:
            return []

        try:
            # 读取今天的日志文件
            log_file = self.log_dir / f"queries_{datetime.now().strftime('%Y%m%d')}.jsonl"

            if not log_file.exists():
                return []

            queries = []
            with open(log_file, 'r', encoding='utf-8') as f:
                for line in f:
                    queries.append(json.loads(line))

            return queries[-limit:]

        except Exception as e:
            logger.error(f"Failed to get recent queries: {e}")
            return []
