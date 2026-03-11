use std::time::Duration;

use super::config::TaskType;

/// Progress estimation for subagent tasks using weighted moving average
pub struct ProgressEstimator {
    task_type: TaskType,
    estimated_total_messages: usize,
    current_progress: f32,
    message_history: Vec<(usize, Duration)>,
    alpha: f32, // EMA smoothing factor
}

impl ProgressEstimator {
    /// Create new progress estimator
    pub fn new(task_type: TaskType) -> Self {
        let estimated_total_messages = match task_type {
            TaskType::CodeReview => 50,
            TaskType::Research => 150,
            TaskType::Analysis => 100,
            TaskType::Documentation => 80,
            TaskType::Testing => 120,
            TaskType::Custom => 100,
        };

        Self {
            task_type,
            estimated_total_messages,
            current_progress: 0.0,
            message_history: Vec::new(),
            alpha: 0.3, // Gives more weight to recent measurements
        }
    }

    /// Update progress using weighted moving average
    pub fn update(&mut self, message_count: usize, elapsed: Duration) {
        // Calculate raw progress
        let raw_progress = (message_count as f32) / (self.estimated_total_messages as f32);

        // If first update, set directly to avoid starting at 0
        if self.message_history.is_empty() {
            self.current_progress = raw_progress;
        } else {
            // Apply exponential moving average
            // new_progress = alpha * raw + (1-alpha) * previous
            self.current_progress = self.alpha * raw_progress + (1.0 - self.alpha) * self.current_progress;
        }

        // Cap at 95% until completion
        self.current_progress = self.current_progress.min(0.95).max(0.0);

        // Store in history
        self.message_history.push((message_count, elapsed));
    }

    /// Get current progress (0.0 - 1.0)
    pub fn get_progress(&self) -> f32 {
        self.current_progress
    }

    /// Estimate remaining time
    pub fn estimate_remaining(&self) -> Option<Duration> {
        // Need at least 5% progress to estimate
        if self.current_progress <= 0.05 {
            return None;
        }

        // Get most recent elapsed time from history
        let elapsed = self.message_history.last()?.1;
        let elapsed_secs = elapsed.as_secs_f32();

        // Estimate total time based on current progress
        let total_estimated = elapsed_secs / self.current_progress;
        let remaining = total_estimated * (1.0 - self.current_progress);

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
        let mut estimator = ProgressEstimator::new(TaskType::Analysis);

        // Initial progress should be 0
        assert_eq!(estimator.get_progress(), 0.0);

        // After updates, should stay within bounds
        estimator.update(10, Duration::from_secs(5));
        assert!(estimator.get_progress() > 0.0);
        assert!(estimator.get_progress() <= 0.95);

        estimator.update(10000, Duration::from_secs(100));
        assert!(estimator.get_progress() <= 0.95);
    }
}
