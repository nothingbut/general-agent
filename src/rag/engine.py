"""
RAG 引擎核心实现
"""
import hashlib
import logging
from pathlib import Path
from typing import List, Dict, Any, Optional
from tqdm import tqdm

from .config import RAGConfig
from .embeddings import create_embedding_provider, EmbeddingProvider
from .storage import create_vector_store, VectorStore
from .chunking import create_chunker, Chunker
from .loaders import create_loader
from .retrieval import SemanticRetriever, RetrievalResult
from .retrieval import utils as retrieval_utils
from .audit import AuditLogger
from .exceptions import IndexingError, RetrievalError

logger = logging.getLogger(__name__)


class RAGEngine:
    """
    RAG 引擎

    整合文档加载、分块、嵌入和向量存储功能
    """

    def __init__(self, config: Optional[RAGConfig] = None):
        """
        初始化 RAG 引擎

        Args:
            config: RAG 配置，如果为 None 则从默认路径加载

        Raises:
            ConfigError: 配置加载失败
        """
        # 加载配置
        if config is None:
            config = RAGConfig.load()

        self.config = config

        # 确保目录存在
        config.ensure_directories()

        # 初始化组件
        self.embedding = create_embedding_provider(config.embedding)
        self.vector_store = create_vector_store(
            config.vector_store,
            dimension=config.embedding.dimension
        )
        self.chunker = create_chunker(config.chunking)

        # 初始化检索器
        self.retriever = SemanticRetriever(
            embedding=self.embedding,
            vector_store=self.vector_store,
            similarity_threshold=config.retrieval.similarity_threshold
        )

        # 初始化审计日志
        self.audit_logger = AuditLogger(
            enabled=config.audit.enabled,
            log_dir="data/rag/audit"
        )

        logger.info(f"RAG Engine initialized with {config.vector_store.type} vector store")

    async def index_documents(
        self,
        path: str,
        recursive: bool | None = None,
        show_progress: bool | None = None
    ) -> Dict[str, Any]:
        """
        索引文档目录

        Args:
            path: 文档目录路径
            recursive: 是否递归处理子目录（默认使用配置）
            show_progress: 是否显示进度条（默认使用配置）

        Returns:
            索引统计信息:
                - total_files: 文件总数
                - total_chunks: 块总数
                - indexed_files: 成功索引的文件数
                - failed_files: 失败的文件列表
                - skipped_files: 跳过的文件数（已存在且未修改）

        Raises:
            IndexingError: 索引失败
        """
        try:
            # 使用配置默认值
            if recursive is None:
                recursive = self.config.indexing.recursive
            if show_progress is None:
                show_progress = self.config.indexing.show_progress

            # 扫描文件
            files = self._scan_files(path, recursive)

            if not files:
                logger.warning(f"No supported files found in {path}")
                return {
                    "total_files": 0,
                    "total_chunks": 0,
                    "indexed_files": 0,
                    "failed_files": [],
                    "skipped_files": 0
                }

            # 统计信息
            stats = {
                "total_files": len(files),
                "total_chunks": 0,
                "indexed_files": 0,
                "failed_files": [],
                "skipped_files": 0
            }

            # 批量处理
            batch_ids = []
            batch_embeddings = []
            batch_documents = []
            batch_metadatas = []

            # 进度条
            iterator = tqdm(files, desc="Indexing documents", disable=not show_progress)

            for file_path in iterator:
                try:
                    # 检查文件是否已索引且未修改
                    file_hash = self._compute_file_hash(file_path)
                    doc_id_prefix = self._get_doc_id_prefix(file_path)

                    # 检查是否需要更新
                    existing_doc = await self._get_document_by_prefix(doc_id_prefix)
                    if existing_doc and existing_doc.get('metadata', {}).get('file_hash') == file_hash:
                        stats["skipped_files"] += 1
                        continue

                    # 如果文件已索引但已修改，先删除旧版本
                    if existing_doc:
                        await self.delete_document(str(file_path))

                    # 加载文档
                    loader = create_loader(file_path)
                    document = await loader.load(file_path)

                    # 分块
                    chunks = await self.chunker.chunk(document.content, document.metadata)

                    # 为每个块准备数据
                    for chunk_idx, chunk in enumerate(chunks):
                        # 生成唯一 ID
                        doc_id = f"{doc_id_prefix}#chunk{chunk_idx}"

                        # 添加到批次
                        batch_ids.append(doc_id)
                        batch_documents.append(chunk.text)

                        # 准备元数据
                        metadata = {
                            "source": str(file_path),
                            "chunk_index": chunk_idx,
                            "total_chunks": len(chunks),
                            "file_hash": file_hash,
                            **chunk.metadata
                        }
                        batch_metadatas.append(metadata)

                        # 如果达到批次大小，处理批次
                        if len(batch_ids) >= self.config.indexing.batch_size:
                            await self._process_batch(
                                batch_ids, batch_documents, batch_metadatas
                            )
                            stats["total_chunks"] += len(batch_ids)
                            batch_ids = []
                            batch_documents = []
                            batch_metadatas = []
                            batch_embeddings = []

                    stats["indexed_files"] += 1

                except Exception as e:
                    logger.error(f"Failed to index {file_path}: {e}")
                    stats["failed_files"].append({
                        "file": str(file_path),
                        "error": str(e)
                    })

            # 处理剩余批次
            if batch_ids:
                await self._process_batch(
                    batch_ids, batch_documents, batch_metadatas
                )
                stats["total_chunks"] += len(batch_ids)

            logger.info(
                f"Indexing complete: {stats['indexed_files']}/{stats['total_files']} files, "
                f"{stats['total_chunks']} chunks"
            )

            return stats

        except Exception as e:
            raise IndexingError(f"Failed to index documents: {e}")

    async def update_document(self, file_path: str) -> None:
        """
        更新单个文档

        Args:
            file_path: 文档文件路径

        Raises:
            IndexingError: 更新失败
        """
        try:
            # 删除旧版本
            await self.delete_document(file_path)

            # 重新索引
            path_obj = Path(file_path)
            await self.index_documents(
                str(path_obj.parent),
                recursive=False,
                show_progress=False
            )

        except Exception as e:
            raise IndexingError(f"Failed to update document {file_path}: {e}")

    async def delete_document(self, file_path: str) -> None:
        """
        删除文档

        Args:
            file_path: 文档文件路径

        Raises:
            IndexingError: 删除失败
        """
        try:
            # 获取文档ID前缀
            doc_id_prefix = self._get_doc_id_prefix(Path(file_path))

            # 查找所有相关的块
            # 注意：ChromaDB 不支持前缀查询，需要获取所有文档并过滤
            # 这里我们使用元数据过滤
            results = await self.vector_store.search(
                query_embedding=await self.embedding.embed_query(""),  # 占位查询
                top_k=10000,  # 获取所有结果
                where={"source": str(file_path)}
            )

            if results:
                # 删除所有找到的块
                ids_to_delete = [r['id'] for r in results]
                await self.vector_store.delete(ids_to_delete)
                logger.info(f"Deleted {len(ids_to_delete)} chunks from {file_path}")

        except Exception as e:
            raise IndexingError(f"Failed to delete document {file_path}: {e}")

    async def clear_index(self) -> None:
        """
        清空索引

        Raises:
            IndexingError: 清空失败
        """
        try:
            await self.vector_store.clear()
            logger.info("Index cleared")
        except Exception as e:
            raise IndexingError(f"Failed to clear index: {e}")

    async def get_stats(self) -> Dict[str, Any]:
        """
        获取索引统计信息

        Returns:
            统计信息字典

        Raises:
            IndexingError: 获取失败
        """
        try:
            return await self.vector_store.get_stats()
        except Exception as e:
            raise IndexingError(f"Failed to get stats: {e}")

    def _scan_files(self, path: str, recursive: bool) -> List[Path]:
        """
        扫描目录中的文件

        Args:
            path: 目录路径
            recursive: 是否递归

        Returns:
            文件路径列表
        """
        path_obj = Path(path)

        if not path_obj.exists():
            raise IndexingError(f"Path does not exist: {path}")

        if path_obj.is_file():
            # 如果是单个文件
            if self._is_supported_file(path_obj):
                return [path_obj]
            else:
                logger.warning(f"Unsupported file type: {path_obj}")
                return []

        # 如果是目录
        files = []
        pattern = "**/*" if recursive else "*"

        for ext in self.config.indexing.supported_extensions:
            files.extend(path_obj.glob(f"{pattern}{ext}"))

        return sorted(files)

    def _is_supported_file(self, path: Path) -> bool:
        """检查文件是否支持"""
        return path.suffix in self.config.indexing.supported_extensions

    def _get_doc_id_prefix(self, path: Path) -> str:
        """
        生成文档 ID 前缀

        Args:
            path: 文件路径

        Returns:
            文档 ID 前缀（基于文件路径的哈希）
        """
        # 使用文件路径的哈希作为前缀
        path_str = str(path.absolute())
        hash_obj = hashlib.md5(path_str.encode())
        return hash_obj.hexdigest()[:16]

    def _compute_file_hash(self, path: Path) -> str:
        """
        计算文件哈希

        Args:
            path: 文件路径

        Returns:
            文件内容的 MD5 哈希
        """
        hash_obj = hashlib.md5()
        with open(path, 'rb') as f:
            for chunk in iter(lambda: f.read(4096), b""):
                hash_obj.update(chunk)
        return hash_obj.hexdigest()

    async def _get_document_by_prefix(self, doc_id_prefix: str) -> Optional[Dict[str, Any]]:
        """
        根据文档 ID 前缀查找文档

        Args:
            doc_id_prefix: 文档 ID 前缀

        Returns:
            文档信息（如果存在）
        """
        try:
            # 尝试获取第一个块
            first_chunk_id = f"{doc_id_prefix}#chunk0"
            results = await self.vector_store.get([first_chunk_id])

            return results[0] if results else None
        except Exception:
            return None

    async def _process_batch(
        self,
        ids: List[str],
        documents: List[str],
        metadatas: List[Dict[str, Any]]
    ) -> None:
        """
        处理一批文档

        Args:
            ids: 文档 ID 列表
            documents: 文档文本列表
            metadatas: 元数据列表
        """
        # 生成嵌入
        embeddings = await self.embedding.embed_documents(documents)

        # 存储到向量库
        await self.vector_store.add(
            ids=ids,
            embeddings=embeddings,
            documents=documents,
            metadatas=metadatas
        )

    async def retrieve(
        self,
        query: str,
        top_k: Optional[int] = None,
        filters: Optional[Dict[str, Any]] = None,
        rerank: bool = True
    ) -> List[RetrievalResult]:
        """
        检索相关文档

        Args:
            query: 查询文本
            top_k: 返回结果数量（默认使用配置中的值）
            filters: 元数据过滤条件（可选）
            rerank: 是否重新排序（默认 True）

        Returns:
            检索结果列表

        Raises:
            RetrievalError: 检索失败
        """
        try:
            # 使用配置默认值
            if top_k is None:
                top_k = self.config.retrieval.top_k

            # 执行检索
            results = await self.retriever.retrieve(
                query=query,
                top_k=top_k,
                filters=filters
            )

            # 重新排序（如果需要）
            if rerank and len(results) > 1:
                results = retrieval_utils.rerank_by_score(results)

            logger.info(f"Retrieved {len(results)} documents for query: {query[:50]}...")

            return results

        except Exception as e:
            raise RetrievalError(f"Failed to retrieve documents: {e}")

    async def query(
        self,
        query: str,
        top_k: Optional[int] = None,
        filters: Optional[Dict[str, Any]] = None,
        max_context_tokens: Optional[int] = None,
        include_metadata: bool = False
    ) -> Dict[str, Any]:
        """
        完整的 RAG 查询（检索 + 格式化上下文）

        Args:
            query: 查询文本
            top_k: 返回结果数量（默认使用配置中的值）
            filters: 元数据过滤条件（可选）
            max_context_tokens: 最大上下文 tokens（默认使用配置中的值）
            include_metadata: 是否在上下文中包含元数据

        Returns:
            查询结果字典:
                - query: 查询文本
                - results: 检索结果列表
                - context: 格式化的上下文文本
                - stats: 统计信息

        Raises:
            RetrievalError: 查询失败
        """
        try:
            # 使用配置默认值
            if max_context_tokens is None:
                max_context_tokens = self.config.retrieval.max_context_tokens

            # 1. 检索
            results = await self.retrieve(
                query=query,
                top_k=top_k,
                filters=filters
            )

            # 2. 截断结果（如果需要）
            if max_context_tokens:
                results = retrieval_utils.truncate_by_tokens(
                    results,
                    max_tokens=max_context_tokens
                )

            # 3. 格式化上下文
            context = self.retriever.format_context(
                results,
                include_metadata=include_metadata
            )

            # 4. 构建返回结果
            result = {
                "query": query,
                "results": results,
                "context": context,
                "stats": {
                    "total_results": len(results),
                    "avg_score": sum(r.score for r in results) / len(results) if results else 0.0,
                    "sources": list(set(r.metadata.get('source', 'Unknown') for r in results))
                }
            }

            # 5. 记录审计日志
            if self.config.audit.log_queries:
                self.audit_logger.log_query(
                    query=query,
                    results=[{"id": r.doc_id, "score": r.score} for r in results],
                    stats=result["stats"]
                )

            return result

        except Exception as e:
            raise RetrievalError(f"Failed to query documents: {e}")
