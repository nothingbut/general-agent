//! Command parser for subagent commands
//!
//! Parses user input commands like `/subagent start` with support for
//! optional flags like `--timeout` and `--model`.

use anyhow::{anyhow, bail, Result};

/// Subagent command types
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum SubagentCommand {
    /// Start subagent command with tasks and options
    Start {
        /// List of tasks to execute
        tasks: Vec<String>,
        /// Optional timeout in seconds
        timeout_secs: Option<u64>,
        /// Optional model specification
        model: Option<String>,
    },
}

/// Parse a subagent command from user input
///
/// Syntax:
/// ```text
/// /subagent start [--timeout <seconds>] [--model <model>] <task1> [task2] ...
/// ```
///
/// # Examples
///
/// ```ignore
/// let cmd = parse_subagent_command("/subagent start 分析代码")?;
/// let cmd = parse_subagent_command("/subagent start --timeout 600 --model sonnet 长任务")?;
/// ```
///
/// # Errors
///
/// Returns an error if:
/// - Input doesn't start with `/subagent`
/// - Subcommand is not `start`
/// - No tasks are provided
/// - Option flags are missing values
/// - Timeout value is not a valid u64
pub fn parse_subagent_command(input: &str) -> Result<SubagentCommand> {
    let input = input.trim();

    // Validate prefix
    if !input.starts_with("/subagent") {
        bail!("Not a valid subagent command");
    }

    let parts: Vec<&str> = input.split_whitespace().collect();

    if parts.len() < 3 {
        bail!("Usage: /subagent start <task> [task2] ...");
    }

    if parts[1] != "start" {
        bail!("Unknown subcommand: {}, only 'start' is supported", parts[1]);
    }

    // Parse options and tasks
    let mut tasks = Vec::new();
    let mut timeout_secs = None;
    let mut model = None;
    let mut i = 2;

    while i < parts.len() {
        match parts[i] {
            "--timeout" => {
                i += 1;
                if i >= parts.len() {
                    bail!("--timeout option requires a value");
                }
                timeout_secs = Some(
                    parts[i]
                        .parse::<u64>()
                        .map_err(|_| anyhow!("--timeout value must be a valid u64"))?,
                );
            }
            "--model" => {
                i += 1;
                if i >= parts.len() {
                    bail!("--model option requires a value");
                }
                model = Some(parts[i].to_string());
            }
            task => {
                // Remove surrounding quotes if present
                let task = task.trim_matches('"').trim_matches('\'');
                if !task.is_empty() {
                    tasks.push(task.to_string());
                }
            }
        }
        i += 1;
    }

    if tasks.is_empty() {
        bail!("At least one task must be specified");
    }

    Ok(SubagentCommand::Start {
        tasks,
        timeout_secs,
        model,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_single_task() {
        let cmd = parse_subagent_command("/subagent start 分析代码").unwrap();
        match cmd {
            SubagentCommand::Start {
                tasks,
                timeout_secs,
                model,
            } => {
                assert_eq!(tasks.len(), 1);
                assert_eq!(tasks[0], "分析代码");
                assert_eq!(timeout_secs, None);
                assert_eq!(model, None);
            }
        }
    }

    #[test]
    fn test_parse_multiple_tasks() {
        let cmd = parse_subagent_command("/subagent start 任务1 任务2 任务3").unwrap();
        match cmd {
            SubagentCommand::Start { tasks, .. } => {
                assert_eq!(tasks.len(), 3);
                assert_eq!(tasks[0], "任务1");
                assert_eq!(tasks[1], "任务2");
                assert_eq!(tasks[2], "任务3");
            }
        }
    }

    #[test]
    fn test_parse_with_timeout() {
        let cmd = parse_subagent_command("/subagent start --timeout 600 长任务").unwrap();
        match cmd {
            SubagentCommand::Start {
                tasks,
                timeout_secs,
                ..
            } => {
                assert_eq!(tasks.len(), 1);
                assert_eq!(timeout_secs, Some(600));
            }
        }
    }

    #[test]
    fn test_parse_with_model() {
        let cmd =
            parse_subagent_command("/subagent start --model sonnet 任务").unwrap();
        match cmd {
            SubagentCommand::Start { model, .. } => {
                assert_eq!(model, Some("sonnet".to_string()));
            }
        }
    }

    #[test]
    fn test_parse_with_all_options() {
        let cmd = parse_subagent_command(
            "/subagent start --timeout 300 --model opus 任务1 任务2",
        )
        .unwrap();
        match cmd {
            SubagentCommand::Start {
                tasks,
                timeout_secs,
                model,
            } => {
                assert_eq!(tasks.len(), 2);
                assert_eq!(tasks[0], "任务1");
                assert_eq!(tasks[1], "任务2");
                assert_eq!(timeout_secs, Some(300));
                assert_eq!(model, Some("opus".to_string()));
            }
        }
    }

    #[test]
    fn test_parse_invalid_command() {
        assert!(parse_subagent_command("not a command").is_err());
        assert!(parse_subagent_command("/subagent").is_err());
        assert!(parse_subagent_command("/subagent start").is_err());
    }

    #[test]
    fn test_parse_invalid_timeout() {
        assert!(parse_subagent_command("/subagent start --timeout abc 任务").is_err());
    }

    #[test]
    fn test_parse_missing_timeout_value() {
        assert!(parse_subagent_command("/subagent start --timeout").is_err());
    }

    #[test]
    fn test_parse_missing_model_value() {
        assert!(parse_subagent_command("/subagent start --model").is_err());
    }

}
