-- Create subagent_sessions table
CREATE TABLE IF NOT EXISTS subagent_sessions (
    session_id TEXT PRIMARY KEY,
    parent_id TEXT NOT NULL,
    session_type TEXT NOT NULL DEFAULT 'Subagent',
    status TEXT NOT NULL DEFAULT 'Idle',
    stage_id TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY (session_id) REFERENCES sessions(id) ON DELETE CASCADE,
    FOREIGN KEY (parent_id) REFERENCES sessions(id) ON DELETE CASCADE
);

-- Create indexes for subagent_sessions
CREATE INDEX idx_subagent_sessions_parent_id ON subagent_sessions(parent_id);
CREATE INDEX idx_subagent_sessions_stage_id ON subagent_sessions(stage_id);
CREATE INDEX idx_subagent_sessions_status ON subagent_sessions(status);

-- Create stages table
CREATE TABLE IF NOT EXISTS stages (
    id TEXT PRIMARY KEY,
    parent_session_id TEXT NOT NULL,
    name TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'Running',
    created_at TEXT NOT NULL,
    completed_at TEXT,
    FOREIGN KEY (parent_session_id) REFERENCES sessions(id) ON DELETE CASCADE
);

-- Create indexes for stages
CREATE INDEX idx_stages_parent_session_id ON stages(parent_session_id);
CREATE INDEX idx_stages_status ON stages(status);
