-- 扩展 workflow_tasks 表以支持重试历史
ALTER TABLE workflow_tasks ADD COLUMN retry_history TEXT;  -- JSON，重试历史记录

-- 扩展 workflows 表以支持断点恢复
ALTER TABLE workflows ADD COLUMN last_completed_task TEXT;  -- 最后完成的任务 ID
ALTER TABLE workflows ADD COLUMN checkpoint_data TEXT;       -- JSON，断点数据
ALTER TABLE workflows ADD COLUMN total_tasks INTEGER DEFAULT 0;  -- 总任务数
ALTER TABLE workflows ADD COLUMN completed_tasks INTEGER DEFAULT 0;  -- 已完成任务数

-- 创建 workflow_execution_log 表记录执行日志
CREATE TABLE IF NOT EXISTS workflow_execution_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    workflow_id TEXT NOT NULL,
    task_id TEXT,
    event_type TEXT NOT NULL,  -- 'workflow_start', 'workflow_complete', 'task_start', 'task_complete', 'task_fail', 'task_retry'
    event_data TEXT,  -- JSON，事件详细数据
    timestamp TIMESTAMP NOT NULL,
    FOREIGN KEY (workflow_id) REFERENCES workflows(id) ON DELETE CASCADE
);

-- 索引优化
CREATE INDEX IF NOT EXISTS idx_workflow_execution_log_workflow_id ON workflow_execution_log(workflow_id);
CREATE INDEX IF NOT EXISTS idx_workflow_execution_log_timestamp ON workflow_execution_log(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_workflows_last_completed_task ON workflows(last_completed_task);
