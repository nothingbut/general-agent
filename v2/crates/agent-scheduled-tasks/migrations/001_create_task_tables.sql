-- 计划任务表
CREATE TABLE IF NOT EXISTS scheduled_tasks (
    id TEXT PRIMARY KEY NOT NULL,
    name TEXT NOT NULL,
    description TEXT,
    owner_id TEXT NOT NULL,
    schedule TEXT NOT NULL,
    schedule_type INTEGER NOT NULL DEFAULT 0,
    task_type INTEGER NOT NULL DEFAULT 0,
    task_payload TEXT NOT NULL DEFAULT '{}',
    status INTEGER NOT NULL DEFAULT 0,
    max_retries INTEGER NOT NULL DEFAULT 3,
    timeout_seconds INTEGER NOT NULL DEFAULT 300,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT,
    last_execution_at TEXT,
    next_execution_at TEXT,
    execution_count INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_tasks_owner ON scheduled_tasks(owner_id);
CREATE INDEX IF NOT EXISTS idx_tasks_status ON scheduled_tasks(status);
CREATE INDEX IF NOT EXISTS idx_tasks_next_exec ON scheduled_tasks(next_execution_at);

-- 任务执行历史表
CREATE TABLE IF NOT EXISTS task_executions (
    id TEXT PRIMARY KEY NOT NULL,
    task_id TEXT NOT NULL,
    started_at TEXT NOT NULL DEFAULT (datetime('now')),
    completed_at TEXT,
    status INTEGER NOT NULL DEFAULT 0,
    result TEXT,
    error_message TEXT,
    retry_count INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (task_id) REFERENCES scheduled_tasks(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_executions_task ON task_executions(task_id);
CREATE INDEX IF NOT EXISTS idx_executions_status ON task_executions(status);
