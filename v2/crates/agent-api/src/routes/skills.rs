use axum::{
    extract::{Path, State},
    routing::get,
    Json, Router,
};

use crate::dto::common::{ApiListResponse, ApiResponse};
use crate::dto::skill::{InvokeSkillRequest, SkillDto};
use crate::error::ApiError;
use crate::state::AppState;

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/", get(list_skills))
        .route("/:name", get(get_skill))
        .route("/:name/invoke", axum::routing::post(invoke_skill))
}

#[utoipa::path(get, path = "/api/v1/skills", tag = "skills",
    responses((status = 200, body = ApiListResponse<SkillDto>))
)]
pub async fn list_skills(
    State(state): State<AppState>,
) -> Result<Json<ApiListResponse<SkillDto>>, ApiError> {
    let registry = state
        .skill_registry()
        .ok_or_else(|| ApiError::BadRequest("技能系统未启用".to_string()))?;

    let skills: Vec<SkillDto> = registry.list_all().iter().map(|s| SkillDto::from(*s)).collect();
    let total = skills.len() as u64;

    Ok(Json(ApiListResponse::ok(skills, Some(total))))
}

#[utoipa::path(get, path = "/api/v1/skills/:name", tag = "skills",
    params(("name" = String, Path, description = "技能名称")),
    responses((status = 200, body = ApiResponse<SkillDto>))
)]
pub async fn get_skill(
    State(state): State<AppState>,
    Path(name): Path<String>,
) -> Result<Json<ApiResponse<SkillDto>>, ApiError> {
    let registry = state
        .skill_registry()
        .ok_or_else(|| ApiError::BadRequest("技能系统未启用".to_string()))?;

    let skill = registry
        .get(&name)
        .map_err(|e| ApiError::NotFound(e.to_string()))?;

    Ok(Json(ApiResponse::ok(SkillDto::from(skill))))
}

#[utoipa::path(post, path = "/api/v1/skills/:name/invoke", tag = "skills",
    params(("name" = String, Path, description = "技能名称")),
    request_body = InvokeSkillRequest,
    responses((status = 200, body = ApiResponse<String>))
)]
pub async fn invoke_skill(
    State(state): State<AppState>,
    Path(name): Path<String>,
    Json(req): Json<InvokeSkillRequest>,
) -> Result<Json<ApiResponse<String>>, ApiError> {
    let registry = state
        .skill_registry()
        .ok_or_else(|| ApiError::BadRequest("技能系统未启用".to_string()))?;

    let skill = registry
        .get(&name)
        .map_err(|e| ApiError::NotFound(e.to_string()))?;

    let result = state
        .skill_executor
        .execute(skill, req.parameters)
        .map_err(|e| ApiError::BadRequest(e.to_string()))?;

    Ok(Json(ApiResponse::ok(result)))
}
