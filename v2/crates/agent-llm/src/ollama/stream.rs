//! Ollama 流式响应实现

use agent_core::{
    error::Result,
    traits::llm::{CompletionStream, StreamChunk},
};
use async_trait::async_trait;
use futures_util::StreamExt;
use tracing::debug;

use super::types::ChatStreamResponse;

/// Ollama 流式响应
pub struct OllamaStream {
    stream: Box<dyn futures_util::Stream<Item = Result<String>> + Unpin + Send>,
    finished: bool,
    finish_reason: Option<String>,
}

impl OllamaStream {
    /// 从 HTTP 响应创建流
    pub fn new(response: reqwest::Response) -> Self {
        let byte_stream = response.bytes_stream();

        // 将字节流转换为字符串流
        let string_stream = byte_stream.map(|result| {
            result
                .map_err(|e| agent_core::Error::LLM(format!("Stream read error: {}", e)))
                .and_then(|bytes| {
                    String::from_utf8(bytes.to_vec())
                        .map_err(|e| agent_core::Error::LLM(format!("UTF-8 decode error: {}", e)))
                })
        });

        Self {
            stream: Box::new(string_stream),
            finished: false,
            finish_reason: None,
        }
    }

    /// 解析 Ollama 流式响应行
    /// Ollama 格式: 每行一个 JSON 对象
    fn parse_line(&mut self, line: &str) -> Result<Option<StreamChunk>> {
        if line.trim().is_empty() {
            return Ok(None);
        }

        // 解析 JSON
        let response: ChatStreamResponse = serde_json::from_str(line)
            .map_err(|e| agent_core::Error::InvalidInput(format!("Failed to parse stream line: {}", e)))?;

        debug!("Parsed stream response: done={}, has_message={}",
               response.done, response.message.is_some());

        // 检查是否完成
        if response.done {
            self.finished = true;
            self.finish_reason = response.done_reason.or(Some("stop".to_string()));

            return Ok(Some(StreamChunk {
                delta: String::new(),
                is_final: true,
                finish_reason: self.finish_reason.clone(),
            }));
        }

        // 提取增量内容
        if let Some(message) = response.message {
            if !message.content.is_empty() {
                return Ok(Some(StreamChunk {
                    delta: message.content,
                    is_final: false,
                    finish_reason: None,
                }));
            }
        }

        Ok(None)
    }
}

#[async_trait]
impl CompletionStream for OllamaStream {
    async fn next(&mut self) -> Result<Option<StreamChunk>> {
        if self.finished {
            return Ok(None);
        }

        loop {
            match self.stream.next().await {
                Some(Ok(data)) => {
                    // 处理可能包含多行的数据
                    for line in data.lines() {
                        if let Some(chunk) = self.parse_line(line)? {
                            if chunk.is_final {
                                self.finished = true;
                            }
                            return Ok(Some(chunk));
                        }
                    }
                }
                Some(Err(e)) => {
                    return Err(e);
                }
                None => {
                    self.finished = true;
                    return Ok(None);
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_line_with_content() {
        let mut stream = OllamaStream {
            stream: Box::new(futures_util::stream::empty()),
            finished: false,
            finish_reason: None,
        };

        let line = r#"{"model":"qwen2.5:0.5b","message":{"role":"assistant","content":"Hello"},"done":false}"#;
        let chunk = stream.parse_line(line).unwrap().unwrap();

        assert_eq!(chunk.delta, "Hello");
        assert!(!chunk.is_final);
        assert!(!stream.finished);
    }

    #[test]
    fn test_parse_line_done() {
        let mut stream = OllamaStream {
            stream: Box::new(futures_util::stream::empty()),
            finished: false,
            finish_reason: None,
        };

        let line = r#"{"model":"qwen2.5:0.5b","done":true,"done_reason":"stop"}"#;
        let chunk = stream.parse_line(line).unwrap().unwrap();

        assert!(chunk.is_final);
        assert_eq!(chunk.delta, "");
        assert_eq!(chunk.finish_reason, Some("stop".to_string()));
        assert!(stream.finished);
    }

    #[test]
    fn test_parse_line_empty_content() {
        let mut stream = OllamaStream {
            stream: Box::new(futures_util::stream::empty()),
            finished: false,
            finish_reason: None,
        };

        let line = r#"{"model":"qwen2.5:0.5b","message":{"role":"assistant","content":""},"done":false}"#;
        let result = stream.parse_line(line).unwrap();

        assert!(result.is_none());
    }

    #[test]
    fn test_parse_line_invalid_json() {
        let mut stream = OllamaStream {
            stream: Box::new(futures_util::stream::empty()),
            finished: false,
            finish_reason: None,
        };

        let line = "invalid json";
        let result = stream.parse_line(line);

        assert!(result.is_err());
    }
}
