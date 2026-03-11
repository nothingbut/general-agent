//! TUI 演示程序
//!
//! 运行: cargo run -p agent-tui --example tui_demo

use agent_core::traits::llm::LLMClient;
use agent_llm::OllamaClient;
use agent_skills::{SkillLoader, SkillRegistry};
use agent_storage::{repository::*, Database};
use agent_tui::{backend::*, TuiApp};
use agent_workflow::{ConversationConfig, ConversationFlow, SessionManager};
use anyhow::Result;
use std::{path::PathBuf, sync::Arc};
use tokio::sync::mpsc;

#[tokio::main]
async fn main() -> Result<()> {
    // 初始化日志 - 输出到文件避免干扰 TUI 显示
    let log_file = std::fs::File::create("tui_demo.log")?;
    tracing_subscriber::fmt()
        .with_env_filter(
            tracing_subscriber::EnvFilter::from_default_env()
                .add_directive(tracing::Level::INFO.into()),
        )
        .with_writer(log_file)
        .init();

    // 初始化数据库
    let db = Database::new("agent_tui_demo.db").await?;
    db.migrate().await?;

    let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
    let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
    let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

    // 创建 LLM 客户端
    let config = agent_llm::ollama::OllamaConfig::new("qwen3.5:0.8b".to_string())
        .with_base_url("http://localhost:11434".to_string());
    let llm_client: Arc<dyn LLMClient> = Arc::new(OllamaClient::new(config)?);

    // 加载技能系统
    let skills_dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../agent-skills/examples/test_skills");
    let loader = SkillLoader::new(skills_dir)?;
    let skills = loader.load_all()?;

    let mut registry = SkillRegistry::new();
    for skill in &skills {
        registry.register(skill.clone());
    }
    println!("✓ 加载了 {} 个技能", skills.len());

    // 创建对话流程（启用技能系统）
    let conversation_config = ConversationConfig::default();
    let conversation_flow = Arc::new(
        ConversationFlow::new(
            session_manager.clone(),
            llm_client.clone(),
            conversation_config,
        )
        .with_skills(Arc::new(registry))
    );

    // 创建通道
    let (update_tx, update_rx) = mpsc::unbounded_channel();

    // 创建 TUI 应用
    let (mut app, backend_rx) = TuiApp::new_with_channel(update_rx)?;

    // 确保至少有一个默认会话
    let session_manager_clone = session_manager.clone();
    let update_tx_clone = update_tx.clone();
    tokio::spawn(async move {
        // 检查是否已有会话
        if let Ok(sessions) = session_manager_clone.list_sessions(1, 0).await {
            if sessions.is_empty() {
                // 创建默认会话
                if let Ok(_) = session_manager_clone.create_session(Some("默认会话".to_string())).await {
                    tracing::info!("创建默认会话");
                    // 刷新会话列表
                    if let Ok(sessions) = session_manager_clone.list_sessions(10, 0).await {
                        let session_infos: Vec<SessionInfo> = sessions
                            .into_iter()
                            .map(|s| SessionInfo {
                                id: s.id,
                                title: s.title,
                                updated_at: s.updated_at,
                            })
                            .collect();
                        let _ = update_tx_clone.send(BackendUpdate::SessionsLoaded {
                            sessions: session_infos,
                        });
                    }
                }
            }
        }
    });

    // 启动后台任务
    let backend_task = tokio::spawn(async move {
        run_backend(backend_rx, update_tx, session_manager, conversation_flow).await
    });

    // 运行应用
    let result = app.run().await;

    // 等待后台任务结束
    backend_task.abort();

    result.map_err(|e| anyhow::anyhow!(e))
}

/// 运行后台任务
async fn run_backend(
    mut cmd_rx: mpsc::UnboundedReceiver<BackendCommand>,
    update_tx: mpsc::UnboundedSender<BackendUpdate>,
    session_manager: Arc<SessionManager>,
    conversation_flow: Arc<ConversationFlow>,
) {
    while let Some(cmd) = cmd_rx.recv().await {
        match cmd {
            BackendCommand::LoadSessions => {
                // 加载会话列表
                if let Ok(sessions) = session_manager.list_sessions(10, 0).await {
                    let session_infos: Vec<SessionInfo> = sessions
                        .into_iter()
                        .map(|s| SessionInfo {
                            id: s.id,
                            title: s.title,
                            updated_at: s.updated_at,
                        })
                        .collect();

                    let _ = update_tx.send(BackendUpdate::SessionsLoaded {
                        sessions: session_infos,
                    });
                }
            }

            BackendCommand::LoadMessages { session_id } => {
                // 加载消息
                if let Ok(messages) = session_manager.get_recent_messages(session_id, 50).await {
                    let message_infos: Vec<MessageInfo> = messages
                        .into_iter()
                        .map(|m| MessageInfo {
                            role: m.role.to_string(),
                            content: m.content,
                            timestamp: m.created_at,
                        })
                        .collect();

                    let _ = update_tx.send(BackendUpdate::MessagesLoaded {
                        session_id,
                        messages: message_infos,
                    });
                }
            }

            BackendCommand::SendMessage { session_id, content } => {
                // 克隆以便在异步任务中使用
                let flow = conversation_flow.clone();
                let tx = update_tx.clone();

                tokio::spawn(async move {
                    match flow.send_message_stream(session_id, content).await {
                        Ok((mut stream, _context)) => {
                            let mut buffer = String::new();

                            while let Ok(Some(chunk)) = stream.next().await {
                                buffer.push_str(&chunk.delta);

                                // 检测段落边界（双换行或句号+换行）
                                if buffer.contains("\n\n") || buffer.ends_with("。\n") {
                                    let paragraph = buffer.clone();
                                    buffer.clear();

                                    let _ = tx.send(BackendUpdate::ParagraphComplete {
                                        session_id,
                                        paragraph,
                                    });
                                }
                            }

                            // 发送剩余内容
                            if !buffer.is_empty() {
                                let _ = tx.send(BackendUpdate::ParagraphComplete {
                                    session_id,
                                    paragraph: buffer,
                                });
                            }

                            let _ = tx.send(BackendUpdate::ResponseComplete { session_id });
                        }
                        Err(e) => {
                            let _ = tx.send(BackendUpdate::Error {
                                session_id,
                                error: e.to_string(),
                            });
                        }
                    }
                });
            }

            BackendCommand::CreateSession { title } => {
                // 创建会话
                if let Ok(_session) = session_manager.create_session(title).await {
                    // 刷新会话列表
                    if let Ok(sessions) = session_manager.list_sessions(10, 0).await {
                        let session_infos: Vec<SessionInfo> = sessions
                            .into_iter()
                            .map(|s| SessionInfo {
                                id: s.id,
                                title: s.title,
                                updated_at: s.updated_at,
                            })
                            .collect();

                        let _ = update_tx.send(BackendUpdate::SessionsLoaded {
                            sessions: session_infos,
                        });
                    }
                }
            }

            BackendCommand::DeleteSession { session_id } => {
                // 删除会话
                if let Ok(_) = session_manager.delete_session(session_id).await {
                    // 刷新会话列表
                    if let Ok(sessions) = session_manager.list_sessions(10, 0).await {
                        let session_infos: Vec<SessionInfo> = sessions
                            .into_iter()
                            .map(|s| SessionInfo {
                                id: s.id,
                                title: s.title,
                                updated_at: s.updated_at,
                            })
                            .collect();

                        let _ = update_tx.send(BackendUpdate::SessionsLoaded {
                            sessions: session_infos,
                        });
                    }
                }
            }
        }
    }
}
