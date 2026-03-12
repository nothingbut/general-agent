-- Add total_tasks and completed_tasks columns to stages table
-- total_tasks: Planned number of tasks in this stage
-- completed_tasks: Number of tasks that have completed successfully
ALTER TABLE stages ADD COLUMN total_tasks INTEGER NOT NULL DEFAULT 0;
ALTER TABLE stages ADD COLUMN completed_tasks INTEGER NOT NULL DEFAULT 0;

-- Create index for progress queries
CREATE INDEX IF NOT EXISTS idx_stages_progress ON stages(total_tasks, completed_tasks);
