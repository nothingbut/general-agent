-- Phase 7.1: Agent Workflow Tables
-- Migration 007: Create workflow, task_executions, and workflow_approvals tables

-- 工作流主表
-- 存储工作流的基本信息、计划和执行状态
CREATE TABLE IF NOT EXISTS workflows (
    id TEXT PRIMARY KEY,
    session_id TEXT NOT NULL,
    goal TEXT NOT NULL,
    plan JSON NOT NULL,  -- 存储完整的任务计划（Task列表）
    status TEXT NOT NULL,  -- pending/running/completed/failed/cancelled
    current_task_id TEXT,  -- 当前正在执行的任务ID
    created_at TIMESTAMP NOT NULL,
    completed_at TIMESTAMP,
    FOREIGN KEY (session_id) REFERENCES sessions(id)
);

-- 任务执行表
-- 存储每个任务的执行记录和结果
CREATE TABLE IF NOT EXISTS task_executions (
    id TEXT PRIMARY KEY,
    workflow_id TEXT NOT NULL,
    task_id TEXT NOT NULL,  -- 任务在计划中的唯一ID
    task_name TEXT NOT NULL,
    tool_name TEXT NOT NULL,  -- mcp:*, skill:*, rag:*, llm:*
    params JSON NOT NULL,  -- 任务参数
    status TEXT NOT NULL,  -- pending/running/completed/failed/skipped
    result JSON,  -- 执行结果
    error TEXT,  -- 错误信息
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    retry_count INTEGER DEFAULT 0,
    FOREIGN KEY (workflow_id) REFERENCES workflows(id)
);

-- 审批记录表
-- 存储危险操作的用户审批记录
CREATE TABLE IF NOT EXISTS workflow_approvals (
    id TEXT PRIMARY KEY,
    workflow_id TEXT NOT NULL,
    task_id TEXT NOT NULL,  -- 关联的任务ID
    status TEXT NOT NULL,  -- pending/approved/rejected
    user_comment TEXT,  -- 用户的审批意见
    created_at TIMESTAMP NOT NULL,
    responded_at TIMESTAMP,
    FOREIGN KEY (workflow_id) REFERENCES workflows(id)
);

-- 性能优化索引
-- workflow 查询索引
CREATE INDEX IF NOT EXISTS idx_workflows_session
    ON workflows(session_id);

CREATE INDEX IF NOT EXISTS idx_workflows_status
    ON workflows(status);

CREATE INDEX IF NOT EXISTS idx_workflows_created
    ON workflows(created_at);

-- task_executions 查询索引
CREATE INDEX IF NOT EXISTS idx_task_executions_workflow
    ON task_executions(workflow_id);

CREATE INDEX IF NOT EXISTS idx_task_executions_status
    ON task_executions(status);

CREATE INDEX IF NOT EXISTS idx_task_executions_started
    ON task_executions(started_at);

-- workflow_approvals 查询索引
CREATE INDEX IF NOT EXISTS idx_approvals_workflow
    ON workflow_approvals(workflow_id);

CREATE INDEX IF NOT EXISTS idx_approvals_status
    ON workflow_approvals(status);

CREATE INDEX IF NOT EXISTS idx_approvals_created
    ON workflow_approvals(created_at);
