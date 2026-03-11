//! RAG 集成测试 - 测试 Ollama + Qdrant 的完整流程
//!
//! 前提条件：
//! - Ollama 服务运行在 localhost:11434
//! - Qdrant 服务运行在 localhost:6333
//! - nomic-embed-text 模型已安装

use agent_core::traits::RAGRetriever;
use agent_rag::{
    chunker::ChunkConfig,
    embeddings::{ollama::OllamaEmbedder, Embedder},
    loader::MarkdownLoader,
    retriever::DefaultRAGRetriever,
    vector_store::{qdrant::QdrantStore, VectorStore},
};
use std::sync::Arc;
use tokio;

/// 测试环境是否就绪
async fn check_services() -> bool {
    // 检查 Ollama
    let ollama_ok = reqwest::get("http://localhost:11434/api/version")
        .await
        .is_ok();

    // 检查 Qdrant
    let qdrant_ok = reqwest::get("http://localhost:6333/healthz").await.is_ok();

    ollama_ok && qdrant_ok
}

#[tokio::test]
#[ignore] // 需要外部服务，使用 --ignored 运行
async fn test_ollama_embedder_integration() {
    if !check_services().await {
        eprintln!("⚠️  Skipping test: Ollama or Qdrant service not available");
        return;
    }

    let embedder = OllamaEmbedder::with_defaults();

    // 测试单文本 embedding
    let text = "This is a test document about artificial intelligence.";
    let result = embedder.embed(text).await;

    assert!(result.is_ok(), "Embedding should succeed");
    let vector = result.unwrap();
    assert_eq!(vector.len(), 768, "nomic-embed-text produces 768-dim vectors");
    assert!(
        vector.iter().any(|&x| x != 0.0),
        "Vector should not be all zeros"
    );
}

#[tokio::test]
#[ignore]
async fn test_ollama_batch_embeddings() {
    if !check_services().await {
        eprintln!("⚠️  Skipping test: Services not available");
        return;
    }

    let embedder = OllamaEmbedder::with_defaults();
    let texts: Vec<String> = vec![
        "First document about machine learning.".to_string(),
        "Second document about deep learning.".to_string(),
        "Third document about neural networks.".to_string(),
    ];

    let result = embedder.embed_batch(&texts).await;
    assert!(result.is_ok());

    let vectors = result.unwrap();
    assert_eq!(vectors.len(), 3);
    for vector in vectors {
        assert_eq!(vector.len(), 768);
    }
}

#[tokio::test]
#[ignore]
async fn test_qdrant_collection_lifecycle() {
    if !check_services().await {
        eprintln!("⚠️  Skipping test: Services not available");
        return;
    }

    let store = QdrantStore::with_defaults().await.unwrap();
    let collection_name = format!("test_collection_{}", uuid::Uuid::new_v4());

    // 创建集合
    let create_result = store.create_collection(&collection_name, 768).await;
    assert!(create_result.is_ok());

    // 检查集合存在
    let exists = store.collection_exists(&collection_name).await.unwrap();
    assert!(exists, "Collection should exist after creation");

    // 删除集合
    let delete_result = store.delete_collection(&collection_name).await;
    assert!(delete_result.is_ok());

    // 检查集合不存在
    let exists_after = store.collection_exists(&collection_name).await.unwrap();
    assert!(!exists_after, "Collection should not exist after deletion");
}

#[tokio::test]
#[ignore]
async fn test_qdrant_insert_and_search() {
    if !check_services().await {
        eprintln!("⚠️  Skipping test: Services not available");
        return;
    }

    let embedder = Arc::new(OllamaEmbedder::with_defaults());
    let store = Arc::new(QdrantStore::with_defaults().await.unwrap());
    let collection_name = format!("test_search_{}", uuid::Uuid::new_v4());

    // 创建集合
    store.create_collection(&collection_name, 768).await.unwrap();

    // 准备测试数据
    let documents = vec![
        (uuid::Uuid::new_v4().to_string(), "Rust is a systems programming language."),
        (uuid::Uuid::new_v4().to_string(), "Python is great for machine learning."),
        (uuid::Uuid::new_v4().to_string(), "Rust has excellent memory safety."),
    ];

    // 插入文档
    for (id, text) in &documents {
        let vector = embedder.embed(text).await.unwrap();
        let mut metadata = std::collections::HashMap::new();
        metadata.insert("text".to_string(), text.to_string());
        metadata.insert("id_label".to_string(), if text.contains("Rust") { "rust".to_string() } else { "other".to_string() });

        store
            .insert(&collection_name, id.clone(), vector, metadata)
            .await
            .unwrap();
    }

    // 搜索相似文档
    let query = "programming language with memory safety";
    let query_vector = embedder.embed(query).await.unwrap();
    let results = store.search(&collection_name, query_vector, 2).await.unwrap();

    assert_eq!(results.len(), 2);
    // 应该找到包含 "Rust" 的文档
    assert!(
        results.iter().any(|r| r.metadata.get("id_label").map(|v| v == "rust").unwrap_or(false)),
        "Should find Rust-related documents"
    );

    // 清理
    store.delete_collection(&collection_name).await.unwrap();
}

#[tokio::test]
#[ignore]
async fn test_full_rag_pipeline() {
    if !check_services().await {
        eprintln!("⚠️  Skipping test: Services not available");
        return;
    }

    let embedder = Arc::new(OllamaEmbedder::with_defaults());
    let store = Arc::new(QdrantStore::with_defaults().await.unwrap());
    let collection_name = format!("test_rag_{}", uuid::Uuid::new_v4());

    // 创建集合
    store.create_collection(&collection_name, 768).await.unwrap();

    // 创建 Retriever
    let retriever = DefaultRAGRetriever::new(embedder, store.clone(), collection_name.clone());

    // 创建临时 Markdown 文件
    let temp_dir = std::env::temp_dir();
    let test_file = temp_dir.join("test_rag_doc.md");
    std::fs::write(
        &test_file,
        r#"# RAG Test Document

## Introduction
This is a test document for RAG (Retrieval Augmented Generation) system.

## Features
- Document loading
- Text chunking
- Vector embedding
- Similarity search

## Conclusion
RAG improves LLM responses with relevant context.
"#,
    )
    .unwrap();

    // 索引文档
    let loader = MarkdownLoader;
    let chunk_config = ChunkConfig {
        chunk_size: 200,
        overlap: 50,
        strategy: agent_rag::chunker::ChunkStrategy::Fixed,
    };

    let index_result = retriever
        .index_document_with_loader(&loader, test_file.to_str().unwrap(), chunk_config)
        .await;
    assert!(index_result.is_ok(), "Document indexing should succeed");

    // 检索测试
    let query = "What are the features of RAG?";
    let results = retriever.retrieve(query, 3).await.unwrap();

    assert!(!results.is_empty(), "Should retrieve results");

    // 验证检索到的内容包含相关信息
    let top_text = &results[0].content;
    assert!(
        top_text.to_lowercase().contains("feature")
            || top_text.to_lowercase().contains("rag")
            || top_text.to_lowercase().contains("retrieval"),
        "Retrieved content should be relevant"
    );

    // 清理
    std::fs::remove_file(test_file).ok();
    store.delete_collection(&collection_name).await.unwrap();
}

#[tokio::test]
#[ignore]
async fn test_rag_with_multiple_documents() {
    if !check_services().await {
        eprintln!("⚠️  Skipping test: Services not available");
        return;
    }

    let embedder = Arc::new(OllamaEmbedder::with_defaults());
    let store = Arc::new(QdrantStore::with_defaults().await.unwrap());
    let collection_name = format!("test_multi_docs_{}", uuid::Uuid::new_v4());

    store.create_collection(&collection_name, 768).await.unwrap();

    let retriever = DefaultRAGRetriever::new(embedder, store.clone(), collection_name.clone());
    let loader = MarkdownLoader;
    let chunk_config = ChunkConfig::default();

    // 创建多个文档
    let temp_dir = std::env::temp_dir();
    let docs = vec![
        ("rust.md", "# Rust\nRust is a systems programming language focusing on safety, speed, and concurrency."),
        ("python.md", "# Python\nPython is a high-level programming language known for its simplicity and readability."),
        ("javascript.md", "# JavaScript\nJavaScript is a versatile language used for web development and beyond."),
    ];

    for (filename, content) in &docs {
        let file_path = temp_dir.join(filename);
        std::fs::write(&file_path, content).unwrap();

        retriever
            .index_document_with_loader(&loader, file_path.to_str().unwrap(), chunk_config.clone())
            .await
            .unwrap();
    }

    // 查询不同主题
    let queries = vec![
        ("safety and memory", "rust"),
        ("simple and readable", "python"),
        ("web development", "javascript"),
    ];

    for (query, expected_keyword) in queries {
        let results = retriever.retrieve(query, 1).await.unwrap();
        assert!(!results.is_empty());

        let content_lower = results[0].content.to_lowercase();
        assert!(
            content_lower.contains(expected_keyword),
            "Query '{}' should retrieve content about '{}'",
            query,
            expected_keyword
        );
    }

    // 清理
    for (filename, _) in docs {
        std::fs::remove_file(temp_dir.join(filename)).ok();
    }
    store.delete_collection(&collection_name).await.unwrap();
}
