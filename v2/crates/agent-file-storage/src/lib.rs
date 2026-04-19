pub mod error;
pub mod file_reference;
pub mod models;
pub mod repository;
pub mod service;
pub mod storage;

pub use error::{FileStorageError, Result};
pub use file_reference::{parse_file_references, replace_file_references, FileReference, FileTarget};
pub use models::{
    detect_file_type, detect_mime_type, AccessLevel, FilePermission, FileVersion, PermissionType,
    UploadedFile,
};
pub use repository::FileRepository;
pub use service::{FileService, StorageStats};
pub use storage::{compute_sha256, FileStorage};
