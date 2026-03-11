//! 文本分块器实现

use agent_core::traits::Document;

/// 分块策略
#[derive(Debug, Clone, Copy)]
pub enum ChunkStrategy {
    /// 固定大小分块
    Fixed,
    /// 语义分块
    Semantic,
    /// 递归分块
    Recursive,
}

/// 分块配置
#[derive(Debug, Clone)]
pub struct ChunkConfig {
    /// 分块大小（字符数）
    pub chunk_size: usize,
    /// 重叠大小（字符数）
    pub overlap: usize,
    /// 分块策略
    pub strategy: ChunkStrategy,
}

impl Default for ChunkConfig {
    fn default() -> Self {
        Self {
            chunk_size: 512,
            overlap: 50,
            strategy: ChunkStrategy::Fixed,
        }
    }
}

/// 文本块
#[derive(Debug, Clone)]
pub struct Chunk {
    /// 块 ID
    pub id: String,
    /// 块内容
    pub content: String,
    /// 源文档 ID
    pub doc_id: String,
    /// 起始位置
    pub start_idx: usize,
    /// 结束位置
    pub end_idx: usize,
}

/// 文本分块器
pub struct Chunker {
    config: ChunkConfig,
}

impl Chunker {
    /// 创建新的分块器
    pub fn new(config: ChunkConfig) -> Self {
        Self { config }
    }

    /// 对文档进行分块
    pub fn chunk(&self, doc: &Document) -> Vec<Chunk> {
        match self.config.strategy {
            ChunkStrategy::Fixed => self.chunk_fixed(doc),
            ChunkStrategy::Semantic => vec![], // TODO
            ChunkStrategy::Recursive => vec![], // TODO
        }
    }

    /// 固定大小分块
    fn chunk_fixed(&self, doc: &Document) -> Vec<Chunk> {
        let chars: Vec<char> = doc.content.chars().collect();
        let mut chunks = Vec::new();
        let stride = self.config.chunk_size.saturating_sub(self.config.overlap);

        let mut start = 0;
        let mut chunk_idx = 0;

        while start < chars.len() {
            let end = (start + self.config.chunk_size).min(chars.len());
            let content: String = chars[start..end].iter().collect();

            chunks.push(Chunk {
                id: format!("{}_{}", doc.id, chunk_idx),
                content,
                doc_id: doc.id.clone(),
                start_idx: start,
                end_idx: end,
            });

            start += stride;
            chunk_idx += 1;

            if stride == 0 {
                break;
            }
        }

        chunks
    }
}
