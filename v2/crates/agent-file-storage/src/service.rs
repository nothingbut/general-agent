use std::path::Path;
use tracing::info;
use uuid::Uuid;

use crate::error::{FileStorageError, Result};
use crate::models::{
    detect_file_type, detect_mime_type, AccessLevel, FilePermission, FileVersion, PermissionType,
    UploadedFile,
};
use crate::repository::FileRepository;
use crate::storage::FileStorage;

pub struct FileService {
    repository: FileRepository,
    storage: FileStorage,
}

impl FileService {
    pub fn new(repository: FileRepository, storage: FileStorage) -> Self {
        Self {
            repository,
            storage,
        }
    }

    pub async fn upload_file(
        &self,
        source_path: &Path,
        owner_id: &str,
        access_level: AccessLevel,
        description: Option<String>,
    ) -> Result<UploadedFile> {
        let original_filename = source_path
            .file_name()
            .and_then(|n| n.to_str())
            .unwrap_or("unnamed")
            .to_string();

        let file_type = detect_file_type(&original_filename).to_string();
        let mime_type = detect_mime_type(&original_filename);

        let (stored_filename, sha256_hash, size_in_bytes) =
            self.storage.store_from_path(source_path).await?;

        let mut file = UploadedFile::new(
            original_filename.clone(),
            stored_filename.clone(),
            file_type,
            mime_type,
            size_in_bytes,
            sha256_hash.clone(),
            owner_id.to_string(),
        );
        file.access_level = access_level;
        file.description = description;

        self.repository.create_file(&file).await?;

        let version = FileVersion::new(
            file.id,
            1,
            stored_filename,
            size_in_bytes,
            sha256_hash,
        );
        self.repository.create_version(&version).await?;

        info!("文件上传成功: {} ({})", original_filename, file.id);
        Ok(file)
    }

    pub async fn upload_data(
        &self,
        filename: &str,
        data: &[u8],
        owner_id: &str,
        access_level: AccessLevel,
    ) -> Result<UploadedFile> {
        let file_type = detect_file_type(filename).to_string();
        let mime_type = detect_mime_type(filename);

        let ext = filename.rsplit('.').next().unwrap_or("bin");
        let (stored_filename, sha256_hash) = self.storage.store_file(data, ext).await?;

        let mut file = UploadedFile::new(
            filename.to_string(),
            stored_filename.clone(),
            file_type,
            mime_type,
            data.len() as i64,
            sha256_hash.clone(),
            owner_id.to_string(),
        );
        file.access_level = access_level;

        self.repository.create_file(&file).await?;

        let version = FileVersion::new(file.id, 1, stored_filename, data.len() as i64, sha256_hash);
        self.repository.create_version(&version).await?;

        Ok(file)
    }

    pub async fn upload_new_version(
        &self,
        file_id: Uuid,
        source_path: &Path,
        user_id: &str,
        change_description: Option<String>,
    ) -> Result<FileVersion> {
        let mut file = self.repository.get_file_by_id(file_id).await?;

        if file.owner_id != user_id {
            let has_write = self
                .repository
                .check_permission(file_id, user_id, PermissionType::Write)
                .await?;
            if !has_write {
                return Err(FileStorageError::PermissionDenied {
                    file_id,
                    user_id: user_id.to_string(),
                });
            }
        }

        let (stored_filename, sha256_hash, size_in_bytes) =
            self.storage.store_from_path(source_path).await?;

        let new_version = file.current_version + 1;
        let mut version = FileVersion::new(
            file_id,
            new_version,
            stored_filename.clone(),
            size_in_bytes,
            sha256_hash,
        );
        version.change_description = change_description;

        self.repository.create_version(&version).await?;

        file.current_version = new_version;
        file.stored_filename = stored_filename;
        file.size_in_bytes = size_in_bytes;
        self.repository.update_file(&file).await?;

        info!("文件新版本上传: {} v{}", file_id, new_version);
        Ok(version)
    }

    pub async fn get_file(&self, file_id: Uuid, user_id: &str) -> Result<UploadedFile> {
        let has = self.repository.has_access(file_id, user_id).await?;
        if !has {
            return Err(FileStorageError::PermissionDenied {
                file_id,
                user_id: user_id.to_string(),
            });
        }
        self.repository.get_file_by_id(file_id).await
    }

    pub async fn get_file_by_name(&self, filename: &str, owner_id: &str) -> Result<UploadedFile> {
        self.repository.get_file_by_name(filename, owner_id).await
    }

    pub async fn read_file_content(&self, file_id: Uuid, user_id: &str) -> Result<Vec<u8>> {
        let file = self.get_file(file_id, user_id).await?;
        self.storage.read_file(&file.stored_filename).await
    }

    pub async fn read_file_as_text(&self, file_id: Uuid, user_id: &str) -> Result<String> {
        let file = self.get_file(file_id, user_id).await?;
        self.storage.read_file_as_text(&file.stored_filename).await
    }

    pub async fn read_version_content(&self, file_id: Uuid, version: i32, user_id: &str) -> Result<Vec<u8>> {
        let has = self.repository.has_access(file_id, user_id).await?;
        if !has {
            return Err(FileStorageError::PermissionDenied {
                file_id,
                user_id: user_id.to_string(),
            });
        }
        let ver = self.repository.get_version(file_id, version).await?;
        self.storage.read_file(&ver.stored_filename).await
    }

    pub async fn list_files(&self, owner_id: &str, access_level: Option<AccessLevel>) -> Result<Vec<UploadedFile>> {
        self.repository.list_files(owner_id, access_level).await
    }

    pub async fn list_accessible_files(&self, user_id: &str) -> Result<Vec<UploadedFile>> {
        self.repository.list_accessible_files(user_id).await
    }

    pub async fn search_files(&self, keyword: &str, user_id: &str) -> Result<Vec<UploadedFile>> {
        self.repository.search_files(keyword, user_id).await
    }

    pub async fn delete_file(&self, file_id: Uuid, user_id: &str) -> Result<()> {
        let file = self.repository.get_file_by_id(file_id).await?;
        if file.owner_id != user_id {
            return Err(FileStorageError::PermissionDenied {
                file_id,
                user_id: user_id.to_string(),
            });
        }

        let versions = self.repository.list_versions(file_id).await?;
        for ver in &versions {
            self.storage.delete_stored_file(&ver.stored_filename).await?;
        }
        self.storage.delete_stored_file(&file.stored_filename).await?;

        self.repository.delete_file(file_id).await?;
        info!("文件删除: {} ({})", file.original_filename, file_id);
        Ok(())
    }

    pub async fn update_access_level(
        &self,
        file_id: Uuid,
        user_id: &str,
        access_level: AccessLevel,
    ) -> Result<UploadedFile> {
        let mut file = self.repository.get_file_by_id(file_id).await?;
        if file.owner_id != user_id {
            return Err(FileStorageError::PermissionDenied {
                file_id,
                user_id: user_id.to_string(),
            });
        }

        file.access_level = access_level;
        self.repository.update_file(&file).await?;
        Ok(file)
    }

    // ========== 版本管理 ==========

    pub async fn list_versions(&self, file_id: Uuid) -> Result<Vec<FileVersion>> {
        self.repository.list_versions(file_id).await
    }

    pub async fn restore_version(&self, file_id: Uuid, version: i32, user_id: &str) -> Result<UploadedFile> {
        let mut file = self.repository.get_file_by_id(file_id).await?;
        if file.owner_id != user_id {
            return Err(FileStorageError::PermissionDenied {
                file_id,
                user_id: user_id.to_string(),
            });
        }

        let ver = self.repository.get_version(file_id, version).await?;

        file.stored_filename = ver.stored_filename.clone();
        file.size_in_bytes = ver.size_in_bytes;
        file.sha256_hash = ver.sha256_hash.clone();
        file.current_version = ver.version;
        self.repository.update_file(&file).await?;

        info!("文件版本恢复: {} → v{}", file_id, version);
        Ok(file)
    }

    // ========== 权限管理 ==========

    pub async fn grant_permission(
        &self,
        file_id: Uuid,
        owner_id: &str,
        target_user: &str,
        permission_type: PermissionType,
    ) -> Result<FilePermission> {
        let file = self.repository.get_file_by_id(file_id).await?;
        if file.owner_id != owner_id {
            return Err(FileStorageError::PermissionDenied {
                file_id,
                user_id: owner_id.to_string(),
            });
        }

        let perm = FilePermission::new(
            file_id,
            target_user.to_string(),
            permission_type,
            owner_id.to_string(),
        );
        self.repository.grant_permission(&perm).await?;

        info!(
            "权限授予: {} -> {} ({}) on {}",
            owner_id, target_user, permission_type, file_id
        );
        Ok(perm)
    }

    pub async fn revoke_permission(
        &self,
        file_id: Uuid,
        owner_id: &str,
        target_user: &str,
        permission_type: PermissionType,
    ) -> Result<()> {
        let file = self.repository.get_file_by_id(file_id).await?;
        if file.owner_id != owner_id {
            return Err(FileStorageError::PermissionDenied {
                file_id,
                user_id: owner_id.to_string(),
            });
        }

        self.repository
            .revoke_permission(file_id, target_user, permission_type)
            .await
    }

    pub async fn list_permissions(&self, file_id: Uuid) -> Result<Vec<FilePermission>> {
        self.repository.list_permissions(file_id).await
    }

    // ========== 统计 ==========

    pub async fn storage_stats(&self, owner_id: &str) -> Result<StorageStats> {
        let file_count = self.repository.count_files(owner_id).await?;
        let db_total_size = self.repository.total_size(owner_id).await?;
        let (disk_file_count, disk_total_size) = self.storage.storage_usage().await?;

        Ok(StorageStats {
            file_count,
            db_total_size,
            disk_file_count,
            disk_total_size,
        })
    }
}

#[derive(Debug, Clone)]
pub struct StorageStats {
    pub file_count: i64,
    pub db_total_size: i64,
    pub disk_file_count: u64,
    pub disk_total_size: u64,
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::repository::FileRepository;
    use crate::storage::FileStorage;
    use sqlx::SqlitePool;
    use tempfile::TempDir;

    async fn setup() -> (FileService, TempDir) {
        let tmp = TempDir::new().unwrap();
        let upload_dir = tmp.path().join("uploads");

        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        sqlx::migrate!("./migrations").run(&pool).await.unwrap();

        let repo = FileRepository::new(pool);
        let storage = FileStorage::new(&upload_dir, 10 * 1024 * 1024).await.unwrap();

        (FileService::new(repo, storage), tmp)
    }

    async fn write_test_file(tmp: &TempDir, name: &str, content: &[u8]) -> std::path::PathBuf {
        let path = tmp.path().join(name);
        tokio::fs::write(&path, content).await.unwrap();
        path
    }

    #[tokio::test]
    async fn test_upload_and_read() {
        let (svc, tmp) = setup().await;
        let source = write_test_file(&tmp, "hello.rs", b"fn main() {}").await;

        let file = svc
            .upload_file(&source, "user1", AccessLevel::Private, None)
            .await
            .unwrap();

        assert_eq!(file.original_filename, "hello.rs");
        assert_eq!(file.file_type, "code");
        assert_eq!(file.size_in_bytes, 12);
        assert_eq!(file.current_version, 1);

        let content = svc.read_file_as_text(file.id, "user1").await.unwrap();
        assert_eq!(content, "fn main() {}");
    }

    #[tokio::test]
    async fn test_upload_data() {
        let (svc, _tmp) = setup().await;

        let file = svc
            .upload_data("notes.md", b"# Hello", "user1", AccessLevel::Private)
            .await
            .unwrap();

        assert_eq!(file.original_filename, "notes.md");
        assert_eq!(file.file_type, "markdown");
        assert_eq!(file.size_in_bytes, 7);
    }

    #[tokio::test]
    async fn test_upload_new_version() {
        let (svc, tmp) = setup().await;

        let source_v1 = write_test_file(&tmp, "code.rs", b"v1 code").await;
        let file = svc
            .upload_file(&source_v1, "user1", AccessLevel::Private, None)
            .await
            .unwrap();

        let source_v2 = write_test_file(&tmp, "code_v2.rs", b"v2 code updated").await;
        let ver = svc
            .upload_new_version(file.id, &source_v2, "user1", Some("更新代码".to_string()))
            .await
            .unwrap();

        assert_eq!(ver.version, 2);
        assert_eq!(ver.change_description, Some("更新代码".to_string()));

        let updated = svc.get_file(file.id, "user1").await.unwrap();
        assert_eq!(updated.current_version, 2);
    }

    #[tokio::test]
    async fn test_version_permission_check() {
        let (svc, tmp) = setup().await;

        let source = write_test_file(&tmp, "secret.txt", b"secret").await;
        let file = svc
            .upload_file(&source, "user1", AccessLevel::Private, None)
            .await
            .unwrap();

        let source_v2 = write_test_file(&tmp, "v2.txt", b"updated").await;
        let result = svc
            .upload_new_version(file.id, &source_v2, "user2", None)
            .await;
        assert!(matches!(result, Err(FileStorageError::PermissionDenied { .. })));
    }

    #[tokio::test]
    async fn test_access_control() {
        let (svc, tmp) = setup().await;

        let source = write_test_file(&tmp, "private.txt", b"secret").await;
        let file = svc
            .upload_file(&source, "user1", AccessLevel::Private, None)
            .await
            .unwrap();

        let result = svc.read_file_content(file.id, "user2").await;
        assert!(matches!(result, Err(FileStorageError::PermissionDenied { .. })));

        let content = svc.read_file_content(file.id, "user1").await.unwrap();
        assert_eq!(content, b"secret");
    }

    #[tokio::test]
    async fn test_public_access() {
        let (svc, tmp) = setup().await;

        let source = write_test_file(&tmp, "public.txt", b"open data").await;
        let file = svc
            .upload_file(&source, "user1", AccessLevel::Public, None)
            .await
            .unwrap();

        let content = svc.read_file_as_text(file.id, "user2").await.unwrap();
        assert_eq!(content, "open data");
    }

    #[tokio::test]
    async fn test_shared_with_permission() {
        let (svc, tmp) = setup().await;

        let source = write_test_file(&tmp, "shared.txt", b"shared data").await;
        let file = svc
            .upload_file(&source, "user1", AccessLevel::Shared, None)
            .await
            .unwrap();

        let result = svc.read_file_content(file.id, "user2").await;
        assert!(matches!(result, Err(FileStorageError::PermissionDenied { .. })));

        svc.grant_permission(file.id, "user1", "user2", PermissionType::Read)
            .await
            .unwrap();

        let content = svc.read_file_as_text(file.id, "user2").await.unwrap();
        assert_eq!(content, "shared data");
    }

    #[tokio::test]
    async fn test_delete_file() {
        let (svc, tmp) = setup().await;

        let source = write_test_file(&tmp, "delete_me.txt", b"bye").await;
        let file = svc
            .upload_file(&source, "user1", AccessLevel::Private, None)
            .await
            .unwrap();

        svc.delete_file(file.id, "user1").await.unwrap();

        let result = svc.get_file(file.id, "user1").await;
        assert!(result.is_err());
    }

    #[tokio::test]
    async fn test_delete_by_non_owner() {
        let (svc, tmp) = setup().await;

        let source = write_test_file(&tmp, "mine.txt", b"mine").await;
        let file = svc
            .upload_file(&source, "user1", AccessLevel::Private, None)
            .await
            .unwrap();

        let result = svc.delete_file(file.id, "user2").await;
        assert!(matches!(result, Err(FileStorageError::PermissionDenied { .. })));
    }

    #[tokio::test]
    async fn test_restore_version() {
        let (svc, tmp) = setup().await;

        let source_v1 = write_test_file(&tmp, "file.txt", b"version 1").await;
        let file = svc
            .upload_file(&source_v1, "user1", AccessLevel::Private, None)
            .await
            .unwrap();

        let source_v2 = write_test_file(&tmp, "file_v2.txt", b"version 2").await;
        svc.upload_new_version(file.id, &source_v2, "user1", None)
            .await
            .unwrap();

        let content = svc.read_file_as_text(file.id, "user1").await.unwrap();
        assert_eq!(content, "version 2");

        svc.restore_version(file.id, 1, "user1").await.unwrap();

        let content = svc.read_file_as_text(file.id, "user1").await.unwrap();
        assert_eq!(content, "version 1");
    }

    #[tokio::test]
    async fn test_search_files() {
        let (svc, tmp) = setup().await;

        let s1 = write_test_file(&tmp, "readme.md", b"# Readme").await;
        let s2 = write_test_file(&tmp, "config.toml", b"[config]").await;

        svc.upload_file(&s1, "user1", AccessLevel::Private, None)
            .await
            .unwrap();
        svc.upload_file(&s2, "user1", AccessLevel::Private, None)
            .await
            .unwrap();

        let results = svc.search_files("readme", "user1").await.unwrap();
        assert_eq!(results.len(), 1);
        assert_eq!(results[0].original_filename, "readme.md");
    }

    #[tokio::test]
    async fn test_storage_stats() {
        let (svc, tmp) = setup().await;

        let s1 = write_test_file(&tmp, "a.txt", b"hello").await;
        let s2 = write_test_file(&tmp, "b.txt", b"world!").await;

        svc.upload_file(&s1, "user1", AccessLevel::Private, None)
            .await
            .unwrap();
        svc.upload_file(&s2, "user1", AccessLevel::Private, None)
            .await
            .unwrap();

        let stats = svc.storage_stats("user1").await.unwrap();
        assert_eq!(stats.file_count, 2);
        assert_eq!(stats.db_total_size, 11);
    }

    #[tokio::test]
    async fn test_read_version_content() {
        let (svc, tmp) = setup().await;

        let s1 = write_test_file(&tmp, "data.txt", b"v1 data").await;
        let file = svc
            .upload_file(&s1, "user1", AccessLevel::Private, None)
            .await
            .unwrap();

        let s2 = write_test_file(&tmp, "data_v2.txt", b"v2 data").await;
        svc.upload_new_version(file.id, &s2, "user1", None)
            .await
            .unwrap();

        let v1_content = svc.read_version_content(file.id, 1, "user1").await.unwrap();
        assert_eq!(v1_content, b"v1 data");

        let v2_content = svc.read_version_content(file.id, 2, "user1").await.unwrap();
        assert_eq!(v2_content, b"v2 data");
    }
}
