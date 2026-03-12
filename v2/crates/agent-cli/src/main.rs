//! Agent CLI 应用

use agent_core::traits::llm::LLMClient;
use agent_llm::{AnthropicClient, OllamaClient};
use agent_skills::{SkillLoader, SkillRegistry};
use agent_storage::Database;
use agent_workflow::{AgentRuntime, ConversationConfig, ConversationFlow};
use anyhow::{Context, Result};
use clap::{Parser, Subcommand};
use colored::*;
use std::path::PathBuf;
use std::sync::Arc;
use uuid::Uuid;

#[derive(Parser)]
#[command(name = "agent")]
#[command(about = "General Agent - AI 对话助手", long_about = None)]
struct Cli {
    #[command(subcommand)]
    command: Commands,

    #[arg(long, env = "AGENT_DB", default_value = "agent.db")]
    db_path: String,

    /// LLM 提供商 (anthropic/ollama)
    #[arg(long, env = "AGENT_PROVIDER", default_value = "ollama")]
    provider: String,

    /// API Key (仅 anthropic 需要)
    #[arg(long, env = "ANTHROPIC_API_KEY")]
    api_key: Option<String>,

    /// Ollama 模型名称
    #[arg(long, env = "OLLAMA_MODEL", default_value = "qwen3.5:0.8b")]
    ollama_model: String,

    /// Ollama 服务地址
    #[arg(long, env = "OLLAMA_BASE_URL", default_value = "http://localhost:11434")]
    ollama_url: String,

    /// 技能文件目录（可选）
    #[arg(long, value_name = "DIR")]
    skills_dir: Option<PathBuf>,
}

#[derive(Subcommand)]
enum Commands {
    /// 创建新会话
    New {
        /// 会话标题
        #[arg(short, long)]
        title: Option<String>,
    },
    /// 列出所有会话
    List {
        /// 显示数量
        #[arg(short, long, default_value = "10")]
        limit: u32,
    },
    /// 开始对话
    Chat {
        /// 会话 ID
        session_id: String,
        /// 是否使用流式输出
        #[arg(short, long)]
        stream: bool,
    },
    /// 删除会话
    Delete {
        /// 会话 ID
        session_id: String,
    },
    /// 搜索会话
    Search {
        /// 搜索关键词
        query: String,
        #[arg(short, long, default_value = "10")]
        limit: u32,
    },
}

struct App {
    runtime: Arc<AgentRuntime>,
}

impl App {
    async fn new(cli: &Cli) -> Result<Self> {
        // 初始化数据库
        let db = Database::new(&cli.db_path)
            .await
            .context("Failed to connect to database")?;
        db.migrate().await.context("Failed to run migrations")?;

        // 创建 LLM 客户端
        let llm_client: Arc<dyn LLMClient> = match cli.provider.as_str() {
            "anthropic" => {
                if let Some(key) = &cli.api_key {
                    Arc::new(AnthropicClient::from_api_key(key.clone())?)
                } else {
                    Arc::new(AnthropicClient::from_env()?)
                }
            }
            "ollama" => {
                let config = agent_llm::ollama::OllamaConfig::new(cli.ollama_model.clone())
                    .with_base_url(cli.ollama_url.clone());
                Arc::new(OllamaClient::new(config)?)
            }
            _ => anyhow::bail!("Unknown provider: {}", cli.provider),
        };

        println!("{} {}", "✓ 使用提供商:".green(), cli.provider.cyan());
        if cli.provider == "ollama" {
            println!("{} {}", "  模型:".dimmed(), cli.ollama_model.yellow());
        }

        // 加载技能（如果指定了目录）
        let skill_registry = if let Some(skills_dir) = &cli.skills_dir {
            let loader = SkillLoader::new(skills_dir.clone())
                .context("Failed to create skill loader")?;
            let skills = loader.load_all()
                .context("Failed to load skills")?;

            let mut registry = SkillRegistry::new();
            for skill in &skills {
                registry.register(skill.clone());
            }

            println!("{} {} skills from {}",
                "✓ 加载技能:".green(),
                skills.len().to_string().cyan(),
                skills_dir.display().to_string().yellow()
            );

            Some(Arc::new(registry))
        } else {
            None
        };

        println!();

        // 创建 AgentRuntime
        let runtime = AgentRuntime::new(db, llm_client, skill_registry)
            .await
            .context("Failed to create AgentRuntime")?;

        Ok(Self {
            runtime: Arc::new(runtime),
        })
    }

    async fn cmd_new(&self, title: Option<String>) -> Result<()> {
        let session = self.runtime.session_manager().create_session(title).await?;

        println!("{}", "✓ 会话创建成功".green().bold());
        println!("ID: {}", session.id.to_string().cyan());
        if let Some(t) = session.title {
            println!("标题: {}", t.yellow());
        }

        Ok(())
    }

    async fn cmd_list(&self, limit: u32) -> Result<()> {
        let sessions = self.runtime.session_manager().list_sessions(limit, 0).await?;

        if sessions.is_empty() {
            println!("{}", "没有找到会话".yellow());
            return Ok(());
        }

        println!("{}", "会话列表:".bold());
        println!();

        for session in sessions {
            let msg_count = self
                .runtime
                .session_manager()
                .count_messages(session.id)
                .await
                .unwrap_or(0);

            println!("  {} {}", "●".cyan(), session.id.to_string().cyan());
            if let Some(title) = session.title {
                println!("    标题: {}", title.yellow());
            }
            println!(
                "    消息数: {} | 更新: {}",
                msg_count.to_string().green(),
                session.updated_at.format("%Y-%m-%d %H:%M").to_string().dimmed()
            );
            println!();
        }

        Ok(())
    }

    async fn cmd_chat(&self, session_id_str: &str, use_stream: bool) -> Result<()> {
        let session_id = Uuid::parse_str(session_id_str).context("Invalid session ID")?;

        // 验证会话存在
        let session = self.runtime.session_manager().load_session(session_id).await?;

        println!("{}", "进入对话模式 (输入 'exit' 退出)".green().bold());
        if let Some(title) = session.title {
            println!("会话: {}", title.yellow());
        }
        println!();

        // 创建对话流程
        let config = ConversationConfig::default();
        let mut flow = ConversationFlow::new(
            self.runtime.session_manager().clone(),
            self.runtime.llm_client().clone(),
            config,
        );

        // 如果启用了技能系统，添加到 flow
        if let Some(registry) = self.runtime.skill_registry() {
            flow = flow.with_skills(registry.clone());
        }

        // 对话循环
        loop {
            print!("{} ", "You:".blue().bold());
            std::io::Write::flush(&mut std::io::stdout())?;

            let mut input = String::new();
            std::io::stdin().read_line(&mut input)?;
            let input = input.trim();

            if input.is_empty() {
                continue;
            }

            if input.eq_ignore_ascii_case("exit") {
                println!("{}", "再见！".green());
                break;
            }

            // 检查是否为 subagent 命令
            if input.starts_with("/subagent") {
                match self.handle_subagent_command(session_id, input).await {
                    Ok(response) => {
                        println!("{}", response.green());
                        println!();
                    }
                    Err(e) => {
                        println!("{} {}", "错误:".red(), e);
                        println!();
                    }
                }
                continue;
            }

            print!("{} ", "AI:".cyan().bold());
            std::io::Write::flush(&mut std::io::stdout())?;

            if use_stream {
                // 流式输出
                let (mut stream, context) = flow
                    .send_message_stream(session_id, input.to_string())
                    .await?;

                let mut full_response = String::new();

                while let Some(chunk) = stream.next().await? {
                    if !chunk.is_final {
                        print!("{}", chunk.delta);
                        std::io::Write::flush(&mut std::io::stdout())?;
                        full_response.push_str(&chunk.delta);
                    }
                }

                println!();
                println!();

                // 保存响应
                context.save_response(full_response).await?;
            } else {
                // 非流式输出
                let response = flow.send_message(session_id, input.to_string()).await?;
                println!("{}", response);
                println!();
            }
        }

        Ok(())
    }

    async fn cmd_delete(&self, session_id_str: &str) -> Result<()> {
        let session_id = Uuid::parse_str(session_id_str).context("Invalid session ID")?;

        self.runtime.session_manager().delete_session(session_id).await?;

        println!("{}", "✓ 会话已删除".green().bold());

        Ok(())
    }

    async fn cmd_search(&self, query: &str, limit: u32) -> Result<()> {
        let sessions = self.runtime.session_manager().search_sessions(query, limit).await?;

        if sessions.is_empty() {
            println!("{}", "没有找到匹配的会话".yellow());
            return Ok(());
        }

        println!("{} '{}':", "搜索结果".bold(), query.yellow());
        println!();

        for session in sessions {
            println!("  {} {}", "●".cyan(), session.id.to_string().cyan());
            if let Some(title) = session.title {
                println!("    标题: {}", title.yellow());
            }
            println!();
        }

        Ok(())
    }

    /// 处理 subagent 命令
    async fn handle_subagent_command(
        &self,
        parent_session_id: Uuid,
        input: &str,
    ) -> Result<String> {
        use agent_workflow::{parse_subagent_command, SubagentCommand};

        // 解析命令
        let command = parse_subagent_command(input)?;

        match command {
            SubagentCommand::Start { tasks, timeout_secs, model } => {
                println!("{}", "正在启动 subagent...".yellow());

                // 构建配置
                let mut config = agent_workflow::subagent::SubagentConfig {
                    title: "CLI Subagent Stage".to_string(),
                    initial_prompt: "Execute the given task".to_string(),
                    shared_context: agent_workflow::subagent::SharedContext::default(),
                    llm_config: agent_workflow::subagent::LLMConfig::default(),
                    keep_alive: false,
                    timeout: None,
                };

                if let Some(timeout) = timeout_secs {
                    config.timeout = Some(std::time::Duration::from_secs(timeout));
                }
                if let Some(model_name) = model {
                    config.llm_config.model = model_name;
                }

                // 创建并执行 Stage
                let stage_id = self.runtime.orchestrator()
                    .create_and_execute_stage(
                        parent_session_id,
                        tasks.clone(),
                        Some(config),
                    )
                    .await?;

                Ok(format!(
                    "✓ 已启动 {} 个 subagent (Stage ID: {})\n提示: 状态将在后台更新",
                    tasks.len(),
                    stage_id.to_string().chars().take(8).collect::<String>()
                ))
            }
        }
    }
}

#[tokio::main]
async fn main() -> Result<()> {
    // 初始化日志
    tracing_subscriber::fmt()
        .with_env_filter(
            tracing_subscriber::EnvFilter::from_default_env()
                .add_directive(tracing::Level::WARN.into()),
        )
        .init();

    let cli = Cli::parse();
    let app = App::new(&cli).await?;

    match cli.command {
        Commands::New { title } => app.cmd_new(title).await?,
        Commands::List { limit } => app.cmd_list(limit).await?,
        Commands::Chat { session_id, stream } => app.cmd_chat(&session_id, stream).await?,
        Commands::Delete { session_id } => app.cmd_delete(&session_id).await?,
        Commands::Search { query, limit } => app.cmd_search(&query, limit).await?,
    }

    Ok(())
}
