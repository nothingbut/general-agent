use serde::{Deserialize, Serialize};
use utoipa::ToSchema;

#[derive(Serialize, ToSchema)]
pub struct ApiResponse<T: Serialize> {
    pub success: bool,
    pub data: Option<T>,
    pub error: Option<String>,
}

impl<T: Serialize> ApiResponse<T> {
    pub fn ok(data: T) -> Self {
        Self {
            success: true,
            data: Some(data),
            error: None,
        }
    }
}

#[derive(Serialize, ToSchema)]
pub struct ApiListResponse<T: Serialize> {
    pub success: bool,
    pub data: Vec<T>,
    pub total: Option<u64>,
}

impl<T: Serialize> ApiListResponse<T> {
    pub fn ok(data: Vec<T>, total: Option<u64>) -> Self {
        Self {
            success: true,
            data,
            total,
        }
    }
}

#[derive(Deserialize, ToSchema)]
pub struct PaginationParams {
    pub limit: Option<u32>,
    pub offset: Option<u32>,
}

impl PaginationParams {
    pub fn limit_or(&self, default: u32) -> u32 {
        self.limit.unwrap_or(default)
    }
    pub fn offset_or(&self, default: u32) -> u32 {
        self.offset.unwrap_or(default)
    }
}
