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
