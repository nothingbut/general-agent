namespace GeneralAgent.Infrastructure.FileStorage.Models;

/// <summary>
/// 权限类型
/// </summary>
public enum PermissionType
{
    /// <summary>
    /// 只读
    /// </summary>
    Read = 0,

    /// <summary>
    /// 读写（可更新文件）
    /// </summary>
    Write = 1
}
