use agent_rag::chunker::*;
use agent_core::traits::Document;
use std::collections::HashMap;

#[test]
fn test_fixed_chunking() {
    let doc = Document {
        id: "test".to_string(),
        content: "a".repeat(1000),
        metadata: HashMap::new(),
    };

    let config = ChunkConfig {
        chunk_size: 100,
        overlap: 10,
        strategy: ChunkStrategy::Fixed,
    };

    let chunker = Chunker::new(config);
    let chunks = chunker.chunk(&doc);

    assert!(chunks.len() > 1);
    assert!(chunks[0].content.len() <= 100);
}
