//! Agent CLI 应用

use agent_context_compression::StrategyType;
use agent_core::traits::llm::LLMClient;
use agent_file_storage::{
    AccessLevel, FileRepository, FileService, FileStorage,
};
use agent_scheduled_tasks::{
    TaskPayload, TaskRepository as ScheduledTaskRepository, TaskService, TaskStatus, TaskType,
};
use agent_skill_extraction::{
    ExtractionRepository, ExtractionService, ExtractionStatus, LlmSkillExtractor,
};
use agent_llm::{AnthropicClient, OllamaClient};
use agent_memory::{
    Memory, MemoryExtractor, MemoryService, MemoryType, SqliteMemoryRepository, VectorMemoryStore,
};
use agent_skills::{SkillLoader, SkillRegistry};
use agent_storage::Database;
use agent_workflow::{AgentRuntime, ConversationConfig, ConversationFlow};
use anyhow::{Context, Result};
use clap::{Parser, Subcommand};
use colored::*;
use std::path::{Path, PathBuf};
use std::sync::Arc;
use tokio::sync::Mutex;
use uuid::Uuid;

fn default_upload_dir(subdir: &str) -> PathBuf {
    let base = std::env::var("HOME")
        .map(PathBuf::from)
        .unwrap_or_else(|_| PathBuf::from("."))
        .join(".agent-v2");
    base.join(subdir)
}

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

    /// 记忆数据库路径
    #[arg(long, env = "AGENT_MEMORY_DB", default_value = "memory.db")]
    memory_db: String,

    /// 文件存储数据库路径
    #[arg(long, env = "AGENT_FILE_DB", default_value = "files.db")]
    file_db: String,

    /// 文件上传目录
    #[arg(long, env = "AGENT_UPLOAD_DIR")]
    upload_dir: Option<PathBuf>,

    /// 最大文件大小（字节，默认 50MB）
    #[arg(long, env = "AGENT_MAX_FILE_SIZE", default_value = "52428800")]
    max_file_size: i64,
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
    /// 压缩会话上下文
    Compress {
        /// 会话 ID
        session_id: String,
        /// 压缩策略 (sliding_window/semantic/hierarchical)
        #[arg(short = 'S', long, default_value = "hierarchical")]
        strategy: String,
    },
    /// 查看压缩状态和历史
    CompressStatus {
        /// 会话 ID
        session_id: String,
    },

    // === 长期记忆命令 ===

    /// 列出记忆
    MemoryList {
        /// 记忆类型过滤 (user/feedback/project/reference/knowledge)
        #[arg(short = 't', long)]
        memory_type: Option<String>,
        /// 显示数量
        #[arg(short, long, default_value = "20")]
        limit: u32,
    },
    /// 查看记忆详情
    MemoryShow {
        /// 记忆 ID
        id: String,
    },
    /// 添加记忆
    MemoryAdd {
        /// 记忆类型 (user/feedback/project/reference/knowledge)
        #[arg(short = 't', long)]
        memory_type: String,
        /// 记忆内容
        content: String,
        /// 来源
        #[arg(short, long)]
        source: Option<String>,
    },
    /// 更新记忆
    MemoryUpdate {
        /// 记忆 ID
        id: String,
        /// 新内容
        content: String,
    },
    /// 删除记忆
    MemoryDelete {
        /// 记忆 ID
        id: String,
    },
    /// 关键词搜索记忆
    MemorySearch {
        /// 搜索关键词
        query: String,
        /// 结果数量
        #[arg(short, long, default_value = "10")]
        limit: u32,
    },
    /// 语义搜索记忆
    MemorySemanticSearch {
        /// 查询文本
        query: String,
        /// 结果数量
        #[arg(short, long, default_value = "5")]
        top_k: usize,
    },
    /// 混合搜索记忆
    MemoryHybridSearch {
        /// 查询文本
        query: String,
        /// 结果数量
        #[arg(short, long, default_value = "5")]
        top_k: usize,
    },
    /// 从会话中提取记忆
    MemoryExtract {
        /// 会话 ID
        session_id: String,
    },
    /// 查找与上下文相关的记忆
    MemoryRelevant {
        /// 上下文文本
        context: String,
        /// 结果数量
        #[arg(short, long, default_value = "5")]
        top_k: usize,
    },
    /// 查看记忆统计
    MemoryStats,

    // === 文件存储命令 ===

    /// 上传文件
    FileUpload {
        /// 文件路径
        path: PathBuf,
        /// 访问级别 (private/shared/public)
        #[arg(short, long, default_value = "private")]
        access_level: String,
        /// 文件描述
        #[arg(short, long)]
        description: Option<String>,
    },
    /// 列出文件
    FileList {
        /// 访问级别过滤
        #[arg(short, long)]
        level: Option<String>,
    },
    /// 查看文件详情
    FileShow {
        /// 文件 ID
        id: String,
    },
    /// 查看文件内容
    FileContent {
        /// 文件 ID
        id: String,
        /// 指定版本号
        #[arg(short, long)]
        version: Option<i32>,
    },
    /// 删除文件
    FileDelete {
        /// 文件 ID
        id: String,
    },
    /// 搜索文件
    FileSearch {
        /// 搜索关键词
        keyword: String,
    },
    /// 分享文件给用户
    FileShare {
        /// 文件 ID
        id: String,
        /// 目标用户
        #[arg(long)]
        user: String,
        /// 权限类型 (read/write)
        #[arg(long, default_value = "read")]
        permission: String,
    },
    /// 撤销文件权限
    FileRevoke {
        /// 文件 ID
        id: String,
        /// 目标用户
        #[arg(long)]
        user: String,
        /// 权限类型 (read/write)
        #[arg(long, default_value = "read")]
        permission: String,
    },
    /// 查看文件权限
    FilePermissions {
        /// 文件 ID
        id: String,
    },
    /// 查看文件版本历史
    FileVersions {
        /// 文件 ID
        id: String,
    },
    /// 恢复文件到指定版本
    FileRestore {
        /// 文件 ID
        id: String,
        /// 目标版本号
        #[arg(long)]
        version: i32,
    },
    /// 文件存储统计
    FileStats,

    // === 技能抽取命令 ===

    /// 从会话中抽取技能
    SkillExtract {
        /// 会话 ID
        session_id: String,
        /// 抽取提示（可选）
        #[arg(short = 'H', long)]
        hint: Option<String>,
        /// 强制覆盖已有技能
        #[arg(short, long)]
        force: bool,
    },
    /// 查看抽取历史
    SkillHistory {
        /// 状态过滤 (success/failed/pending)
        #[arg(short, long)]
        status: Option<String>,
        /// 显示数量
        #[arg(short, long, default_value = "20")]
        limit: u32,
    },
    /// 查看抽取统计
    SkillStats,

    // === 计划任务命令 ===

    /// 创建计划任务
    TaskCreate {
        /// 任务名称
        name: String,
        /// 调度表达式（Cron 或自然语言，如 "0 9 * * *" 或 "每天上午9点"）
        #[arg(short, long)]
        schedule: String,
        /// 任务类型 (skill/reminder/command)
        #[arg(short = 't', long, default_value = "command")]
        task_type: String,
        /// 任务负载（技能名/提醒内容/命令）
        #[arg(short, long)]
        payload: String,
        /// 任务描述
        #[arg(short, long)]
        description: Option<String>,
    },
    /// 列出计划任务
    TaskList {
        /// 状态过滤 (pending/running/completed/failed/paused)
        #[arg(short, long)]
        status: Option<String>,
    },
    /// 查看任务详情
    TaskShow {
        /// 任务 ID
        id: String,
    },
    /// 暂停任务
    TaskPause {
        /// 任务 ID
        id: String,
    },
    /// 恢复任务
    TaskResume {
        /// 任务 ID
        id: String,
    },
    /// 删除任务
    TaskDelete {
        /// 任务 ID
        id: String,
    },
    /// 查看任务执行历史
    TaskHistory {
        /// 任务 ID
        id: String,
        /// 显示数量
        #[arg(short, long, default_value = "10")]
        limit: u32,
    },
    /// 查看任务统计
    TaskStats,

    // === TUI 模式 ===

    /// 启动 TUI 终端界面
    Tui,
}

struct App {
    runtime: Arc<AgentRuntime>,
    memory_service: Arc<Mutex<MemoryService>>,
    file_service: Arc<FileService>,
    extraction_service: Arc<ExtractionService>,
    task_service: Arc<TaskService>,
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

        // 初始化记忆服务
        let memory_db_url = format!("sqlite:{}?mode=rwc", cli.memory_db);
        let memory_pool = sqlx::SqlitePool::connect(&memory_db_url)
            .await
            .context("Failed to connect to memory database")?;
        sqlx::migrate!("../agent-memory/migrations")
            .run(&memory_pool)
            .await
            .context("Failed to run memory migrations")?;

        let memory_repo: Arc<dyn agent_memory::MemoryRepository> =
            Arc::new(SqliteMemoryRepository::new(memory_pool));
        let vector_store = VectorMemoryStore::new(memory_repo.clone());
        let extractor = MemoryExtractor::new(llm_client.clone(), "default".to_string());

        let mut memory_service = MemoryService::new(memory_repo, vector_store, extractor);
        memory_service.initialize().await.ok();

        let memory_service = Arc::new(Mutex::new(memory_service));
        println!("{}", "✓ 长期记忆服务已启用".green());

        // 初始化文件存储服务
        let upload_dir = cli.upload_dir.clone().unwrap_or_else(|| {
            default_upload_dir("uploads")
        });
        let file_db_url = format!("sqlite:{}?mode=rwc", cli.file_db);
        let file_pool = sqlx::SqlitePool::connect(&file_db_url)
            .await
            .context("Failed to connect to file database")?;
        sqlx::migrate!("../agent-file-storage/migrations")
            .run(&file_pool)
            .await
            .context("Failed to run file storage migrations")?;

        let file_repo = FileRepository::new(file_pool);
        let file_storage = FileStorage::new(&upload_dir, cli.max_file_size)
            .await
            .context("Failed to initialize file storage")?;
        let file_service = Arc::new(FileService::new(file_repo, file_storage));
        println!("{} {}", "✓ 文件存储已启用:".green(), upload_dir.display().to_string().dimmed());

        // 初始化技能抽取服务
        let extraction_db_url = "sqlite:extraction.db?mode=rwc";
        let extraction_pool = sqlx::SqlitePool::connect(extraction_db_url)
            .await
            .context("Failed to connect to extraction database")?;
        sqlx::migrate!("../agent-skill-extraction/migrations")
            .run(&extraction_pool)
            .await
            .context("Failed to run extraction migrations")?;

        let extraction_repo = ExtractionRepository::new(extraction_pool);
        let skills_dir_for_extraction = cli.skills_dir.clone().unwrap_or_else(|| {
            default_upload_dir("skills")
        });
        let extractor = Arc::new(LlmSkillExtractor::new(
            llm_client.clone(),
            cli.ollama_model.clone(),
        ));
        let extraction_service = Arc::new(ExtractionService::new(
            extractor,
            extraction_repo,
            skills_dir_for_extraction,
        ));
        println!("{}", "✓ 技能抽取服务已启用".green());

        // 初始化计划任务服务
        let task_db_url = "sqlite:tasks.db?mode=rwc";
        let task_pool = sqlx::SqlitePool::connect(task_db_url)
            .await
            .context("Failed to connect to task database")?;
        sqlx::migrate!("../agent-scheduled-tasks/migrations")
            .run(&task_pool)
            .await
            .context("Failed to run task migrations")?;

        let task_repo = ScheduledTaskRepository::new(task_pool);
        let task_service = Arc::new(TaskService::new(task_repo));
        println!("{}", "✓ 计划任务服务已启用".green());

        // 创建 AgentRuntime
        let runtime = AgentRuntime::new(db, llm_client, skill_registry)
            .await
            .context("Failed to create AgentRuntime")?;

        Ok(Self {
            runtime: Arc::new(runtime),
            memory_service,
            file_service,
            extraction_service,
            task_service,
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

        // 启用上下文压缩
        if let Ok(compression) = agent_context_compression::CompressionService::new(
            self.runtime.llm_client().clone(),
            agent_context_compression::CompressionConfig::default(),
        ) {
            flow = flow.with_compression(compression);
            println!("{}", "  ✓ 上下文压缩已启用".dimmed());
        }

        // 启用长期记忆
        {
            let memory_pool_url = "sqlite:memory.db?mode=rwc";
            if let Ok(pool) = sqlx::SqlitePool::connect(memory_pool_url).await {
                let _ = sqlx::migrate!("../agent-memory/migrations").run(&pool).await;
                let repo: Arc<dyn agent_memory::MemoryRepository> =
                    Arc::new(SqliteMemoryRepository::new(pool));
                let vs = VectorMemoryStore::new(repo.clone());
                let ext = MemoryExtractor::new(self.runtime.llm_client().clone(), "default".to_string());
                let mut ms = MemoryService::new(repo, vs, ext);
                ms.initialize().await.ok();
                flow = flow.with_memory(ms);
                println!("{}", "  ✓ 长期记忆已启用".dimmed());
            }
        }

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

    async fn cmd_compress(&self, session_id_str: &str, strategy_str: &str) -> Result<()> {
        let session_id = Uuid::parse_str(session_id_str).context("Invalid session ID")?;

        // 验证会话存在
        let session = self.runtime.session_manager().load_session(session_id).await?;
        let title = session.title.unwrap_or_else(|| "无标题".to_string());

        let strategy = match strategy_str {
            "sliding_window" => StrategyType::SlidingWindow,
            "semantic" => StrategyType::Semantic,
            "hierarchical" => StrategyType::Hierarchical,
            _ => anyhow::bail!("未知策略: {}（可选: sliding_window, semantic, hierarchical）", strategy_str),
        };

        println!("{} {}", "压缩会话:".bold(), title.yellow());
        println!("{} {:?}", "策略:".dimmed(), strategy);
        println!();

        // 创建带压缩的 flow
        let config = ConversationConfig::default();
        let compression = agent_context_compression::CompressionService::new(
            self.runtime.llm_client().clone(),
            agent_context_compression::CompressionConfig::default(),
        ).context("Failed to create compression service")?;

        let flow = ConversationFlow::new(
            self.runtime.session_manager().clone(),
            self.runtime.llm_client().clone(),
            config,
        ).with_compression(compression);

        let result = flow.compress_session(session_id, Some(strategy)).await?;

        println!("{}", "✓ 压缩完成".green().bold());
        println!("  原始消息数: {}", result.original_count.to_string().cyan());
        println!("  压缩后消息数: {}", result.compressed_count.to_string().cyan());
        println!("  原始 Token 数: {}", result.original_tokens.to_string().yellow());
        println!("  压缩后 Token 数: {}", result.compressed_tokens.to_string().yellow());
        println!(
            "  压缩率: {}",
            format!("{:.1}%", result.compression_ratio * 100.0).green()
        );
        let saved = result.original_tokens.saturating_sub(result.compressed_tokens);
        println!("  节省 Token: {}", saved.to_string().green().bold());

        Ok(())
    }

    async fn cmd_compress_status(&self, session_id_str: &str) -> Result<()> {
        let session_id = Uuid::parse_str(session_id_str).context("Invalid session ID")?;

        // 验证会话存在
        let session = self.runtime.session_manager().load_session(session_id).await?;
        let title = session.title.unwrap_or_else(|| "无标题".to_string());

        // 创建带压缩的 flow 来估算 token
        let config = ConversationConfig::default();
        let compression = agent_context_compression::CompressionService::new(
            self.runtime.llm_client().clone(),
            agent_context_compression::CompressionConfig::default(),
        ).context("Failed to create compression service")?;

        let flow = ConversationFlow::new(
            self.runtime.session_manager().clone(),
            self.runtime.llm_client().clone(),
            config,
        ).with_compression(compression);

        let messages = self.runtime.session_manager().get_messages(session_id, None).await?;
        let token_count = flow.estimate_session_tokens(session_id).await?;

        println!("{} {}", "会话压缩状态:".bold(), title.yellow());
        println!();
        println!("  消息数: {}", messages.len().to_string().cyan());
        println!("  Token 数: {}", token_count.to_string().yellow());

        let compression_config = agent_context_compression::CompressionConfig::default();
        let needs_compression = messages.len() >= compression_config.auto_trigger_threshold;
        if needs_compression {
            println!(
                "  状态: {} (超过阈值 {})",
                "建议压缩".red().bold(),
                compression_config.auto_trigger_threshold
            );
        } else {
            println!(
                "  状态: {} (阈值 {})",
                "无需压缩".green(),
                compression_config.auto_trigger_threshold
            );
        }

        // 显示压缩历史
        let history = flow.compression_history().await;
        if !history.is_empty() {
            println!();
            println!("  {}", "压缩历史:".bold());
            for record in &history {
                println!(
                    "    {} | 策略: {} | {} -> {} msgs | 压缩率: {:.1}%",
                    record.timestamp.format("%Y-%m-%d %H:%M"),
                    record.strategy_used.cyan(),
                    record.original_message_count,
                    record.compressed_message_count,
                    record.compression_ratio * 100.0,
                );
            }
        }

        Ok(())
    }

    // === 长期记忆命令 ===

    fn parse_memory_type(s: &str) -> Result<MemoryType> {
        MemoryType::from_str(s)
            .ok_or_else(|| anyhow::anyhow!("未知记忆类型: {}（可选: user, feedback, project, reference, knowledge）", s))
    }

    async fn cmd_memory_list(&self, memory_type: Option<String>, limit: u32) -> Result<()> {
        let service = self.memory_service.lock().await;

        let memories = if let Some(ref mt_str) = memory_type {
            let mt = Self::parse_memory_type(mt_str)?;
            service.list_by_type(mt, limit).await
        } else {
            use agent_memory::MemoryQuery;
            service.list(&MemoryQuery::default().with_limit(limit)).await
        }
        .map_err(|e| anyhow::anyhow!("{}", e))?;

        if memories.is_empty() {
            println!("{}", "没有找到记忆".yellow());
            return Ok(());
        }

        println!("{} (共 {} 条)", "记忆列表:".bold(), memories.len().to_string().cyan());
        println!();

        for m in &memories {
            println!("  {} {}", "●".cyan(), m.id.to_string()[..8].cyan());
            println!("    类型: {} | 更新: {}",
                format!("{}", m.memory_type).yellow(),
                m.updated_at.format("%Y-%m-%d %H:%M").to_string().dimmed()
            );
            let preview: String = m.content.chars().take(80).collect();
            println!("    内容: {}", preview);
            println!();
        }

        Ok(())
    }

    async fn cmd_memory_show(&self, id_str: &str) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid memory ID")?;
        let service = self.memory_service.lock().await;

        let memory = service.get(id).await.map_err(|e| anyhow::anyhow!("{}", e))?;

        match memory {
            Some(m) => {
                println!("{}", "记忆详情:".bold());
                println!("  ID:       {}", m.id.to_string().cyan());
                println!("  类型:     {}", format!("{}", m.memory_type).yellow());
                println!("  内容:     {}", m.content);
                if let Some(ref src) = m.source {
                    println!("  来源:     {}", src.dimmed());
                }
                if let Some(ref sid) = m.session_id {
                    println!("  会话 ID:  {}", sid.to_string().dimmed());
                }
                println!("  创建时间: {}", m.created_at.format("%Y-%m-%d %H:%M:%S"));
                println!("  更新时间: {}", m.updated_at.format("%Y-%m-%d %H:%M:%S"));
            }
            None => {
                println!("{}", "记忆不存在".red());
            }
        }

        Ok(())
    }

    async fn cmd_memory_add(&self, type_str: &str, content: String, source: Option<String>) -> Result<()> {
        let mt = Self::parse_memory_type(type_str)?;
        let mut memory = Memory::new(mt, content);
        if let Some(src) = source {
            memory = memory.with_source(src);
        }

        let service = self.memory_service.lock().await;
        let created = service.create(memory).await.map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{}", "✓ 记忆已创建".green().bold());
        println!("  ID: {}", created.id.to_string().cyan());
        println!("  类型: {}", format!("{}", created.memory_type).yellow());

        Ok(())
    }

    async fn cmd_memory_update(&self, id_str: &str, content: String) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid memory ID")?;
        let service = self.memory_service.lock().await;

        let memory = service.get(id).await.map_err(|e| anyhow::anyhow!("{}", e))?;
        match memory {
            Some(mut m) => {
                m.content = content;
                m.updated_at = chrono::Utc::now();
                service.update(&m).await.map_err(|e| anyhow::anyhow!("{}", e))?;
                println!("{}", "✓ 记忆已更新".green().bold());
            }
            None => {
                println!("{}", "记忆不存在".red());
            }
        }

        Ok(())
    }

    async fn cmd_memory_delete(&self, id_str: &str) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid memory ID")?;
        let service = self.memory_service.lock().await;
        service.delete(id).await.map_err(|e| anyhow::anyhow!("{}", e))?;
        println!("{}", "✓ 记忆已删除".green().bold());
        Ok(())
    }

    async fn cmd_memory_search(&self, query: &str, limit: u32) -> Result<()> {
        let service = self.memory_service.lock().await;
        let results = service.search_keyword(query, limit).await.map_err(|e| anyhow::anyhow!("{}", e))?;

        if results.is_empty() {
            println!("{}", "没有找到匹配的记忆".yellow());
            return Ok(());
        }

        println!("{} '{}' (共 {} 条)", "关键词搜索结果:".bold(), query.yellow(), results.len().to_string().cyan());
        println!();

        for m in &results {
            println!("  {} {} [{}]", "●".cyan(), m.id.to_string()[..8].cyan(), format!("{}", m.memory_type).yellow());
            let preview: String = m.content.chars().take(100).collect();
            println!("    {}", preview);
            println!();
        }

        Ok(())
    }

    async fn cmd_memory_semantic_search(&self, query: &str, top_k: usize) -> Result<()> {
        let service = self.memory_service.lock().await;
        let results = service.search_semantic(query, top_k).await.map_err(|e| anyhow::anyhow!("{}", e))?;

        if results.is_empty() {
            println!("{}", "没有找到语义匹配的记忆".yellow());
            return Ok(());
        }

        println!("{} '{}' (共 {} 条)", "语义搜索结果:".bold(), query.yellow(), results.len().to_string().cyan());
        if !service.is_vector_available() {
            println!("{}", "  (向量服务不可用，已降级到关键词搜索)".dimmed());
        }
        println!();

        for m in &results {
            println!("  {} {} [{}]", "●".cyan(), m.id.to_string()[..8].cyan(), format!("{}", m.memory_type).yellow());
            let preview: String = m.content.chars().take(100).collect();
            println!("    {}", preview);
            println!();
        }

        Ok(())
    }

    async fn cmd_memory_hybrid_search(&self, query: &str, top_k: usize) -> Result<()> {
        let service = self.memory_service.lock().await;
        let results = service.search_hybrid(query, top_k).await.map_err(|e| anyhow::anyhow!("{}", e))?;

        if results.is_empty() {
            println!("{}", "没有找到匹配的记忆".yellow());
            return Ok(());
        }

        println!("{} '{}' (共 {} 条)", "混合搜索结果:".bold(), query.yellow(), results.len().to_string().cyan());
        println!();

        for m in &results {
            println!("  {} {} [{}]", "●".cyan(), m.id.to_string()[..8].cyan(), format!("{}", m.memory_type).yellow());
            let preview: String = m.content.chars().take(100).collect();
            println!("    {}", preview);
            println!();
        }

        Ok(())
    }

    async fn cmd_memory_extract(&self, session_id_str: &str) -> Result<()> {
        let session_id = Uuid::parse_str(session_id_str).context("Invalid session ID")?;

        let messages = self.runtime.session_manager().get_messages(session_id, None).await?;
        if messages.is_empty() {
            println!("{}", "会话中没有消息".yellow());
            return Ok(());
        }

        println!("{} ({} 条消息)...", "正在提取记忆".yellow(), messages.len());

        let service = self.memory_service.lock().await;
        let saved = service
            .extract_from_messages(&messages, Some(session_id))
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        if saved.is_empty() {
            println!("{}", "未提取到记忆".yellow());
        } else {
            println!("{} 提取了 {} 条记忆", "✓".green(), saved.len().to_string().cyan().bold());
            for m in &saved {
                println!("  {} [{}] {}", "●".green(), format!("{}", m.memory_type).yellow(), m.content);
            }
        }

        Ok(())
    }

    async fn cmd_memory_relevant(&self, context: &str, top_k: usize) -> Result<()> {
        let service = self.memory_service.lock().await;
        let results = service.find_relevant(context, top_k).await.map_err(|e| anyhow::anyhow!("{}", e))?;

        if results.is_empty() {
            println!("{}", "没有找到相关记忆".yellow());
            return Ok(());
        }

        println!("{} (共 {} 条)", "相关记忆:".bold(), results.len().to_string().cyan());
        println!();

        for m in &results {
            println!("  {} {} [{}]", "●".cyan(), m.id.to_string()[..8].cyan(), format!("{}", m.memory_type).yellow());
            let preview: String = m.content.chars().take(100).collect();
            println!("    {}", preview);
            println!();
        }

        Ok(())
    }

    async fn cmd_memory_stats(&self) -> Result<()> {
        let service = self.memory_service.lock().await;
        let stats = service.stats().await.map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{}", "记忆统计:".bold());
        println!("  总记忆数: {}", stats.total_memories.to_string().cyan().bold());
        println!(
            "  向量搜索: {}",
            if stats.vector_available {
                "可用".green().to_string()
            } else {
                "不可用（降级到关键词搜索）".yellow().to_string()
            }
        );
        println!();

        println!("  {}", "按类型统计:".bold());
        for (mt, count) in &stats.type_counts {
            if *count > 0 {
                println!("    {}: {}", format!("{}", mt).yellow(), count.to_string().cyan());
            }
        }

        Ok(())
    }

    // === 文件存储命令 ===

    fn parse_access_level(s: &str) -> Result<AccessLevel> {
        AccessLevel::from_str(s)
            .ok_or_else(|| anyhow::anyhow!("未知访问级别: {}（可选: private, shared, public）", s))
    }

    async fn cmd_file_upload(&self, path: &Path, access_level_str: &str, description: Option<String>) -> Result<()> {
        let access_level = Self::parse_access_level(access_level_str)?;

        if !path.exists() {
            anyhow::bail!("文件不存在: {}", path.display());
        }

        let file = self.file_service
            .upload_file(path, "default", access_level, description)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{}", "✓ 文件上传成功".green().bold());
        println!("  ID:       {}", file.id.to_string().cyan());
        println!("  文件名:   {}", file.original_filename.yellow());
        println!("  类型:     {}", file.file_type);
        println!("  MIME:     {}", file.mime_type.dimmed());
        println!("  大小:     {} 字节", file.size_in_bytes.to_string().cyan());
        println!("  访问级别: {}", file.access_level.to_string().yellow());
        println!("  SHA256:   {}", file.sha256_hash[..16].to_string().dimmed());

        Ok(())
    }

    async fn cmd_file_list(&self, level: Option<String>) -> Result<()> {
        let access_level = level.as_deref().map(Self::parse_access_level).transpose()?;

        let files = self.file_service
            .list_files("default", access_level)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        if files.is_empty() {
            println!("{}", "没有文件".yellow());
            return Ok(());
        }

        println!("{} (共 {} 个)", "文件列表:".bold(), files.len().to_string().cyan());
        println!();

        for f in &files {
            println!("  {} {}", "●".cyan(), f.id.to_string()[..8].cyan());
            println!("    文件名: {} ({})", f.original_filename.yellow(), f.file_type);
            println!("    大小: {} 字节 | 版本: v{} | 访问: {}",
                f.size_in_bytes.to_string().green(),
                f.current_version,
                f.access_level.to_string().yellow()
            );
            println!("    上传: {}", f.uploaded_at.format("%Y-%m-%d %H:%M").to_string().dimmed());
            println!();
        }

        Ok(())
    }

    async fn cmd_file_show(&self, id_str: &str) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid file ID")?;

        let file = self.file_service
            .get_file(id, "default")
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{}", "文件详情:".bold());
        println!("  ID:         {}", file.id.to_string().cyan());
        println!("  文件名:     {}", file.original_filename.yellow());
        println!("  类型:       {}", file.file_type);
        println!("  MIME:       {}", file.mime_type.dimmed());
        println!("  大小:       {} 字节", file.size_in_bytes.to_string().cyan());
        println!("  SHA256:     {}", file.sha256_hash.dimmed());
        println!("  访问级别:   {}", file.access_level.to_string().yellow());
        println!("  所有者:     {}", file.owner_id);
        println!("  当前版本:   v{}", file.current_version);
        println!("  上传时间:   {}", file.uploaded_at.format("%Y-%m-%d %H:%M:%S"));
        if let Some(ref desc) = file.description {
            println!("  描述:       {}", desc);
        }
        if let Some(ref updated) = file.updated_at {
            println!("  更新时间:   {}", updated.format("%Y-%m-%d %H:%M:%S"));
        }

        Ok(())
    }

    async fn cmd_file_content(&self, id_str: &str, version: Option<i32>) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid file ID")?;

        let content = match version {
            Some(v) => {
                let data = self.file_service
                    .read_version_content(id, v, "default")
                    .await
                    .map_err(|e| anyhow::anyhow!("{}", e))?;
                String::from_utf8(data)
                    .map_err(|e| anyhow::anyhow!("非文本文件: {}", e))?
            }
            None => {
                self.file_service
                    .read_file_as_text(id, "default")
                    .await
                    .map_err(|e| anyhow::anyhow!("{}", e))?
            }
        };

        println!("{}", content);
        Ok(())
    }

    async fn cmd_file_delete(&self, id_str: &str) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid file ID")?;

        self.file_service
            .delete_file(id, "default")
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{}", "✓ 文件已删除".green().bold());
        Ok(())
    }

    async fn cmd_file_search(&self, keyword: &str) -> Result<()> {
        let files = self.file_service
            .search_files(keyword, "default")
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        if files.is_empty() {
            println!("{}", "没有找到匹配的文件".yellow());
            return Ok(());
        }

        println!("{} '{}' (共 {} 个)", "搜索结果:".bold(), keyword.yellow(), files.len().to_string().cyan());
        println!();

        for f in &files {
            println!("  {} {} - {} ({}, {} 字节)",
                "●".cyan(),
                f.id.to_string()[..8].cyan(),
                f.original_filename.yellow(),
                f.file_type,
                f.size_in_bytes
            );
        }

        Ok(())
    }

    async fn cmd_file_share(&self, id_str: &str, user: &str, permission_str: &str) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid file ID")?;
        let perm_type = agent_file_storage::PermissionType::from_str(permission_str)
            .ok_or_else(|| anyhow::anyhow!("未知权限类型: {}（可选: read, write）", permission_str))?;

        self.file_service
            .grant_permission(id, "default", user, perm_type)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{} 已授予 {} {} 权限", "✓".green(), user.cyan(), permission_str.yellow());
        Ok(())
    }

    async fn cmd_file_revoke(&self, id_str: &str, user: &str, permission_str: &str) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid file ID")?;
        let perm_type = agent_file_storage::PermissionType::from_str(permission_str)
            .ok_or_else(|| anyhow::anyhow!("未知权限类型: {}（可选: read, write）", permission_str))?;

        self.file_service
            .revoke_permission(id, "default", user, perm_type)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{} 已撤销 {} 的 {} 权限", "✓".green(), user.cyan(), permission_str.yellow());
        Ok(())
    }

    async fn cmd_file_permissions(&self, id_str: &str) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid file ID")?;

        let perms = self.file_service
            .list_permissions(id)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        if perms.is_empty() {
            println!("{}", "没有额外权限设置".yellow());
            return Ok(());
        }

        println!("{} (共 {} 条)", "权限列表:".bold(), perms.len().to_string().cyan());
        println!();

        for p in &perms {
            println!("  {} 用户: {} | 权限: {} | 授予者: {} | 时间: {}",
                "●".cyan(),
                p.user_id.yellow(),
                p.permission_type.to_string().green(),
                p.granted_by.dimmed(),
                p.granted_at.format("%Y-%m-%d %H:%M").to_string().dimmed()
            );
        }

        Ok(())
    }

    async fn cmd_file_versions(&self, id_str: &str) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid file ID")?;

        let versions = self.file_service
            .list_versions(id)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        if versions.is_empty() {
            println!("{}", "没有版本记录".yellow());
            return Ok(());
        }

        println!("{} (共 {} 个)", "版本历史:".bold(), versions.len().to_string().cyan());
        println!();

        for v in &versions {
            println!("  {} v{} | {} 字节 | {}",
                "●".cyan(),
                v.version.to_string().yellow(),
                v.size_in_bytes.to_string().green(),
                v.uploaded_at.format("%Y-%m-%d %H:%M").to_string().dimmed()
            );
            if let Some(ref desc) = v.change_description {
                println!("    描述: {}", desc);
            }
        }

        Ok(())
    }

    async fn cmd_file_restore(&self, id_str: &str, version: i32) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid file ID")?;

        let file = self.file_service
            .restore_version(id, version, "default")
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{} 文件已恢复到 v{}", "✓".green(), version.to_string().yellow());
        println!("  当前版本: v{}", file.current_version);
        Ok(())
    }

    async fn cmd_file_stats(&self) -> Result<()> {
        let stats = self.file_service
            .storage_stats("default")
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{}", "文件存储统计:".bold());
        println!("  数据库文件数:   {}", stats.file_count.to_string().cyan().bold());
        println!("  数据库总大小:   {} 字节", stats.db_total_size.to_string().cyan());
        println!("  磁盘文件数:     {}", stats.disk_file_count.to_string().green());
        println!("  磁盘总大小:     {} 字节", stats.disk_total_size.to_string().green());

        Ok(())
    }

    // === 技能抽取命令 ===

    fn parse_extraction_status(s: &str) -> Result<ExtractionStatus> {
        ExtractionStatus::from_str(s)
            .ok_or_else(|| anyhow::anyhow!("未知状态: {}（可选: success, failed, pending）", s))
    }

    async fn cmd_skill_extract(&self, session_id_str: &str, hint: Option<String>, force: bool) -> Result<()> {
        let session_id = Uuid::parse_str(session_id_str).context("Invalid session ID")?;

        let messages = self.runtime.session_manager().get_messages(session_id, None).await?;
        if messages.is_empty() {
            println!("{}", "会话中没有消息".yellow());
            return Ok(());
        }

        println!("{} ({} 条消息)...", "正在分析对话并抽取技能".yellow(), messages.len());

        let result = self.extraction_service
            .extract_and_save(session_id, &messages, hint.as_deref(), force)
            .await;

        match result {
            Ok(Some(path)) => {
                println!("{}", "✓ 技能抽取并保存成功".green().bold());
                println!("  文件: {}", path.display().to_string().cyan());
            }
            Ok(None) => {
                println!("{}", "未识别到可复用的对话模式".yellow());
            }
            Err(e) => {
                println!("{} {}", "✗ 抽取失败:".red(), e);
            }
        }

        Ok(())
    }

    async fn cmd_skill_history(&self, status: Option<String>, limit: u32) -> Result<()> {
        let status_filter = status.as_deref().map(Self::parse_extraction_status).transpose()?;

        let records = self.extraction_service
            .list_history(status_filter, limit)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        if records.is_empty() {
            println!("{}", "没有抽取历史".yellow());
            return Ok(());
        }

        println!("{} (共 {} 条)", "抽取历史:".bold(), records.len().to_string().cyan());
        println!();

        for r in &records {
            let status_str = match r.status {
                ExtractionStatus::Success => "成功".green().to_string(),
                ExtractionStatus::Failed => "失败".red().to_string(),
                ExtractionStatus::Pending => "等待".yellow().to_string(),
            };
            println!("  {} {} | {} | {} 条消息",
                "●".cyan(),
                r.extracted_at.format("%Y-%m-%d %H:%M").to_string().dimmed(),
                status_str,
                r.message_count,
            );
            if let Some(ref name) = r.skill_name {
                let full = match &r.skill_namespace {
                    Some(ns) => format!("{}:{}", ns, name),
                    None => name.clone(),
                };
                println!("    技能: {}", full.yellow());
            }
            if let Some(ref err) = r.error_message {
                println!("    错误: {}", err.red());
            }
            println!();
        }

        Ok(())
    }

    async fn cmd_skill_stats(&self) -> Result<()> {
        let stats = self.extraction_service
            .stats()
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{}", "技能抽取统计:".bold());
        println!("  总抽取次数:   {}", stats.total_extractions.to_string().cyan().bold());
        println!("  成功次数:     {}", stats.successful.to_string().green());
        println!("  失败次数:     {}", stats.failed.to_string().red());
        println!("  独立技能数:   {}", stats.unique_skills.to_string().yellow());

        Ok(())
    }

    // === 计划任务命令 ===

    fn parse_task_type(s: &str) -> Result<TaskType> {
        match s {
            "skill" => Ok(TaskType::SkillInvocation),
            "reminder" => Ok(TaskType::MemoryReminder),
            "command" => Ok(TaskType::CustomCommand),
            _ => anyhow::bail!("未知任务类型: {}（可选: skill, reminder, command）", s),
        }
    }

    fn parse_task_status(s: &str) -> Result<TaskStatus> {
        match s {
            "pending" => Ok(TaskStatus::Pending),
            "running" => Ok(TaskStatus::Running),
            "completed" => Ok(TaskStatus::Completed),
            "failed" => Ok(TaskStatus::Failed),
            "paused" => Ok(TaskStatus::Paused),
            _ => anyhow::bail!("未知任务状态: {}（可选: pending, running, completed, failed, paused）", s),
        }
    }

    fn format_task_type(t: &TaskType) -> &'static str {
        match t {
            TaskType::SkillInvocation => "技能调用",
            TaskType::MemoryReminder => "记忆提醒",
            TaskType::CustomCommand => "自定义命令",
        }
    }

    fn format_task_status(s: &TaskStatus) -> ColoredString {
        match s {
            TaskStatus::Pending => "等待中".yellow(),
            TaskStatus::Running => "运行中".blue(),
            TaskStatus::Completed => "已完成".green(),
            TaskStatus::Failed => "已失败".red(),
            TaskStatus::Paused => "已暂停".dimmed(),
        }
    }

    async fn cmd_task_create(
        &self,
        name: String,
        schedule: String,
        task_type_str: String,
        payload_str: String,
        description: Option<String>,
    ) -> Result<()> {
        let task_type = Self::parse_task_type(&task_type_str)?;
        let payload = match task_type {
            TaskType::SkillInvocation => TaskPayload::skill(payload_str, None),
            TaskType::MemoryReminder => TaskPayload::reminder(payload_str),
            TaskType::CustomCommand => TaskPayload::command(payload_str),
        };

        let mut task = self.task_service
            .create_task(name, "default".into(), schedule, task_type, payload)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        if let Some(desc) = description {
            task.description = Some(desc);
        }

        println!("{}", "✓ 计划任务已创建".green().bold());
        println!("  ID:       {}", task.id.to_string().cyan());
        println!("  名称:     {}", task.name.yellow());
        println!("  类型:     {}", Self::format_task_type(&task.task_type));
        println!("  调度:     {}", task.schedule.dimmed());
        if let Some(ref next) = task.next_execution_at {
            println!("  下次执行: {}", next.format("%Y-%m-%d %H:%M:%S").to_string().green());
        }

        Ok(())
    }

    async fn cmd_task_list(&self, status: Option<String>) -> Result<()> {
        let status_filter = status.as_deref().map(Self::parse_task_status).transpose()?;

        let tasks = self.task_service
            .list_tasks("default", status_filter)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        if tasks.is_empty() {
            println!("{}", "没有计划任务".yellow());
            return Ok(());
        }

        println!("{} (共 {} 个)", "计划任务列表:".bold(), tasks.len().to_string().cyan());
        println!();

        for t in &tasks {
            println!("  {} {}", "●".cyan(), t.id.to_string()[..8].cyan());
            println!("    名称: {} | 类型: {} | 状态: {}",
                t.name.yellow(),
                Self::format_task_type(&t.task_type),
                Self::format_task_status(&t.status),
            );
            println!("    调度: {} | 执行次数: {}",
                t.schedule.dimmed(),
                t.execution_count.to_string().green(),
            );
            if let Some(ref next) = t.next_execution_at {
                println!("    下次执行: {}", next.format("%Y-%m-%d %H:%M").to_string().green());
            }
            println!();
        }

        Ok(())
    }

    async fn cmd_task_show(&self, id_str: &str) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid task ID")?;

        let task = self.task_service
            .get_task(id)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{}", "任务详情:".bold());
        println!("  ID:         {}", task.id.to_string().cyan());
        println!("  名称:       {}", task.name.yellow());
        if let Some(ref desc) = task.description {
            println!("  描述:       {}", desc);
        }
        println!("  类型:       {}", Self::format_task_type(&task.task_type));
        println!("  状态:       {}", Self::format_task_status(&task.status));
        println!("  调度:       {}", task.schedule);
        println!("  调度类型:   {}", format!("{:?}", task.schedule_type).dimmed());
        println!("  执行次数:   {}", task.execution_count.to_string().cyan());
        println!("  最大重试:   {}", task.max_retries);
        println!("  超时时间:   {}s", task.timeout_seconds);
        println!("  创建时间:   {}", task.created_at.format("%Y-%m-%d %H:%M:%S"));
        if let Some(ref next) = task.next_execution_at {
            println!("  下次执行:   {}", next.format("%Y-%m-%d %H:%M:%S").to_string().green());
        }
        if let Some(ref last) = task.last_execution_at {
            println!("  上次执行:   {}", last.format("%Y-%m-%d %H:%M:%S").to_string().dimmed());
        }

        Ok(())
    }

    async fn cmd_task_pause(&self, id_str: &str) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid task ID")?;

        let task = self.task_service
            .pause_task(id)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{} 任务 '{}' 已暂停", "✓".green(), task.name.yellow());
        Ok(())
    }

    async fn cmd_task_resume(&self, id_str: &str) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid task ID")?;

        let task = self.task_service
            .resume_task(id)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{} 任务 '{}' 已恢复", "✓".green(), task.name.yellow());
        if let Some(ref next) = task.next_execution_at {
            println!("  下次执行: {}", next.format("%Y-%m-%d %H:%M:%S").to_string().green());
        }
        Ok(())
    }

    async fn cmd_task_delete(&self, id_str: &str) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid task ID")?;

        self.task_service
            .delete_task(id)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{}", "✓ 任务已删除".green().bold());
        Ok(())
    }

    async fn cmd_task_history(&self, id_str: &str, limit: u32) -> Result<()> {
        let id = Uuid::parse_str(id_str).context("Invalid task ID")?;

        let executions = self.task_service
            .list_executions(id, limit)
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        if executions.is_empty() {
            println!("{}", "没有执行历史".yellow());
            return Ok(());
        }

        println!("{} (共 {} 条)", "执行历史:".bold(), executions.len().to_string().cyan());
        println!();

        for e in &executions {
            let status_str = match e.status {
                agent_scheduled_tasks::ExecutionStatus::Success => "成功".green().to_string(),
                agent_scheduled_tasks::ExecutionStatus::Failed => "失败".red().to_string(),
                agent_scheduled_tasks::ExecutionStatus::Timeout => "超时".yellow().to_string(),
            };
            println!("  {} {} | {}",
                "●".cyan(),
                e.started_at.format("%Y-%m-%d %H:%M:%S").to_string().dimmed(),
                status_str,
            );
            if let Some(ref completed) = e.completed_at {
                let duration = completed.signed_duration_since(e.started_at);
                println!("    耗时: {}ms", duration.num_milliseconds());
            }
            if let Some(ref result) = e.result {
                let preview: String = result.chars().take(100).collect();
                println!("    结果: {}", preview);
            }
            if let Some(ref err) = e.error_message {
                println!("    错误: {}", err.red());
            }
            println!();
        }

        Ok(())
    }

    async fn cmd_task_stats(&self) -> Result<()> {
        let stats = self.task_service
            .stats("default")
            .await
            .map_err(|e| anyhow::anyhow!("{}", e))?;

        println!("{}", "计划任务统计:".bold());
        println!("  总任务数:   {}", stats.total_tasks.to_string().cyan().bold());
        println!("  活跃任务:   {}", stats.active_tasks.to_string().green());
        println!("  暂停任务:   {}", stats.paused_tasks.to_string().yellow());
        println!("  失败任务:   {}", stats.failed_tasks.to_string().red());
        println!("  总执行次数: {}", stats.total_executions.to_string().cyan());

        Ok(())
    }

    // === TUI 模式 ===

    async fn cmd_tui(self: Arc<Self>) -> Result<()> {
        use agent_tui::TuiApp;

        let (mut tui_app, runner) = TuiApp::with_runtime(self.runtime.clone())
            .map_err(|e| anyhow::anyhow!("TUI 初始化失败: {}", e))?;

        tokio::spawn(async move {
            runner.run().await;
        });

        tui_app
            .run()
            .await
            .map_err(|e| anyhow::anyhow!("TUI 运行错误: {}", e))?;

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
        Commands::Compress { session_id, strategy } => app.cmd_compress(&session_id, &strategy).await?,
        Commands::CompressStatus { session_id } => app.cmd_compress_status(&session_id).await?,
        Commands::MemoryList { memory_type, limit } => app.cmd_memory_list(memory_type, limit).await?,
        Commands::MemoryShow { id } => app.cmd_memory_show(&id).await?,
        Commands::MemoryAdd { memory_type, content, source } => app.cmd_memory_add(&memory_type, content, source).await?,
        Commands::MemoryUpdate { id, content } => app.cmd_memory_update(&id, content).await?,
        Commands::MemoryDelete { id } => app.cmd_memory_delete(&id).await?,
        Commands::MemorySearch { query, limit } => app.cmd_memory_search(&query, limit).await?,
        Commands::MemorySemanticSearch { query, top_k } => app.cmd_memory_semantic_search(&query, top_k).await?,
        Commands::MemoryHybridSearch { query, top_k } => app.cmd_memory_hybrid_search(&query, top_k).await?,
        Commands::MemoryExtract { session_id } => app.cmd_memory_extract(&session_id).await?,
        Commands::MemoryRelevant { context, top_k } => app.cmd_memory_relevant(&context, top_k).await?,
        Commands::MemoryStats => app.cmd_memory_stats().await?,
        Commands::FileUpload { path, access_level, description } => app.cmd_file_upload(&path, &access_level, description).await?,
        Commands::FileList { level } => app.cmd_file_list(level).await?,
        Commands::FileShow { id } => app.cmd_file_show(&id).await?,
        Commands::FileContent { id, version } => app.cmd_file_content(&id, version).await?,
        Commands::FileDelete { id } => app.cmd_file_delete(&id).await?,
        Commands::FileSearch { keyword } => app.cmd_file_search(&keyword).await?,
        Commands::FileShare { id, user, permission } => app.cmd_file_share(&id, &user, &permission).await?,
        Commands::FileRevoke { id, user, permission } => app.cmd_file_revoke(&id, &user, &permission).await?,
        Commands::FilePermissions { id } => app.cmd_file_permissions(&id).await?,
        Commands::FileVersions { id } => app.cmd_file_versions(&id).await?,
        Commands::FileRestore { id, version } => app.cmd_file_restore(&id, version).await?,
        Commands::FileStats => app.cmd_file_stats().await?,
        Commands::SkillExtract { session_id, hint, force } => app.cmd_skill_extract(&session_id, hint, force).await?,
        Commands::SkillHistory { status, limit } => app.cmd_skill_history(status, limit).await?,
        Commands::SkillStats => app.cmd_skill_stats().await?,
        Commands::TaskCreate { name, schedule, task_type, payload, description } => app.cmd_task_create(name, schedule, task_type, payload, description).await?,
        Commands::TaskList { status } => app.cmd_task_list(status).await?,
        Commands::TaskShow { id } => app.cmd_task_show(&id).await?,
        Commands::TaskPause { id } => app.cmd_task_pause(&id).await?,
        Commands::TaskResume { id } => app.cmd_task_resume(&id).await?,
        Commands::TaskDelete { id } => app.cmd_task_delete(&id).await?,
        Commands::TaskHistory { id, limit } => app.cmd_task_history(&id, limit).await?,
        Commands::TaskStats => app.cmd_task_stats().await?,
        Commands::Tui => Arc::new(app).cmd_tui().await?,
    }

    Ok(())
}
