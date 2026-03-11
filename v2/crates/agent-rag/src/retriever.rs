//! RAG 检索器实现

use agent_core::traits::{Document, RAGRetriever};
use async_trait::async_trait;
use std::sync::Arc;
use crate::{
    chunker::{Chunker, ChunkConfig},
    embeddings::Embedder,
    loader::DocumentLoader,
    vector_store::VectorStore,
    Result,
};

/// 默认 RAG 检索器
pub struct DefaultRAGRetriever {
    embedder: Arc<dyn Embedder>,
    vector_store: Arc<dyn VectorStore>,
    collection_name: String,
}

impl DefaultRAGRetriever {
    /// 创建新的检索器
    pub fn new(
        embedder: Arc<dyn Embedder>,
        vector_store: Arc<dyn VectorStore>,
        collection_name: String,
    ) -> Self {
        Self {
            embedder,
            vector_store,
            collection_name,
        }
    }

    /// 索引单个文档
    pub async fn index_document_with_loader(
        &self,
        loader: &dyn DocumentLoader,
        source: &str,
        chunk_config: ChunkConfig,
    ) -> Result<()> {
        // 1. 加载文档
        let documents = loader.load(source).await?;

        // 2. 分块
        let chunker = Chunker::new(chunk_config);
        let mut all_chunks = Vec::new();
        for doc in &documents {
            let chunks = chunker.chunk(doc);
            all_chunks.extend(chunks);
        }

        // 3. 生成 embeddings
        let texts: Vec<String> = all_chunks.iter().map(|c| c.content.clone()).collect();
        let embeddings = self.embedder.embed_batch(&texts).await?;

        // 4. 存储到向量数据库
        // 使用 UUID 作为向量数据库的 ID，避免文件路径等非 UUID 格式问题
        let ids: Vec<String> = (0..all_chunks.len())
            .map(|_| uuid::Uuid::new_v4().to_string())
            .collect();

        let metadatas = all_chunks
            .into_iter()
            .map(|c| {
                let mut meta = std::collections::HashMap::new();
                meta.insert("content".to_string(), c.content);
                meta.insert("doc_id".to_string(), c.doc_id);
                meta.insert("chunk_id".to_string(), c.id); // 保留原始 chunk ID
                meta
            })
            .collect();

        self.vector_store
            .insert_batch(&self.collection_name, ids, embeddings, metadatas)
            .await?;

        Ok(())
    }
}

#[async_trait]
impl RAGRetriever for DefaultRAGRetriever {
    async fn retrieve(&self, query: &str, top_k: usize) -> agent_core::error::Result<Vec<Document>> {
        // 1. 生成查询 embedding
        let query_embedding = self
            .embedder
            .embed(query)
            .await
            .map_err(|e| agent_core::error::Error::External(e.to_string()))?;

        // 2. 向量搜索
        let results = self
            .vector_store
            .search(&self.collection_name, query_embedding, top_k)
            .await
            .map_err(|e| agent_core::error::Error::External(e.to_string()))?;

        // 3. 转换为 Document
        let documents = results
            .into_iter()
            .map(|r| Document {
                id: r.id,
                content: r.content.unwrap_or_default(),
                metadata: r.metadata,
            })
            .collect();

        Ok(documents)
    }

    async fn index_document(&self, doc: Document) -> agent_core::error::Result<()> {
        // 简单实现：直接索引文档内容
        let embedding = self
            .embedder
            .embed(&doc.content)
            .await
            .map_err(|e| agent_core::error::Error::External(e.to_string()))?;

        self.vector_store
            .insert(&self.collection_name, doc.id.clone(), embedding, doc.metadata)
            .await
            .map_err(|e| agent_core::error::Error::External(e.to_string()))?;

        Ok(())
    }
}
