//! Workflow 编排器 - DAG 依赖解析和任务调度
//!
//! 本模块实现了基于 DAG（有向无环图）的工作流编排器，负责：
//! - 解析任务依赖关系
//! - 检测循环依赖
//! - 计算可执行任务
//! - 并行调度任务执行
//!
//! # 示例
//!
//! ```rust
//! use agent_workflow::workflow::*;
//!
//! # tokio_test::block_on(async {
//! // 创建工作流
//! let mut workflow = Workflow::new("test", "Test Workflow");
//! let mut task_a = Task::new("A", "Task A", TaskType::Custom("test".to_string()));
//! let mut task_b = Task::new("B", "Task B", TaskType::Custom("test".to_string()));
//! task_b.dependencies.push("A".to_string());
//!
//! workflow.add_task(task_a);
//! workflow.add_task(task_b);
//!
//! // 创建编排器并执行
//! let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
//! let executor = TaskExecutor::new();
//! let result = orchestrator.execute(&executor).await.unwrap();
//!
//! assert_eq!(result.task_results.len(), 2);
//! # });
//! ```

use petgraph::Graph;
use petgraph::graph::NodeIndex;
use std::collections::HashMap;
use std::sync::Arc;
use tokio::sync::RwLock;
use anyhow::{Result, bail};

use super::models::{Workflow, Task, WorkflowResult, TaskStatus, WorkflowStatus};
use super::executor::TaskExecutor;

/// 工作流控制状态
#[derive(Debug, Clone, PartialEq)]
pub enum ControlState {
    /// 正常运行
    Running,
    /// 请求取消
    CancelRequested,
    /// 请求暂停
    PauseRequested,
    /// 已暂停
    Paused,
}

/// 工作流编排器
pub struct WorkflowOrchestrator {
    /// 工作流 ID
    workflow_id: String,
    /// DAG 图结构 (节点=TaskID)
    /// 用于循环检测和拓扑排序
    #[allow(dead_code)]
    graph: Graph<String, ()>,
    /// 任务映射表 (TaskID -> Task)
    task_map: HashMap<String, Task>,
    /// 节点映射表 (TaskID -> NodeIndex)
    /// 用于构建 DAG 时的节点查找
    #[allow(dead_code)]
    node_map: HashMap<String, NodeIndex>,
    /// 控制状态（用于取消和暂停）
    control_state: Arc<RwLock<ControlState>>,
}

impl std::fmt::Debug for WorkflowOrchestrator {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("WorkflowOrchestrator")
            .field("workflow_id", &self.workflow_id)
            .field("task_count", &self.task_map.len())
            .finish()
    }
}

impl WorkflowOrchestrator {
    /// 从工作流创建编排器
    ///
    /// 构建 DAG 图结构并验证依赖关系。
    ///
    /// # 错误
    ///
    /// - 如果存在循环依赖，返回错误
    /// - 如果依赖的任务不存在，返回错误
    ///
    /// # 示例
    ///
    /// ```rust
    /// use agent_workflow::workflow::*;
    ///
    /// let mut workflow = Workflow::new("test", "Test");
    /// workflow.add_task(Task::new("A", "Task A", TaskType::Custom("test".to_string())));
    ///
    /// let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    /// ```
    pub fn new(workflow: Workflow) -> Result<Self> {
        let mut graph = Graph::new();
        let mut task_map = HashMap::new();
        let mut node_map = HashMap::new();
        let workflow_id = workflow.id.clone();

        // 1. 添加所有任务节点
        for task in workflow.tasks {
            let node_idx = graph.add_node(task.id.clone());
            node_map.insert(task.id.clone(), node_idx);
            task_map.insert(task.id.clone(), task);
        }

        // 2. 添加依赖边 (from -> to 表示 to 依赖 from)
        for task in task_map.values() {
            let to_idx = node_map[&task.id];
            for dep_id in &task.dependencies {
                if let Some(&from_idx) = node_map.get(dep_id) {
                    graph.add_edge(from_idx, to_idx, ());
                } else {
                    bail!("Dependency not found: {}", dep_id);
                }
            }
        }

        // 3. 检测循环依赖
        if petgraph::algo::is_cyclic_directed(&graph) {
            bail!("Cyclic dependency detected in workflow");
        }

        Ok(Self {
            workflow_id,
            graph,
            task_map,
            node_map,
            control_state: Arc::new(RwLock::new(ControlState::Running)),
        })
    }

    /// 请求取消工作流
    ///
    /// 设置取消标志，当前正在执行的任务会完成，但不会启动新任务。
    pub async fn cancel(&self) {
        let mut state = self.control_state.write().await;
        *state = ControlState::CancelRequested;
    }

    /// 请求暂停工作流
    ///
    /// 设置暂停标志，当前正在执行的任务会完成，然后工作流进入暂停状态。
    pub async fn pause(&self) {
        let mut state = self.control_state.write().await;
        if *state == ControlState::Running {
            *state = ControlState::PauseRequested;
        }
    }

    /// 恢复暂停的工作流
    ///
    /// 将暂停状态改为运行状态，允许继续执行。
    /// 注意：需要重新调用 execute() 来实际恢复执行。
    pub async fn resume(&self) {
        let mut state = self.control_state.write().await;
        if *state == ControlState::Paused {
            *state = ControlState::Running;
        }
    }

    /// 获取当前控制状态
    pub async fn get_control_state(&self) -> ControlState {
        self.control_state.read().await.clone()
    }

    /// 获取可以立即执行的任务（所有依赖已完成）
    ///
    /// 根据已完成的任务列表，计算哪些任务的依赖已全部满足。
    ///
    /// # 参数
    ///
    /// - `completed` - 已完成的任务 ID 列表
    ///
    /// # 返回
    ///
    /// 可以执行的任务 ID 列表（无特定顺序）
    pub fn get_ready_tasks(&self, completed: &[String]) -> Vec<String> {
        use std::collections::HashSet;

        let completed_set: HashSet<_> = completed.iter().collect();

        self.task_map
            .values()
            .filter(|task| {
                // 所有依赖都已完成
                task.dependencies.iter().all(|dep| completed_set.contains(dep))
            })
            .filter(|task| {
                // 自己还未完成
                !completed_set.contains(&task.id)
            })
            .map(|task| task.id.clone())
            .collect()
    }

    /// 获取任务
    pub fn get_task(&self, task_id: &str) -> Option<&Task> {
        self.task_map.get(task_id)
    }

    /// 获取任务总数
    pub fn task_count(&self) -> usize {
        self.task_map.len()
    }

    /// 执行整个工作流
    ///
    /// 按照 DAG 依赖关系，批次执行所有任务：
    /// 1. 计算可执行任务（依赖已满足）
    /// 2. 并行执行该批次的所有任务
    /// 3. 等待批次完成
    /// 4. 重复直到所有任务完成
    ///
    /// # 参数
    ///
    /// - `executor` - 任务执行器
    ///
    /// # 返回
    ///
    /// 工作流执行结果，包含所有任务的结果和总执行时间
    ///
    /// # 错误
    ///
    /// - 如果任何任务失败，立即停止工作流并返回错误
    /// - 如果工作流卡住（有任务但无法执行），返回错误
    ///
    /// # 示例
    ///
    /// ```rust
    /// # use agent_workflow::workflow::*;
    /// # tokio_test::block_on(async {
    /// let mut workflow = Workflow::new("test", "Test");
    /// workflow.add_task(Task::new("A", "Task A", TaskType::Custom("test".to_string())));
    ///
    /// let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    /// let executor = TaskExecutor::new();
    /// let result = orchestrator.execute(&executor).await.unwrap();
    ///
    /// assert_eq!(result.task_results.len(), 1);
    /// # });
    /// ```
    pub async fn execute(&self, executor: &TaskExecutor) -> Result<WorkflowResult> {
        use std::time::Instant;
        use futures::future::join_all;

        let start = Instant::now();
        let mut completed = Vec::new();
        let mut results = HashMap::new();

        loop {
            // 0. 检查控制状态
            let state = self.control_state.read().await.clone();
            match state {
                ControlState::CancelRequested => {
                    bail!("Workflow cancelled by user");
                }
                ControlState::PauseRequested => {
                    // 设置为已暂停状态
                    let mut state = self.control_state.write().await;
                    *state = ControlState::Paused;
                    bail!("Workflow paused by user");
                }
                ControlState::Paused => {
                    bail!("Workflow is paused, call resume() to continue");
                }
                ControlState::Running => {
                    // 继续执行
                }
            }

            // 1. 获取就绪任务
            let ready_tasks = self.get_ready_tasks(&completed);

            if ready_tasks.is_empty() {
                // 检查是否全部完成
                if completed.len() == self.task_map.len() {
                    break; // 成功完成
                } else {
                    // 有任务但无法执行（不应该发生）
                    bail!("Workflow stuck: some tasks cannot be executed");
                }
            }

            // 2. 并行执行所有就绪任务
            let handles: Vec<_> = ready_tasks
                .iter()
                .map(|task_id| {
                    let task = self.get_task(task_id).unwrap().clone();
                    let executor = executor.clone();

                    tokio::spawn(async move { executor.execute_task(&task).await })
                })
                .collect();

            // 3. 等待所有任务完成
            let task_results = join_all(handles).await;

            // 4. 处理结果
            for result in task_results {
                match result {
                    Ok(task_result) => {
                        if task_result.status == TaskStatus::Completed {
                            completed.push(task_result.task_id.clone());
                        } else {
                            // 任务失败，整个工作流失败
                            let task_id = task_result.task_id.clone();
                            results.insert(task_id.clone(), task_result);
                            bail!("Task {} failed", task_id);
                        }

                        results.insert(task_result.task_id.clone(), task_result);
                    }
                    Err(e) => {
                        bail!("Task execution panicked: {}", e);
                    }
                }
            }
        }

        Ok(WorkflowResult {
            workflow_id: self.workflow_id.clone(),
            task_results: results,
            execution_time_ms: start.elapsed().as_millis() as u64,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::workflow::models::*;

    fn create_test_task(id: &str, deps: Vec<&str>) -> Task {
        Task {
            id: id.to_string(),
            name: format!("Task {}", id),
            task_type: TaskType::Custom("test".to_string()),
            dependencies: deps.iter().map(|s| s.to_string()).collect(),
            config: TaskConfig::default(),
            metadata: HashMap::new(),
        }
    }

    #[test]
    fn test_simple_dag() {
        let mut workflow = Workflow::new("test", "Test Workflow");
        workflow.add_task(create_test_task("A", vec![]));
        workflow.add_task(create_test_task("B", vec!["A"]));

        let orch = WorkflowOrchestrator::new(workflow).unwrap();

        // 初始：只有 A 可以执行
        let ready = orch.get_ready_tasks(&[]);
        assert_eq!(ready, vec!["A"]);

        // A 完成后：B 可以执行
        let ready = orch.get_ready_tasks(&["A".to_string()]);
        assert_eq!(ready, vec!["B"]);
    }

    #[test]
    fn test_parallel_tasks() {
        let mut workflow = Workflow::new("test", "Test Workflow");
        workflow.add_task(create_test_task("A", vec![]));
        workflow.add_task(create_test_task("B", vec![]));
        workflow.add_task(create_test_task("C", vec!["A", "B"]));

        let orch = WorkflowOrchestrator::new(workflow).unwrap();

        // 初始：A 和 B 都可以执行
        let mut ready = orch.get_ready_tasks(&[]);
        ready.sort();
        assert_eq!(ready, vec!["A", "B"]);

        // A 完成：B 仍可执行（独立任务），但 C 还需要等 B
        let ready = orch.get_ready_tasks(&["A".to_string()]);
        assert_eq!(ready, vec!["B"]);

        // 只有 B 完成：C 还需要等 A（但这个分支不测试，因为上面已经测试了A完成的情况）

        // A 和 B 都完成：C 可以执行
        let ready = orch.get_ready_tasks(&["A".to_string(), "B".to_string()]);
        assert_eq!(ready, vec!["C"]);
    }

    #[test]
    fn test_cyclic_dependency() {
        let mut workflow = Workflow::new("test", "Test Workflow");
        workflow.add_task(create_test_task("A", vec!["B"]));
        workflow.add_task(create_test_task("B", vec!["A"]));

        let result = WorkflowOrchestrator::new(workflow);
        assert!(result.is_err());
        assert!(result.unwrap_err().to_string().contains("Cyclic"));
    }

    #[test]
    fn test_missing_dependency() {
        let mut workflow = Workflow::new("test", "Test Workflow");
        workflow.add_task(create_test_task("A", vec!["B"])); // B 不存在

        let result = WorkflowOrchestrator::new(workflow);
        assert!(result.is_err());
        assert!(result.unwrap_err().to_string().contains("Dependency not found"));
    }

    #[tokio::test]
    async fn test_execute_simple_workflow() {
        // 创建简单工作流：A -> B -> C
        let mut workflow = Workflow::new("test-wf", "Test Workflow");
        workflow.add_task(create_test_task("A", vec![]));
        workflow.add_task(create_test_task("B", vec!["A"]));
        workflow.add_task(create_test_task("C", vec!["B"]));

        let orch = WorkflowOrchestrator::new(workflow).unwrap();
        let executor = TaskExecutor::new();

        let result = orch.execute(&executor).await.unwrap();

        // 验证所有任务完成
        assert_eq!(result.task_results.len(), 3);
        assert_eq!(
            result.task_results["A"].status,
            TaskStatus::Completed
        );
        assert_eq!(
            result.task_results["B"].status,
            TaskStatus::Completed
        );
        assert_eq!(
            result.task_results["C"].status,
            TaskStatus::Completed
        );

        // 验证执行时间合理（至少大于 0）
        assert!(result.execution_time_ms > 0);
    }

    #[tokio::test]
    async fn test_execute_parallel_workflow() {
        // 创建并行工作流：A, B 并行 -> C 依赖 A+B
        let mut workflow = Workflow::new("test-wf", "Test Workflow");
        workflow.add_task(create_test_task("A", vec![]));
        workflow.add_task(create_test_task("B", vec![]));
        workflow.add_task(create_test_task("C", vec!["A", "B"]));

        let orch = WorkflowOrchestrator::new(workflow).unwrap();
        let executor = TaskExecutor::new();

        let result = orch.execute(&executor).await.unwrap();

        // 验证所有任务完成
        assert_eq!(result.task_results.len(), 3);
        for (task_id, task_result) in &result.task_results {
            assert_eq!(
                task_result.status,
                TaskStatus::Completed,
                "Task {} should be completed",
                task_id
            );
        }
    }

    #[tokio::test]
    async fn test_execute_complex_dag() {
        // 创建复杂 DAG：
        //     A
        //    / \
        //   B   C
        //    \ /
        //     D
        let mut workflow = Workflow::new("test-wf", "Test Workflow");
        workflow.add_task(create_test_task("A", vec![]));
        workflow.add_task(create_test_task("B", vec!["A"]));
        workflow.add_task(create_test_task("C", vec!["A"]));
        workflow.add_task(create_test_task("D", vec!["B", "C"]));

        let orch = WorkflowOrchestrator::new(workflow).unwrap();
        let executor = TaskExecutor::new();

        let result = orch.execute(&executor).await.unwrap();

        // 验证所有任务完成
        assert_eq!(result.task_results.len(), 4);
        assert!(result.task_results.values().all(|r| r.status == TaskStatus::Completed));
    }
}
