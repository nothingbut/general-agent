using GeneralAgent.Infrastructure.FileStorage.Models;
using GeneralAgent.Infrastructure.FileStorage.Repositories;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Services;

/// <summary>
/// 文件库服务（跨会话文件访问）
/// </summary>
public class FileLibraryService : IFileLibraryService
{
    private readonly FileRepository _fileRepository;
    private readonly IFilePermissionRepository _permissionRepository;
    private readonly IFilePermissionService _permissionService;
    private readonly ILogger<FileLibraryService> _logger;

    public FileLibraryService(
        FileRepository fileRepository,
        IFilePermissionRepository permissionRepository,
        IFilePermissionService permissionService,
        ILogger<FileLibraryService> logger)
    {
        _fileRepository = fileRepository;
        _permissionRepository = permissionRepository;
        _permissionService = permissionService;
        _logger = logger;
    }

    /// <summary>
    /// 列出用户可访问的所有文件（跨会话）
    /// </summary>
    public async Task<List<UploadedFile>> ListAccessibleFilesAsync(
        string userId,
        FileAccessLevel? filterByLevel = null,
        CancellationToken cancellationToken = default)
    {
        var accessibleFiles = new List<UploadedFile>();

        // 1. 用户拥有的文件
        if (!filterByLevel.HasValue || filterByLevel == FileAccessLevel.Private ||
            filterByLevel == FileAccessLevel.Shared || filterByLevel == FileAccessLevel.Public)
        {
            var ownedFiles = await _fileRepository.ListByOwnerAsync(userId, cancellationToken);

            if (filterByLevel.HasValue)
            {
                ownedFiles = ownedFiles.Where(f => f.AccessLevel == filterByLevel.Value).ToList();
            }

            accessibleFiles.AddRange(ownedFiles);
        }

        // 2. 公开文件（非用户自己的）
        if (!filterByLevel.HasValue || filterByLevel == FileAccessLevel.Public)
        {
            var publicFiles = await _fileRepository.ListByAccessLevelAsync(
                FileAccessLevel.Public, cancellationToken);

            // 排除用户自己的文件（已在上面添加）
            var otherPublicFiles = publicFiles.Where(f => f.OwnerId != userId);
            accessibleFiles.AddRange(otherPublicFiles);
        }

        // 3. 共享给用户的文件
        if (!filterByLevel.HasValue || filterByLevel == FileAccessLevel.Shared)
        {
            var userPermissions = await _permissionRepository.ListByUserIdAsync(userId, cancellationToken);

            foreach (var permission in userPermissions)
            {
                var file = await _fileRepository.GetByIdAsync(permission.FileId, cancellationToken);

                if (file != null && file.IsLatest && file.OwnerId != userId)
                {
                    accessibleFiles.Add(file);
                }
            }
        }

        // 去重并按上传时间排序
        var uniqueFiles = accessibleFiles
            .GroupBy(f => f.Id)
            .Select(g => g.First())
            .OrderByDescending(f => f.UploadedAt)
            .ToList();

        _logger.LogInformation("列出用户可访问文件: UserId={UserId}, Count={Count}", userId, uniqueFiles.Count);

        return uniqueFiles;
    }

    /// <summary>
    /// 搜索文件（按名称、标签、摘要）
    /// </summary>
    public async Task<List<UploadedFile>> SearchFilesAsync(
        string userId,
        string keyword,
        CancellationToken cancellationToken = default)
    {
        // 搜索所有匹配的文件
        var searchResults = await _fileRepository.SearchAsync(keyword, null, cancellationToken);

        // 过滤出用户有权限访问的文件
        var accessibleFiles = new List<UploadedFile>();

        foreach (var file in searchResults)
        {
            var hasAccess = await _permissionService.HasAccessAsync(
                file.Id, userId, PermissionType.Read, cancellationToken);

            if (hasAccess)
            {
                accessibleFiles.Add(file);
            }
        }

        _logger.LogInformation("搜索文件: UserId={UserId}, Keyword={Keyword}, Count={Count}",
            userId, keyword, accessibleFiles.Count);

        return accessibleFiles;
    }

    /// <summary>
    /// 获取文件（带权限检查）
    /// </summary>
    public async Task<UploadedFile?> GetFileAsync(
        Guid fileId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // 检查权限
        var hasAccess = await _permissionService.HasAccessAsync(
            fileId, userId, PermissionType.Read, cancellationToken);

        if (!hasAccess)
        {
            _logger.LogWarning("用户无权访问文件: UserId={UserId}, FileId={FileId}", userId, fileId);
            return null;
        }

        return await _fileRepository.GetByIdAsync(fileId, cancellationToken);
    }

    /// <summary>
    /// 列出用户拥有的文件
    /// </summary>
    public async Task<List<UploadedFile>> ListOwnedFilesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _fileRepository.ListByOwnerAsync(userId, cancellationToken);
    }

    /// <summary>
    /// 列出与用户共享的文件
    /// </summary>
    public async Task<List<UploadedFile>> ListSharedFilesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var userPermissions = await _permissionRepository.ListByUserIdAsync(userId, cancellationToken);
        var sharedFiles = new List<UploadedFile>();

        foreach (var permission in userPermissions)
        {
            var file = await _fileRepository.GetByIdAsync(permission.FileId, cancellationToken);

            if (file != null && file.IsLatest && file.AccessLevel == FileAccessLevel.Shared)
            {
                sharedFiles.Add(file);
            }
        }

        return sharedFiles.OrderByDescending(f => f.UploadedAt).ToList();
    }

    /// <summary>
    /// 列出公开文件
    /// </summary>
    public async Task<List<UploadedFile>> ListPublicFilesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _fileRepository.ListByAccessLevelAsync(
            FileAccessLevel.Public, cancellationToken);
    }
}
