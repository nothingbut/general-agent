-- 技能抽取历史表
CREATE TABLE IF NOT EXISTS extraction_history (
    id TEXT PRIMARY KEY NOT NULL,
    session_id TEXT NOT NULL,
    extracted_at TEXT NOT NULL DEFAULT (datetime('now')),
    status TEXT NOT NULL DEFAULT 'pending',
    skill_name TEXT,
    skill_namespace TEXT,
    message_count INTEGER NOT NULL DEFAULT 0,
    error_message TEXT
);

CREATE INDEX IF NOT EXISTS idx_extraction_history_session ON extraction_history(session_id);
CREATE INDEX IF NOT EXISTS idx_extraction_history_status ON extraction_history(status);
CREATE INDEX IF NOT EXISTS idx_extraction_history_skill ON extraction_history(skill_name);
