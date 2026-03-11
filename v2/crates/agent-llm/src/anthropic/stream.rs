//! Anthropic 流式响应实现

use agent_core::{
    error::Result,
    traits::llm::{CompletionStream, StreamChunk},
};
use async_trait::async_trait;
use futures_util::StreamExt;

use super::types::{Delta, StreamEvent};

/// Anthropic 流式响应
pub struct AnthropicStream {
    stream: Box<dyn futures_util::Stream<Item = Result<String>> + Unpin + Send>,
    finished: bool,
    finish_reason: Option<String>,
}

impl AnthropicStream {
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

    /// 解析 SSE 事件
    fn parse_event(&mut self, line: &str) -> Result<Option<StreamChunk>> {
        // SSE 格式: "data: {...}"
        if !line.starts_with("data: ") {
            return Ok(None);
        }

        let json_str = &line[6..]; // 跳过 "data: " 前缀

        // 检查流结束标记
        if json_str == "[DONE]" {
            return Ok(Some(StreamChunk {
                delta: String::new(),
                is_final: true,
                finish_reason: self.finish_reason.clone(),
            }));
        }

        // 解析 JSON 事件
        let event: StreamEvent = serde_json::from_str(json_str)
            .map_err(|e| agent_core::Error::InvalidInput(format!("Failed to parse stream event: {}", e)))?;

        match event {
            StreamEvent::ContentBlockDelta { delta, .. } => {
                if let Delta::TextDelta { text } = delta {
                    Ok(Some(StreamChunk {
                        delta: text,
                        is_final: false,
                        finish_reason: None,
                    }))
                } else {
                    Ok(None)
                }
            }
            StreamEvent::MessageDelta { delta } => {
                if let Some(reason) = delta.stop_reason {
                    self.finish_reason = Some(reason);
                }
                Ok(None)
            }
            StreamEvent::MessageStop => {
                self.finished = true;
                Ok(Some(StreamChunk {
                    delta: String::new(),
                    is_final: true,
                    finish_reason: self.finish_reason.clone(),
                }))
            }
            StreamEvent::Error { error } => {
                Err(agent_core::Error::LLM(format!(
                    "Stream error: {} - {}",
                    error.error_type, error.message
                )))
            }
            // 忽略其他事件类型
            _ => Ok(None),
        }
    }
}

#[async_trait]
impl CompletionStream for AnthropicStream {
    async fn next(&mut self) -> Result<Option<StreamChunk>> {
        if self.finished {
            return Ok(None);
        }

        loop {
            match self.stream.next().await {
                Some(Ok(data)) => {
                    // 处理可能包含多行的数据
                    for line in data.lines() {
                        let line = line.trim();
                        if line.is_empty() {
                            continue;
                        }

                        if let Some(chunk) = self.parse_event(line)? {
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
    fn test_parse_event_text_delta() {
        let mut stream = AnthropicStream {
            stream: Box::new(futures_util::stream::empty()),
            finished: false,
            finish_reason: None,
        };

        let line = r#"data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}"#;
        let chunk = stream.parse_event(line).unwrap().unwrap();

        assert_eq!(chunk.delta, "Hello");
        assert!(!chunk.is_final);
    }

    #[test]
    fn test_parse_event_message_stop() {
        let mut stream = AnthropicStream {
            stream: Box::new(futures_util::stream::empty()),
            finished: false,
            finish_reason: Some("end_turn".to_string()),
        };

        let line = r#"data: {"type":"message_stop"}"#;
        let chunk = stream.parse_event(line).unwrap().unwrap();

        assert!(chunk.is_final);
        assert_eq!(chunk.finish_reason, Some("end_turn".to_string()));
    }

    #[test]
    fn test_parse_event_done() {
        let mut stream = AnthropicStream {
            stream: Box::new(futures_util::stream::empty()),
            finished: false,
            finish_reason: None,
        };

        let line = "data: [DONE]";
        let chunk = stream.parse_event(line).unwrap().unwrap();

        assert!(chunk.is_final);
        assert_eq!(chunk.delta, "");
    }
}
