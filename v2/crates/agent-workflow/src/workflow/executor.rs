//! 任务执行器 - 执行单个任务
//!
//! 本模块实现了任务执行器，负责：
//! - 执行不同类型的任务
//! - 超时控制
//! - 重试机制（指数退避）
//! - 错误处理
//!
//! # 示例
//!
//! ```rust
//! use agent_workflow::workflow::*;
//!
//! # tokio_test::block_on(async {
//! let executor = TaskExecutor::new();
//! let task = Task::new("test", "Test Task", TaskType::Custom("demo".to_string()));
//!
//! let result = executor.execute_task(&task).await;
//! assert_eq!(result.status, TaskStatus::Completed);
//! # });
//! ```

use tokio::time::{timeout, Duration};
use anyhow::{Result, bail};
use std::sync::Arc;

use super::models::*;

/// MCP 客户端管理器 - 管理多个 MCP 服务器连接
#[derive(Clone)]
pub struct MCPClientManager {
    clients: Arc<tokio::sync::RwLock<std::collections::HashMap<String, Arc<dyn agent_core::traits::MCPClient>>>>,
}

impl MCPClientManager {
    /// 创建新的管理器
    pub fn new() -> Self {
        Self {
            clients: Arc::new(tokio::sync::RwLock::new(std::collections::HashMap::new())),
        }
    }

    /// 添加 MCP 客户端
    pub async fn add_client(&self, server_name: String, client: Arc<dyn agent_core::traits::MCPClient>) {
        let mut clients = self.clients.write().await;
        clients.insert(server_name, client);
    }

    /// 获取 MCP 客户端
    pub async fn get_client(&self, server_name: &str) -> Option<Arc<dyn agent_core::traits::MCPClient>> {
        let clients = self.clients.read().await;
        clients.get(server_name).cloned()
    }
}

impl Default for MCPClientManager {
    fn default() -> Self {
        Self::new()
    }
}

/// 任务执行器
#[derive(Clone)]
pub struct TaskExecutor {
    /// LLM 客户端（可选）
    llm_client: Option<Arc<agent_llm::AnthropicClient>>,
    /// Skills 注册表（可选）
    skill_registry: Option<Arc<agent_skills::SkillRegistry>>,
    /// MCP 客户端管理器（可选）
    mcp_manager: Option<MCPClientManager>,
}

impl TaskExecutor {
    /// 创建新的执行器（无依赖）
    pub fn new() -> Self {
        Self {
            llm_client: None,
            skill_registry: None,
            mcp_manager: None,
        }
    }

    /// 创建带 LLM 客户端的执行器
    pub fn with_llm_client(llm_client: Arc<agent_llm::AnthropicClient>) -> Self {
        Self {
            llm_client: Some(llm_client),
            skill_registry: None,
            mcp_manager: None,
        }
    }

    /// 创建带 Skills 注册表的执行器
    pub fn with_skill_registry(skill_registry: Arc<agent_skills::SkillRegistry>) -> Self {
        Self {
            llm_client: None,
            skill_registry: Some(skill_registry),
            mcp_manager: None,
        }
    }

    /// 创建带 MCP 管理器的执行器
    pub fn with_mcp_manager(mcp_manager: MCPClientManager) -> Self {
        Self {
            llm_client: None,
            skill_registry: None,
            mcp_manager: Some(mcp_manager),
        }
    }

    /// 创建完整配置的执行器
    pub fn with_all_dependencies(
        llm_client: Arc<agent_llm::AnthropicClient>,
        skill_registry: Arc<agent_skills::SkillRegistry>,
        mcp_manager: MCPClientManager,
    ) -> Self {
        Self {
            llm_client: Some(llm_client),
            skill_registry: Some(skill_registry),
            mcp_manager: Some(mcp_manager),
        }
    }

    /// 设置 LLM 客户端
    pub fn set_llm_client(&mut self, llm_client: Arc<agent_llm::AnthropicClient>) {
        self.llm_client = Some(llm_client);
    }

    /// 设置 Skills 注册表
    pub fn set_skill_registry(&mut self, skill_registry: Arc<agent_skills::SkillRegistry>) {
        self.skill_registry = Some(skill_registry);
    }

    /// 设置 MCP 管理器
    pub fn set_mcp_manager(&mut self, mcp_manager: MCPClientManager) {
        self.mcp_manager = Some(mcp_manager);
    }

    /// 执行单个任务
    pub async fn execute_task(&self, task: &Task) -> TaskResult {
        let start = std::time::Instant::now();

        // 应用超时
        let result = timeout(
            Duration::from_secs(task.config.timeout_secs),
            self.execute_task_inner(task),
        )
        .await;

        let elapsed = start.elapsed().as_millis() as u64;

        match result {
            Ok(Ok(output)) => TaskResult::success(task.id.clone(), output, elapsed),
            Ok(Err(e)) => TaskResult::failure(task.id.clone(), e.to_string(), elapsed),
            Err(_) => TaskResult::failure(
                task.id.clone(),
                "Task execution timeout".to_string(),
                elapsed,
            ),
        }
    }

    /// 执行任务的内部逻辑（带重试）
    async fn execute_task_inner(&self, task: &Task) -> Result<String> {
        let mut last_error = None;

        for attempt in 0..=task.config.retry_count {
            if attempt > 0 {
                // 指数退避
                let delay = Duration::from_millis(100 * 2u64.pow(attempt - 1));
                tokio::time::sleep(delay).await;
            }

            match self.execute_task_once(task).await {
                Ok(output) => return Ok(output),
                Err(e) => {
                    last_error = Some(e);
                }
            }
        }

        Err(last_error.unwrap())
    }

    /// 执行任务一次（实际的任务逻辑）
    async fn execute_task_once(&self, task: &Task) -> Result<String> {
        match &task.task_type {
            TaskType::Custom(name) => {
                // 模拟任务执行
                tokio::time::sleep(Duration::from_millis(10)).await;
                Ok(format!("Executed custom task: {}", name))
            }
            TaskType::LLMCall { prompt, model, temperature, max_tokens } => {
                self.execute_llm_call(prompt, model.as_deref(), *temperature, *max_tokens).await
            }
            TaskType::SkillExecution { skill_name, params } => {
                self.execute_skill_execution(skill_name, params.as_ref()).await
            }
            TaskType::MCPToolCall { server_name, tool_name, params } => {
                self.execute_mcp_tool_call(server_name, tool_name, params.as_ref()).await
            }
            TaskType::Subworkflow { workflow_id } => {
                bail!("Subworkflow not implemented yet: {}", workflow_id)
            }
        }
    }

    /// 执行 LLM 调用
    async fn execute_llm_call(
        &self,
        prompt: &str,
        model: Option<&str>,
        temperature: Option<f32>,
        max_tokens: Option<u32>,
    ) -> Result<String> {
        use agent_core::{
            models::{Message, MessageRole},
            traits::llm::{CompletionRequest, LLMClient},
        };
        use uuid::Uuid;

        // 获取 LLM 客户端
        let client = if let Some(ref client) = self.llm_client {
            client.clone()
        } else {
            // 如果没有预设客户端，尝试从环境变量创建
            Arc::new(
                agent_llm::AnthropicClient::from_env()
                    .map_err(|e| anyhow::anyhow!("Failed to create LLM client: {}. Set ANTHROPIC_API_KEY or configure executor with llm_client.", e))?
            )
        };

        // 构建消息
        let session_id = Uuid::new_v4(); // 临时会话 ID
        let message = Message::new(session_id, MessageRole::User, prompt.to_string());

        // 构建请求
        let mut request = CompletionRequest::new(
            vec![message],
            model.unwrap_or("claude-3-5-sonnet-20241022").to_string(),
        );

        if let Some(temp) = temperature {
            request = request.with_temperature(temp);
        }
        if let Some(tokens) = max_tokens {
            request = request.with_max_tokens(tokens);
        }

        // 调用 LLM
        let response = client.complete(request).await
            .map_err(|e| anyhow::anyhow!("LLM call failed: {}", e))?;

        Ok(response.content)
    }

    /// 执行 Skills 技能
    async fn execute_skill_execution(
        &self,
        skill_name: &str,
        params: Option<&serde_json::Value>,
    ) -> Result<String> {
        use agent_skills::SkillExecutionContext;
        use std::collections::HashMap;

        // 获取 Skills 注册表
        let registry = self.skill_registry.as_ref()
            .ok_or_else(|| anyhow::anyhow!("Skill registry not configured. Use TaskExecutor::with_skill_registry()"))?;

        // 获取技能定义
        let skill = registry.get(skill_name)
            .map_err(|e| anyhow::anyhow!("Failed to get skill '{}': {}", skill_name, e))?;

        // 解析参数
        let parameters: HashMap<String, String> = if let Some(params) = params {
            // 将 JSON 参数转换为 HashMap<String, String>
            params.as_object()
                .ok_or_else(|| anyhow::anyhow!("Skill parameters must be a JSON object"))?
                .iter()
                .map(|(k, v)| {
                    let value = match v {
                        serde_json::Value::String(s) => s.clone(),
                        serde_json::Value::Number(n) => n.to_string(),
                        serde_json::Value::Bool(b) => b.to_string(),
                        _ => v.to_string(),
                    };
                    (k.clone(), value)
                })
                .collect()
        } else {
            HashMap::new()
        };

        // 创建执行上下文
        let context = SkillExecutionContext::new(skill.clone(), parameters);

        // 验证参数
        context.validate()
            .map_err(|e| anyhow::anyhow!("Skill parameter validation failed: {}", e))?;

        // 构建提示词
        let prompt = context.build_prompt();

        Ok(prompt)
    }

    /// 执行 MCP 工具调用
    async fn execute_mcp_tool_call(
        &self,
        server_name: &str,
        tool_name: &str,
        params: Option<&serde_json::Value>,
    ) -> Result<String> {
        use agent_core::traits::MCPClient;

        // 获取 MCP 管理器
        let manager = self.mcp_manager.as_ref()
            .ok_or_else(|| anyhow::anyhow!("MCP manager not configured. Use TaskExecutor::with_mcp_manager()"))?;

        // 获取指定服务器的客户端
        let client = manager.get_client(server_name).await
            .ok_or_else(|| anyhow::anyhow!("MCP server '{}' not found. Add it to the manager first.", server_name))?;

        // 准备参数
        let args = params.cloned().unwrap_or(serde_json::json!({}));

        // 调用工具
        let result = client.call_tool(tool_name, args).await
            .map_err(|e| anyhow::anyhow!("MCP tool call failed: {}", e))?;

        // 将结果转换为字符串
        let output = if result.is_string() {
            result.as_str().unwrap().to_string()
        } else {
            serde_json::to_string_pretty(&result)
                .unwrap_or_else(|_| result.to_string())
        };

        Ok(output)
    }
}

impl Default for TaskExecutor {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_execute_simple_task() {
        let executor = TaskExecutor::new();

        let task = Task::new("test", "Test Task", TaskType::Custom("hello".to_string()));

        let result = executor.execute_task(&task).await;
        assert_eq!(result.status, TaskStatus::Completed);
        assert!(result.output.is_some());
        assert!(result.error.is_none());
    }

    #[tokio::test]
    async fn test_execute_with_timeout() {
        let executor = TaskExecutor::new();

        let mut task = Task::new("test", "Test Task", TaskType::Custom("slow".to_string()));
        task.config.timeout_secs = 1; // 1 秒超时
        // 注意：我们的模拟任务只需要 10ms，所以不会超时

        let result = executor.execute_task(&task).await;
        assert_eq!(result.status, TaskStatus::Completed);
    }

    #[tokio::test]
    async fn test_execute_skill_without_registry() {
        let executor = TaskExecutor::new();

        let task = Task::new(
            "test",
            "Test Task",
            TaskType::SkillExecution {
                skill_name: "test_skill".to_string(),
                params: None,
            },
        );

        let result = executor.execute_task(&task).await;
        assert!(matches!(result.status, TaskStatus::Failed(_)));
        assert!(result.error.is_some());
        let error = result.error.unwrap();
        assert!(
            error.contains("registry not configured"),
            "Expected 'registry not configured' in error, got: {}",
            error
        );
    }

    #[tokio::test]
    #[ignore] // 需要真实的 API Key，在 CI 中跳过
    async fn test_execute_llm_call() {
        // 只有在设置了 ANTHROPIC_API_KEY 时才运行
        if std::env::var("ANTHROPIC_API_KEY").is_err() {
            return;
        }

        let executor = TaskExecutor::new();

        let task = Task::new(
            "test",
            "Test LLM Call",
            TaskType::LLMCall {
                prompt: "What is 2+2? Answer with just the number.".to_string(),
                model: Some("claude-3-5-sonnet-20241022".to_string()),
                temperature: Some(0.0),
                max_tokens: Some(10),
            },
        );

        let result = executor.execute_task(&task).await;
        assert_eq!(result.status, TaskStatus::Completed);
        assert!(result.output.is_some());
        let output = result.output.unwrap();
        assert!(output.contains("4"), "Expected '4' in output, got: {}", output);
    }

    #[tokio::test]
    async fn test_execute_skill() {
        use agent_skills::{SkillDefinition, SkillParameter, SkillRegistry};
        use std::sync::Arc;

        // 创建测试技能
        let mut skill = SkillDefinition::new(
            "greeting".to_string(),
            "Greet the user".to_string(),
        );
        skill.content = "Hello {name}! Your age is {age}.".to_string();
        skill.parameters.push(SkillParameter::new(
            "name".to_string(),
            "string".to_string(),
            true,
            "User's name".to_string(),
        ));
        skill.parameters.push(SkillParameter::new(
            "age".to_string(),
            "number".to_string(),
            true,
            "User's age".to_string(),
        ));

        // 创建注册表并注册技能
        let mut registry = SkillRegistry::new();
        registry.register(skill);

        // 创建执行器
        let executor = TaskExecutor::with_skill_registry(Arc::new(registry));

        // 创建任务
        let task = Task::new(
            "test",
            "Test Skill",
            TaskType::SkillExecution {
                skill_name: "greeting".to_string(),
                params: Some(serde_json::json!({
                    "name": "Alice",
                    "age": "25"
                })),
            },
        );

        // 执行任务
        let result = executor.execute_task(&task).await;
        assert_eq!(result.status, TaskStatus::Completed);
        assert!(result.output.is_some());
        let output = result.output.unwrap();
        assert_eq!(output, "Hello Alice! Your age is 25.");
    }

    #[tokio::test]
    async fn test_execute_skill_missing_parameter() {
        use agent_skills::{SkillDefinition, SkillParameter, SkillRegistry};
        use std::sync::Arc;

        // 创建测试技能
        let mut skill = SkillDefinition::new(
            "greeting".to_string(),
            "Greet the user".to_string(),
        );
        skill.content = "Hello {name}!".to_string();
        skill.parameters.push(SkillParameter::new(
            "name".to_string(),
            "string".to_string(),
            true,
            "User's name".to_string(),
        ));

        // 创建注册表并注册技能
        let mut registry = SkillRegistry::new();
        registry.register(skill);

        // 创建执行器
        let executor = TaskExecutor::with_skill_registry(Arc::new(registry));

        // 创建任务（缺少必需参数）
        let task = Task::new(
            "test",
            "Test Skill",
            TaskType::SkillExecution {
                skill_name: "greeting".to_string(),
                params: None, // 缺少必需参数
            },
        );

        // 执行任务
        let result = executor.execute_task(&task).await;
        assert!(matches!(result.status, TaskStatus::Failed(_)));
        assert!(result.error.is_some());
        let error = result.error.unwrap();
        assert!(
            error.contains("missing"),
            "Expected 'missing' in error, got: {}",
            error
        );
    }
}
