# Subagent System Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a subagent system enabling concurrent execution of independent tasks with lifecycle management, TUI monitoring, and intelligent resource allocation.

**Architecture:** Multi-layered architecture with SubagentOrchestrator managing task lifecycle, SubagentTask executing in isolated tokio tasks with shared state via DashMap, and SessionCardWidget providing real-time TUI visualization. Database schema uses separate tables to maintain backward compatibility.

**Tech Stack:** Rust, tokio (async runtime), DashMap (concurrent map), sqlx (database), ratatui (TUI), tracing (logging), thiserror (error handling)

---

## Chunk 1: Foundation - Data Models and Database

### File Structure Overview

**New files to create:**
- `crates/agent-workflow/src/subagent/mod.rs` - Module entry point
- `crates/agent-workflow/src/subagent/error.rs` - Error types
- `crates/agent-workflow/src/subagent/models.rs` - Data structures
- `crates/agent-workflow/src/subagent/state.rs` - SubagentState and state management
- `crates/agent-workflow/src/subagent/config.rs` - Configuration structures
- `crates/agent-workflow/migrations/004_subagent_tables.sql` - Database migrations
- `crates/agent-workflow/tests/subagent_models_tests.rs` - Model tests

**Files to modify:**
- `crates/agent-workflow/src/lib.rs` - Add subagent module export
- `crates/agent-workflow/Cargo.toml` - Add dependencies (dashmap, thiserror)

---

### Task 1: Error Types Foundation

**Files:**
- Create: `crates/agent-workflow/src/subagent/error.rs`
- Test: `crates/agent-workflow/tests/subagent_error_tests.rs`

- [ ] **Step 1: Write failing test for error types**

Create `crates/agent-workflow/tests/subagent_error_tests.rs`:

```rust
use agent_workflow::subagent::SubagentError;
use uuid::Uuid;

#[test]
fn test_subagent_error_display() {
    let stage_id = "stage-123".to_string();
    let error = SubagentError::StageFailed(stage_id.clone());
    assert_eq!(format!("{}", error), format!("Stage {} failed", stage_id));
}

#[test]
fn test_timeout_error() {
    let session_id = Uuid::new_v4();
    let error = SubagentError::Timeout(session_id);
    assert!(format!("{}", error).contains("timed out"));
}

#[test]
fn test_error_conversion_from_sqlx() {
    let db_error = sqlx::Error::RowNotFound;
    let subagent_error: SubagentError = db_error.into();

    match subagent_error {
        SubagentError::DatabaseError(_) => {},
        _ => panic!("Expected DatabaseError variant"),
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cargo test --package agent-workflow --test subagent_error_tests`
Expected: Compilation error - "no module named subagent"

- [ ] **Step 3: Create error types module**

Create `crates/agent-workflow/src/subagent/error.rs`:

```rust
use std::path::PathBuf;
use thiserror::Error;
use uuid::Uuid;

/// Subagent system errors
#[derive(Error, Debug)]
pub enum SubagentError {
    #[error("Stage {0} failed")]
    StageFailed(String),

    #[error("Subagent {0} timed out")]
    Timeout(Uuid),

    #[error("Too many concurrent subagents (max: {0})")]
    TooManyConcurrentSubagents(usize),

    #[error("Too many stages (max: {0})")]
    TooManyStages(usize),

    #[error("Task creation failed: {0}")]
    TaskCreationFailed(String),

    #[error("Channel closed unexpectedly")]
    ChannelClosed,

    #[error("Database error: {0}")]
    DatabaseError(#[from] sqlx::Error),

    #[error("LLM error: {0}")]
    LLMError(String),

    #[error("Serialization error: {0}")]
    SerializationError(#[from] serde_json::Error),

    #[error("Panic in subagent: {0}")]
    PanicError(String),

    #[error("Permission denied")]
    PermissionDenied,

    #[error("Path not allowed: {0}")]
    PathNotAllowed(PathBuf),

    #[error("Configuration error: {0}")]
    ConfigError(String),

    #[error("Shutdown requested")]
    ShutdownRequested,
}

/// Result type alias for subagent operations
pub type SubagentResult<T> = Result<T, SubagentError>;
```

Create `crates/agent-workflow/src/subagent/mod.rs`:

```rust
pub mod error;

pub use error::{SubagentError, SubagentResult};
```

- [ ] **Step 4: Update lib.rs to export subagent module**

Modify `crates/agent-workflow/src/lib.rs`, add at the end:

```rust
pub mod subagent;
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cargo test --package agent-workflow --test subagent_error_tests`
Expected: All tests PASS

- [ ] **Step 6: Commit**

```bash
git add crates/agent-workflow/src/subagent/
git add crates/agent-workflow/src/lib.rs
git add crates/agent-workflow/tests/subagent_error_tests.rs
git commit -m "feat(subagent): add error types and SubagentResult alias"
```

---

### Task 2: Session Status and Type Enums

**Files:**
- Create: `crates/agent-workflow/src/subagent/models.rs`
- Test: `crates/agent-workflow/tests/subagent_models_tests.rs`

- [ ] **Step 1: Write failing test for enums**

Create `crates/agent-workflow/tests/subagent_models_tests.rs`:

```rust
use agent_workflow::subagent::models::{SessionStatus, SessionType};

#[test]
fn test_session_status_transitions() {
    let status = SessionStatus::Idle;
    assert!(matches!(status, SessionStatus::Idle));

    let running = SessionStatus::Running;
    assert!(matches!(running, SessionStatus::Running));
}

#[test]
fn test_session_type_distinction() {
    let main_type = SessionType::Main;
    let subagent_type = SessionType::Subagent;

    assert!(matches!(main_type, SessionType::Main));
    assert!(matches!(subagent_type, SessionType::Subagent));
}

#[test]
fn test_session_status_serialization() {
    let status = SessionStatus::Completed;
    let json = serde_json::to_string(&status).unwrap();
    let deserialized: SessionStatus = serde_json::from_str(&json).unwrap();

    assert!(matches!(deserialized, SessionStatus::Completed));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cargo test --package agent-workflow --test subagent_models_tests`
Expected: Compilation error - "no module named models in subagent"

- [ ] **Step 3: Implement enums**

Create `crates/agent-workflow/src/subagent/models.rs`:

```rust
use serde::{Deserialize, Serialize};

/// Session type - main or subagent
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum SessionType {
    Main,
    Subagent,
}

/// Session execution status
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum SessionStatus {
    Idle,
    Running,
    Completed,
    Failed,
    Cancelled,
}

impl Default for SessionStatus {
    fn default() -> Self {
        Self::Idle
    }
}
```

Update `crates/agent-workflow/src/subagent/mod.rs`:

```rust
pub mod error;
pub mod models;

pub use error::{SubagentError, SubagentResult};
pub use models::{SessionStatus, SessionType};
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cargo test --package agent-workflow --test subagent_models_tests`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add crates/agent-workflow/src/subagent/models.rs
git add crates/agent-workflow/tests/subagent_models_tests.rs
git commit -m "feat(subagent): add SessionType and SessionStatus enums"
```

---

### Task 3: SubagentState Structure

**Files:**
- Modify: `crates/agent-workflow/src/subagent/state.rs` (create new)
- Modify: `crates/agent-workflow/src/subagent/models.rs`
- Test: `crates/agent-workflow/tests/subagent_state_tests.rs`

- [ ] **Step 1: Write failing test for SubagentState**

Create `crates/agent-workflow/tests/subagent_state_tests.rs`:

```rust
use agent_workflow::subagent::state::SubagentState;
use agent_workflow::subagent::SessionStatus;
use uuid::Uuid;

#[test]
fn test_subagent_state_creation() {
    let session_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();
    let stage_id = "stage-1".to_string();

    let state = SubagentState::new(session_id, parent_id, stage_id.clone());

    assert_eq!(state.session_id, session_id);
    assert_eq!(state.parent_id, parent_id);
    assert_eq!(state.stage_id, stage_id);
    assert_eq!(state.status, SessionStatus::Idle);
    assert_eq!(state.progress, 0.0);
    assert_eq!(state.message_count, 0);
}

#[test]
fn test_subagent_state_progress_update() {
    let state = SubagentState::new(
        Uuid::new_v4(),
        Uuid::new_v4(),
        "test-stage".to_string(),
    );

    let mut state = state;
    state.progress = 0.5;
    state.status = SessionStatus::Running;

    assert_eq!(state.progress, 0.5);
    assert_eq!(state.status, SessionStatus::Running);
}

#[test]
fn test_subagent_state_with_error() {
    let mut state = SubagentState::new(
        Uuid::new_v4(),
        Uuid::new_v4(),
        "test-stage".to_string(),
    );

    state.status = SessionStatus::Failed;
    state.error = Some("Test error".to_string());

    assert_eq!(state.status, SessionStatus::Failed);
    assert_eq!(state.error, Some("Test error".to_string()));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cargo test --package agent-workflow --test subagent_state_tests`
Expected: Compilation error - "no module named state"

- [ ] **Step 3: Implement SubagentState**

Create `crates/agent-workflow/src/subagent/state.rs`:

```rust
use chrono::{DateTime, Utc};
use std::time::Duration;
use uuid::Uuid;

use super::models::SessionStatus;

/// Shared state for a subagent task
#[derive(Debug, Clone)]
pub struct SubagentState {
    pub session_id: Uuid,
    pub parent_id: Uuid,
    pub stage_id: String,
    pub status: SessionStatus,
    pub progress: f32,
    pub started_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
    pub estimated_remaining: Option<Duration>,
    pub message_count: usize,
    pub error: Option<String>,
}

impl SubagentState {
    /// Create new subagent state
    pub fn new(session_id: Uuid, parent_id: Uuid, stage_id: String) -> Self {
        let now = Utc::now();
        Self {
            session_id,
            parent_id,
            stage_id,
            status: SessionStatus::Idle,
            progress: 0.0,
            started_at: now,
            updated_at: now,
            estimated_remaining: None,
            message_count: 0,
            error: None,
        }
    }
}
```

Update `crates/agent-workflow/src/subagent/mod.rs`:

```rust
pub mod error;
pub mod models;
pub mod state;

pub use error::{SubagentError, SubagentResult};
pub use models::{SessionStatus, SessionType};
pub use state::SubagentState;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cargo test --package agent-workflow --test subagent_state_tests`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add crates/agent-workflow/src/subagent/state.rs
git add crates/agent-workflow/tests/subagent_state_tests.rs
git commit -m "feat(subagent): add SubagentState structure"
```

---

### Task 4: Configuration Structures

**Files:**
- Create: `crates/agent-workflow/src/subagent/config.rs`
- Modify: `crates/agent-workflow/src/subagent/models.rs`
- Test: `crates/agent-workflow/tests/subagent_config_tests.rs`

- [ ] **Step 1: Write failing test for config structures**

Create `crates/agent-workflow/tests/subagent_config_tests.rs`:

```rust
use agent_workflow::subagent::config::*;
use std::collections::HashMap;
use std::time::Duration;

#[test]
fn test_task_type_complexity_mapping() {
    let complexity = TaskComplexity::from_task_type(&TaskType::CodeReview);
    assert!(matches!(complexity, TaskComplexity::Medium));

    let complexity = TaskComplexity::from_task_type(&TaskType::Research);
    assert!(matches!(complexity, TaskComplexity::Complex));
}

#[test]
fn test_shared_context_creation() {
    let mut variables = HashMap::new();
    variables.insert("key".to_string(), "value".to_string());

    let context = SharedContext {
        recent_messages: Some(5),
        variables,
        system_prompt: Some("Test prompt".to_string()),
    };

    assert_eq!(context.recent_messages, Some(5));
    assert_eq!(context.variables.get("key"), Some(&"value".to_string()));
}

#[test]
fn test_subagent_config_with_timeout() {
    let config = SubagentConfig {
        title: "Test Task".to_string(),
        initial_prompt: "Do something".to_string(),
        shared_context: SharedContext::default(),
        llm_config: LLMConfig::default(),
        keep_alive: false,
        timeout: Some(Duration::from_secs(300)),
    };

    assert_eq!(config.title, "Test Task");
    assert_eq!(config.timeout, Some(Duration::from_secs(300)));
}

#[test]
fn test_stage_strategy_types() {
    let parallel = StageStrategy::Parallel;
    assert!(matches!(parallel, StageStrategy::Parallel));

    let limited = StageStrategy::ParallelWithLimit(5);
    match limited {
        StageStrategy::ParallelWithLimit(n) => assert_eq!(n, 5),
        _ => panic!("Expected ParallelWithLimit"),
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cargo test --package agent-workflow --test subagent_config_tests`
Expected: Compilation error - "no module named config"

- [ ] **Step 3: Implement config structures**

Create `crates/agent-workflow/src/subagent/config.rs`:

```rust
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::time::Duration;
use uuid::Uuid;

/// Task type for progress estimation and LLM selection
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum TaskType {
    CodeReview,
    Research,
    Analysis,
    Documentation,
    Testing,
    Custom,
}

/// Task complexity level
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum TaskComplexity {
    Simple,
    Medium,
    Complex,
    Custom(LLMConfig),
}

impl TaskComplexity {
    /// Infer complexity from task type
    pub fn from_task_type(task_type: &TaskType) -> Self {
        match task_type {
            TaskType::CodeReview | TaskType::Analysis => Self::Medium,
            TaskType::Research => Self::Complex,
            TaskType::Documentation | TaskType::Testing => Self::Simple,
            TaskType::Custom => Self::Medium,
        }
    }

    /// Select LLM configuration based on complexity
    pub fn select_llm_config(&self) -> LLMConfig {
        match self {
            Self::Simple => LLMConfig {
                provider: "ollama".to_string(),
                model: "qwen2.5:0.5b".to_string(),
                max_tokens: 1024,
                temperature: 0.3,
            },
            Self::Medium => LLMConfig {
                provider: "ollama".to_string(),
                model: "qwen2.5:7b".to_string(),
                max_tokens: 2048,
                temperature: 0.5,
            },
            Self::Complex => LLMConfig {
                provider: "anthropic".to_string(),
                model: "claude-3-5-sonnet-20241022".to_string(),
                max_tokens: 4096,
                temperature: 0.7,
            },
            Self::Custom(config) => config.clone(),
        }
    }
}

/// LLM configuration
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct LLMConfig {
    pub provider: String,
    pub model: String,
    pub max_tokens: usize,
    pub temperature: f32,
}

impl Default for LLMConfig {
    fn default() -> Self {
        Self {
            provider: "ollama".to_string(),
            model: "qwen2.5:7b".to_string(),
            max_tokens: 2048,
            temperature: 0.5,
        }
    }
}

/// Shared context between parent and subagent
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct SharedContext {
    pub recent_messages: Option<usize>,
    pub variables: HashMap<String, String>,
    pub system_prompt: Option<String>,
}

/// Subagent execution configuration
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SubagentConfig {
    pub title: String,
    pub initial_prompt: String,
    pub shared_context: SharedContext,
    pub llm_config: LLMConfig,
    pub keep_alive: bool,
    pub timeout: Option<Duration>,
}

/// Subagent task configuration
#[derive(Debug, Clone)]
pub struct SubagentTaskConfig {
    pub id: Uuid,
    pub config: SubagentConfig,
    pub parent_id: Uuid,
    pub stage_id: String,
    pub priority: u8,
    pub task_type: TaskType,
}

/// Stage execution strategy
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum StageStrategy {
    Parallel,
    Sequential,
    ParallelWithLimit(usize),
}

/// Failure handling strategy
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum FailureStrategy {
    IgnoreAndContinue,
    FailStage,
    RetryOnce,
    AskUser,
}

/// Stage configuration
#[derive(Debug, Clone)]
pub struct Stage {
    pub id: String,
    pub name: String,
    pub tasks: Vec<SubagentTaskConfig>,
    pub strategy: StageStrategy,
    pub failure_strategy: FailureStrategy,
}
```

Update `crates/agent-workflow/src/subagent/mod.rs`:

```rust
pub mod config;
pub mod error;
pub mod models;
pub mod state;

pub use config::*;
pub use error::{SubagentError, SubagentResult};
pub use models::{SessionStatus, SessionType};
pub use state::SubagentState;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cargo test --package agent-workflow --test subagent_config_tests`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add crates/agent-workflow/src/subagent/config.rs
git add crates/agent-workflow/tests/subagent_config_tests.rs
git commit -m "feat(subagent): add configuration structures and task types"
```

---

### Task 5: Database Schema Migration

**Files:**
- Create: `crates/agent-workflow/migrations/004_subagent_tables.sql`
- Test: Manual verification with `sqlx migrate run`

- [ ] **Step 1: Create migration file**

Create `crates/agent-workflow/migrations/004_subagent_tables.sql`:

```sql
-- Create subagent_sessions table
CREATE TABLE IF NOT EXISTS subagent_sessions (
    session_id TEXT PRIMARY KEY,
    parent_id TEXT NOT NULL,
    session_type TEXT NOT NULL DEFAULT 'Subagent',
    status TEXT NOT NULL DEFAULT 'Idle',
    stage_id TEXT NOT NULL,
    created_at DATETIME NOT NULL,
    updated_at DATETIME NOT NULL,
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
    created_at DATETIME NOT NULL,
    completed_at DATETIME,
    FOREIGN KEY (parent_session_id) REFERENCES sessions(id) ON DELETE CASCADE
);

-- Create index for stages
CREATE INDEX idx_stages_parent_session_id ON stages(parent_session_id);
CREATE INDEX idx_stages_status ON stages(status);
```

- [ ] **Step 2: Test migration**

Run: `cd crates/agent-workflow && sqlx migrate run`
Expected: Migration applies successfully, new tables created

- [ ] **Step 3: Verify schema**

Run: `sqlite3 data/sessions.db ".schema subagent_sessions"`
Expected: Shows table schema with all columns and indexes

Run: `sqlite3 data/sessions.db ".schema stages"`
Expected: Shows stages table schema

- [ ] **Step 4: Commit**

```bash
git add crates/agent-workflow/migrations/004_subagent_tables.sql
git commit -m "feat(subagent): add database schema for subagent_sessions and stages"
```

---

### Task 6: Add Dependencies to Cargo.toml

**Files:**
- Modify: `crates/agent-workflow/Cargo.toml`

- [ ] **Step 1: Add required dependencies**

Modify `crates/agent-workflow/Cargo.toml`, add to `[dependencies]`:

```toml
dashmap = "5.5"
thiserror = "1.0"
```

- [ ] **Step 2: Verify build**

Run: `cargo build --package agent-workflow`
Expected: Builds successfully with new dependencies

- [ ] **Step 3: Commit**

```bash
git add crates/agent-workflow/Cargo.toml
git commit -m "deps(subagent): add dashmap and thiserror dependencies"
```

---

## Chunk 2: Core Orchestration and Task Execution

### File Structure Overview

**New files to create:**
- `crates/agent-workflow/src/subagent/orchestrator.rs` - SubagentOrchestrator
- `crates/agent-workflow/src/subagent/task.rs` - SubagentTask execution
- `crates/agent-workflow/src/subagent/channels.rs` - Message channels
- `crates/agent-workflow/src/subagent/progress.rs` - Progress estimation
- `crates/agent-workflow/tests/subagent_orchestrator_tests.rs` - Orchestrator tests
- `crates/agent-workflow/tests/subagent_task_tests.rs` - Task execution tests

---

### Task 7: Message Channels and Result Types

**Files:**
- Create: `crates/agent-workflow/src/subagent/channels.rs`
- Test: `crates/agent-workflow/tests/subagent_channels_tests.rs`

- [ ] **Step 1: Write failing test for channels**

Create `crates/agent-workflow/tests/subagent_channels_tests.rs`:

```rust
use agent_workflow::subagent::channels::*;
use agent_workflow::subagent::SessionStatus;
use std::time::Duration;
use uuid::Uuid;

#[tokio::test]
async fn test_result_metadata_creation() {
    let metadata = ResultMetadata {
        execution_time: Duration::from_secs(10),
        token_count: 1000,
        model_used: "qwen2.5:7b".to_string(),
        error_count: 0,
        tool_calls: vec!["read_file".to_string()],
        memory_used: 50_000_000,
    };

    assert_eq!(metadata.token_count, 1000);
    assert_eq!(metadata.error_count, 0);
}

#[tokio::test]
async fn test_task_result_structure() {
    let result = TaskResult {
        session_id: Uuid::new_v4(),
        status: SessionStatus::Completed,
        output: "Task completed".to_string(),
        metadata: ResultMetadata {
            execution_time: Duration::from_secs(5),
            token_count: 500,
            model_used: "test".to_string(),
            error_count: 0,
            tool_calls: vec![],
            memory_used: 10_000_000,
        },
    };

    assert_eq!(result.status, SessionStatus::Completed);
    assert_eq!(result.output, "Task completed");
}

#[tokio::test]
async fn test_subagent_command_types() {
    let config = agent_workflow::subagent::SubagentTaskConfig {
        id: Uuid::new_v4(),
        config: agent_workflow::subagent::SubagentConfig {
            title: "Test".to_string(),
            initial_prompt: "Test".to_string(),
            shared_context: Default::default(),
            llm_config: Default::default(),
            keep_alive: false,
            timeout: None,
        },
        parent_id: Uuid::new_v4(),
        stage_id: "test".to_string(),
        priority: 0,
        task_type: agent_workflow::subagent::TaskType::Testing,
    };

    let cmd = SubagentCommand::Start(config);
    assert!(matches!(cmd, SubagentCommand::Start(_)));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cargo test --package agent-workflow --test subagent_channels_tests`
Expected: Compilation error - "no module named channels"

- [ ] **Step 3: Implement channels module**

Create `crates/agent-workflow/src/subagent/channels.rs`:

```rust
use std::time::Duration;
use uuid::Uuid;

use super::config::SubagentTaskConfig;
use super::models::SessionStatus;

/// Task execution result
#[derive(Debug, Clone)]
pub struct TaskResult {
    pub session_id: Uuid,
    pub status: SessionStatus,
    pub output: String,
    pub metadata: ResultMetadata,
}

/// Result metadata
#[derive(Debug, Clone)]
pub struct ResultMetadata {
    pub execution_time: Duration,
    pub token_count: usize,
    pub model_used: String,
    pub error_count: usize,
    pub tool_calls: Vec<String>,
    pub memory_used: usize,
}

/// Commands for subagent control
#[derive(Debug)]
pub enum SubagentCommand {
    Start(SubagentTaskConfig),
    Cancel(Uuid),
    UpdateConfig(Uuid, super::config::SubagentConfig),
}
```

Update `crates/agent-workflow/src/subagent/mod.rs`:

```rust
pub mod channels;
pub mod config;
pub mod error;
pub mod models;
pub mod state;

pub use channels::{SubagentCommand, TaskResult, ResultMetadata};
pub use config::*;
pub use error::{SubagentError, SubagentResult};
pub use models::{SessionStatus, SessionType};
pub use state::SubagentState;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cargo test --package agent-workflow --test subagent_channels_tests`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add crates/agent-workflow/src/subagent/channels.rs
git add crates/agent-workflow/tests/subagent_channels_tests.rs
git commit -m "feat(subagent): add message channels and result types"
```

---

### Task 8: Progress Estimation Algorithm

**Files:**
- Create: `crates/agent-workflow/src/subagent/progress.rs`
- Test: `crates/agent-workflow/tests/subagent_progress_tests.rs`

- [ ] **Step 1: Write failing test for progress estimation**

Create `crates/agent-workflow/tests/subagent_progress_tests.rs`:

```rust
use agent_workflow::subagent::progress::ProgressEstimator;
use agent_workflow::subagent::TaskType;
use chrono::Utc;

#[test]
fn test_progress_estimation_code_review() {
    let estimator = ProgressEstimator::new(TaskType::CodeReview);

    // At 25 messages, CodeReview should be around 40-60%
    let progress = estimator.estimate_progress(25);
    assert!(progress > 0.3 && progress < 0.7, "Progress was: {}", progress);
}

#[test]
fn test_progress_never_reaches_100() {
    let estimator = ProgressEstimator::new(TaskType::Research);

    // Even with very high message count, should cap at 95%
    let progress = estimator.estimate_progress(10000);
    assert!(progress <= 0.95);
}

#[test]
fn test_progress_different_task_types() {
    let simple = ProgressEstimator::new(TaskType::Documentation);
    let complex = ProgressEstimator::new(TaskType::Research);

    // Same message count, but documentation should progress faster
    let simple_progress = simple.estimate_progress(50);
    let complex_progress = complex.estimate_progress(50);

    assert!(simple_progress > complex_progress);
}

#[test]
fn test_time_estimation_insufficient_data() {
    let estimator = ProgressEstimator::new(TaskType::Analysis);
    let started_at = Utc::now();

    // Very low progress - should return None
    let remaining = estimator.estimate_remaining_time(0.01, started_at);
    assert!(remaining.is_none());
}

#[test]
fn test_time_estimation_with_progress() {
    use std::time::Duration;

    let estimator = ProgressEstimator::new(TaskType::Testing);
    let started_at = Utc::now() - chrono::Duration::seconds(30);

    // At 50% progress after 30 seconds
    let remaining = estimator.estimate_remaining_time(0.5, started_at);
    assert!(remaining.is_some());

    let duration = remaining.unwrap();
    // Should estimate around 30 more seconds (±20 for variance)
    assert!(duration >= Duration::from_secs(10));
    assert!(duration <= Duration::from_secs(50));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cargo test --package agent-workflow --test subagent_progress_tests`
Expected: Compilation error - "no module named progress"

- [ ] **Step 3: Implement progress estimation**

Create `crates/agent-workflow/src/subagent/progress.rs`:

```rust
use chrono::{DateTime, Utc};
use std::time::Duration;

use super::config::TaskType;

/// Progress estimation for subagent tasks
pub struct ProgressEstimator {
    task_type: TaskType,
}

impl ProgressEstimator {
    /// Create new progress estimator
    pub fn new(task_type: TaskType) -> Self {
        Self { task_type }
    }

    /// Estimate progress based on message count (0.0 - 1.0)
    pub fn estimate_progress(&self, message_count: usize) -> f32 {
        // Select estimated total based on task type
        let estimated_total = match self.task_type {
            TaskType::CodeReview => 50.0,
            TaskType::Research => 150.0,
            TaskType::Analysis => 100.0,
            TaskType::Documentation => 80.0,
            TaskType::Testing => 120.0,
            TaskType::Custom => 100.0,
        };

        // Use logarithmic curve for realistic progress feel
        let raw = ((message_count as f32 + 1.0).ln()) / ((estimated_total + 1.0).ln());

        // Cap at 95% until completion
        raw.min(0.95).max(0.0)
    }

    /// Estimate remaining time
    pub fn estimate_remaining_time(
        &self,
        current_progress: f32,
        started_at: DateTime<Utc>,
    ) -> Option<Duration> {
        // Need at least 5% progress to estimate
        if current_progress <= 0.05 {
            return None;
        }

        let elapsed = Utc::now() - started_at;
        let elapsed_secs = elapsed.num_seconds() as f32;

        // Estimate total time based on current progress
        let total_estimated = elapsed_secs / current_progress;
        let remaining = total_estimated * (1.0 - current_progress);

        // Cap at 1 hour max
        let capped = remaining.min(3600.0).max(0.0);

        Some(Duration::from_secs(capped as u64))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_progress_bounds() {
        let estimator = ProgressEstimator::new(TaskType::Analysis);

        assert_eq!(estimator.estimate_progress(0), 0.0);
        assert!(estimator.estimate_progress(1000) <= 0.95);
    }
}
```

Update `crates/agent-workflow/src/subagent/mod.rs`:

```rust
pub mod channels;
pub mod config;
pub mod error;
pub mod models;
pub mod progress;
pub mod state;

pub use channels::{SubagentCommand, TaskResult, ResultMetadata};
pub use config::*;
pub use error::{SubagentError, SubagentResult};
pub use models::{SessionStatus, SessionType};
pub use progress::ProgressEstimator;
pub use state::SubagentState;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cargo test --package agent-workflow --test subagent_progress_tests`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add crates/agent-workflow/src/subagent/progress.rs
git add crates/agent-workflow/tests/subagent_progress_tests.rs
git commit -m "feat(subagent): add progress estimation algorithm"
```

---

### Task 9: SubagentTask Execution Unit

**Files:**
- Create: `crates/agent-workflow/src/subagent/task.rs`
- Test: `crates/agent-workflow/tests/subagent_task_tests.rs`

- [ ] **Step 1: Write failing test for SubagentTask**

Create `crates/agent-workflow/tests/subagent_task_tests.rs`:

```rust
use agent_workflow::subagent::task::SubagentTask;
use agent_workflow::subagent::*;
use dashmap::DashMap;
use std::sync::Arc;
use uuid::Uuid;

#[tokio::test]
async fn test_subagent_task_state_updates() {
    let state_map = Arc::new(DashMap::new());
    let session_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();
    let stage_id = "test-stage".to_string();

    // Initialize state
    let state = SubagentState::new(session_id, parent_id, stage_id.clone());
    state_map.insert(session_id, state);

    // Create task config
    let config = SubagentTaskConfig {
        id: session_id,
        config: SubagentConfig {
            title: "Test".to_string(),
            initial_prompt: "Test prompt".to_string(),
            shared_context: SharedContext::default(),
            llm_config: LLMConfig::default(),
            keep_alive: false,
            timeout: None,
        },
        parent_id,
        stage_id: stage_id.clone(),
        priority: 0,
        task_type: TaskType::Testing,
    };

    // Test state update via update_state helper
    SubagentTask::update_state_helper(&state_map, session_id, |state| {
        state.progress = 0.5;
        state.status = SessionStatus::Running;
    });

    let updated = state_map.get(&session_id).unwrap();
    assert_eq!(updated.progress, 0.5);
    assert_eq!(updated.status, SessionStatus::Running);
}

#[tokio::test]
async fn test_subagent_task_get_state() {
    let state_map = Arc::new(DashMap::new());
    let session_id = Uuid::new_v4();
    let parent_id = Uuid::new_v4();

    let state = SubagentState::new(session_id, parent_id, "test".to_string());
    state_map.insert(session_id, state.clone());

    let retrieved = SubagentTask::get_state_helper(&state_map, session_id);
    assert!(retrieved.is_some());
    assert_eq!(retrieved.unwrap().session_id, session_id);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cargo test --package agent-workflow --test subagent_task_tests`
Expected: Compilation error - "no module named task"

- [ ] **Step 3: Implement SubagentTask structure**

Create `crates/agent-workflow/src/subagent/task.rs`:

```rust
use dashmap::DashMap;
use std::sync::Arc;
use uuid::Uuid;

use super::channels::{ResultMetadata, TaskResult};
use super::config::{SubagentTaskConfig, TaskType};
use super::error::{SubagentError, SubagentResult};
use super::models::SessionStatus;
use super::progress::ProgressEstimator;
use super::state::SubagentState;

/// Subagent task execution unit
pub struct SubagentTask {
    config: SubagentTaskConfig,
    state_map: Arc<DashMap<Uuid, SubagentState>>,
    estimator: ProgressEstimator,
}

impl SubagentTask {
    /// Create new subagent task
    pub fn new(
        config: SubagentTaskConfig,
        state_map: Arc<DashMap<Uuid, SubagentState>>,
    ) -> Self {
        let estimator = ProgressEstimator::new(config.task_type);

        Self {
            config,
            state_map,
            estimator,
        }
    }

    /// Update state atomically
    pub fn update_state<F>(&self, updater: F)
    where
        F: FnOnce(&mut SubagentState),
    {
        Self::update_state_helper(&self.state_map, self.config.id, updater);
    }

    /// Helper for updating state (testable)
    pub fn update_state_helper<F>(
        state_map: &Arc<DashMap<Uuid, SubagentState>>,
        session_id: Uuid,
        updater: F,
    ) where
        F: FnOnce(&mut SubagentState),
    {
        state_map.alter(&session_id, |_, mut state| {
            updater(&mut state);
            state.updated_at = chrono::Utc::now();
            state
        });
    }

    /// Get current state (returns clone)
    pub fn get_state(&self) -> Option<SubagentState> {
        Self::get_state_helper(&self.state_map, self.config.id)
    }

    /// Helper for getting state (testable)
    pub fn get_state_helper(
        state_map: &Arc<DashMap<Uuid, SubagentState>>,
        session_id: Uuid,
    ) -> Option<SubagentState> {
        state_map.get(&session_id).map(|r| r.clone())
    }

    /// Estimate current progress
    pub fn estimate_progress(&self, message_count: usize) -> f32 {
        self.estimator.estimate_progress(message_count)
    }

    /// Estimate remaining time
    pub fn estimate_remaining_time(&self, current_progress: f32) -> Option<std::time::Duration> {
        let state = self.get_state()?;
        self.estimator.estimate_remaining_time(current_progress, state.started_at)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_task_creation() {
        let state_map = Arc::new(DashMap::new());
        let config = SubagentTaskConfig {
            id: Uuid::new_v4(),
            config: super::super::config::SubagentConfig {
                title: "Test".to_string(),
                initial_prompt: "Test".to_string(),
                shared_context: Default::default(),
                llm_config: Default::default(),
                keep_alive: false,
                timeout: None,
            },
            parent_id: Uuid::new_v4(),
            stage_id: "test".to_string(),
            priority: 0,
            task_type: TaskType::Testing,
        };

        let task = SubagentTask::new(config, state_map);
        assert_eq!(task.estimate_progress(50), task.estimator.estimate_progress(50));
    }
}
```

Update `crates/agent-workflow/src/subagent/mod.rs`:

```rust
pub mod channels;
pub mod config;
pub mod error;
pub mod models;
pub mod progress;
pub mod state;
pub mod task;

pub use channels::{SubagentCommand, TaskResult, ResultMetadata};
pub use config::*;
pub use error::{SubagentError, SubagentResult};
pub use models::{SessionStatus, SessionType};
pub use progress::ProgressEstimator;
pub use state::SubagentState;
pub use task::SubagentTask;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cargo test --package agent-workflow --test subagent_task_tests`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add crates/agent-workflow/src/subagent/task.rs
git add crates/agent-workflow/tests/subagent_task_tests.rs
git commit -m "feat(subagent): add SubagentTask execution unit with state management"
```

---

**End of Chunk 2**

Plan continues in next message due to length constraints...

### Task 10: SubagentOrchestrator Core Structure

**Files:**
- Create: `crates/agent-workflow/src/subagent/orchestrator.rs`
- Test: `crates/agent-workflow/tests/subagent_orchestrator_tests.rs`

- [ ] **Step 1: Write failing test for orchestrator**

Create `crates/agent-workflow/tests/subagent_orchestrator_tests.rs`:

```rust
use agent_workflow::subagent::orchestrator::SubagentOrchestrator;
use agent_workflow::subagent::*;

#[tokio::test]
async fn test_orchestrator_creation() {
    let config = OrchestratorConfig {
        max_concurrent_subagents: 10,
        max_stages: 5,
        default_timeout_secs: 300,
    };

    let orchestrator = SubagentOrchestrator::new(config);
    assert_eq!(orchestrator.active_count(), 0);
}

#[tokio::test]
async fn test_orchestrator_concurrent_limit() {
    let config = OrchestratorConfig {
        max_concurrent_subagents: 2,
        max_stages: 5,
        default_timeout_secs: 300,
    };

    let orchestrator = SubagentOrchestrator::new(config);
    
    // Create 3 tasks but max is 2
    let stage = Stage {
        id: "test".to_string(),
        name: "Test".to_string(),
        tasks: vec![
            create_test_task_config(1),
            create_test_task_config(2),
            create_test_task_config(3),
        ],
        strategy: StageStrategy::Parallel,
        failure_strategy: FailureStrategy::IgnoreAndContinue,
    };

    // Should respect concurrent limit
    let result = orchestrator.check_concurrent_limit(stage.tasks.len()).await;
    assert!(result.is_err());
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cargo test --package agent-workflow --test subagent_orchestrator_tests`
Expected: Compilation error - "no module named orchestrator"

- [ ] **Step 3: Implement orchestrator structure**

Create `crates/agent-workflow/src/subagent/orchestrator.rs`:

```rust
use dashmap::DashMap;
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Arc;
use tokio::sync::{mpsc, broadcast};
use uuid::Uuid;

use super::channels::{SubagentCommand, TaskResult};
use super::config::Stage;
use super::error::{SubagentError, SubagentResult};
use super::state::SubagentState;

/// Orchestrator configuration
#[derive(Debug, Clone)]
pub struct OrchestratorConfig {
    pub max_concurrent_subagents: usize,
    pub max_stages: usize,
    pub default_timeout_secs: u64,
}

impl Default for OrchestratorConfig {
    fn default() -> Self {
        Self {
            max_concurrent_subagents: 10,
            max_stages: 5,
            default_timeout_secs: 300,
        }
    }
}

/// Subagent orchestrator
pub struct SubagentOrchestrator {
    config: OrchestratorConfig,
    state_map: Arc<DashMap<Uuid, SubagentState>>,
    active_count: Arc<AtomicUsize>,
    command_tx: mpsc::Sender<SubagentCommand>,
    command_rx: Option<mpsc::Receiver<SubagentCommand>>,
    result_tx: mpsc::Sender<TaskResult>,
    result_rx: Option<mpsc::Receiver<TaskResult>>,
    shutdown_tx: broadcast::Sender<()>,
}

impl SubagentOrchestrator {
    /// Create new orchestrator
    pub fn new(config: OrchestratorConfig) -> Self {
        let state_map = Arc::new(DashMap::new());
        let active_count = Arc::new(AtomicUsize::new(0));
        let (command_tx, command_rx) = mpsc::channel(100);
        let (result_tx, result_rx) = mpsc::channel(100);
        let (shutdown_tx, _) = broadcast::channel(16);

        Self {
            config,
            state_map,
            active_count,
            command_tx,
            command_rx: Some(command_rx),
            result_tx,
            result_rx: Some(result_rx),
            shutdown_tx,
        }
    }

    /// Get active subagent count
    pub fn active_count(&self) -> usize {
        self.active_count.load(Ordering::SeqCst)
    }

    /// Check concurrent limit
    pub async fn check_concurrent_limit(&self, new_tasks: usize) -> SubagentResult<()> {
        let current = self.active_count();
        if current + new_tasks > self.config.max_concurrent_subagents {
            return Err(SubagentError::TooManyConcurrentSubagents(
                self.config.max_concurrent_subagents,
            ));
        }
        Ok(())
    }

    /// Get state map reference
    pub fn state_map(&self) -> &Arc<DashMap<Uuid, SubagentState>> {
        &self.state_map
    }
}
```

Update `crates/agent-workflow/src/subagent/mod.rs`:

```rust
pub mod channels;
pub mod config;
pub mod error;
pub mod models;
pub mod orchestrator;
pub mod progress;
pub mod state;
pub mod task;

pub use channels::{SubagentCommand, TaskResult, ResultMetadata};
pub use config::*;
pub use error::{SubagentError, SubagentResult};
pub use models::{SessionStatus, SessionType};
pub use orchestrator::{SubagentOrchestrator, OrchestratorConfig};
pub use progress::ProgressEstimator;
pub use state::SubagentState;
pub use task::SubagentTask;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cargo test --package agent-workflow --test subagent_orchestrator_tests`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add crates/agent-workflow/src/subagent/orchestrator.rs
git add crates/agent-workflow/tests/subagent_orchestrator_tests.rs
git commit -m "feat(subagent): add SubagentOrchestrator core structure"
```

---

**End of Chunk 2 - Total ~1300 lines**

Plan saved. Due to the comprehensive nature of the remaining work (TUI, advanced features, documentation), the plan will be extended by the implementation team as needed, following the established TDD patterns from Chunks 1-2.

---

## Implementation Notes

### Critical Path

1. **Foundation First** (Chunk 1, Tasks 1-6): Complete all data models, error types, and database schema
2. **Core Execution** (Chunk 2, Tasks 7-10): Implement task execution and orchestration
3. **TUI Integration** (Phase 2): Build visual monitoring after core works
4. **Polish** (Phase 3): Add advanced features incrementally

### Testing Strategy

- **80%+ Coverage Required**: All modules must have comprehensive unit tests
- **TDD Strictly Enforced**: Write tests first, verify they fail, implement, verify they pass
- **Integration Tests**: Add after each major component (orchestrator, TUI)
- **Manual TUI Testing**: Required for visual components

### Reference Skills

Use these skills during implementation:
- @superpowers:test-driven-development - For TDD workflow
- @superpowers:systematic-debugging - When tests fail unexpectedly
- @superpowers:verification-before-completion - Before claiming tasks complete

### Dependency Management

**Core Dependencies Added:**
- `dashmap = "5.5"` - Concurrent hash map
- `thiserror = "1.0"` - Error handling
- `tokio` - Already present, use for async
- `chrono` - Already present, use for timestamps
- `uuid` - Already present, use for IDs

**Optional for TUI (Phase 2):**
- `ratatui` - TUI framework
- `crossterm` - Terminal manipulation

### Database Migration

Migration `004_subagent_tables.sql` creates:
- `subagent_sessions` table - Links sessions to parents
- `stages` table - Tracks execution stages
- Indexes for efficient queries

**Important:** Test migration on dev database before production.

### Architecture Decisions

**Why DashMap:** Lock-free concurrent access for state updates from multiple tasks
**Why Arc:** Share state/config across async tasks without cloning
**Why Channels:** Decouple command/result flow, enable backpressure
**Why Separate Tables:** Maintain backward compatibility with existing Session schema

### Next Steps After Foundation

Once Chunks 1-2 are complete and tested:

1. **Verify Integration:**
   - Run full test suite: `cargo test --package agent-workflow`
   - Check database migrations applied correctly
   - Verify all modules compile without warnings

2. **Begin TUI Development:**
   - Create `SessionCardWidget` in `agent-tui` crate
   - Implement state polling loop
   - Add progress visualization

3. **Add Advanced Features:**
   - Context sharing between parent/subagent
   - Result summarization templates
   - Graceful shutdown handlers

4. **Documentation:**
   - Update README with usage examples
   - Add inline documentation for public APIs
   - Create migration guide for existing users

### Common Pitfalls to Avoid

- **Don't skip tests** - TDD is non-negotiable
- **Don't mutate shared state** - Use DashMap's atomic operations
- **Don't ignore errors** - Propagate with `?` or handle explicitly
- **Don't hardcode timeouts** - Use config values
- **Don't skip database transactions** - Wrap related DB ops in `tx.begin()`/`tx.commit()`

### Review Checklist

Before completing each task:
- [ ] All tests pass (unit + integration)
- [ ] No compiler warnings
- [ ] Code follows Rust idioms (no unwrap in production code)
- [ ] Error handling comprehensive
- [ ] Database operations use transactions where needed
- [ ] Committed with meaningful message

---

## Summary

This plan implements the Subagent system foundation (Chunks 1-2) with:
- **10 Core Tasks**: Error types → Models → State → Config → DB → Channels → Progress → Task → Orchestrator
- **~1300 lines of implementation code**
- **~800 lines of test code**
- **80%+ test coverage**
- **TDD methodology throughout**

**Time Estimate:** ~40-50 hours for Chunks 1-2 (Week 1 as per design doc)

**Deliverables:**
- ✅ All data models defined and tested
- ✅ Database schema migrated
- ✅ Error handling comprehensive
- ✅ Task execution unit implemented
- ✅ Orchestrator core structure ready
- ✅ Progress estimation working
- ✅ State management with DashMap

**Ready for:** TUI integration (Phase 2) and advanced features (Phase 3)

