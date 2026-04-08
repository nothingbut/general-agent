-- ==========================================
-- 计划任务功能数据库迁移脚本
-- 版本: 001
-- 创建时间: 2026-04-08
-- 说明: 创建 scheduled_tasks 和 task_executions 表
-- ==========================================

-- 1. 创建 scheduled_tasks 表
CREATE TABLE IF NOT EXISTS scheduled_tasks (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    owner_id TEXT NOT NULL,

    -- 调度配置
    schedule TEXT NOT NULL,
    schedule_type INTEGER NOT NULL,
    start_at TEXT NULL,
    end_at TEXT NULL,

    -- 任务配置
    task_type INTEGER NOT NULL,
    task_payload TEXT NOT NULL,
    max_retries INTEGER NOT NULL DEFAULT 3,
    timeout_seconds INTEGER NOT NULL DEFAULT 300,

    -- 状态管理
    status INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL,
    last_execution_at TEXT NULL,
    next_execution_at TEXT NULL,
    execution_count INTEGER NOT NULL DEFAULT 0,

    -- 元数据
    tags TEXT NULL,
    metadata TEXT NULL
);

-- 2. 创建 scheduled_tasks 表的索引
CREATE INDEX IF NOT EXISTS idx_scheduled_tasks_owner_id ON scheduled_tasks(owner_id);
CREATE INDEX IF NOT EXISTS idx_scheduled_tasks_status ON scheduled_tasks(status);
CREATE INDEX IF NOT EXISTS idx_scheduled_tasks_next_execution ON scheduled_tasks(next_execution_at);
CREATE INDEX IF NOT EXISTS idx_scheduled_tasks_created_at ON scheduled_tasks(created_at);

-- 3. 创建 task_executions 表
CREATE TABLE IF NOT EXISTS task_executions (
    id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,

    -- 执行信息
    started_at TEXT NOT NULL,
    completed_at TEXT NULL,
    status INTEGER NOT NULL DEFAULT 0,

    -- 结果和日志
    output TEXT NULL,
    error TEXT NULL,
    retry_count INTEGER NOT NULL DEFAULT 0,
    duration_ms INTEGER NULL,

    -- 元数据
    metadata TEXT NULL,

    -- 外键约束（级联删除）
    FOREIGN KEY (task_id) REFERENCES scheduled_tasks(id) ON DELETE CASCADE
);

-- 4. 创建 task_executions 表的索引
CREATE INDEX IF NOT EXISTS idx_task_executions_task_id ON task_executions(task_id);
CREATE INDEX IF NOT EXISTS idx_task_executions_started_at ON task_executions(started_at);
CREATE INDEX IF NOT EXISTS idx_task_executions_status ON task_executions(status);

-- ==========================================
-- 迁移完成
-- ==========================================
