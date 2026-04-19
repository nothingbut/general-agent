use axum::{
    extract::{Path, Query, State},
    response::sse::{Event, KeepAlive, Sse},
    routing::{get, post},
    Json, Router,
};
use futures::stream::Stream;
use std::convert::Infallible;
use uuid::Uuid;

use crate::dto::common::{ApiListResponse, ApiResponse, PaginationParams};
use crate::dto::message::{MessageDto, SendMessageRequest};
use crate::error::ApiError;
use crate::state::AppState;

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/:session_id", post(send_message))
        .route("/:session_id/stream", post(send_message_stream))
        .route("/:session_id/messages", get(list_messages))
}

#[utoipa::path(post, path = "/api/v1/chat/:session_id", tag = "chat",
    params(("session_id" = Uuid, Path, description = "会话 ID")),
    request_body = SendMessageRequest,
    responses((status = 200, body = ApiResponse<MessageDto>))
)]
pub async fn send_message(
    State(state): State<AppState>,
    Path(session_id): Path<Uuid>,
    Json(req): Json<SendMessageRequest>,
) -> Result<Json<ApiResponse<MessageDto>>, ApiError> {
    if req.content.trim().is_empty() {
        return Err(ApiError::BadRequest("消息内容不能为空".to_string()));
    }

    let _response = state
        .conversation_flow
        .send_message(session_id, req.content)
        .await?;

    let messages = state
        .session_manager()
        .get_recent_messages(session_id, 1)
        .await?;

    let dto = messages
        .into_iter()
        .last()
        .map(MessageDto::from)
        .ok_or_else(|| ApiError::Internal("响应消息未找到".to_string()))?;

    Ok(Json(ApiResponse::ok(dto)))
}

#[utoipa::path(post, path = "/api/v1/chat/:session_id/stream", tag = "chat",
    params(("session_id" = Uuid, Path, description = "会话 ID")),
    request_body = SendMessageRequest,
    responses((status = 200, description = "SSE 流式响应"))
)]
pub async fn send_message_stream(
    State(state): State<AppState>,
    Path(session_id): Path<Uuid>,
    Json(req): Json<SendMessageRequest>,
) -> Result<Sse<impl Stream<Item = Result<Event, Infallible>>>, ApiError> {
    if req.content.trim().is_empty() {
        return Err(ApiError::BadRequest("消息内容不能为空".to_string()));
    }

    let (mut stream, save_context) = state
        .conversation_flow
        .send_message_stream(session_id, req.content)
        .await?;

    let sse_stream = async_stream::stream! {
        let mut full_response = String::new();

        loop {
            match stream.next().await {
                Ok(Some(chunk)) => {
                    full_response.push_str(&chunk.delta);
                    let event = Event::default()
                        .event("token")
                        .data(&chunk.delta);
                    yield Ok(event);

                    if chunk.is_final {
                        break;
                    }
                }
                Ok(None) => break,
                Err(e) => {
                    let event = Event::default()
                        .event("error")
                        .data(e.to_string());
                    yield Ok(event);
                    break;
                }
            }
        }

        if let Err(e) = save_context.save_response(full_response).await {
            let event = Event::default()
                .event("error")
                .data(format!("保存响应失败: {}", e));
            yield Ok(event);
        }

        let done_event = Event::default().event("done").data("[DONE]");
        yield Ok(done_event);
    };

    Ok(Sse::new(sse_stream).keep_alive(KeepAlive::default()))
}

#[utoipa::path(get, path = "/api/v1/chat/:session_id/messages", tag = "chat",
    params(
        ("session_id" = Uuid, Path, description = "会话 ID"),
        ("limit" = Option<u32>, Query, description = "每页数量"),
    ),
    responses((status = 200, body = ApiListResponse<MessageDto>))
)]
pub async fn list_messages(
    State(state): State<AppState>,
    Path(session_id): Path<Uuid>,
    Query(params): Query<PaginationParams>,
) -> Result<Json<ApiListResponse<MessageDto>>, ApiError> {
    let limit = params.limit_or(50);

    let messages = state
        .session_manager()
        .get_messages(session_id, Some(limit))
        .await?;

    let count = state
        .session_manager()
        .count_messages(session_id)
        .await?;

    let dtos: Vec<MessageDto> = messages.into_iter().map(MessageDto::from).collect();

    Ok(Json(ApiListResponse::ok(dtos, Some(count))))
}
