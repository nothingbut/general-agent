use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use utoipa::ToSchema;
use uuid::Uuid;

#[derive(Serialize, ToSchema)]
pub struct FileDto {
    pub id: Uuid,
    pub original_filename: String,
    pub file_type: String,
    pub mime_type: String,
    pub size_in_bytes: i64,
    pub access_level: String,
    pub owner_id: String,
    pub current_version: i32,
    pub description: Option<String>,
    pub uploaded_at: DateTime<Utc>,
}

impl From<agent_file_storage::UploadedFile> for FileDto {
    fn from(f: agent_file_storage::UploadedFile) -> Self {
        Self {
            id: f.id,
            original_filename: f.original_filename,
            file_type: f.file_type,
            mime_type: f.mime_type,
            size_in_bytes: f.size_in_bytes,
            access_level: f.access_level.as_str().to_string(),
            owner_id: f.owner_id,
            current_version: f.current_version,
            description: f.description,
            uploaded_at: f.uploaded_at,
        }
    }
}

#[derive(Serialize, ToSchema)]
pub struct FileVersionDto {
    pub id: Uuid,
    pub file_id: Uuid,
    pub version: i32,
    pub size_in_bytes: i64,
    pub uploaded_at: DateTime<Utc>,
    pub change_description: Option<String>,
}

impl From<agent_file_storage::FileVersion> for FileVersionDto {
    fn from(v: agent_file_storage::FileVersion) -> Self {
        Self {
            id: v.id,
            file_id: v.file_id,
            version: v.version,
            size_in_bytes: v.size_in_bytes,
            uploaded_at: v.uploaded_at,
            change_description: v.change_description,
        }
    }
}

#[derive(Deserialize, ToSchema)]
pub struct UploadFileRequest {
    pub filename: String,
    pub access_level: Option<String>,
    pub description: Option<String>,
}

#[derive(Deserialize, ToSchema)]
pub struct UpdateAccessLevelRequest {
    pub access_level: String,
}

#[derive(Deserialize, ToSchema)]
pub struct GrantPermissionRequest {
    pub target_user: String,
    pub permission_type: String,
}

#[derive(Serialize, ToSchema)]
pub struct FilePermissionDto {
    pub id: Uuid,
    pub file_id: Uuid,
    pub user_id: String,
    pub permission_type: String,
    pub granted_at: DateTime<Utc>,
    pub granted_by: String,
}

impl From<agent_file_storage::FilePermission> for FilePermissionDto {
    fn from(p: agent_file_storage::FilePermission) -> Self {
        Self {
            id: p.id,
            file_id: p.file_id,
            user_id: p.user_id,
            permission_type: p.permission_type.as_str().to_string(),
            granted_at: p.granted_at,
            granted_by: p.granted_by,
        }
    }
}

#[derive(Serialize, ToSchema)]
pub struct StorageStatsDto {
    pub file_count: i64,
    pub db_total_size: i64,
    pub disk_file_count: u64,
    pub disk_total_size: u64,
}

impl From<agent_file_storage::StorageStats> for StorageStatsDto {
    fn from(s: agent_file_storage::StorageStats) -> Self {
        Self {
            file_count: s.file_count,
            db_total_size: s.db_total_size,
            disk_file_count: s.disk_file_count,
            disk_total_size: s.disk_total_size,
        }
    }
}
