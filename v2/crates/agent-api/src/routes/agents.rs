use axum::{
    extract::State,
    routing::{get, post},
    Json, Router,
};

use agent_multi_agent::CollaborationStrategy;

use crate::dto::common::{ApiListResponse, ApiResponse};
use crate::dto::multi_agent::{AgentInfoDto, CollaborationRequest, CollaborationResultDto};
use crate::error::ApiError;
use crate::state::AppState;

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/", get(list_agents))
        .route("/collaborate", post(collaborate))
}

#[utoipa::path(get, path = "/api/v1/agents", tag = "agents",
    responses((status = 200, body = ApiListResponse<AgentInfoDto>))
)]
pub async fn list_agents(
    State(state): State<AppState>,
) -> Result<Json<ApiListResponse<AgentInfoDto>>, ApiError> {
    let service = state
        .multi_agent_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("多 Agent 协作未启用".to_string()))?;

    let agents = service.list_agents();
    let total = agents.len() as u64;
    let dtos: Vec<AgentInfoDto> = agents.into_iter().map(AgentInfoDto::from).collect();

    Ok(Json(ApiListResponse::ok(dtos, Some(total))))
}

#[utoipa::path(post, path = "/api/v1/agents/collaborate", tag = "agents",
    request_body = CollaborationRequest,
    responses((status = 200, body = ApiResponse<CollaborationResultDto>))
)]
pub async fn collaborate(
    State(state): State<AppState>,
    Json(req): Json<CollaborationRequest>,
) -> Result<Json<ApiResponse<CollaborationResultDto>>, ApiError> {
    let service = state
        .multi_agent_service
        .as_ref()
        .ok_or_else(|| ApiError::BadRequest("多 Agent 协作未启用".to_string()))?;

    let strategy = match req.strategy.as_str() {
        "parallel" => CollaborationStrategy::Parallel,
        "sequential" => CollaborationStrategy::Sequential,
        "pipeline" => CollaborationStrategy::Pipeline,
        "voting" => {
            let min_votes = req.min_votes.unwrap_or(2);
            CollaborationStrategy::Voting { min_votes }
        }
        other => {
            return Err(ApiError::BadRequest(format!(
                "无效的协作策略: {}，可选: parallel, sequential, pipeline, voting",
                other
            )));
        }
    };

    let subtask_specs: Vec<(String, String, serde_json::Value)> = req
        .subtasks
        .into_iter()
        .map(|s| (s.agent_id, s.task, s.context))
        .collect();

    let result = service
        .execute_collaboration(req.title, req.description, strategy, subtask_specs)
        .await
        .map_err(|e| ApiError::Internal(e.to_string()))?;

    Ok(Json(ApiResponse::ok(CollaborationResultDto::from(result))))
}
