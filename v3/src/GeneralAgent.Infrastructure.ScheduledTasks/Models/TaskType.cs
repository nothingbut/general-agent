namespace GeneralAgent.Infrastructure.ScheduledTasks.Models;

/// <summary>
/// 任务类型
/// </summary>
public enum TaskType
{
    /// <summary>
    /// 技能调用：执行技能（如 @skill-name arg="value"）
    /// </summary>
    SkillInvocation = 0,

    /// <summary>
    /// 记忆提醒：发送通知或创建会话
    /// </summary>
    MemoryReminder = 1,

    /// <summary>
    /// 自定义命令：执行 CLI 命令
    /// </summary>
    CustomCommand = 2
}
