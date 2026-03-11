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
        let estimated_total: f32 = match self.task_type {
            TaskType::CodeReview => 50.0,
            TaskType::Research => 150.0,
            TaskType::Analysis => 100.0,
            TaskType::Documentation => 80.0,
            TaskType::Testing => 120.0,
            TaskType::Custom => 100.0,
        };

        // Use sigmoid-like curve for more realistic progress
        // Progress is slower at the beginning and end, faster in the middle
        let ratio = message_count as f32 / estimated_total;

        // Apply sigmoid transformation: 1 / (1 + e^(-k*(x-0.5)))
        // where k controls steepness (using 6.0 for moderate curve)
        let k = 6.0;
        let sigmoid = 1.0 / (1.0 + (-k * (ratio - 0.5)).exp());

        // Cap at 95% until completion
        sigmoid.min(0.95).max(0.0)
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

        // With sigmoid curve, 0 messages gives small non-zero value
        let zero_progress = estimator.estimate_progress(0);
        assert!(zero_progress < 0.1, "Expected small progress at 0 messages, got {}", zero_progress);

        // Very high message count should cap at 95%
        assert!(estimator.estimate_progress(1000) <= 0.95);
    }
}
