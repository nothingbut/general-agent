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

    // Insert Stage record
    let stage_id = "stage-123";
    let parent_session_id = "main-session-1";
    let stage_name = "Stage - 10:30:45";

    sqlx::query(
        "INSERT INTO stages (id, parent_session_id, name, status, created_at) VALUES (?, ?, ?, ?, datetime('now'))"
    )
    .bind(stage_id)
    .bind(parent_session_id)
    .bind(stage_name)
    .bind("Running")
    .execute(db.pool())
    .await
    .unwrap();

    // Query and verify stage data
    let result: (String, String, i32) = sqlx::query_as(
        "SELECT name, parent_session_id,
         (SELECT COUNT(*) FROM subagent_sessions WHERE stage_id = ?) as total_tasks
         FROM stages WHERE id = ?"
    )
    .bind(stage_id)
    .bind(stage_id)
    .fetch_one(db.pool())
    .await
    .unwrap();

    assert_eq!(result.0, stage_name);
    assert_eq!(result.1, parent_session_id);
    // Note: total_tasks will be 0 initially since we haven't inserted subagent_sessions yet
    // This is expected - we'll verify the count increases as subagents are added
    assert_eq!(result.2, 0);
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
        "INSERT INTO stages (id, parent_session_id, name, status, created_at) VALUES (?, ?, ?, ?, datetime('now'))"
    )
    .bind(stage_id)
    .bind("main-session-1")
    .bind("Test Stage")
    .bind("Running")
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
