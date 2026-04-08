namespace GeneralAgent.Infrastructure.FileStorage.Models;

/// <summary>
/// 文件访问级别
/// </summary>
public enum FileAccessLevel
{
    /// <summary>
    /// 私有：仅所有者可访问
    /// </summary>
    Private = 0,

    /// <summary>
    /// 共享：指定用户可访问（需要权限记录）
    /// </summary>
    Shared = 1,

    /// <summary>
    /// 公开：所有用户可访问
    /// </summary>
    Public = 2
}
