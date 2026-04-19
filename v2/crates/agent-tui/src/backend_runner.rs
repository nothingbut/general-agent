//! 后端运行器 - 连接 UI 命令到 AgentRuntime

#[allow(unused_imports)]
use crate::backend::{BackendCommand, BackendUpdate, FileInfo, MemoryInfo, MessageInfo, SessionInfo};
use agent_workflow::{AgentRuntime, ConversationConfig, ConversationFlow};
use std::sync::Arc;
use tokio::sync::mpsc;
use tracing::{error, info};
use uuid::Uuid;

pub struct BackendRunner {
    runtime: Arc<AgentRuntime>,
    cmd_rx: mpsc::UnboundedReceiver<BackendCommand>,
    update_tx: mpsc::UnboundedSender<BackendUpdate>,
}

impl BackendRunner {
    pub fn new(
        runtime: Arc<AgentRuntime>,
        cmd_rx: mpsc::UnboundedReceiver<BackendCommand>,
        update_tx: mpsc::UnboundedSender<BackendUpdate>,
    ) -> Self {
        Self {
            runtime,
            cmd_rx,
            update_tx,
        }
    }

    pub async fn run(mut self) {
        info!("BackendRunner 已启动");

        while let Some(cmd) = self.cmd_rx.recv().await {
            match cmd {
                BackendCommand::LoadSessions => {
                    self.handle_load_sessions().await;
                }
                BackendCommand::LoadMessages { session_id } => {
                    self.handle_load_messages(session_id).await;
                }
                BackendCommand::SendMessage {
                    session_id,
                    content,
                } => {
                    self.handle_send_message(session_id, content).await;
                }
                BackendCommand::CreateSession { title } => {
                    self.handle_create_session(title).await;
                }
                BackendCommand::DeleteSession { session_id } => {
                    self.handle_delete_session(session_id).await;
                }
                BackendCommand::LoadMemories => {
                    self.handle_load_memories().await;
                }
                BackendCommand::LoadFiles => {
                    self.handle_load_files().await;
                }
            }
        }

        info!("BackendRunner 已停止");
    }

    async fn handle_load_sessions(&self) {
        match self
            .runtime
            .session_manager()
            .list_sessions(50, 0)
            .await
        {
            Ok(sessions) => {
                let infos: Vec<SessionInfo> = sessions
                    .into_iter()
                    .map(|s| SessionInfo {
                        id: s.id,
                        title: s.title,
                        updated_at: s.updated_at,
                    })
                    .collect();
                let _ = self
                    .update_tx
                    .send(BackendUpdate::SessionsLoaded { sessions: infos });
            }
            Err(e) => {
                error!("加载会话列表失败: {}", e);
            }
        }
    }

    async fn handle_load_messages(&self, session_id: Uuid) {
        match self
            .runtime
            .session_manager()
            .get_messages(session_id, None)
            .await
        {
            Ok(messages) => {
                let infos: Vec<MessageInfo> = messages
                    .into_iter()
                    .map(|m| MessageInfo {
                        role: format!("{:?}", m.role).to_lowercase(),
                        content: m.content,
                        timestamp: m.created_at,
                    })
                    .collect();
                let _ = self.update_tx.send(BackendUpdate::MessagesLoaded {
                    session_id,
                    messages: infos,
                });
            }
            Err(e) => {
                let _ = self.update_tx.send(BackendUpdate::Error {
                    session_id,
                    error: format!("加载消息失败: {}", e),
                });
            }
        }
    }

    async fn handle_send_message(&self, session_id: Uuid, content: String) {
        let config = ConversationConfig::default();
        let flow = ConversationFlow::new(
            self.runtime.session_manager().clone(),
            self.runtime.llm_client().clone(),
            config,
        );

        match flow.send_message(session_id, content).await {
            Ok(response) => {
                // 逐 token 发送以实现流式渲染效果
                for chunk in split_into_tokens(&response) {
                    let _ = self.update_tx.send(BackendUpdate::StreamingToken {
                        session_id,
                        token: chunk.to_string(),
                    });
                }
                let _ = self
                    .update_tx
                    .send(BackendUpdate::ResponseComplete { session_id });
            }
            Err(e) => {
                let _ = self.update_tx.send(BackendUpdate::Error {
                    session_id,
                    error: format!("发送失败: {}", e),
                });
            }
        }
    }

    async fn handle_create_session(&self, title: Option<String>) {
        match self
            .runtime
            .session_manager()
            .create_session(title)
            .await
        {
            Ok(_) => {
                self.handle_load_sessions().await;
            }
            Err(e) => {
                error!("创建会话失败: {}", e);
            }
        }
    }

    async fn handle_delete_session(&self, session_id: Uuid) {
        match self
            .runtime
            .session_manager()
            .delete_session(session_id)
            .await
        {
            Ok(_) => {
                self.handle_load_sessions().await;
            }
            Err(e) => {
                error!("删除会话失败: {}", e);
            }
        }
    }

    async fn handle_load_memories(&self) {
        // 目前返回空列表，待 agent-memory 集成后填充
        let _ = self
            .update_tx
            .send(BackendUpdate::MemoriesLoaded { memories: vec![] });
    }

    async fn handle_load_files(&self) {
        // 目前返回空列表，待 agent-file-storage 集成后填充
        let _ = self
            .update_tx
            .send(BackendUpdate::FilesLoaded { files: vec![] });
    }
}

fn split_into_tokens(text: &str) -> Vec<&str> {
    let mut tokens = Vec::new();
    let mut start = 0;
    let bytes = text.as_bytes();
    let len = bytes.len();

    while start < len {
        let end = if bytes[start] == b' ' {
            let mut e = start + 1;
            while e < len && bytes[e] != b' ' && !is_cjk_byte_start(bytes, e) {
                e += 1;
            }
            e
        } else if is_cjk_byte_start(bytes, start) {
            let ch = text[start..].chars().next().unwrap();
            start + ch.len_utf8()
        } else if bytes[start] == b'\n' {
            start + 1
        } else {
            let mut e = start + 1;
            while e < len && bytes[e] != b' ' && bytes[e] != b'\n' && !is_cjk_byte_start(bytes, e) {
                e += 1;
            }
            e
        };
        tokens.push(&text[start..end]);
        start = end;
    }

    tokens
}

fn is_cjk_byte_start(bytes: &[u8], pos: usize) -> bool {
    // CJK characters are encoded as 3-byte sequences starting with 0xE0..=0xEF
    // or 4-byte sequences starting with 0xF0..=0xF4
    matches!(bytes[pos], 0xE0..=0xEF | 0xF0..=0xF4)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_backend_runner_channels() {
        let (_cmd_tx, _cmd_rx) = mpsc::unbounded_channel::<BackendCommand>();
        let (_update_tx, _update_rx) = mpsc::unbounded_channel::<BackendUpdate>();
    }

    #[test]
    fn test_split_into_tokens_english() {
        let tokens = split_into_tokens("hello world");
        assert_eq!(tokens.join(""), "hello world");
        assert!(tokens.len() >= 2);
    }

    #[test]
    fn test_split_into_tokens_chinese() {
        let tokens = split_into_tokens("你好世界");
        assert_eq!(tokens.join(""), "你好世界");
        assert_eq!(tokens.len(), 4);
    }

    #[test]
    fn test_split_into_tokens_mixed() {
        let tokens = split_into_tokens("hello 你好");
        assert_eq!(tokens.join(""), "hello 你好");
        assert!(tokens.len() >= 3);
    }

    #[test]
    fn test_split_into_tokens_empty() {
        let tokens = split_into_tokens("");
        assert!(tokens.is_empty());
    }

    #[test]
    fn test_split_into_tokens_newlines() {
        let tokens = split_into_tokens("line1\nline2");
        assert_eq!(tokens.join(""), "line1\nline2");
        assert!(tokens.len() >= 3);
    }
}
