use axum::{
    extract::{Path, Query, State},
    routing::get,
    Json, Router,
};
use uuid::Uuid;

use crate::dto::common::{ApiListResponse, ApiResponse, PaginationParams};
use crate::dto::session::{CreateSessionRequest, SessionDto, SessionStatsDto, UpdateSessionRequest};
use crate::error::ApiError;
use crate::state::AppState;

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/", get(list_sessions).post(create_session))
        .route(
            "/:id",
            get(get_session).put(update_session).delete(delete_session),
        )
        .route("/:id/stats", get(get_session_stats))
}

#[utoipa::path(get, path = "/api/v1/sessions", tag = "sessions",
    params(("limit" = Option<u32>, Query, description = "每页数量"), ("offset" = Option<u32>, Query, description = "偏移量")),
    responses((status = 200, body = ApiListResponse<SessionDto>))
)]
pub async fn list_sessions(
    State(state): State<AppState>,
    Query(params): Query<PaginationParams>,
) -> Result<Json<ApiListResponse<SessionDto>>, ApiError> {
    let limit = params.limit_or(20);
    let offset = params.offset_or(0);

    let sessions = state.session_manager().list_sessions(limit, offset).await?;
    let total = state.session_manager().count_sessions().await?;
    let dtos: Vec<SessionDto> = sessions.into_iter().map(SessionDto::from).collect();

    Ok(Json(ApiListResponse::ok(dtos, Some(total))))
}

#[utoipa::path(post, path = "/api/v1/sessions", tag = "sessions",
    request_body = CreateSessionRequest,
    responses((status = 200, body = ApiResponse<SessionDto>))
)]
pub async fn create_session(
    State(state): State<AppState>,
    Json(req): Json<CreateSessionRequest>,
) -> Result<Json<ApiResponse<SessionDto>>, ApiError> {
    let session = state.session_manager().create_session(req.title).await?;
    Ok(Json(ApiResponse::ok(SessionDto::from(session))))
}

#[utoipa::path(get, path = "/api/v1/sessions/{id}", tag = "sessions",
    params(("id" = Uuid, Path, description = "会话 ID")),
    responses((status = 200, body = ApiResponse<SessionDto>))
)]
pub async fn get_session(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
) -> Result<Json<ApiResponse<SessionDto>>, ApiError> {
    let session = state.session_manager().load_session(id).await?;
    Ok(Json(ApiResponse::ok(SessionDto::from(session))))
}

#[utoipa::path(put, path = "/api/v1/sessions/{id}", tag = "sessions",
    params(("id" = Uuid, Path, description = "会话 ID")),
    request_body = UpdateSessionRequest,
    responses((status = 200, body = ApiResponse<SessionDto>))
)]
pub async fn update_session(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
    Json(req): Json<UpdateSessionRequest>,
) -> Result<Json<ApiResponse<SessionDto>>, ApiError> {
    let session = state
        .session_manager()
        .update_session_title(id, req.title)
        .await?;
    Ok(Json(ApiResponse::ok(SessionDto::from(session))))
}

#[utoipa::path(delete, path = "/api/v1/sessions/{id}", tag = "sessions",
    params(("id" = Uuid, Path, description = "会话 ID")),
    responses((status = 200, body = ApiResponse<()>))
)]
pub async fn delete_session(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
) -> Result<Json<ApiResponse<()>>, ApiError> {
    state.session_manager().delete_session(id).await?;
    Ok(Json(ApiResponse::ok(())))
}

#[utoipa::path(get, path = "/api/v1/sessions/{id}/stats", tag = "sessions",
    params(("id" = Uuid, Path, description = "会话 ID")),
    responses((status = 200, body = ApiResponse<SessionStatsDto>))
)]
pub async fn get_session_stats(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
) -> Result<Json<ApiResponse<SessionStatsDto>>, ApiError> {
    let (session, message_count) = state.session_manager().get_session_stats(id).await?;
    Ok(Json(ApiResponse::ok(SessionStatsDto {
        session: SessionDto::from(session),
        message_count,
    })))
}
