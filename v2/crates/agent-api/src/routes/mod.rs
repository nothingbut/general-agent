pub mod agents;
pub mod chat;
pub mod files;
pub mod health;
pub mod memory;
pub mod sessions;
pub mod skills;

use crate::openapi::ApiDoc;
use crate::state::AppState;
use axum::Router;
use tower_http::cors::{Any, CorsLayer};
use tower_http::trace::TraceLayer;
use utoipa::OpenApi;
use utoipa_swagger_ui::SwaggerUi;

pub fn build_router(state: AppState) -> Router {
    let cors = CorsLayer::new()
        .allow_origin(Any)
        .allow_methods(Any)
        .allow_headers(Any);

    Router::new()
        .merge(health::router())
        .nest("/api/v1/sessions", sessions::router())
        .nest("/api/v1/chat", chat::router())
        .nest("/api/v1/skills", skills::router())
        .nest("/api/v1/memories", memory::router())
        .nest("/api/v1/files", files::router())
        .nest("/api/v1/agents", agents::router())
        .merge(SwaggerUi::new("/swagger-ui").url("/api-docs/openapi.json", ApiDoc::openapi()))
        .layer(TraceLayer::new_for_http())
        .layer(cors)
        .with_state(state)
}
