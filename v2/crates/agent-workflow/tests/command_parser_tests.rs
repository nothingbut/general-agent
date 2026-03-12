use agent_workflow::command_parser::{parse_subagent_command, SubagentCommand};

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
fn test_parse_invalid_command() {
    assert!(parse_subagent_command("not a command").is_err());
    assert!(parse_subagent_command("/subagent").is_err());
    assert!(parse_subagent_command("/subagent start").is_err());
}
