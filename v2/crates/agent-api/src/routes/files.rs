use axum::{
    extract::{Path, State},
    routing::get,
    Json, Router,
};
use uuid::Uuid;

use crate::dto::common::{ApiListResponse, ApiResponse};
use crate::dto::file::{
    FileDto, FilePermissionDto, FileVersionDto, GrantPermissionRequest, StorageStatsDto,
    UpdateAccessLevelRequest,
};
use crate::error::ApiError;
use crate::state::AppState;

const DEFAULT_USER: &str = "default";

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/", get(list_files))
        .route("/stats", get(storage_stats))
        .route("/:id", get(get_file).delete(delete_file))
        .route("/:id/access", axum::routing::put(update_access_level))
        .route("/:id/versions", get(list_versions))
        .route(
            "/:id/permissions",
            get(list_permissions).post(grant_permission),
        )
}

#[utoipa::path(get, path = "/api/v1/files", tag = "files",
    responses((status = 200, body = ApiListResponse<FileDto>))
)]
pub async fn list_files(
    State(state): State<AppState>,
) -> Result<Json<ApiListResponse<FileDto>>, ApiError> {
    let service = state
        .file_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("文件存储未启用".to_string()))?;

    let files = service.list_files(DEFAULT_USER, None).await?;
    let total = files.len() as u64;
    let dtos: Vec<FileDto> = files.into_iter().map(FileDto::from).collect();

    Ok(Json(ApiListResponse::ok(dtos, Some(total))))
}

#[utoipa::path(get, path = "/api/v1/files/{id}", tag = "files",
    params(("id" = Uuid, Path, description = "文件 ID")),
    responses((status = 200, body = ApiResponse<FileDto>))
)]
pub async fn get_file(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
) -> Result<Json<ApiResponse<FileDto>>, ApiError> {
    let service = state
        .file_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("文件存储未启用".to_string()))?;

    let file = service.get_file(id, DEFAULT_USER).await?;
    Ok(Json(ApiResponse::ok(FileDto::from(file))))
}

#[utoipa::path(delete, path = "/api/v1/files/{id}", tag = "files",
    params(("id" = Uuid, Path, description = "文件 ID")),
    responses((status = 200, body = ApiResponse<()>))
)]
pub async fn delete_file(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
) -> Result<Json<ApiResponse<()>>, ApiError> {
    let service = state
        .file_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("文件存储未启用".to_string()))?;

    service.delete_file(id, DEFAULT_USER).await?;
    Ok(Json(ApiResponse::ok(())))
}

#[utoipa::path(put, path = "/api/v1/files/{id}/access", tag = "files",
    params(("id" = Uuid, Path, description = "文件 ID")),
    request_body = UpdateAccessLevelRequest,
    responses((status = 200, body = ApiResponse<FileDto>))
)]
pub async fn update_access_level(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
    Json(req): Json<UpdateAccessLevelRequest>,
) -> Result<Json<ApiResponse<FileDto>>, ApiError> {
    let service = state
        .file_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("文件存储未启用".to_string()))?;

    let access_level = agent_file_storage::AccessLevel::from_str(&req.access_level)
        .ok_or_else(|| {
            ApiError::BadRequest(format!("无效的访问级别: {}", req.access_level))
        })?;

    let file = service
        .update_access_level(id, DEFAULT_USER, access_level)
        .await?;
    Ok(Json(ApiResponse::ok(FileDto::from(file))))
}

#[utoipa::path(get, path = "/api/v1/files/{id}/versions", tag = "files",
    params(("id" = Uuid, Path, description = "文件 ID")),
    responses((status = 200, body = ApiListResponse<FileVersionDto>))
)]
pub async fn list_versions(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
) -> Result<Json<ApiListResponse<FileVersionDto>>, ApiError> {
    let service = state
        .file_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("文件存储未启用".to_string()))?;

    let versions = service.list_versions(id).await?;
    let total = versions.len() as u64;
    let dtos: Vec<FileVersionDto> = versions.into_iter().map(FileVersionDto::from).collect();

    Ok(Json(ApiListResponse::ok(dtos, Some(total))))
}

#[utoipa::path(get, path = "/api/v1/files/{id}/permissions", tag = "files",
    params(("id" = Uuid, Path, description = "文件 ID")),
    responses((status = 200, body = ApiListResponse<FilePermissionDto>))
)]
pub async fn list_permissions(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
) -> Result<Json<ApiListResponse<FilePermissionDto>>, ApiError> {
    let service = state
        .file_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("文件存储未启用".to_string()))?;

    let permissions = service.list_permissions(id).await?;
    let total = permissions.len() as u64;
    let dtos: Vec<FilePermissionDto> = permissions.into_iter().map(FilePermissionDto::from).collect();

    Ok(Json(ApiListResponse::ok(dtos, Some(total))))
}

#[utoipa::path(post, path = "/api/v1/files/{id}/permissions", tag = "files",
    params(("id" = Uuid, Path, description = "文件 ID")),
    request_body = GrantPermissionRequest,
    responses((status = 200, body = ApiResponse<FilePermissionDto>))
)]
pub async fn grant_permission(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
    Json(req): Json<GrantPermissionRequest>,
) -> Result<Json<ApiResponse<FilePermissionDto>>, ApiError> {
    let service = state
        .file_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("文件存储未启用".to_string()))?;

    let permission_type = agent_file_storage::PermissionType::from_str(&req.permission_type)
        .ok_or_else(|| {
            ApiError::BadRequest(format!("无效的权限类型: {}", req.permission_type))
        })?;

    let perm = service
        .grant_permission(id, DEFAULT_USER, &req.target_user, permission_type)
        .await?;

    Ok(Json(ApiResponse::ok(FilePermissionDto::from(perm))))
}

#[utoipa::path(get, path = "/api/v1/files/stats", tag = "files",
    responses((status = 200, body = ApiResponse<StorageStatsDto>))
)]
pub async fn storage_stats(
    State(state): State<AppState>,
) -> Result<Json<ApiResponse<StorageStatsDto>>, ApiError> {
    let service = state
        .file_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("文件存储未启用".to_string()))?;

    let stats = service.storage_stats(DEFAULT_USER).await?;
    Ok(Json(ApiResponse::ok(StorageStatsDto::from(stats))))
}
