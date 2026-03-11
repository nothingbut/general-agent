-- Create sessions table
CREATE TABLE IF NOT EXISTS sessions (
    id TEXT PRIMARY KEY NOT NULL,
    title TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    context TEXT NOT NULL DEFAULT '{}'
);

-- Create index on updated_at for sorting
CREATE INDEX IF NOT EXISTS idx_sessions_updated_at ON sessions(updated_at DESC);

-- Create index on title for searching
CREATE INDEX IF NOT EXISTS idx_sessions_title ON sessions(title);
