using GeneralAgent.Infrastructure.FileStorage.Models;
using GeneralAgent.Infrastructure.FileStorage.Repositories;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Services;

/// <summary>
/// 文件权限服务
/// </summary>
public class FilePermissionService : IFilePermissionService
{
    private readonly IFilePermissionRepository _permissionRepository;
    private readonly FileRepository _fileRepository;
    private readonly ILogger<FilePermissionService> _logger;

    public FilePermissionService(
        IFilePermissionRepository permissionRepository,
        FileRepository fileRepository,
        ILogger<FilePermissionService> logger)
    {
        _permissionRepository = permissionRepository;
        _fileRepository = fileRepository;
        _logger = logger;
    }

    /// <summary>
    /// 授予权限
    /// </summary>
    public async Task GrantPermissionAsync(
        Guid fileId,
        string userId,
        string grantedBy,
        PermissionType permission,
        CancellationToken cancellationToken = default)
    {
        // 验证文件存在
        var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken);
        if (file == null)
        {
            throw new InvalidOperationException($"文件不存在: {fileId}");
        }

        // 验证授权人是文件所有者
        if (file.OwnerId != grantedBy)
        {
            throw new UnauthorizedAccessException($"只有文件所有者可以授予权限: {fileId}");
        }

        // 检查是否已存在权限
        var existingPermission = await _permissionRepository.GetByFileAndUserAsync(
            fileId, userId, cancellationToken);

        if (existingPermission != null)
        {
            // 更新现有权限
            var updated = existingPermission.WithPermission(permission);
            await _permissionRepository.UpdateAsync(updated, cancellationToken);
            _logger.LogInformation("更新文件权限: FileId={FileId}, UserId={UserId}, Permission={Permission}",
                fileId, userId, permission);
        }
        else
        {
            // 创建新权限
            var newPermission = FilePermission.Create(fileId, userId, grantedBy, permission);
            await _permissionRepository.SaveAsync(newPermission, cancellationToken);
            _logger.LogInformation("授予文件权限: FileId={FileId}, UserId={UserId}, Permission={Permission}",
                fileId, userId, permission);
        }
    }

    /// <summary>
    /// 撤销权限
    /// </summary>
    public async Task RevokePermissionAsync(
        Guid fileId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _permissionRepository.DeleteByFileAndUserAsync(fileId, userId, cancellationToken);

        if (!deleted)
        {
            _logger.LogWarning("撤销权限失败，权限不存在: FileId={FileId}, UserId={UserId}", fileId, userId);
        }
        else
        {
            _logger.LogInformation("撤销文件权限: FileId={FileId}, UserId={UserId}", fileId, userId);
        }
    }

    /// <summary>
    /// 列出文件的所有权限
    /// </summary>
    public async Task<List<FilePermission>> ListPermissionsAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        return await _permissionRepository.ListByFileIdAsync(fileId, cancellationToken);
    }

    /// <summary>
    /// 更新文件访问级别
    /// </summary>
    public async Task UpdateAccessLevelAsync(
        Guid fileId,
        string ownerId,
        FileAccessLevel newLevel,
        CancellationToken cancellationToken = default)
    {
        // 验证文件存在
        var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken);
        if (file == null)
        {
            throw new InvalidOperationException($"文件不存在: {fileId}");
        }

        // 验证是文件所有者
        if (file.OwnerId != ownerId)
        {
            throw new UnauthorizedAccessException($"只有文件所有者可以修改访问级别: {fileId}");
        }

        // 更新访问级别
        var updated = file.WithAccessLevel(newLevel);
        await _fileRepository.UpdateAsync(updated, cancellationToken);

        _logger.LogInformation("更新文件访问级别: FileId={FileId}, OldLevel={OldLevel}, NewLevel={NewLevel}",
            fileId, file.AccessLevel, newLevel);

        // 如果改为私有，删除所有权限记录
        if (newLevel == FileAccessLevel.Private)
        {
            var deletedCount = await _permissionRepository.DeleteByFileIdAsync(fileId, cancellationToken);
            _logger.LogInformation("文件改为私有，删除所有权限记录: FileId={FileId}, Count={Count}",
                fileId, deletedCount);
        }
    }

    /// <summary>
    /// 检查用户是否有文件访问权限
    /// </summary>
    public async Task<bool> HasAccessAsync(
        Guid fileId,
        string userId,
        PermissionType requiredPermission = PermissionType.Read,
        CancellationToken cancellationToken = default)
    {
        // 获取文件
        var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken);
        if (file == null)
        {
            return false;
        }

        // 1. 如果是文件所有者，有完全权限
        if (file.OwnerId == userId)
        {
            return true;
        }

        // 2. 如果文件是公开的，所有人都有读权限
        if (file.AccessLevel == FileAccessLevel.Public)
        {
            return requiredPermission == PermissionType.Read;
        }

        // 3. 如果文件是共享的，检查权限表
        if (file.AccessLevel == FileAccessLevel.Shared)
        {
            var permission = await _permissionRepository.GetByFileAndUserAsync(
                fileId, userId, cancellationToken);

            if (permission == null)
            {
                return false;
            }

            // 如果需要写权限，检查是否有写权限
            if (requiredPermission == PermissionType.Write)
            {
                return permission.Permission == PermissionType.Write;
            }

            // 如果只需要读权限，任何权限都可以
            return true;
        }

        // 4. 私有文件，非所有者无权访问
        return false;
    }
}
