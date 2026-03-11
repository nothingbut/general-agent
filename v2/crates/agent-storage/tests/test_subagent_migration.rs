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
    let result: (String, String, String, String, String) = sqlx::query_as(
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
    assert_eq!(result.4, "stage-1");
}

#[tokio::test]
async fn test_foreign_key_constraints() {
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

    // Should fail due to foreign key constraint
    assert!(result.is_err(), "Foreign key constraint should prevent insertion");
}
