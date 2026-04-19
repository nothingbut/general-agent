use std::sync::Arc;
use tokio::sync::mpsc;
use tracing;

use crate::error::{MultiAgentError, Result};
use crate::models::AgentMessage;
use crate::registry::AgentRegistry;

pub struct MessageRouter {
    registry: Arc<AgentRegistry>,
    tx: mpsc::Sender<AgentMessage>,
    rx: Option<mpsc::Receiver<AgentMessage>>,
}

impl MessageRouter {
    pub fn new(registry: Arc<AgentRegistry>, buffer_size: usize) -> Self {
        let (tx, rx) = mpsc::channel(buffer_size);
        Self {
            registry,
            tx,
            rx: Some(rx),
        }
    }

    pub fn sender(&self) -> mpsc::Sender<AgentMessage> {
        self.tx.clone()
    }

    pub fn take_receiver(&mut self) -> Option<mpsc::Receiver<AgentMessage>> {
        self.rx.take()
    }

    pub async fn route(&self, message: AgentMessage) -> Result<AgentMessage> {
        let target_agent = self.registry.get(&message.to_agent)?;

        tracing::debug!(
            from = %message.from_agent,
            to = %message.to_agent,
            correlation_id = %message.correlation_id,
            "Routing message"
        );

        target_agent.handle_message(message).await
    }

    pub async fn send(&self, message: AgentMessage) -> Result<()> {
        self.tx
            .send(message)
            .await
            .map_err(|_| MultiAgentError::ChannelClosed)
    }

    pub async fn start_routing(mut self) -> Result<()> {
        let mut rx = self
            .rx
            .take()
            .ok_or_else(|| MultiAgentError::Config("Receiver already taken".to_string()))?;

        while let Some(message) = rx.recv().await {
            let registry = self.registry.clone();
            tokio::spawn(async move {
                match registry.get(&message.to_agent) {
                    Ok(agent) => {
                        if let Err(e) = agent.handle_message(message).await {
                            tracing::error!(error = %e, "Failed to deliver message");
                        }
                    }
                    Err(e) => {
                        tracing::warn!(error = %e, "Target agent not found for message routing");
                    }
                }
            });
        }

        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::{AgentInfo, AgentMessage};
    use crate::traits::Agent;
    use async_trait::async_trait;
    use std::sync::atomic::{AtomicUsize, Ordering};
    use uuid::Uuid;

    struct CountingAgent {
        info: AgentInfo,
        call_count: AtomicUsize,
    }

    impl CountingAgent {
        fn new(id: &str) -> Self {
            Self {
                info: AgentInfo::new(id, id),
                call_count: AtomicUsize::new(0),
            }
        }

        fn calls(&self) -> usize {
            self.call_count.load(Ordering::SeqCst)
        }
    }

    #[async_trait]
    impl Agent for CountingAgent {
        fn info(&self) -> &AgentInfo {
            &self.info
        }

        async fn handle_message(&self, msg: AgentMessage) -> crate::Result<AgentMessage> {
            self.call_count.fetch_add(1, Ordering::SeqCst);
            Ok(AgentMessage::task_response(
                &self.info.id,
                &msg.from_agent,
                "handled",
                serde_json::json!({}),
                msg.correlation_id,
            ))
        }

        async fn execute_task(
            &self,
            _task: &str,
            _context: serde_json::Value,
        ) -> crate::Result<String> {
            self.call_count.fetch_add(1, Ordering::SeqCst);
            Ok("done".to_string())
        }
    }

    #[tokio::test]
    async fn test_route_message() {
        let registry = Arc::new(AgentRegistry::new());
        let agent = Arc::new(CountingAgent::new("target"));
        registry.register(agent.clone()).unwrap();

        let router = MessageRouter::new(registry, 16);

        let msg = AgentMessage::task_request(
            "sender",
            "target",
            "do something",
            serde_json::json!({}),
            Uuid::new_v4(),
        );

        let response = router.route(msg).await.unwrap();
        assert_eq!(response.from_agent, "target");
        assert_eq!(agent.calls(), 1);
    }

    #[tokio::test]
    async fn test_route_to_unknown_agent() {
        let registry = Arc::new(AgentRegistry::new());
        let router = MessageRouter::new(registry, 16);

        let msg = AgentMessage::task_request(
            "sender",
            "nonexistent",
            "task",
            serde_json::json!({}),
            Uuid::new_v4(),
        );

        let err = router.route(msg).await.unwrap_err();
        assert!(matches!(err, MultiAgentError::AgentNotFound(_)));
    }

    #[tokio::test]
    async fn test_send_via_channel() {
        let registry = Arc::new(AgentRegistry::new());
        let mut router = MessageRouter::new(registry, 16);
        let mut rx = router.take_receiver().unwrap();

        let msg = AgentMessage::task_request(
            "a",
            "b",
            "task",
            serde_json::json!({}),
            Uuid::new_v4(),
        );
        let corr_id = msg.correlation_id;

        router.send(msg).await.unwrap();

        let received = rx.recv().await.unwrap();
        assert_eq!(received.correlation_id, corr_id);
    }
}
