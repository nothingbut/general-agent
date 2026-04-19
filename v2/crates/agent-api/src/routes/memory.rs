use axum::{
    extract::{Path, State},
    routing::{get, post},
    Json, Router,
};
use uuid::Uuid;

use crate::dto::common::ApiResponse;
use crate::dto::memory::{
    CreateMemoryRequest, MemoryDto, MemoryStatsDto, SearchMemoryRequest, UpdateMemoryRequest,
};
use crate::error::ApiError;
use crate::state::AppState;

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/", post(create_memory))
        .route("/search", post(search_memories))
        .route("/stats", get(memory_stats))
        .route(
            "/:id",
            get(get_memory).put(update_memory).delete(delete_memory),
        )
}

#[utoipa::path(post, path = "/api/v1/memories", tag = "memory",
    request_body = CreateMemoryRequest,
    responses((status = 200, body = ApiResponse<MemoryDto>))
)]
pub async fn create_memory(
    State(state): State<AppState>,
    Json(req): Json<CreateMemoryRequest>,
) -> Result<Json<ApiResponse<MemoryDto>>, ApiError> {
    let service = state
        .memory_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("记忆系统未启用".to_string()))?;

    let memory_type = agent_memory::MemoryType::from_str(&req.memory_type)
        .ok_or_else(|| ApiError::BadRequest(format!("无效的记忆类型: {}", req.memory_type)))?;

    let mut memory = agent_memory::Memory::new(memory_type, req.content);
    if let Some(source) = req.source {
        memory = memory.with_source(source);
    }
    if let Some(session_id) = req.session_id {
        memory = memory.with_session(session_id);
    }

    let created = service.lock().await.create(memory).await?;
    Ok(Json(ApiResponse::ok(MemoryDto::from(created))))
}

#[utoipa::path(get, path = "/api/v1/memories/{id}", tag = "memory",
    params(("id" = Uuid, Path, description = "记忆 ID")),
    responses((status = 200, body = ApiResponse<MemoryDto>))
)]
pub async fn get_memory(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
) -> Result<Json<ApiResponse<MemoryDto>>, ApiError> {
    let service = state
        .memory_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("记忆系统未启用".to_string()))?;

    let memory = service
        .lock()
        .await
        .get(id)
        .await?
        .ok_or_else(|| ApiError::NotFound(format!("记忆不存在: {}", id)))?;

    Ok(Json(ApiResponse::ok(MemoryDto::from(memory))))
}

#[utoipa::path(put, path = "/api/v1/memories/{id}", tag = "memory",
    params(("id" = Uuid, Path, description = "记忆 ID")),
    request_body = UpdateMemoryRequest,
    responses((status = 200, body = ApiResponse<MemoryDto>))
)]
pub async fn update_memory(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
    Json(req): Json<UpdateMemoryRequest>,
) -> Result<Json<ApiResponse<MemoryDto>>, ApiError> {
    let service = state
        .memory_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("记忆系统未启用".to_string()))?;

    let svc = service.lock().await;
    let mut memory = svc
        .get(id)
        .await?
        .ok_or_else(|| ApiError::NotFound(format!("记忆不存在: {}", id)))?;

    if let Some(content) = req.content {
        memory.content = content;
    }
    if let Some(source) = req.source {
        memory.source = Some(source);
    }

    let updated = svc.update(&memory).await?;
    Ok(Json(ApiResponse::ok(MemoryDto::from(updated))))
}

#[utoipa::path(delete, path = "/api/v1/memories/{id}", tag = "memory",
    params(("id" = Uuid, Path, description = "记忆 ID")),
    responses((status = 200, body = ApiResponse<()>))
)]
pub async fn delete_memory(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
) -> Result<Json<ApiResponse<()>>, ApiError> {
    let service = state
        .memory_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("记忆系统未启用".to_string()))?;

    service.lock().await.delete(id).await?;
    Ok(Json(ApiResponse::ok(())))
}

#[utoipa::path(post, path = "/api/v1/memories/search", tag = "memory",
    request_body = SearchMemoryRequest,
    responses((status = 200, body = ApiResponse<Vec<MemoryDto>>))
)]
pub async fn search_memories(
    State(state): State<AppState>,
    Json(req): Json<SearchMemoryRequest>,
) -> Result<Json<ApiResponse<Vec<MemoryDto>>>, ApiError> {
    let service = state
        .memory_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("记忆系统未启用".to_string()))?;

    let limit = req.limit.unwrap_or(10);
    let mode = req.mode.as_deref().unwrap_or("keyword");

    let svc = service.lock().await;
    let memories = match mode {
        "semantic" => svc.search_semantic(&req.query, limit as usize).await?,
        "hybrid" => svc.search_hybrid(&req.query, limit as usize).await?,
        _ => svc.search_keyword(&req.query, limit).await?,
    };

    let dtos: Vec<MemoryDto> = memories.into_iter().map(MemoryDto::from).collect();
    Ok(Json(ApiResponse::ok(dtos)))
}

#[utoipa::path(get, path = "/api/v1/memories/stats", tag = "memory",
    responses((status = 200, body = ApiResponse<MemoryStatsDto>))
)]
pub async fn memory_stats(
    State(state): State<AppState>,
) -> Result<Json<ApiResponse<MemoryStatsDto>>, ApiError> {
    let service = state
        .memory_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("记忆系统未启用".to_string()))?;

    let stats = service.lock().await.stats().await?;

    let dto = MemoryStatsDto {
        total_memories: stats.total_memories,
        type_counts: stats
            .type_counts
            .into_iter()
            .map(|(t, c)| (t.as_str().to_string(), c))
            .collect(),
        vector_available: stats.vector_available,
    };

    Ok(Json(ApiResponse::ok(dto)))
}
