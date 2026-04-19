use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::fmt;
use uuid::Uuid;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum AccessLevel {
    Private,
    Shared,
    Public,
}

impl fmt::Display for AccessLevel {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            AccessLevel::Private => write!(f, "private"),
            AccessLevel::Shared => write!(f, "shared"),
            AccessLevel::Public => write!(f, "public"),
        }
    }
}

impl AccessLevel {
    pub fn from_str(s: &str) -> Option<Self> {
        match s.to_lowercase().as_str() {
            "private" => Some(AccessLevel::Private),
            "shared" => Some(AccessLevel::Shared),
            "public" => Some(AccessLevel::Public),
            _ => None,
        }
    }

    pub fn as_str(&self) -> &'static str {
        match self {
            AccessLevel::Private => "private",
            AccessLevel::Shared => "shared",
            AccessLevel::Public => "public",
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct UploadedFile {
    pub id: Uuid,
    pub original_filename: String,
    pub stored_filename: String,
    pub file_type: String,
    pub mime_type: String,
    pub size_in_bytes: i64,
    pub sha256_hash: String,
    pub uploaded_at: DateTime<Utc>,
    pub updated_at: Option<DateTime<Utc>>,
    pub access_level: AccessLevel,
    pub owner_id: String,
    pub current_version: i32,
    pub description: Option<String>,
    pub metadata: Option<serde_json::Value>,
}

impl UploadedFile {
    pub fn new(
        original_filename: String,
        stored_filename: String,
        file_type: String,
        mime_type: String,
        size_in_bytes: i64,
        sha256_hash: String,
        owner_id: String,
    ) -> Self {
        Self {
            id: Uuid::new_v4(),
            original_filename,
            stored_filename,
            file_type,
            mime_type,
            size_in_bytes,
            sha256_hash,
            uploaded_at: Utc::now(),
            updated_at: None,
            access_level: AccessLevel::Private,
            owner_id,
            current_version: 1,
            description: None,
            metadata: None,
        }
    }

    pub fn is_text_based(&self) -> bool {
        matches!(
            self.file_type.as_str(),
            "text" | "code" | "config" | "log" | "markdown" | "json" | "yaml" | "toml" | "xml"
                | "csv" | "html" | "css" | "sql"
        )
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FileVersion {
    pub id: Uuid,
    pub file_id: Uuid,
    pub version: i32,
    pub stored_filename: String,
    pub size_in_bytes: i64,
    pub sha256_hash: String,
    pub uploaded_at: DateTime<Utc>,
    pub change_description: Option<String>,
}

impl FileVersion {
    pub fn new(
        file_id: Uuid,
        version: i32,
        stored_filename: String,
        size_in_bytes: i64,
        sha256_hash: String,
    ) -> Self {
        Self {
            id: Uuid::new_v4(),
            file_id,
            version,
            stored_filename,
            size_in_bytes,
            sha256_hash,
            uploaded_at: Utc::now(),
            change_description: None,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum PermissionType {
    Read,
    Write,
}

impl fmt::Display for PermissionType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            PermissionType::Read => write!(f, "read"),
            PermissionType::Write => write!(f, "write"),
        }
    }
}

impl PermissionType {
    pub fn from_str(s: &str) -> Option<Self> {
        match s.to_lowercase().as_str() {
            "read" => Some(PermissionType::Read),
            "write" => Some(PermissionType::Write),
            _ => None,
        }
    }

    pub fn as_str(&self) -> &'static str {
        match self {
            PermissionType::Read => "read",
            PermissionType::Write => "write",
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FilePermission {
    pub id: Uuid,
    pub file_id: Uuid,
    pub user_id: String,
    pub permission_type: PermissionType,
    pub granted_at: DateTime<Utc>,
    pub granted_by: String,
}

impl FilePermission {
    pub fn new(
        file_id: Uuid,
        user_id: String,
        permission_type: PermissionType,
        granted_by: String,
    ) -> Self {
        Self {
            id: Uuid::new_v4(),
            file_id,
            user_id,
            permission_type,
            granted_at: Utc::now(),
            granted_by,
        }
    }
}

pub fn detect_file_type(filename: &str) -> &'static str {
    let ext = filename.rsplit('.').next().unwrap_or("").to_lowercase();
    match ext.as_str() {
        "rs" | "py" | "js" | "ts" | "go" | "java" | "c" | "cpp" | "h" | "hpp" | "cs"
        | "rb" | "php" | "swift" | "kt" | "scala" | "sh" | "bash" | "zsh" | "ps1" => "code",
        "txt" | "text" => "text",
        "md" | "markdown" => "markdown",
        "json" => "json",
        "yaml" | "yml" => "yaml",
        "toml" => "toml",
        "xml" => "xml",
        "html" | "htm" => "html",
        "css" | "scss" | "sass" | "less" => "css",
        "csv" | "tsv" => "csv",
        "sql" => "sql",
        "log" => "log",
        "ini" | "cfg" | "conf" | "env" | "properties" => "config",
        "png" | "jpg" | "jpeg" | "gif" | "bmp" | "svg" | "webp" | "ico" => "image",
        "pdf" => "pdf",
        "doc" | "docx" => "document",
        "xls" | "xlsx" => "spreadsheet",
        "zip" | "tar" | "gz" | "bz2" | "7z" | "rar" => "archive",
        _ => "unknown",
    }
}

pub fn detect_mime_type(filename: &str) -> String {
    mime_guess::from_path(filename)
        .first_or_octet_stream()
        .to_string()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_access_level_display() {
        assert_eq!(AccessLevel::Private.to_string(), "private");
        assert_eq!(AccessLevel::Shared.to_string(), "shared");
        assert_eq!(AccessLevel::Public.to_string(), "public");
    }

    #[test]
    fn test_access_level_from_str() {
        assert_eq!(AccessLevel::from_str("private"), Some(AccessLevel::Private));
        assert_eq!(AccessLevel::from_str("SHARED"), Some(AccessLevel::Shared));
        assert_eq!(AccessLevel::from_str("Public"), Some(AccessLevel::Public));
        assert_eq!(AccessLevel::from_str("invalid"), None);
    }

    #[test]
    fn test_uploaded_file_new() {
        let file = UploadedFile::new(
            "test.rs".to_string(),
            "abc123.rs".to_string(),
            "code".to_string(),
            "text/x-rust".to_string(),
            1024,
            "sha256hash".to_string(),
            "user1".to_string(),
        );
        assert_eq!(file.original_filename, "test.rs");
        assert_eq!(file.access_level, AccessLevel::Private);
        assert_eq!(file.current_version, 1);
        assert!(file.is_text_based());
    }

    #[test]
    fn test_is_text_based() {
        let make_file = |ft: &str| UploadedFile {
            id: Uuid::new_v4(),
            original_filename: "test".to_string(),
            stored_filename: "test".to_string(),
            file_type: ft.to_string(),
            mime_type: "application/octet-stream".to_string(),
            size_in_bytes: 0,
            sha256_hash: "".to_string(),
            uploaded_at: Utc::now(),
            updated_at: None,
            access_level: AccessLevel::Private,
            owner_id: "user1".to_string(),
            current_version: 1,
            description: None,
            metadata: None,
        };

        assert!(make_file("code").is_text_based());
        assert!(make_file("markdown").is_text_based());
        assert!(make_file("json").is_text_based());
        assert!(!make_file("image").is_text_based());
        assert!(!make_file("archive").is_text_based());
    }

    #[test]
    fn test_detect_file_type() {
        assert_eq!(detect_file_type("main.rs"), "code");
        assert_eq!(detect_file_type("readme.md"), "markdown");
        assert_eq!(detect_file_type("config.toml"), "toml");
        assert_eq!(detect_file_type("data.json"), "json");
        assert_eq!(detect_file_type("style.css"), "css");
        assert_eq!(detect_file_type("photo.png"), "image");
        assert_eq!(detect_file_type("doc.pdf"), "pdf");
        assert_eq!(detect_file_type("archive.zip"), "archive");
        assert_eq!(detect_file_type("unknown.xyz"), "unknown");
        assert_eq!(detect_file_type("noext"), "unknown");
    }

    #[test]
    fn test_detect_mime_type() {
        assert_eq!(detect_mime_type("test.json"), "application/json");
        assert_eq!(detect_mime_type("test.html"), "text/html");
        assert!(detect_mime_type("test.rs").contains("rust") || detect_mime_type("test.rs") == "application/octet-stream");
    }

    #[test]
    fn test_file_version_new() {
        let file_id = Uuid::new_v4();
        let ver = FileVersion::new(file_id, 2, "stored_v2.rs".to_string(), 2048, "hash2".to_string());
        assert_eq!(ver.file_id, file_id);
        assert_eq!(ver.version, 2);
        assert!(ver.change_description.is_none());
    }

    #[test]
    fn test_permission_type() {
        assert_eq!(PermissionType::from_str("read"), Some(PermissionType::Read));
        assert_eq!(PermissionType::from_str("WRITE"), Some(PermissionType::Write));
        assert_eq!(PermissionType::from_str("exec"), None);
        assert_eq!(PermissionType::Read.to_string(), "read");
    }

    #[test]
    fn test_file_permission_new() {
        let file_id = Uuid::new_v4();
        let perm = FilePermission::new(
            file_id,
            "user2".to_string(),
            PermissionType::Read,
            "user1".to_string(),
        );
        assert_eq!(perm.file_id, file_id);
        assert_eq!(perm.user_id, "user2");
        assert_eq!(perm.granted_by, "user1");
    }
}
