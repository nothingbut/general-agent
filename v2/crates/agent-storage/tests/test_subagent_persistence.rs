use agent_storage::Database;

#[tokio::test]
async fn test_save_stage() {
    // Create in-memory database and run migrations
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    // Enable foreign keys
    sqlx::query("PRAGMA foreign_keys = ON")
        .execute(db.pool())
        .await
        .unwrap();

    // Create main session (parent)
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("main-session-1")
    .bind("Main Session")
    .bind("2026-03-12T00:00:00Z")
    .bind("2026-03-12T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    // Insert Stage record with total_tasks=3
    let stage_id = "stage-123";
    let parent_session_id = "main-session-1";
    let stage_name = "Stage - 10:30:45";
    let total_tasks = 3i64;

    sqlx::query(
        "INSERT INTO stages (id, parent_session_id, name, status, created_at, total_tasks, completed_tasks)
         VALUES (?, ?, ?, 'Running', datetime('now'), ?, 0)"
    )
    .bind(stage_id)
    .bind(parent_session_id)
    .bind(stage_name)
    .bind(total_tasks)
    .execute(db.pool())
    .await
    .unwrap();

    // Query and verify stage data (read total_tasks column)
    let result: (String, String, i64) = sqlx::query_as(
        "SELECT name, parent_session_id, total_tasks FROM stages WHERE id = ?"
    )
    .bind(stage_id)
    .fetch_one(db.pool())
    .await
    .unwrap();

    assert_eq!(result.0, stage_name);
    assert_eq!(result.1, parent_session_id);
    assert_eq!(result.2, 3); // Verify stored total_tasks value
}

#[tokio::test]
async fn test_save_subagent_session() {
    // Create in-memory database and run migrations
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    // Enable foreign keys
    sqlx::query("PRAGMA foreign_keys = ON")
        .execute(db.pool())
        .await
        .unwrap();

    // Create main session (parent)
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("main-session-1")
    .bind("Main Session")
    .bind("2026-03-12T00:00:00Z")
    .bind("2026-03-12T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    // Create Stage
    let stage_id = "stage-123";
    sqlx::query(
        "INSERT INTO stages (id, parent_session_id, name, status, created_at, total_tasks, completed_tasks)
         VALUES (?, ?, ?, 'Running', datetime('now'), 1, 0)"
    )
    .bind(stage_id)
    .bind("main-session-1")
    .bind("Test Stage")
    .execute(db.pool())
    .await
    .unwrap();

    // Create subagent session - dual table insertion
    let session_id = "subagent-session-1";
    let title = "Task 1: Test subagent task";

    // Step 1: Insert into sessions table (base session record)
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind(session_id)
    .bind(title)
    .bind("2026-03-12T00:00:00Z")
    .bind("2026-03-12T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    // Step 2: Insert into subagent_sessions table (subagent metadata)
    sqlx::query(
        "INSERT INTO subagent_sessions (session_id, parent_id, session_type, status, stage_id, created_at, updated_at) VALUES (?, ?, ?, ?, ?, datetime('now'), datetime('now'))"
    )
    .bind(session_id)
    .bind("main-session-1")
    .bind("Subagent")
    .bind("Idle")
    .bind(stage_id)
    .execute(db.pool())
    .await
    .unwrap();

    // Query and verify dual-table insertion
    let result: (String, String, String) = sqlx::query_as(
        "SELECT ss.session_type, ss.status, ss.stage_id
         FROM subagent_sessions ss
         WHERE ss.session_id = ?"
    )
    .bind(session_id)
    .fetch_one(db.pool())
    .await
    .unwrap();

    assert_eq!(result.0, "Subagent");
    assert_eq!(result.1, "Idle");
    assert_eq!(result.2, stage_id);

    // Also verify the base session exists
    let session_exists: (i32,) = sqlx::query_as(
        "SELECT COUNT(*) FROM sessions WHERE id = ?"
    )
    .bind(session_id)
    .fetch_one(db.pool())
    .await
    .unwrap();

    assert_eq!(session_exists.0, 1);
}

#[tokio::test]
async fn test_transaction_rollback_on_constraint_violation() {
    // Create in-memory database and run migrations
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    // Enable foreign keys
    sqlx::query("PRAGMA foreign_keys = ON")
        .execute(db.pool())
        .await
        .unwrap();

    // Create main session (parent)
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind("main-session-1")
    .bind("Main Session")
    .bind("2026-03-12T00:00:00Z")
    .bind("2026-03-12T00:00:00Z")
    .bind("{}")
    .execute(db.pool())
    .await
    .unwrap();

    // Create Stage
    sqlx::query(
        "INSERT INTO stages (id, parent_session_id, name, status, created_at, total_tasks, completed_tasks)
         VALUES (?, ?, ?, 'Running', datetime('now'), 1, 0)"
    )
    .bind("stage-123")
    .bind("main-session-1")
    .bind("Test Stage")
    .execute(db.pool())
    .await
    .unwrap();

    let session_id = "subagent-session-1";

    // Simulate transaction failure: Begin transaction, insert into sessions,
    // then try to insert into subagent_sessions with invalid parent_id (should fail)
    let mut tx = db.pool().begin().await.unwrap();

    // Step 1: Insert into sessions table (succeeds)
    sqlx::query(
        "INSERT INTO sessions (id, title, created_at, updated_at, context) VALUES (?, ?, ?, ?, ?)"
    )
    .bind(session_id)
    .bind("Test Task")
    .bind("2026-03-12T00:00:00Z")
    .bind("2026-03-12T00:00:00Z")
    .bind("{}")
    .execute(&mut *tx)
    .await
    .unwrap();

    // Step 2: Try to insert with nonexistent parent_id (should fail due to FK constraint)
    let result = sqlx::query(
        "INSERT INTO subagent_sessions (session_id, parent_id, session_type, status, stage_id, created_at, updated_at)
         VALUES (?, ?, 'Subagent', 'Idle', ?, datetime('now'), datetime('now'))"
    )
    .bind(session_id)
    .bind("nonexistent-parent")  // Invalid parent_id - will violate FK constraint
    .bind("stage-123")
    .execute(&mut *tx)
    .await;

    // Should fail due to foreign key constraint
    assert!(result.is_err(), "Second insert should fail due to FK constraint");

    // Transaction is NOT committed (will be dropped and auto-rolled back)
    drop(tx);

    // Verify that the first insert was also rolled back (no orphaned session)
    let count: (i32,) = sqlx::query_as(
        "SELECT COUNT(*) FROM sessions WHERE id = ?"
    )
    .bind(session_id)
    .fetch_one(db.pool())
    .await
    .unwrap();

    assert_eq!(count.0, 0, "Session record should NOT exist due to transaction rollback");
}
