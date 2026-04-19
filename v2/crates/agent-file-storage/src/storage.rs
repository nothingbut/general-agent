use sha2::{Digest, Sha256};
use std::path::{Path, PathBuf};
use tokio::fs;
use uuid::Uuid;

use crate::error::{FileStorageError, Result};

pub struct FileStorage {
    base_dir: PathBuf,
    max_file_size: i64,
}

impl FileStorage {
    pub async fn new(base_dir: impl Into<PathBuf>, max_file_size: i64) -> Result<Self> {
        let base_dir = base_dir.into();
        fs::create_dir_all(&base_dir).await?;
        Ok(Self {
            base_dir,
            max_file_size,
        })
    }

    pub fn base_dir(&self) -> &Path {
        &self.base_dir
    }

    pub fn max_file_size(&self) -> i64 {
        self.max_file_size
    }

    pub async fn store_file(&self, data: &[u8], extension: &str) -> Result<(String, String)> {
        if data.len() as i64 > self.max_file_size {
            return Err(FileStorageError::FileTooLarge {
                size: data.len() as i64,
                max_size: self.max_file_size,
            });
        }

        let hash = compute_sha256(data);
        let stored_name = format!("{}.{}", Uuid::new_v4(), extension);
        let path = self.base_dir.join(&stored_name);

        fs::write(&path, data).await?;
        Ok((stored_name, hash))
    }

    pub async fn store_from_path(&self, source: &Path) -> Result<(String, String, i64)> {
        let metadata = fs::metadata(source).await?;
        let size = metadata.len() as i64;

        if size > self.max_file_size {
            return Err(FileStorageError::FileTooLarge {
                size,
                max_size: self.max_file_size,
            });
        }

        let data = fs::read(source).await?;
        let hash = compute_sha256(&data);

        let ext = source
            .extension()
            .and_then(|e| e.to_str())
            .unwrap_or("bin");
        let stored_name = format!("{}.{}", Uuid::new_v4(), ext);
        let path = self.base_dir.join(&stored_name);

        fs::write(&path, &data).await?;
        Ok((stored_name, hash, size))
    }

    pub async fn read_file(&self, stored_filename: &str) -> Result<Vec<u8>> {
        let path = self.base_dir.join(stored_filename);
        let data = fs::read(&path).await.map_err(|e| {
            if e.kind() == std::io::ErrorKind::NotFound {
                FileStorageError::FileNotFoundByName(stored_filename.to_string())
            } else {
                FileStorageError::IoError(e)
            }
        })?;
        Ok(data)
    }

    pub async fn read_file_as_text(&self, stored_filename: &str) -> Result<String> {
        let data = self.read_file(stored_filename).await?;
        String::from_utf8(data).map_err(|e| {
            FileStorageError::IoError(std::io::Error::new(
                std::io::ErrorKind::InvalidData,
                format!("非 UTF-8 文件: {}", e),
            ))
        })
    }

    pub async fn delete_stored_file(&self, stored_filename: &str) -> Result<()> {
        let path = self.base_dir.join(stored_filename);
        if path.exists() {
            fs::remove_file(&path).await?;
        }
        Ok(())
    }

    pub async fn file_exists(&self, stored_filename: &str) -> bool {
        self.base_dir.join(stored_filename).exists()
    }

    pub async fn storage_usage(&self) -> Result<(u64, u64)> {
        let mut total_size: u64 = 0;
        let mut file_count: u64 = 0;

        let mut entries = fs::read_dir(&self.base_dir).await?;
        while let Some(entry) = entries.next_entry().await? {
            if entry.file_type().await?.is_file() {
                total_size += entry.metadata().await?.len();
                file_count += 1;
            }
        }

        Ok((file_count, total_size))
    }
}

pub fn compute_sha256(data: &[u8]) -> String {
    let mut hasher = Sha256::new();
    hasher.update(data);
    hex::encode(hasher.finalize())
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;

    async fn setup_storage() -> (FileStorage, TempDir) {
        let tmp = TempDir::new().unwrap();
        let storage = FileStorage::new(tmp.path(), 10 * 1024 * 1024).await.unwrap();
        (storage, tmp)
    }

    #[tokio::test]
    async fn test_store_and_read_file() {
        let (storage, _tmp) = setup_storage().await;
        let data = b"fn main() { println!(\"hello\"); }";

        let (stored_name, hash) = storage.store_file(data, "rs").await.unwrap();
        assert!(stored_name.ends_with(".rs"));
        assert!(!hash.is_empty());

        let read_data = storage.read_file(&stored_name).await.unwrap();
        assert_eq!(read_data, data);
    }

    #[tokio::test]
    async fn test_store_from_path() {
        let (storage, tmp) = setup_storage().await;
        let source = tmp.path().join("input.txt");
        tokio::fs::write(&source, b"test content").await.unwrap();

        let (stored_name, hash, size) = storage.store_from_path(&source).await.unwrap();
        assert!(stored_name.ends_with(".txt"));
        assert!(!hash.is_empty());
        assert_eq!(size, 12);

        let data = storage.read_file(&stored_name).await.unwrap();
        assert_eq!(data, b"test content");
    }

    #[tokio::test]
    async fn test_read_as_text() {
        let (storage, _tmp) = setup_storage().await;
        let text = "你好世界";

        let (stored_name, _) = storage.store_file(text.as_bytes(), "txt").await.unwrap();
        let read_text = storage.read_file_as_text(&stored_name).await.unwrap();
        assert_eq!(read_text, text);
    }

    #[tokio::test]
    async fn test_file_too_large() {
        let tmp = TempDir::new().unwrap();
        let storage = FileStorage::new(tmp.path(), 100).await.unwrap();

        let data = vec![0u8; 200];
        let result = storage.store_file(&data, "bin").await;
        assert!(matches!(result, Err(FileStorageError::FileTooLarge { .. })));
    }

    #[tokio::test]
    async fn test_delete_file() {
        let (storage, _tmp) = setup_storage().await;
        let (stored_name, _) = storage.store_file(b"data", "txt").await.unwrap();

        assert!(storage.file_exists(&stored_name).await);
        storage.delete_stored_file(&stored_name).await.unwrap();
        assert!(!storage.file_exists(&stored_name).await);
    }

    #[tokio::test]
    async fn test_delete_nonexistent_file() {
        let (storage, _tmp) = setup_storage().await;
        storage.delete_stored_file("nonexistent.txt").await.unwrap();
    }

    #[tokio::test]
    async fn test_read_nonexistent_file() {
        let (storage, _tmp) = setup_storage().await;
        let result = storage.read_file("nonexistent.txt").await;
        assert!(matches!(result, Err(FileStorageError::FileNotFoundByName(_))));
    }

    #[tokio::test]
    async fn test_storage_usage() {
        let (storage, _tmp) = setup_storage().await;

        storage.store_file(b"hello", "txt").await.unwrap();
        storage.store_file(b"world!", "txt").await.unwrap();

        let (count, total) = storage.storage_usage().await.unwrap();
        assert_eq!(count, 2);
        assert_eq!(total, 11);
    }

    #[tokio::test]
    async fn test_compute_sha256() {
        let hash = compute_sha256(b"hello");
        assert_eq!(
            hash,
            "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824"
        );
    }
}
