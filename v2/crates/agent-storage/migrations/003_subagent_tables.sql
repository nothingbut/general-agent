-- Create stages table first (referenced by subagent_sessions.stage_id)
-- Stores workflow stages for subagent session grouping
CREATE TABLE IF NOT EXISTS stages (
    id TEXT PRIMARY KEY,
    parent_session_id TEXT NOT NULL,
    name TEXT NOT NULL,
    -- Stage status values: Running, Completed, Failed
    status TEXT NOT NULL DEFAULT 'Running' CHECK (status IN ('Running', 'Completed', 'Failed')),
    -- Using TEXT for timestamps to maintain consistency with existing sessions table
    created_at TEXT NOT NULL,
    completed_at TEXT,
    FOREIGN KEY (parent_session_id) REFERENCES sessions(id) ON DELETE CASCADE
);

-- Create indexes for stages
CREATE INDEX IF NOT EXISTS idx_stages_parent_session_id ON stages(parent_session_id);
CREATE INDEX IF NOT EXISTS idx_stages_status ON stages(status);

-- Create subagent_sessions table
-- Links subagent sessions to their parent sessions and workflow stages
CREATE TABLE IF NOT EXISTS subagent_sessions (
    session_id TEXT PRIMARY KEY,
    parent_id TEXT NOT NULL,
    -- Session type is always 'Subagent' for this table
    session_type TEXT NOT NULL DEFAULT 'Subagent' CHECK (session_type = 'Subagent'),
    -- Valid session statuses: Idle, Running, Completed, Failed, Cancelled
    status TEXT NOT NULL DEFAULT 'Idle' CHECK (status IN ('Idle', 'Running', 'Completed', 'Failed', 'Cancelled')),
    -- stage_id is nullable to allow stage deletion without deleting subagent sessions
    stage_id TEXT,
    -- Using TEXT for timestamps to maintain consistency with existing sessions table
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    -- Cascade delete when either session or parent is deleted
    FOREIGN KEY (session_id) REFERENCES sessions(id) ON DELETE CASCADE,
    FOREIGN KEY (parent_id) REFERENCES sessions(id) ON DELETE CASCADE,
    -- Set to NULL when stage is deleted, preserving the subagent session
    FOREIGN KEY (stage_id) REFERENCES stages(id) ON DELETE SET NULL
);

-- Create indexes for subagent_sessions
CREATE INDEX IF NOT EXISTS idx_subagent_sessions_parent_id ON subagent_sessions(parent_id);
CREATE INDEX IF NOT EXISTS idx_subagent_sessions_stage_id ON subagent_sessions(stage_id);
CREATE INDEX IF NOT EXISTS idx_subagent_sessions_status ON subagent_sessions(status);
-- Composite index for common query pattern: find subagents by parent and status
CREATE INDEX IF NOT EXISTS idx_subagent_sessions_parent_status ON subagent_sessions(parent_id, status);
