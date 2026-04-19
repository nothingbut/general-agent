use utoipa::OpenApi;

#[allow(unused_imports)]
use crate::dto::common::{ApiListResponse, ApiResponse, PaginationParams};
use crate::dto::file::{
    FileDto, FilePermissionDto, FileVersionDto, GrantPermissionRequest, StorageStatsDto,
    UpdateAccessLevelRequest,
};
use crate::dto::memory::{
    CreateMemoryRequest, MemoryDto, MemoryStatsDto, SearchMemoryRequest, UpdateMemoryRequest,
};
use crate::dto::message::{MessageDto, SendMessageRequest};
use crate::dto::session::{
    CreateSessionRequest, SessionDto, SessionStatsDto, UpdateSessionRequest,
};
use crate::dto::skill::{InvokeSkillRequest, SkillDto, SkillParameterDto};
use crate::routes::health::HealthResponse;

#[derive(OpenApi)]
#[openapi(
    info(
        title = "General Agent V2 API",
        version = "0.1.0",
        description = "通用 AI Agent 系统 — RESTful Web API"
    ),
    paths(
        crate::routes::health::health_check,
        crate::routes::sessions::list_sessions,
        crate::routes::sessions::create_session,
        crate::routes::sessions::get_session,
        crate::routes::sessions::update_session,
        crate::routes::sessions::delete_session,
        crate::routes::sessions::get_session_stats,
        crate::routes::chat::send_message,
        crate::routes::chat::send_message_stream,
        crate::routes::chat::list_messages,
        crate::routes::skills::list_skills,
        crate::routes::skills::get_skill,
        crate::routes::skills::invoke_skill,
        crate::routes::memory::create_memory,
        crate::routes::memory::get_memory,
        crate::routes::memory::update_memory,
        crate::routes::memory::delete_memory,
        crate::routes::memory::search_memories,
        crate::routes::memory::memory_stats,
        crate::routes::files::list_files,
        crate::routes::files::get_file,
        crate::routes::files::delete_file,
        crate::routes::files::update_access_level,
        crate::routes::files::list_versions,
        crate::routes::files::list_permissions,
        crate::routes::files::grant_permission,
        crate::routes::files::storage_stats,
    ),
    components(schemas(
        HealthResponse,
        SessionDto, CreateSessionRequest, UpdateSessionRequest, SessionStatsDto,
        MessageDto, SendMessageRequest,
        SkillDto, SkillParameterDto, InvokeSkillRequest,
        MemoryDto, CreateMemoryRequest, UpdateMemoryRequest, SearchMemoryRequest, MemoryStatsDto,
        FileDto, FileVersionDto, FilePermissionDto,
        GrantPermissionRequest, UpdateAccessLevelRequest, StorageStatsDto,
    )),
    tags(
        (name = "health", description = "健康检查"),
        (name = "sessions", description = "会话管理"),
        (name = "chat", description = "对话交互"),
        (name = "skills", description = "技能系统"),
        (name = "memory", description = "长期记忆"),
        (name = "files", description = "文件存储"),
    )
)]
pub struct ApiDoc;
