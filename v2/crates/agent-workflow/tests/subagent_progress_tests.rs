use agent_workflow::subagent::progress::ProgressEstimator;
use agent_workflow::subagent::TaskType;
use std::time::Duration;

#[test]
fn test_progress_estimation_code_review() {
    let mut estimator = ProgressEstimator::new(TaskType::CodeReview);

    // At 25 messages, CodeReview should be around 40-60%
    estimator.update(25, Duration::from_secs(30));
    let progress = estimator.get_progress();
    assert!(progress > 0.3 && progress < 0.7, "Progress was: {}", progress);
}

#[test]
fn test_progress_never_reaches_100() {
    let mut estimator = ProgressEstimator::new(TaskType::Research);

    // Even with very high message count, should cap at 95%
    estimator.update(10000, Duration::from_secs(1000));
    let progress = estimator.get_progress();
    assert!(progress <= 0.95);
}

#[test]
fn test_progress_different_task_types() {
    let mut simple = ProgressEstimator::new(TaskType::Documentation);
    let mut complex = ProgressEstimator::new(TaskType::Research);

    // Same message count, but documentation should progress faster
    simple.update(50, Duration::from_secs(60));
    complex.update(50, Duration::from_secs(60));

    let simple_progress = simple.get_progress();
    let complex_progress = complex.get_progress();

    assert!(simple_progress > complex_progress);
}

#[test]
fn test_time_estimation_insufficient_data() {
    let estimator = ProgressEstimator::new(TaskType::Analysis);

    // Very low progress - should return None
    let remaining = estimator.estimate_remaining();
    assert!(remaining.is_none());
}

#[test]
fn test_time_estimation_with_progress() {
    let mut estimator = ProgressEstimator::new(TaskType::Testing);

    // At 50% progress after 30 seconds
    estimator.update(60, Duration::from_secs(30));
    let remaining = estimator.estimate_remaining();
    assert!(remaining.is_some());

    let duration = remaining.unwrap();
    // Should estimate around 30 more seconds (±20 for variance)
    assert!(duration >= Duration::from_secs(10));
    assert!(duration <= Duration::from_secs(50));
}
