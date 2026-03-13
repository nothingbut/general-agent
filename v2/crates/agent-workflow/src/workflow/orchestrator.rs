//! Workflow 编排器 - DAG 依赖解析和任务调度

use petgraph::Graph;
use petgraph::graph::NodeIndex;
use std::collections::HashMap;
use anyhow::{Result, bail};

use super::models::{Workflow, Task};

/// 工作流编排器
#[derive(Debug)]
pub struct WorkflowOrchestrator {
    /// DAG 图结构 (节点=TaskID)
    graph: Graph<String, ()>,
    /// 任务映射表 (TaskID -> Task)
    task_map: HashMap<String, Task>,
    /// 节点映射表 (TaskID -> NodeIndex)
    node_map: HashMap<String, NodeIndex>,
}

impl WorkflowOrchestrator {
    /// 从工作流创建编排器
    pub fn new(workflow: Workflow) -> Result<Self> {
        let mut graph = Graph::new();
        let mut task_map = HashMap::new();
        let mut node_map = HashMap::new();

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
            graph,
            task_map,
            node_map,
        })
    }

    /// 获取可以立即执行的任务（所有依赖已完成）
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
}
