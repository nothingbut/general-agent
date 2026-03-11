use agent_storage::Database;

#[tokio::test]
async fn test_subagent_tables_created() {
    // Create in-memory database and run migrations
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    // Verify subagent_sessions table exists
    let result: (i32,) = sqlx::query_as(
        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='subagent_sessions'"
    )
    .fetch_one(db.pool())
    .await
    .unwrap();

    assert_eq!(result.0, 1, "subagent_sessions table should exist");

    // Verify stages table exists
    let result: (i32,) = sqlx::query_as(
        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='stages'"
    )
    .fetch_one(db.pool())
    .await
    .unwrap();

    assert_eq!(result.0, 1, "stages table should exist");

    // Verify indexes exist
    let indexes: Vec<(String,)> = sqlx::query_as(
        "SELECT name FROM sqlite_master WHERE type='index' AND (name LIKE 'idx_subagent_%' OR name LIKE 'idx_stages_%') ORDER BY name"
    )
    .fetch_all(db.pool())
    .await
    .unwrap();

    let index_names: Vec<String> = indexes.into_iter().map(|(name,)| name).collect();

    // Expected indexes
    let expected_indexes = vec![
        "idx_stages_parent_session_id",
        "idx_stages_status",
        "idx_subagent_sessions_parent_id",
        "idx_subagent_sessions_parent_status",
        "idx_subagent_sessions_stage_id",
        "idx_subagent_sessions_status",
    ];

    for expected in &expected_indexes {
        assert!(
            index_names.contains(&expected.to_string()),
            "Index {} should exist. Found: {:?}",
            expected,
            index_names
        );
    }
}

#[tokio::test]
async fn test_subagent_sessions_schema() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    // Test inserting into sessions first (parent table)
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("parent-session-1")
    .bind("Parent Session")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("subagent-session-1")
    .bind("Subagent Session")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    // Test inserting into stages
    sqlx::query(
        "INSERT INTO stages (id, parent_session_id, name, status, created_at) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("stage-1")
    .bind("parent-session-1")
    .bind("Test Stage")
    .bind("Running")
    .bind("2026-03-11T00:00:00Z")
    .execute(db.pool())
    .await
    .unwrap();

    // Test inserting into subagent_sessions
    sqlx::query(
        "INSERT INTO subagent_sessions (session_id, parent_id, session_type, status, stage_id, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?, ?)"
    )
    .bind("subagent-session-1")
    .bind("parent-session-1")
    .bind("Subagent")
    .bind("Idle")
    .bind("stage-1")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .execute(db.pool())
    .await
    .unwrap();

    // Query back the data
    let result: (String, String, String, String, Option<String>) = sqlx::query_as(
        "SELECT session_id, parent_id, session_type, status, stage_id FROM subagent_sessions WHERE session_id = ?"
    )
    .bind("subagent-session-1")
    .fetch_one(db.pool())
    .await
    .unwrap();

    assert_eq!(result.0, "subagent-session-1");
    assert_eq!(result.1, "parent-session-1");
    assert_eq!(result.2, "Subagent");
    assert_eq!(result.3, "Idle");
    assert_eq!(result.4, Some("stage-1".to_string()));
}

#[tokio::test]
async fn test_foreign_key_parent_id_constraint() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    // Enable foreign keys for this test
    sqlx::query("PRAGMA foreign_keys = ON")
        .execute(db.pool())
        .await
        .unwrap();

    // Try to insert into subagent_sessions without parent session
    let result = sqlx::query(
        "INSERT INTO subagent_sessions (session_id, parent_id, session_type, status, stage_id, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?, ?)"
    )
    .bind("subagent-1")
    .bind("nonexistent-parent")
    .bind("Subagent")
    .bind("Idle")
    .bind("stage-1")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .execute(db.pool())
    .await;

    // Should fail due to parent_id foreign key constraint
    assert!(result.is_err(), "Foreign key constraint on parent_id should prevent insertion");
}

#[tokio::test]
async fn test_foreign_key_session_id_constraint() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    // Enable foreign keys
    sqlx::query("PRAGMA foreign_keys = ON")
        .execute(db.pool())
        .await
        .unwrap();

    // Create parent session but not the subagent session in sessions table
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("parent-1")
    .bind("Parent")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    // Try to insert subagent_session with non-existent session_id
    let result = sqlx::query(
        "INSERT INTO subagent_sessions (session_id, parent_id, session_type, status, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?)"
    )
    .bind("nonexistent-session")
    .bind("parent-1")
    .bind("Subagent")
    .bind("Idle")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .execute(db.pool())
    .await;

    // Should fail due to session_id foreign key constraint
    assert!(result.is_err(), "Foreign key constraint on session_id should prevent insertion");
}

#[tokio::test]
async fn test_foreign_key_cascade_deletion() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    // Enable foreign keys
    sqlx::query("PRAGMA foreign_keys = ON")
        .execute(db.pool())
        .await
        .unwrap();

    // Create parent session
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("parent-1")
    .bind("Parent")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    // Create subagent session
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("subagent-1")
    .bind("Subagent")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    // Create stage
    sqlx::query(
        "INSERT INTO stages (id, parent_session_id, name, status, created_at) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("stage-1")
    .bind("parent-1")
    .bind("Stage 1")
    .bind("Running")
    .bind("2026-03-11T00:00:00Z")
    .execute(db.pool())
    .await
    .unwrap();

    // Link subagent to parent
    sqlx::query(
        "INSERT INTO subagent_sessions (session_id, parent_id, session_type, status, stage_id, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?, ?)"
    )
    .bind("subagent-1")
    .bind("parent-1")
    .bind("Subagent")
    .bind("Idle")
    .bind("stage-1")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .execute(db.pool())
    .await
    .unwrap();

    // Delete parent session
    sqlx::query("DELETE FROM sessions WHERE id = ?")
        .bind("parent-1")
        .execute(db.pool())
        .await
        .unwrap();

    // Verify subagent_sessions entry was cascade deleted
    let count: (i32,) = sqlx::query_as(
        "SELECT COUNT(*) FROM subagent_sessions WHERE session_id = ?"
    )
    .bind("subagent-1")
    .fetch_one(db.pool())
    .await
    .unwrap();

    assert_eq!(count.0, 0, "Subagent session should be cascade deleted");

    // Verify stage was also cascade deleted
    let stage_count: (i32,) = sqlx::query_as(
        "SELECT COUNT(*) FROM stages WHERE id = ?"
    )
    .bind("stage-1")
    .fetch_one(db.pool())
    .await
    .unwrap();

    assert_eq!(stage_count.0, 0, "Stage should be cascade deleted");
}

#[tokio::test]
async fn test_foreign_key_stage_parent_session_constraint() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    // Enable foreign keys
    sqlx::query("PRAGMA foreign_keys = ON")
        .execute(db.pool())
        .await
        .unwrap();

    // Try to insert stage without parent session
    let result = sqlx::query(
        "INSERT INTO stages (id, parent_session_id, name, status, created_at) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("stage-1")
    .bind("nonexistent-parent")
    .bind("Stage 1")
    .bind("Running")
    .bind("2026-03-11T00:00:00Z")
    .execute(db.pool())
    .await;

    // Should fail due to parent_session_id foreign key constraint
    assert!(result.is_err(), "Foreign key constraint on stages.parent_session_id should prevent insertion");
}

#[tokio::test]
async fn test_stage_deletion_sets_null() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    // Enable foreign keys
    sqlx::query("PRAGMA foreign_keys = ON")
        .execute(db.pool())
        .await
        .unwrap();

    // Create parent session
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("parent-1")
    .bind("Parent")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    // Create subagent session
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("subagent-1")
    .bind("Subagent")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    // Create stage
    sqlx::query(
        "INSERT INTO stages (id, parent_session_id, name, status, created_at) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("stage-1")
    .bind("parent-1")
    .bind("Stage 1")
    .bind("Running")
    .bind("2026-03-11T00:00:00Z")
    .execute(db.pool())
    .await
    .unwrap();

    // Link subagent to stage
    sqlx::query(
        "INSERT INTO subagent_sessions (session_id, parent_id, session_type, status, stage_id, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?, ?)"
    )
    .bind("subagent-1")
    .bind("parent-1")
    .bind("Subagent")
    .bind("Idle")
    .bind("stage-1")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .execute(db.pool())
    .await
    .unwrap();

    // Delete stage
    sqlx::query("DELETE FROM stages WHERE id = ?")
        .bind("stage-1")
        .execute(db.pool())
        .await
        .unwrap();

    // Verify subagent_sessions still exists but stage_id is NULL
    let result: (String, Option<String>) = sqlx::query_as(
        "SELECT session_id, stage_id FROM subagent_sessions WHERE session_id = ?"
    )
    .bind("subagent-1")
    .fetch_one(db.pool())
    .await
    .unwrap();

    assert_eq!(result.0, "subagent-1");
    assert_eq!(result.1, None, "stage_id should be NULL after stage deletion");
}

#[tokio::test]
async fn test_check_constraint_status_values() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    // Create required parent session
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("parent-1")
    .bind("Parent")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    // Test valid status values
    for status in &["Idle", "Running", "Completed", "Failed", "Cancelled"] {
        // Create a session for each status test
        sqlx::query(
            "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
        )
        .bind(format!("subagent-{}", status))
        .bind("Subagent")
        .bind("2026-03-11T00:00:00Z")
        .bind("2026-03-11T00:00:00Z")
        .bind("{}")
        .execute(db.pool())
        .await
        .unwrap();

        let result = sqlx::query(
            "INSERT INTO subagent_sessions (session_id, parent_id, session_type, status, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?)"
        )
        .bind(format!("subagent-{}", status))
        .bind("parent-1")
        .bind("Subagent")
        .bind(status)
        .bind("2026-03-11T00:00:00Z")
        .bind("2026-03-11T00:00:00Z")
        .execute(db.pool())
        .await;

        assert!(result.is_ok(), "Valid status {} should be accepted", status);
    }

    // Test invalid status value
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("subagent-invalid")
    .bind("Subagent")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    let result = sqlx::query(
        "INSERT INTO subagent_sessions (session_id, parent_id, session_type, status, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?)"
    )
    .bind("subagent-invalid")
    .bind("parent-1")
    .bind("Subagent")
    .bind("InvalidStatus")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .execute(db.pool())
    .await;

    assert!(result.is_err(), "CHECK constraint should prevent invalid status value");
}

#[tokio::test]
async fn test_check_constraint_session_type() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    // Create required parent and session
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("parent-1")
    .bind("Parent")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("subagent-1")
    .bind("Subagent")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    // Test invalid session_type
    let result = sqlx::query(
        "INSERT INTO subagent_sessions (session_id, parent_id, session_type, status, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?)"
    )
    .bind("subagent-1")
    .bind("parent-1")
    .bind("InvalidType")
    .bind("Idle")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .execute(db.pool())
    .await;

    assert!(result.is_err(), "CHECK constraint should prevent non-Subagent session_type");
}

#[tokio::test]
async fn test_check_constraint_stage_status() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    // Create parent session
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("parent-1")
    .bind("Parent")
    .bind("2026-03-11T00:00:00Z")
    .bind("2026-03-11T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    // Test valid stage status values
    for status in &["Running", "Completed", "Failed"] {
        let result = sqlx::query(
            "INSERT INTO stages (id, parent_session_id, name, status, created_at) VALUES (?, ?, ?, ?, ?)"
        )
        .bind(format!("stage-{}", status))
        .bind("parent-1")
        .bind(format!("Stage {}", status))
        .bind(status)
        .bind("2026-03-11T00:00:00Z")
        .execute(db.pool())
        .await;

        assert!(result.is_ok(), "Valid stage status {} should be accepted", status);
    }

    // Test invalid stage status
    let result = sqlx::query(
        "INSERT INTO stages (id, parent_session_id, name, status, created_at) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("stage-invalid")
    .bind("parent-1")
    .bind("Invalid Stage")
    .bind("InvalidStatus")
    .bind("2026-03-11T00:00:00Z")
    .execute(db.pool())
    .await;

    assert!(result.is_err(), "CHECK constraint should prevent invalid stage status");
}
