using GeneralAgent.Infrastructure.FileStorage.Models;

namespace GeneralAgent.Infrastructure.FileStorage.Repositories;

/// <summary>
/// 文件权限仓储接口
/// </summary>
public interface IFilePermissionRepository
{
    /// <summary>
    /// 保存权限记录
    /// </summary>
    Task<FilePermission> SaveAsync(FilePermission permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 ID 获取权限
    /// </summary>
    Task<FilePermission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取文件的所有权限
    /// </summary>
    Task<List<FilePermission>> ListByFileIdAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的所有权限
    /// </summary>
    Task<List<FilePermission>> ListByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查用户是否有文件权限
    /// </summary>
    Task<FilePermission?> GetByFileAndUserAsync(
        Guid fileId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新权限
    /// </summary>
    Task<FilePermission> UpdateAsync(FilePermission permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除权限
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除文件的所有权限
    /// </summary>
    Task<int> DeleteByFileIdAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除特定用户对文件的权限
    /// </summary>
    Task<bool> DeleteByFileAndUserAsync(Guid fileId, string userId, CancellationToken cancellationToken = default);
}
