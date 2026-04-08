namespace GeneralAgent.Infrastructure.FileStorage.Models;

/// <summary>
/// 文件权限模型（用于支持 Shared 级别的细粒度权限控制）
/// </summary>
public record FilePermission
{
    /// <summary>
    /// 权限记录唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 文件 ID
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// 被授权用户 ID
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// 权限类型
    /// </summary>
    public PermissionType Permission { get; init; }

    /// <summary>
    /// 授权时间
    /// </summary>
    public DateTime GrantedAt { get; init; }

    /// <summary>
    /// 授权人 ID
    /// </summary>
    public string GrantedBy { get; init; } = string.Empty;

    /// <summary>
    /// 创建新的权限记录
    /// </summary>
    public static FilePermission Create(
        Guid fileId,
        string userId,
        string grantedBy,
        PermissionType permission)
    {
        return new FilePermission
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            UserId = userId,
            Permission = permission,
            GrantedAt = DateTime.UtcNow,
            GrantedBy = grantedBy
        };
    }

    /// <summary>
    /// 创建副本并更新权限类型
    /// </summary>
    public FilePermission WithPermission(PermissionType permission)
    {
        return this with { Permission = permission };
    }
}
