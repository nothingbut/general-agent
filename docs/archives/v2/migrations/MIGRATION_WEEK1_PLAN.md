# Workflow 迁移 Week 1 执行计划

**日期**: 2026-03-13 - 2026-03-20
**目标**: Orchestrator + Executor 核心功能
**状态**: 准备开始

---

## 🎯 本周目标

完成 Workflow 系统的核心编排和执行逻辑：
1. ✅ 工作流模型定义（Workflow, Task, TaskStatus）
2. ✅ DAG 依赖解析（petgraph 集成）
3. ✅ 基础任务调度器（并行执行）
4. ✅ 任务执行引擎（重试、超时、取消）
5. ✅ 基础测试（单元测试 + 简单集成测试）

**验收标准**: 能够执行包含 3-5 个任务的简单 DAG 工作流

---

## 📅 Day-by-Day 计划

### Day 1: 项目设置和模型定义（3-4 小时）

#### Task 1.1: 创建分支和目录结构
```bash
cd v2/
git checkout -b feature/workflow-migration

# 创建目录结构
mkdir -p crates/agent-workflow/src/workflow
cd crates/agent-workflow/src/workflow
touch mod.rs models.rs orchestrator.rs executor.rs
```

#### Task 1.2: 添加依赖
编辑 `crates/agent-workflow/Cargo.toml`:
```toml
[dependencies]
# 已有依赖...

# 新增：
petgraph = "0.6"              # DAG 图结构
futures = { workspace = true } # Future 工具
tokio = { workspace = true, features = ["full"] }
```

#### Task 1.3: 定义核心模型
创建 `crates/agent-workflow/src/workflow/models.rs`:
```rust
use serde::{Deserialize, Serialize};
use uuid::Uuid;
use chrono::{DateTime, Utc};
use std::collections::HashMap;

/// 工作流定义
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Workflow {
    pub id: String,
    pub name: String,
    pub tasks: Vec<Task>,
    pub created_at: DateTime<Utc>,
}

/// 任务定义
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Task {
    pub id: String,
    pub name: String,
    pub task_type: TaskType,
    pub dependencies: Vec<String>, // Task IDs
    pub config: TaskConfig,
    pub metadata: HashMap<String, String>,
}

/// 任务类型
#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum TaskType {
    LLMCall,
    SkillExecution,
    MCPToolCall,
    Subworkflow,
    Custom(String),
}

/// 任务配置
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TaskConfig {
    pub retry_count: u32,
    pub timeout_secs: u64,
    pub priority: i32,
}

/// 任务状态
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum TaskStatus {
    Pending,
    Running,
    Completed,
    Failed(String),
    Cancelled,
}

/// 工作流执行结果
#[derive(Debug)]
pub struct WorkflowResult {
    pub workflow_id: String,
    pub task_results: HashMap<String, TaskResult>,
    pub execution_time_ms: u64,
}

/// 任务执行结果
#[derive(Debug, Clone)]
pub struct TaskResult {
    pub task_id: String,
    pub status: TaskStatus,
    pub output: Option<String>,
    pub error: Option<String>,
    pub execution_time_ms: u64,
}
```

**验收**: `cargo check` 通过

---

### Day 2: DAG 依赖解析（4-5 小时）

#### Task 2.1: 实现 DAG 构建器
创建 `crates/agent-workflow/src/workflow/orchestrator.rs`:
```rust
use petgraph::Graph;
use petgraph::graph::NodeIndex;
use std::collections::HashMap;
use anyhow::{Result, bail};

use super::models::{Workflow, Task};

pub struct WorkflowOrchestrator {
    graph: Graph<String, ()>, // 节点=TaskID，边=依赖关系
    task_map: HashMap<String, Task>,
    node_map: HashMap<String, NodeIndex>, // TaskID -> NodeIndex
}

impl WorkflowOrchestrator {
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

        // 2. 添加依赖边
        for task in task_map.values() {
            let from_idx = node_map[&task.id];
            for dep_id in &task.dependencies {
                if let Some(&to_idx) = node_map.get(dep_id) {
                    graph.add_edge(to_idx, from_idx, ());
                } else {
                    bail!("Dependency not found: {}", dep_id);
                }
            }
        }

        // 3. 检测循环依赖
        if petgraph::algo::is_cyclic_directed(&graph) {
            bail!("Cyclic dependency detected");
        }

        Ok(Self {
            graph,
            task_map,
            node_map,
        })
    }

    /// 获取可以立即执行的任务（所有依赖已完成）
    pub fn get_ready_tasks(&self, completed: &[String]) -> Vec<String> {
        let completed_set: std::collections::HashSet<_> =
            completed.iter().collect();

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

    pub fn get_task(&self, task_id: &str) -> Option<&Task> {
        self.task_map.get(task_id)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::workflow::models::*;

    #[test]
    fn test_simple_dag() {
        let workflow = Workflow {
            id: "test".to_string(),
            name: "Test".to_string(),
            tasks: vec![
                Task {
                    id: "A".to_string(),
                    name: "Task A".to_string(),
                    task_type: TaskType::Custom("test".to_string()),
                    dependencies: vec![],
                    config: TaskConfig {
                        retry_count: 0,
                        timeout_secs: 10,
                        priority: 0,
                    },
                    metadata: Default::default(),
                },
                Task {
                    id: "B".to_string(),
                    name: "Task B".to_string(),
                    task_type: TaskType::Custom("test".to_string()),
                    dependencies: vec!["A".to_string()],
                    config: TaskConfig {
                        retry_count: 0,
                        timeout_secs: 10,
                        priority: 0,
                    },
                    metadata: Default::default(),
                },
            ],
            created_at: chrono::Utc::now(),
        };

        let orch = WorkflowOrchestrator::new(workflow).unwrap();

        // 初始：只有 A 可以执行
        let ready = orch.get_ready_tasks(&[]);
        assert_eq!(ready, vec!["A"]);

        // A 完成后：B 可以执行
        let ready = orch.get_ready_tasks(&["A".to_string()]);
        assert_eq!(ready, vec!["B"]);
    }
}
```

**验收**: `cargo test -p agent-workflow` 通过

---

### Day 3: 任务执行器框架（4-5 小时）

#### Task 3.1: 实现基础执行器
创建 `crates/agent-workflow/src/workflow/executor.rs`:
```rust
use tokio::time::{timeout, Duration};
use anyhow::{Result, bail};

use super::models::*;

pub struct TaskExecutor;

impl TaskExecutor {
    pub fn new() -> Self {
        Self
    }

    /// 执行单个任务
    pub async fn execute_task(&self, task: &Task) -> TaskResult {
        let start = std::time::Instant::now();

        // 应用超时
        let result = timeout(
            Duration::from_secs(task.config.timeout_secs),
            self.execute_task_inner(task)
        ).await;

        let elapsed = start.elapsed().as_millis() as u64;

        match result {
            Ok(Ok(output)) => TaskResult {
                task_id: task.id.clone(),
                status: TaskStatus::Completed,
                output: Some(output),
                error: None,
                execution_time_ms: elapsed,
            },
            Ok(Err(e)) => TaskResult {
                task_id: task.id.clone(),
                status: TaskStatus::Failed(e.to_string()),
                output: None,
                error: Some(e.to_string()),
                execution_time_ms: elapsed,
            },
            Err(_) => TaskResult {
                task_id: task.id.clone(),
                status: TaskStatus::Failed("Timeout".to_string()),
                output: None,
                error: Some("Task execution timeout".to_string()),
                execution_time_ms: elapsed,
            },
        }
    }

    /// 执行任务的内部逻辑（带重试）
    async fn execute_task_inner(&self, task: &Task) -> Result<String> {
        let mut last_error = None;

        for attempt in 0..=task.config.retry_count {
            if attempt > 0 {
                // 指数退避
                let delay = Duration::from_millis(100 * 2u64.pow(attempt - 1));
                tokio::time::sleep(delay).await;
            }

            match self.execute_task_once(task).await {
                Ok(output) => return Ok(output),
                Err(e) => {
                    last_error = Some(e);
                }
            }
        }

        Err(last_error.unwrap())
    }

    /// 执行任务一次（实际的任务逻辑）
    async fn execute_task_once(&self, task: &Task) -> Result<String> {
        match &task.task_type {
            TaskType::Custom(name) => {
                // TODO: 集成实际的任务执行逻辑
                // 暂时返回模拟结果
                Ok(format!("Executed custom task: {}", name))
            }
            TaskType::LLMCall => {
                bail!("LLMCall not implemented yet")
            }
            TaskType::SkillExecution => {
                bail!("SkillExecution not implemented yet")
            }
            TaskType::MCPToolCall => {
                bail!("MCPToolCall not implemented yet")
            }
            TaskType::Subworkflow => {
                bail!("Subworkflow not implemented yet")
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_execute_simple_task() {
        let executor = TaskExecutor::new();

        let task = Task {
            id: "test".to_string(),
            name: "Test Task".to_string(),
            task_type: TaskType::Custom("hello".to_string()),
            dependencies: vec![],
            config: TaskConfig {
                retry_count: 0,
                timeout_secs: 5,
                priority: 0,
            },
            metadata: Default::default(),
        };

        let result = executor.execute_task(&task).await;
        assert_eq!(result.status, TaskStatus::Completed);
    }
}
```

**验收**: `cargo test -p agent-workflow` 通过

---

### Day 4: 集成 Orchestrator + Executor（4-5 小时）

#### Task 4.1: 实现工作流执行主循环
在 `orchestrator.rs` 中添加:
```rust
impl WorkflowOrchestrator {
    /// 执行整个工作流
    pub async fn execute(&self, executor: &TaskExecutor) -> Result<WorkflowResult> {
        use std::collections::HashMap;
        use std::time::Instant;

        let start = Instant::now();
        let mut completed = Vec::new();
        let mut results = HashMap::new();

        loop {
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
            let mut handles = Vec::new();
            for task_id in ready_tasks {
                let task = self.get_task(&task_id).unwrap().clone();
                let executor = executor.clone(); // 需要实现 Clone

                let handle = tokio::spawn(async move {
                    executor.execute_task(&task).await
                });
                handles.push(handle);
            }

            // 3. 等待所有任务完成
            for handle in handles {
                let result = handle.await?;

                if result.status == TaskStatus::Completed {
                    completed.push(result.task_id.clone());
                } else {
                    // 任务失败，整个工作流失败
                    bail!("Task {} failed: {:?}", result.task_id, result.error);
                }

                results.insert(result.task_id.clone(), result);
            }
        }

        Ok(WorkflowResult {
            workflow_id: "".to_string(), // TODO: 从 workflow 获取
            task_results: results,
            execution_time_ms: start.elapsed().as_millis() as u64,
        })
    }
}
```

#### Task 4.2: 修复 Clone 问题
```rust
// 在 executor.rs 中添加
#[derive(Clone)]
pub struct TaskExecutor;
```

**验收**: 能够执行简单的 2-3 任务 DAG

---

### Day 5: 测试和文档（3-4 小时）

#### Task 5.1: 集成测试
创建 `crates/agent-workflow/tests/workflow_integration.rs`:
```rust
use agent_workflow::workflow::*;

#[tokio::test]
async fn test_simple_workflow_execution() {
    // 创建工作流：A -> B -> C
    let workflow = models::Workflow {
        id: "test-workflow".to_string(),
        name: "Test Workflow".to_string(),
        tasks: vec![
            models::Task {
                id: "A".to_string(),
                name: "Task A".to_string(),
                task_type: models::TaskType::Custom("test".to_string()),
                dependencies: vec![],
                config: models::TaskConfig {
                    retry_count: 2,
                    timeout_secs: 10,
                    priority: 0,
                },
                metadata: Default::default(),
            },
            models::Task {
                id: "B".to_string(),
                name: "Task B".to_string(),
                task_type: models::TaskType::Custom("test".to_string()),
                dependencies: vec!["A".to_string()],
                config: models::TaskConfig {
                    retry_count: 2,
                    timeout_secs: 10,
                    priority: 0,
                },
                metadata: Default::default(),
            },
            models::Task {
                id: "C".to_string(),
                name: "Task C".to_string(),
                task_type: models::TaskType::Custom("test".to_string()),
                dependencies: vec!["B".to_string()],
                config: models::TaskConfig {
                    retry_count: 2,
                    timeout_secs: 10,
                    priority: 0,
                },
                metadata: Default::default(),
            },
        ],
        created_at: chrono::Utc::now(),
    };

    let orchestrator = orchestrator::WorkflowOrchestrator::new(workflow).unwrap();
    let executor = executor::TaskExecutor::new();

    let result = orchestrator.execute(&executor).await.unwrap();

    // 验证所有任务完成
    assert_eq!(result.task_results.len(), 3);
    assert_eq!(
        result.task_results["A"].status,
        models::TaskStatus::Completed
    );
    assert_eq!(
        result.task_results["B"].status,
        models::TaskStatus::Completed
    );
    assert_eq!(
        result.task_results["C"].status,
        models::TaskStatus::Completed
    );
}
```

#### Task 5.2: 文档
创建 `crates/agent-workflow/README.md`:
```markdown
# Agent Workflow

Rust 实现的工作流编排系统，支持 DAG 依赖解析和并行任务执行。

## 功能

- ✅ DAG 工作流定义
- ✅ 依赖关系解析
- ✅ 并行任务执行
- ✅ 任务重试机制
- ✅ 超时控制
- ⏳ 取消支持（计划中）
- ⏳ 暂停/恢复（计划中）

## 使用示例

见 `tests/workflow_integration.rs`
```

**验收**:
- 所有测试通过
- 文档清晰

---

## 📊 Week 1 验收标准

### 功能验收
- [ ] 能创建包含 3-5 个任务的 Workflow
- [ ] Orchestrator 正确解析 DAG 依赖
- [ ] 检测循环依赖并报错
- [ ] Executor 能执行 Custom 类型任务
- [ ] 支持任务重试（指数退避）
- [ ] 支持任务超时
- [ ] 并行执行独立任务

### 代码质量
- [ ] 所有单元测试通过
- [ ] 至少 1 个集成测试通过
- [ ] `cargo clippy` 无警告
- [ ] `cargo fmt` 格式正确

### 文档
- [ ] README 说明使用方法
- [ ] 关键函数有文档注释
- [ ] 有使用示例

---

## 🚧 已知限制（本周）

1. **只支持 Custom 任务类型**
   - LLMCall, SkillExecution 等待下周集成

2. **无持久化**
   - 工作流状态只在内存中
   - 数据库集成下周进行

3. **无取消/暂停**
   - 只能等待任务完成或失败

4. **无性能监控**
   - 只有基本的执行时间统计

---

## 📝 Week 2 预览

下周将集成现有的 LLM/Skills/MCP 功能：
- TaskType::LLMCall 集成 agent-llm
- TaskType::SkillExecution 集成 agent-skills  - TaskType::MCPToolCall 集成 agent-mcp
- 工作流状态持久化（SQLite）
- 取消和暂停支持

---

**创建日期**: 2026-03-13
**执行周期**: Week 1
**预计完成**: 2026-03-20
