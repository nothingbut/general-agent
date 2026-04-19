use chrono::{DateTime, Utc};
use sqlx::SqlitePool;
use uuid::Uuid;

use crate::error::{FileStorageError, Result};
use crate::models::{AccessLevel, FilePermission, FileVersion, PermissionType, UploadedFile};

pub struct FileRepository {
    pool: SqlitePool,
}

impl FileRepository {
    pub fn new(pool: SqlitePool) -> Self {
        Self { pool }
    }

    pub async fn run_migrations(&self) -> Result<()> {
        sqlx::migrate!("./migrations").run(&self.pool).await.map_err(|e| {
            FileStorageError::DatabaseError(sqlx::Error::Protocol(format!(
                "迁移失败: {}",
                e
            )))
        })?;
        Ok(())
    }

    // ========== UploadedFile CRUD ==========

    pub async fn create_file(&self, file: &UploadedFile) -> Result<()> {
        let id = file.id.to_string();
        let access_level = file.access_level.as_str();
        let uploaded_at = file.uploaded_at.to_rfc3339();
        let updated_at = file.updated_at.map(|dt| dt.to_rfc3339());
        let metadata = file.metadata.as_ref().map(|m| m.to_string());

        sqlx::query(
            r#"INSERT INTO uploaded_files
               (id, original_filename, stored_filename, file_type, mime_type,
                size_in_bytes, sha256_hash, uploaded_at, updated_at, access_level,
                owner_id, current_version, description, metadata)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"#,
        )
        .bind(&id)
        .bind(&file.original_filename)
        .bind(&file.stored_filename)
        .bind(&file.file_type)
        .bind(&file.mime_type)
        .bind(file.size_in_bytes)
        .bind(&file.sha256_hash)
        .bind(&uploaded_at)
        .bind(&updated_at)
        .bind(access_level)
        .bind(&file.owner_id)
        .bind(file.current_version)
        .bind(&file.description)
        .bind(&metadata)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    pub async fn get_file_by_id(&self, id: Uuid) -> Result<UploadedFile> {
        let id_str = id.to_string();
        let row = sqlx::query_as::<_, FileRow>(
            "SELECT * FROM uploaded_files WHERE id = ?",
        )
        .bind(&id_str)
        .fetch_optional(&self.pool)
        .await?;

        match row {
            Some(r) => Ok(r.into_uploaded_file()?),
            None => Err(FileStorageError::FileNotFound(id)),
        }
    }

    pub async fn get_file_by_name(&self, filename: &str, owner_id: &str) -> Result<UploadedFile> {
        let row = sqlx::query_as::<_, FileRow>(
            "SELECT * FROM uploaded_files WHERE original_filename = ? AND owner_id = ? ORDER BY uploaded_at DESC LIMIT 1",
        )
        .bind(filename)
        .bind(owner_id)
        .fetch_optional(&self.pool)
        .await?;

        match row {
            Some(r) => Ok(r.into_uploaded_file()?),
            None => Err(FileStorageError::FileNotFoundByName(filename.to_string())),
        }
    }

    pub async fn list_files(&self, owner_id: &str, access_level: Option<AccessLevel>) -> Result<Vec<UploadedFile>> {
        let rows = match access_level {
            Some(level) => {
                sqlx::query_as::<_, FileRow>(
                    "SELECT * FROM uploaded_files WHERE owner_id = ? AND access_level = ? ORDER BY uploaded_at DESC",
                )
                .bind(owner_id)
                .bind(level.as_str())
                .fetch_all(&self.pool)
                .await?
            }
            None => {
                sqlx::query_as::<_, FileRow>(
                    "SELECT * FROM uploaded_files WHERE owner_id = ? ORDER BY uploaded_at DESC",
                )
                .bind(owner_id)
                .fetch_all(&self.pool)
                .await?
            }
        };

        rows.into_iter().map(|r| r.into_uploaded_file()).collect()
    }

    pub async fn list_accessible_files(&self, user_id: &str) -> Result<Vec<UploadedFile>> {
        let rows = sqlx::query_as::<_, FileRow>(
            r#"SELECT DISTINCT f.* FROM uploaded_files f
               WHERE f.owner_id = ?
                  OR f.access_level = 'public'
                  OR f.id IN (
                      SELECT file_id FROM file_permissions WHERE user_id = ?
                  )
               ORDER BY f.uploaded_at DESC"#,
        )
        .bind(user_id)
        .bind(user_id)
        .fetch_all(&self.pool)
        .await?;

        rows.into_iter().map(|r| r.into_uploaded_file()).collect()
    }

    pub async fn update_file(&self, file: &UploadedFile) -> Result<()> {
        let id = file.id.to_string();
        let access_level = file.access_level.as_str();
        let updated_at = Utc::now().to_rfc3339();
        let metadata = file.metadata.as_ref().map(|m| m.to_string());

        let rows_affected = sqlx::query(
            r#"UPDATE uploaded_files SET
               original_filename = ?, stored_filename = ?, size_in_bytes = ?,
               sha256_hash = ?, access_level = ?, current_version = ?,
               description = ?, metadata = ?, updated_at = ?
               WHERE id = ?"#,
        )
        .bind(&file.original_filename)
        .bind(&file.stored_filename)
        .bind(file.size_in_bytes)
        .bind(&file.sha256_hash)
        .bind(access_level)
        .bind(file.current_version)
        .bind(&file.description)
        .bind(&metadata)
        .bind(&updated_at)
        .bind(&id)
        .execute(&self.pool)
        .await?
        .rows_affected();

        if rows_affected == 0 {
            return Err(FileStorageError::FileNotFound(file.id));
        }
        Ok(())
    }

    pub async fn delete_file(&self, id: Uuid) -> Result<()> {
        let id_str = id.to_string();
        let rows_affected = sqlx::query("DELETE FROM uploaded_files WHERE id = ?")
            .bind(&id_str)
            .execute(&self.pool)
            .await?
            .rows_affected();

        if rows_affected == 0 {
            return Err(FileStorageError::FileNotFound(id));
        }
        Ok(())
    }

    pub async fn search_files(&self, keyword: &str, owner_id: &str) -> Result<Vec<UploadedFile>> {
        let pattern = format!("%{}%", keyword);
        let rows = sqlx::query_as::<_, FileRow>(
            r#"SELECT * FROM uploaded_files
               WHERE (owner_id = ? OR access_level = 'public')
                 AND (original_filename LIKE ? OR description LIKE ? OR file_type LIKE ?)
               ORDER BY uploaded_at DESC"#,
        )
        .bind(owner_id)
        .bind(&pattern)
        .bind(&pattern)
        .bind(&pattern)
        .fetch_all(&self.pool)
        .await?;

        rows.into_iter().map(|r| r.into_uploaded_file()).collect()
    }

    // ========== FileVersion CRUD ==========

    pub async fn create_version(&self, version: &FileVersion) -> Result<()> {
        let id = version.id.to_string();
        let file_id = version.file_id.to_string();
        let uploaded_at = version.uploaded_at.to_rfc3339();

        sqlx::query(
            r#"INSERT INTO file_versions
               (id, file_id, version, stored_filename, size_in_bytes, sha256_hash,
                uploaded_at, change_description)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?)"#,
        )
        .bind(&id)
        .bind(&file_id)
        .bind(version.version)
        .bind(&version.stored_filename)
        .bind(version.size_in_bytes)
        .bind(&version.sha256_hash)
        .bind(&uploaded_at)
        .bind(&version.change_description)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    pub async fn list_versions(&self, file_id: Uuid) -> Result<Vec<FileVersion>> {
        let file_id_str = file_id.to_string();
        let rows = sqlx::query_as::<_, VersionRow>(
            "SELECT * FROM file_versions WHERE file_id = ? ORDER BY version DESC",
        )
        .bind(&file_id_str)
        .fetch_all(&self.pool)
        .await?;

        rows.into_iter().map(|r| r.into_file_version()).collect()
    }

    pub async fn get_version(&self, file_id: Uuid, version: i32) -> Result<FileVersion> {
        let file_id_str = file_id.to_string();
        let row = sqlx::query_as::<_, VersionRow>(
            "SELECT * FROM file_versions WHERE file_id = ? AND version = ?",
        )
        .bind(&file_id_str)
        .bind(version)
        .fetch_optional(&self.pool)
        .await?;

        match row {
            Some(r) => Ok(r.into_file_version()?),
            None => Err(FileStorageError::VersionNotFound { file_id, version }),
        }
    }

    // ========== FilePermission CRUD ==========

    pub async fn grant_permission(&self, permission: &FilePermission) -> Result<()> {
        let id = permission.id.to_string();
        let file_id = permission.file_id.to_string();
        let perm_type = permission.permission_type.as_str();
        let granted_at = permission.granted_at.to_rfc3339();

        sqlx::query(
            r#"INSERT INTO file_permissions
               (id, file_id, user_id, permission_type, granted_at, granted_by)
               VALUES (?, ?, ?, ?, ?, ?)"#,
        )
        .bind(&id)
        .bind(&file_id)
        .bind(&permission.user_id)
        .bind(perm_type)
        .bind(&granted_at)
        .bind(&permission.granted_by)
        .execute(&self.pool)
        .await
        .map_err(|e| {
            if let sqlx::Error::Database(ref db_err) = e {
                if db_err.message().contains("UNIQUE constraint failed") {
                    return FileStorageError::DuplicatePermission {
                        file_id: permission.file_id,
                        user_id: permission.user_id.clone(),
                        permission: perm_type.to_string(),
                    };
                }
            }
            FileStorageError::DatabaseError(e)
        })?;

        Ok(())
    }

    pub async fn revoke_permission(&self, file_id: Uuid, user_id: &str, permission_type: PermissionType) -> Result<()> {
        let file_id_str = file_id.to_string();
        let perm_type = permission_type.as_str();

        sqlx::query(
            "DELETE FROM file_permissions WHERE file_id = ? AND user_id = ? AND permission_type = ?",
        )
        .bind(&file_id_str)
        .bind(user_id)
        .bind(perm_type)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    pub async fn list_permissions(&self, file_id: Uuid) -> Result<Vec<FilePermission>> {
        let file_id_str = file_id.to_string();
        let rows = sqlx::query_as::<_, PermissionRow>(
            "SELECT * FROM file_permissions WHERE file_id = ? ORDER BY granted_at DESC",
        )
        .bind(&file_id_str)
        .fetch_all(&self.pool)
        .await?;

        rows.into_iter().map(|r| r.into_file_permission()).collect()
    }

    pub async fn check_permission(&self, file_id: Uuid, user_id: &str, permission_type: PermissionType) -> Result<bool> {
        let file_id_str = file_id.to_string();
        let perm_type = permission_type.as_str();

        let row: Option<(i32,)> = sqlx::query_as(
            "SELECT 1 FROM file_permissions WHERE file_id = ? AND user_id = ? AND permission_type = ?",
        )
        .bind(&file_id_str)
        .bind(user_id)
        .bind(perm_type)
        .fetch_optional(&self.pool)
        .await?;

        Ok(row.is_some())
    }

    pub async fn has_access(&self, file_id: Uuid, user_id: &str) -> Result<bool> {
        let file = self.get_file_by_id(file_id).await?;

        if file.owner_id == user_id {
            return Ok(true);
        }
        if file.access_level == AccessLevel::Public {
            return Ok(true);
        }
        if file.access_level == AccessLevel::Shared {
            return self.check_permission(file_id, user_id, PermissionType::Read).await;
        }
        Ok(false)
    }

    // ========== 统计 ==========

    pub async fn count_files(&self, owner_id: &str) -> Result<i64> {
        let row: (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM uploaded_files WHERE owner_id = ?",
        )
        .bind(owner_id)
        .fetch_one(&self.pool)
        .await?;

        Ok(row.0)
    }

    pub async fn total_size(&self, owner_id: &str) -> Result<i64> {
        let row: (Option<i64>,) = sqlx::query_as(
            "SELECT SUM(size_in_bytes) FROM uploaded_files WHERE owner_id = ?",
        )
        .bind(owner_id)
        .fetch_one(&self.pool)
        .await?;

        Ok(row.0.unwrap_or(0))
    }
}

// ========== 内部行类型 ==========

#[derive(sqlx::FromRow)]
struct FileRow {
    id: String,
    original_filename: String,
    stored_filename: String,
    file_type: String,
    mime_type: String,
    size_in_bytes: i64,
    sha256_hash: String,
    uploaded_at: String,
    updated_at: Option<String>,
    access_level: String,
    owner_id: String,
    current_version: i32,
    description: Option<String>,
    metadata: Option<String>,
}

impl FileRow {
    fn into_uploaded_file(self) -> Result<UploadedFile> {
        let id = Uuid::parse_str(&self.id)
            .map_err(|e| FileStorageError::DatabaseError(sqlx::Error::Protocol(e.to_string())))?;
        let uploaded_at = DateTime::parse_from_rfc3339(&self.uploaded_at)
            .map(|dt| dt.with_timezone(&Utc))
            .map_err(|e| FileStorageError::DatabaseError(sqlx::Error::Protocol(e.to_string())))?;
        let updated_at = self
            .updated_at
            .map(|s| DateTime::parse_from_rfc3339(&s).map(|dt| dt.with_timezone(&Utc)))
            .transpose()
            .map_err(|e| FileStorageError::DatabaseError(sqlx::Error::Protocol(e.to_string())))?;
        let access_level = AccessLevel::from_str(&self.access_level).unwrap_or(AccessLevel::Private);
        let metadata = self
            .metadata
            .map(|s| serde_json::from_str(&s))
            .transpose()?;

        Ok(UploadedFile {
            id,
            original_filename: self.original_filename,
            stored_filename: self.stored_filename,
            file_type: self.file_type,
            mime_type: self.mime_type,
            size_in_bytes: self.size_in_bytes,
            sha256_hash: self.sha256_hash,
            uploaded_at,
            updated_at,
            access_level,
            owner_id: self.owner_id,
            current_version: self.current_version,
            description: self.description,
            metadata,
        })
    }
}

#[derive(sqlx::FromRow)]
struct VersionRow {
    id: String,
    file_id: String,
    version: i32,
    stored_filename: String,
    size_in_bytes: i64,
    sha256_hash: String,
    uploaded_at: String,
    change_description: Option<String>,
}

impl VersionRow {
    fn into_file_version(self) -> Result<FileVersion> {
        let id = Uuid::parse_str(&self.id)
            .map_err(|e| FileStorageError::DatabaseError(sqlx::Error::Protocol(e.to_string())))?;
        let file_id = Uuid::parse_str(&self.file_id)
            .map_err(|e| FileStorageError::DatabaseError(sqlx::Error::Protocol(e.to_string())))?;
        let uploaded_at = DateTime::parse_from_rfc3339(&self.uploaded_at)
            .map(|dt| dt.with_timezone(&Utc))
            .map_err(|e| FileStorageError::DatabaseError(sqlx::Error::Protocol(e.to_string())))?;

        Ok(FileVersion {
            id,
            file_id,
            version: self.version,
            stored_filename: self.stored_filename,
            size_in_bytes: self.size_in_bytes,
            sha256_hash: self.sha256_hash,
            uploaded_at,
            change_description: self.change_description,
        })
    }
}

#[derive(sqlx::FromRow)]
struct PermissionRow {
    id: String,
    file_id: String,
    user_id: String,
    permission_type: String,
    granted_at: String,
    granted_by: String,
}

impl PermissionRow {
    fn into_file_permission(self) -> Result<FilePermission> {
        let id = Uuid::parse_str(&self.id)
            .map_err(|e| FileStorageError::DatabaseError(sqlx::Error::Protocol(e.to_string())))?;
        let file_id = Uuid::parse_str(&self.file_id)
            .map_err(|e| FileStorageError::DatabaseError(sqlx::Error::Protocol(e.to_string())))?;
        let granted_at = DateTime::parse_from_rfc3339(&self.granted_at)
            .map(|dt| dt.with_timezone(&Utc))
            .map_err(|e| FileStorageError::DatabaseError(sqlx::Error::Protocol(e.to_string())))?;
        let permission_type =
            PermissionType::from_str(&self.permission_type).unwrap_or(PermissionType::Read);

        Ok(FilePermission {
            id,
            file_id,
            user_id: self.user_id,
            permission_type,
            granted_at,
            granted_by: self.granted_by,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    async fn setup_db() -> SqlitePool {
        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        sqlx::migrate!("./migrations").run(&pool).await.unwrap();
        pool
    }

    fn make_test_file(owner: &str) -> UploadedFile {
        UploadedFile::new(
            "test.rs".to_string(),
            format!("{}.rs", Uuid::new_v4()),
            "code".to_string(),
            "text/x-rust".to_string(),
            1024,
            "abc123hash".to_string(),
            owner.to_string(),
        )
    }

    #[tokio::test]
    async fn test_create_and_get_file() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);
        let file = make_test_file("user1");
        let id = file.id;

        repo.create_file(&file).await.unwrap();
        let fetched = repo.get_file_by_id(id).await.unwrap();

        assert_eq!(fetched.id, id);
        assert_eq!(fetched.original_filename, "test.rs");
        assert_eq!(fetched.owner_id, "user1");
        assert_eq!(fetched.access_level, AccessLevel::Private);
    }

    #[tokio::test]
    async fn test_get_file_not_found() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);
        let result = repo.get_file_by_id(Uuid::new_v4()).await;
        assert!(matches!(result, Err(FileStorageError::FileNotFound(_))));
    }

    #[tokio::test]
    async fn test_get_file_by_name() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);
        let file = make_test_file("user1");

        repo.create_file(&file).await.unwrap();
        let fetched = repo.get_file_by_name("test.rs", "user1").await.unwrap();
        assert_eq!(fetched.original_filename, "test.rs");
    }

    #[tokio::test]
    async fn test_list_files() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);

        let f1 = make_test_file("user1");
        let mut f2 = make_test_file("user1");
        f2.original_filename = "data.json".to_string();
        let f3 = make_test_file("user2");

        repo.create_file(&f1).await.unwrap();
        repo.create_file(&f2).await.unwrap();
        repo.create_file(&f3).await.unwrap();

        let files = repo.list_files("user1", None).await.unwrap();
        assert_eq!(files.len(), 2);

        let files = repo.list_files("user2", None).await.unwrap();
        assert_eq!(files.len(), 1);
    }

    #[tokio::test]
    async fn test_list_files_by_access_level() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);

        let f1 = make_test_file("user1");
        let mut f2 = make_test_file("user1");
        f2.access_level = AccessLevel::Public;

        repo.create_file(&f1).await.unwrap();
        repo.create_file(&f2).await.unwrap();

        let private = repo.list_files("user1", Some(AccessLevel::Private)).await.unwrap();
        assert_eq!(private.len(), 1);

        let public = repo.list_files("user1", Some(AccessLevel::Public)).await.unwrap();
        assert_eq!(public.len(), 1);
    }

    #[tokio::test]
    async fn test_update_file() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);
        let mut file = make_test_file("user1");
        let id = file.id;

        repo.create_file(&file).await.unwrap();

        file.description = Some("更新后的描述".to_string());
        file.access_level = AccessLevel::Public;
        file.current_version = 2;
        repo.update_file(&file).await.unwrap();

        let updated = repo.get_file_by_id(id).await.unwrap();
        assert_eq!(updated.description, Some("更新后的描述".to_string()));
        assert_eq!(updated.access_level, AccessLevel::Public);
        assert_eq!(updated.current_version, 2);
        assert!(updated.updated_at.is_some());
    }

    #[tokio::test]
    async fn test_delete_file() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);
        let file = make_test_file("user1");
        let id = file.id;

        repo.create_file(&file).await.unwrap();
        repo.delete_file(id).await.unwrap();

        let result = repo.get_file_by_id(id).await;
        assert!(matches!(result, Err(FileStorageError::FileNotFound(_))));
    }

    #[tokio::test]
    async fn test_delete_not_found() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);
        let result = repo.delete_file(Uuid::new_v4()).await;
        assert!(matches!(result, Err(FileStorageError::FileNotFound(_))));
    }

    #[tokio::test]
    async fn test_search_files() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);

        let mut f1 = make_test_file("user1");
        f1.original_filename = "hello_world.rs".to_string();
        let mut f2 = make_test_file("user1");
        f2.original_filename = "config.toml".to_string();

        repo.create_file(&f1).await.unwrap();
        repo.create_file(&f2).await.unwrap();

        let results = repo.search_files("hello", "user1").await.unwrap();
        assert_eq!(results.len(), 1);
        assert_eq!(results[0].original_filename, "hello_world.rs");
    }

    #[tokio::test]
    async fn test_create_and_list_versions() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);
        let file = make_test_file("user1");
        let file_id = file.id;

        repo.create_file(&file).await.unwrap();

        let v1 = FileVersion::new(file_id, 1, "stored_v1.rs".to_string(), 1024, "hash1".to_string());
        let v2 = FileVersion::new(file_id, 2, "stored_v2.rs".to_string(), 2048, "hash2".to_string());

        repo.create_version(&v1).await.unwrap();
        repo.create_version(&v2).await.unwrap();

        let versions = repo.list_versions(file_id).await.unwrap();
        assert_eq!(versions.len(), 2);
        assert_eq!(versions[0].version, 2);
        assert_eq!(versions[1].version, 1);
    }

    #[tokio::test]
    async fn test_get_version() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);
        let file = make_test_file("user1");
        let file_id = file.id;

        repo.create_file(&file).await.unwrap();
        let v1 = FileVersion::new(file_id, 1, "stored.rs".to_string(), 1024, "hash".to_string());
        repo.create_version(&v1).await.unwrap();

        let fetched = repo.get_version(file_id, 1).await.unwrap();
        assert_eq!(fetched.version, 1);

        let missing = repo.get_version(file_id, 99).await;
        assert!(matches!(missing, Err(FileStorageError::VersionNotFound { .. })));
    }

    #[tokio::test]
    async fn test_grant_and_check_permission() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);
        let file = make_test_file("user1");
        let file_id = file.id;

        repo.create_file(&file).await.unwrap();

        let perm = FilePermission::new(file_id, "user2".to_string(), PermissionType::Read, "user1".to_string());
        repo.grant_permission(&perm).await.unwrap();

        assert!(repo.check_permission(file_id, "user2", PermissionType::Read).await.unwrap());
        assert!(!repo.check_permission(file_id, "user2", PermissionType::Write).await.unwrap());
        assert!(!repo.check_permission(file_id, "user3", PermissionType::Read).await.unwrap());
    }

    #[tokio::test]
    async fn test_revoke_permission() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);
        let file = make_test_file("user1");
        let file_id = file.id;

        repo.create_file(&file).await.unwrap();

        let perm = FilePermission::new(file_id, "user2".to_string(), PermissionType::Read, "user1".to_string());
        repo.grant_permission(&perm).await.unwrap();
        assert!(repo.check_permission(file_id, "user2", PermissionType::Read).await.unwrap());

        repo.revoke_permission(file_id, "user2", PermissionType::Read).await.unwrap();
        assert!(!repo.check_permission(file_id, "user2", PermissionType::Read).await.unwrap());
    }

    #[tokio::test]
    async fn test_duplicate_permission() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);
        let file = make_test_file("user1");
        let file_id = file.id;

        repo.create_file(&file).await.unwrap();

        let perm1 = FilePermission::new(file_id, "user2".to_string(), PermissionType::Read, "user1".to_string());
        repo.grant_permission(&perm1).await.unwrap();

        let perm2 = FilePermission::new(file_id, "user2".to_string(), PermissionType::Read, "user1".to_string());
        let result = repo.grant_permission(&perm2).await;
        assert!(matches!(result, Err(FileStorageError::DuplicatePermission { .. })));
    }

    #[tokio::test]
    async fn test_list_permissions() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);
        let file = make_test_file("user1");
        let file_id = file.id;

        repo.create_file(&file).await.unwrap();

        let p1 = FilePermission::new(file_id, "user2".to_string(), PermissionType::Read, "user1".to_string());
        let p2 = FilePermission::new(file_id, "user3".to_string(), PermissionType::Write, "user1".to_string());
        repo.grant_permission(&p1).await.unwrap();
        repo.grant_permission(&p2).await.unwrap();

        let perms = repo.list_permissions(file_id).await.unwrap();
        assert_eq!(perms.len(), 2);
    }

    #[tokio::test]
    async fn test_has_access() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);

        // Private 文件 - 仅所有者可访问
        let private_file = make_test_file("user1");
        let private_id = private_file.id;
        repo.create_file(&private_file).await.unwrap();

        assert!(repo.has_access(private_id, "user1").await.unwrap());
        assert!(!repo.has_access(private_id, "user2").await.unwrap());

        // Public 文件 - 所有人可访问
        let mut public_file = make_test_file("user1");
        public_file.access_level = AccessLevel::Public;
        let public_id = public_file.id;
        repo.create_file(&public_file).await.unwrap();

        assert!(repo.has_access(public_id, "user2").await.unwrap());

        // Shared 文件 - 需要权限
        let mut shared_file = make_test_file("user1");
        shared_file.access_level = AccessLevel::Shared;
        let shared_id = shared_file.id;
        repo.create_file(&shared_file).await.unwrap();

        assert!(!repo.has_access(shared_id, "user2").await.unwrap());

        let perm = FilePermission::new(shared_id, "user2".to_string(), PermissionType::Read, "user1".to_string());
        repo.grant_permission(&perm).await.unwrap();
        assert!(repo.has_access(shared_id, "user2").await.unwrap());
    }

    #[tokio::test]
    async fn test_list_accessible_files() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);

        // user1 的 private 文件
        let f1 = make_test_file("user1");
        repo.create_file(&f1).await.unwrap();

        // user1 的 public 文件
        let mut f2 = make_test_file("user1");
        f2.access_level = AccessLevel::Public;
        repo.create_file(&f2).await.unwrap();

        // user1 shared 给 user2 的文件
        let mut f3 = make_test_file("user1");
        f3.access_level = AccessLevel::Shared;
        let f3_id = f3.id;
        repo.create_file(&f3).await.unwrap();
        let perm = FilePermission::new(f3_id, "user2".to_string(), PermissionType::Read, "user1".to_string());
        repo.grant_permission(&perm).await.unwrap();

        // user2 应能看到: public 文件 + shared 文件 = 2 个
        let accessible = repo.list_accessible_files("user2").await.unwrap();
        assert_eq!(accessible.len(), 2);
    }

    #[tokio::test]
    async fn test_count_and_total_size() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);

        let f1 = make_test_file("user1");
        let mut f2 = make_test_file("user1");
        f2.size_in_bytes = 2048;

        repo.create_file(&f1).await.unwrap();
        repo.create_file(&f2).await.unwrap();

        let count = repo.count_files("user1").await.unwrap();
        assert_eq!(count, 2);

        let total = repo.total_size("user1").await.unwrap();
        assert_eq!(total, 1024 + 2048);
    }

    #[tokio::test]
    async fn test_cascade_delete_versions_and_permissions() {
        let pool = setup_db().await;
        let repo = FileRepository::new(pool);

        let file = make_test_file("user1");
        let file_id = file.id;
        repo.create_file(&file).await.unwrap();

        let ver = FileVersion::new(file_id, 1, "stored.rs".to_string(), 1024, "hash".to_string());
        repo.create_version(&ver).await.unwrap();

        let perm = FilePermission::new(file_id, "user2".to_string(), PermissionType::Read, "user1".to_string());
        repo.grant_permission(&perm).await.unwrap();

        // 删除文件应级联删除版本和权限
        repo.delete_file(file_id).await.unwrap();

        let versions = repo.list_versions(file_id).await.unwrap();
        assert!(versions.is_empty());

        let perms = repo.list_permissions(file_id).await.unwrap();
        assert!(perms.is_empty());
    }
}
