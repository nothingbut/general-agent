use async_trait::async_trait;
use std::sync::Arc;

use agent_core::traits::llm::{CompletionRequest, LLMClient};

use crate::error::Result;
use crate::models::*;
use crate::traits::Agent;

pub struct LlmAgent {
    info: AgentInfo,
    llm_client: Arc<dyn LLMClient>,
    model: String,
}

impl LlmAgent {
    pub fn new(
        info: AgentInfo,
        llm_client: Arc<dyn LLMClient>,
        model: impl Into<String>,
    ) -> Self {
        Self {
            info,
            llm_client,
            model: model.into(),
        }
    }

    pub fn search_agent(llm_client: Arc<dyn LLMClient>, model: impl Into<String>) -> Self {
        let info = AgentInfo::new("search-agent", "Search Agent")
            .with_description("Searches for information and retrieves relevant data")
            .with_capability(AgentCapability::Search)
            .with_capability(AgentCapability::DataExtraction)
            .with_system_prompt(
                "You are a search specialist. Given a query, find and return the most relevant information. \
                 Be concise and factual. Structure your response clearly.",
            );

        Self::new(info, llm_client, model)
    }

    pub fn analysis_agent(llm_client: Arc<dyn LLMClient>, model: impl Into<String>) -> Self {
        let info = AgentInfo::new("analysis-agent", "Analysis Agent")
            .with_description("Analyzes data, code, and documents for insights")
            .with_capability(AgentCapability::Analysis)
            .with_capability(AgentCapability::Reasoning)
            .with_system_prompt(
                "You are an analysis specialist. Examine the given data or content thoroughly. \
                 Identify patterns, issues, and insights. Provide structured analysis with clear conclusions.",
            );

        Self::new(info, llm_client, model)
    }

    pub fn summary_agent(llm_client: Arc<dyn LLMClient>, model: impl Into<String>) -> Self {
        let info = AgentInfo::new("summary-agent", "Summary Agent")
            .with_description("Summarizes and synthesizes information from multiple sources")
            .with_capability(AgentCapability::Summary)
            .with_system_prompt(
                "You are a summarization specialist. Given one or more pieces of information, \
                 synthesize them into a clear, coherent summary. Highlight key points and conclusions.",
            );

        Self::new(info, llm_client, model)
    }

    fn build_messages(
        &self,
        task: &str,
        context: &serde_json::Value,
    ) -> Vec<agent_core::models::Message> {
        use agent_core::models::{Message, MessageRole};
        use uuid::Uuid;

        let session_id = Uuid::new_v4();
        let mut messages = Vec::new();

        if let Some(prompt) = &self.info.system_prompt {
            messages.push(Message::new(
                session_id,
                MessageRole::System,
                prompt.clone(),
            ));
        }

        let user_content = if context.is_null() || context == &serde_json::json!({}) {
            task.to_string()
        } else {
            format!("{}\n\nContext:\n{}", task, serde_json::to_string_pretty(context).unwrap_or_default())
        };

        messages.push(Message::new(session_id, MessageRole::User, user_content));

        messages
    }
}

#[async_trait]
impl Agent for LlmAgent {
    fn info(&self) -> &AgentInfo {
        &self.info
    }

    async fn handle_message(&self, message: AgentMessage) -> Result<AgentMessage> {
        match &message.content {
            MessageContent::TaskRequest { task, context } => {
                let result = self.execute_task(task, context.clone()).await?;
                Ok(AgentMessage::task_response(
                    &self.info.id,
                    &message.from_agent,
                    result,
                    serde_json::json!({"model": self.model}),
                    message.correlation_id,
                ))
            }
            MessageContent::Ping => Ok(AgentMessage {
                id: uuid::Uuid::new_v4(),
                from_agent: self.info.id.clone(),
                to_agent: message.from_agent.clone(),
                content: MessageContent::Pong,
                correlation_id: message.correlation_id,
                created_at: chrono::Utc::now(),
            }),
            _ => Ok(AgentMessage::error(
                &self.info.id,
                &message.from_agent,
                "UNSUPPORTED",
                "Message type not supported",
                message.correlation_id,
            )),
        }
    }

    async fn execute_task(&self, task: &str, context: serde_json::Value) -> Result<String> {
        let messages = self.build_messages(task, &context);
        let request = CompletionRequest::new(messages, self.model.clone());

        let response = self
            .llm_client
            .complete(request)
            .await
            .map_err(|e| crate::error::MultiAgentError::LLM(e.to_string()))?;

        Ok(response.content)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_factory_methods() {
        use agent_core::traits::llm::{CompletionRequest, CompletionResponse, CompletionStream, LLMClient, ModelInfo, StreamChunk, TokenUsage};
        use async_trait::async_trait;

        struct FakeLlm;

        #[async_trait]
        impl LLMClient for FakeLlm {
            async fn complete(&self, _req: CompletionRequest) -> agent_core::Result<CompletionResponse> {
                Ok(CompletionResponse {
                    content: "test".to_string(),
                    model: "fake".to_string(),
                    usage: TokenUsage::new(10, 5),
                    finish_reason: None,
                })
            }
            async fn stream(&self, _req: CompletionRequest) -> agent_core::Result<Box<dyn CompletionStream>> {
                unimplemented!()
            }
            async fn list_models(&self) -> agent_core::Result<Vec<ModelInfo>> {
                Ok(vec![])
            }
            fn provider_name(&self) -> &str {
                "fake"
            }
        }

        let llm = Arc::new(FakeLlm);

        let search = LlmAgent::search_agent(llm.clone(), "test-model");
        assert_eq!(search.info().id, "search-agent");
        assert!(search.info().has_capability(&AgentCapability::Search));

        let analysis = LlmAgent::analysis_agent(llm.clone(), "test-model");
        assert_eq!(analysis.info().id, "analysis-agent");
        assert!(analysis.info().has_capability(&AgentCapability::Analysis));

        let summary = LlmAgent::summary_agent(llm, "test-model");
        assert_eq!(summary.info().id, "summary-agent");
        assert!(summary.info().has_capability(&AgentCapability::Summary));
    }
}
