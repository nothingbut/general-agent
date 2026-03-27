namespace GeneralAgent.Core.Models;

/// <summary>
/// 记忆类型枚举
/// </summary>
public enum MemoryType
{
    /// <summary>
    /// 用户相关记忆：用户角色、偏好、职责、知识背景
    /// </summary>
    User,

    /// <summary>
    /// 反馈记忆：用户对工作方式的指导（应该做什么、避免什么）
    /// </summary>
    Feedback,

    /// <summary>
    /// 项目记忆：正在进行的工作、目标、计划、截止日期
    /// </summary>
    Project,

    /// <summary>
    /// 参考记忆：外部系统的位置和用途（文档链接、工具位置）
    /// </summary>
    Reference,

    /// <summary>
    /// 知识记忆：技术知识、领域知识、最佳实践
    /// </summary>
    Knowledge
}
