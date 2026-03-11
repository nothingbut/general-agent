"""
检索工具函数
"""
from typing import List, Dict, Any, Optional
from collections import defaultdict
import logging

from .base import RetrievalResult

logger = logging.getLogger(__name__)


def rerank_by_score(
    results: List[RetrievalResult],
    reverse: bool = True
) -> List[RetrievalResult]:
    """
    按分数重新排序检索结果

    Args:
        results: 检索结果列表
        reverse: True 为降序（默认），False 为升序

    Returns:
        排序后的结果列表
    """
    return sorted(results, key=lambda x: x.score, reverse=reverse)


def filter_by_score(
    results: List[RetrievalResult],
    min_score: float = 0.0,
    max_score: float = 1.0
) -> List[RetrievalResult]:
    """
    按分数过滤检索结果

    Args:
        results: 检索结果列表
        min_score: 最小分数（包含）
        max_score: 最大分数（包含）

    Returns:
        过滤后的结果列表
    """
    return [
        r for r in results
        if min_score <= r.score <= max_score
    ]


def filter_by_metadata(
    results: List[RetrievalResult],
    metadata_filters: Dict[str, Any]
) -> List[RetrievalResult]:
    """
    按元数据过滤检索结果

    Args:
        results: 检索结果列表
        metadata_filters: 元数据过滤条件（key-value 对）

    Returns:
        过滤后的结果列表
    """
    filtered = []

    for result in results:
        match = True
        for key, value in metadata_filters.items():
            if key not in result.metadata or result.metadata[key] != value:
                match = False
                break

        if match:
            filtered.append(result)

    return filtered


def deduplicate_results(
    results: List[RetrievalResult],
    key: str = 'doc_id'
) -> List[RetrievalResult]:
    """
    去重检索结果

    Args:
        results: 检索结果列表
        key: 用于去重的键（'doc_id' 或 'document'）

    Returns:
        去重后的结果列表（保留第一次出现的结果）
    """
    seen = set()
    deduplicated = []

    for result in results:
        if key == 'doc_id':
            unique_key = result.doc_id
        elif key == 'document':
            unique_key = result.document
        else:
            raise ValueError(f"Invalid key: {key}. Must be 'doc_id' or 'document'")

        if unique_key not in seen:
            seen.add(unique_key)
            deduplicated.append(result)

    if len(deduplicated) < len(results):
        logger.debug(f"Deduplicated {len(results)} results to {len(deduplicated)}")

    return deduplicated


def merge_results(
    results_list: List[List[RetrievalResult]],
    strategy: str = 'max'
) -> List[RetrievalResult]:
    """
    合并多个检索结果列表

    Args:
        results_list: 多个检索结果列表
        strategy: 合并策略
            - 'max': 取最大分数
            - 'avg': 取平均分数
            - 'sum': 取分数总和

    Returns:
        合并后的结果列表，按分数降序排列
    """
    # 使用 doc_id 作为键，收集所有出现的分数
    doc_scores = defaultdict(list)
    doc_info = {}  # 存储文档信息

    for results in results_list:
        for result in results:
            doc_id = result.doc_id
            doc_scores[doc_id].append(result.score)

            # 保存文档信息（使用第一次出现的版本）
            if doc_id not in doc_info:
                doc_info[doc_id] = result

    # 根据策略计算最终分数
    merged = []
    for doc_id, scores in doc_scores.items():
        if strategy == 'max':
            final_score = max(scores)
        elif strategy == 'avg':
            final_score = sum(scores) / len(scores)
        elif strategy == 'sum':
            final_score = sum(scores)
        else:
            raise ValueError(
                f"Invalid strategy: {strategy}. "
                "Must be 'max', 'avg', or 'sum'"
            )

        # 创建新的结果（更新分数）
        original = doc_info[doc_id]
        merged.append(RetrievalResult(
            document=original.document,
            score=final_score,
            metadata=original.metadata,
            doc_id=original.doc_id
        ))

    # 按分数降序排序
    merged = sorted(merged, key=lambda x: x.score, reverse=True)

    logger.debug(
        f"Merged {sum(len(r) for r in results_list)} results "
        f"from {len(results_list)} lists into {len(merged)} unique results"
    )

    return merged


def truncate_by_tokens(
    results: List[RetrievalResult],
    max_tokens: int,
    estimate_tokens: Optional[callable] = None
) -> List[RetrievalResult]:
    """
    按 token 数截断检索结果

    Args:
        results: 检索结果列表
        max_tokens: 最大 token 数
        estimate_tokens: Token 估算函数（默认使用字符数 / 2）

    Returns:
        截断后的结果列表
    """
    if estimate_tokens is None:
        # 默认估算：中文约 1 字符 = 2 tokens，英文约 4 字符 = 1 token
        # 这里简化为 2 字符 = 1 token
        estimate_tokens = lambda text: len(text) // 2

    truncated = []
    total_tokens = 0

    for result in results:
        doc_tokens = estimate_tokens(result.document)

        if total_tokens + doc_tokens <= max_tokens:
            truncated.append(result)
            total_tokens += doc_tokens
        else:
            # 如果剩余空间足够，尝试截断当前文档
            remaining_tokens = max_tokens - total_tokens
            if remaining_tokens > 50:  # 至少保留 50 tokens
                # 估算可以保留的字符数
                remaining_chars = remaining_tokens * 2
                truncated_doc = result.document[:remaining_chars] + "..."

                truncated.append(RetrievalResult(
                    document=truncated_doc,
                    score=result.score,
                    metadata=result.metadata,
                    doc_id=result.doc_id
                ))

            break

    if len(truncated) < len(results):
        logger.debug(
            f"Truncated {len(results)} results to {len(truncated)} "
            f"(approx {total_tokens} tokens)"
        )

    return truncated


def group_by_source(
    results: List[RetrievalResult]
) -> Dict[str, List[RetrievalResult]]:
    """
    按来源分组检索结果

    Args:
        results: 检索结果列表

    Returns:
        按来源分组的字典
    """
    grouped = defaultdict(list)

    for result in results:
        source = result.metadata.get('source', 'Unknown')
        grouped[source].append(result)

    return dict(grouped)
