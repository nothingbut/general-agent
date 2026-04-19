-- 文件上传主表
CREATE TABLE IF NOT EXISTS uploaded_files (
    id TEXT PRIMARY KEY NOT NULL,
    original_filename TEXT NOT NULL,
    stored_filename TEXT NOT NULL,
    file_type TEXT NOT NULL,
    mime_type TEXT NOT NULL DEFAULT 'application/octet-stream',
    size_in_bytes INTEGER NOT NULL,
    sha256_hash TEXT NOT NULL,
    uploaded_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT,
    access_level TEXT NOT NULL DEFAULT 'private',
    owner_id TEXT NOT NULL,
    current_version INTEGER NOT NULL DEFAULT 1,
    description TEXT,
    metadata TEXT
);

-- 文件版本表
CREATE TABLE IF NOT EXISTS file_versions (
    id TEXT PRIMARY KEY NOT NULL,
    file_id TEXT NOT NULL,
    version INTEGER NOT NULL,
    stored_filename TEXT NOT NULL,
    size_in_bytes INTEGER NOT NULL,
    sha256_hash TEXT NOT NULL,
    uploaded_at TEXT NOT NULL DEFAULT (datetime('now')),
    change_description TEXT,
    FOREIGN KEY (file_id) REFERENCES uploaded_files(id) ON DELETE CASCADE
);

-- 文件权限表
CREATE TABLE IF NOT EXISTS file_permissions (
    id TEXT PRIMARY KEY NOT NULL,
    file_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    permission_type TEXT NOT NULL DEFAULT 'read',
    granted_at TEXT NOT NULL DEFAULT (datetime('now')),
    granted_by TEXT NOT NULL,
    FOREIGN KEY (file_id) REFERENCES uploaded_files(id) ON DELETE CASCADE,
    UNIQUE(file_id, user_id, permission_type)
);

-- 索引
CREATE INDEX IF NOT EXISTS idx_uploaded_files_owner ON uploaded_files(owner_id);
CREATE INDEX IF NOT EXISTS idx_uploaded_files_access_level ON uploaded_files(access_level);
CREATE INDEX IF NOT EXISTS idx_uploaded_files_filename ON uploaded_files(original_filename);
CREATE INDEX IF NOT EXISTS idx_file_versions_file_id ON file_versions(file_id);
CREATE INDEX IF NOT EXISTS idx_file_permissions_file_id ON file_permissions(file_id);
CREATE INDEX IF NOT EXISTS idx_file_permissions_user_id ON file_permissions(user_id);
