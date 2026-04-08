using GeneralAgent.Infrastructure.FileStorage.Models;
using GeneralAgent.Infrastructure.FileStorage.Repositories;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Services;

/// <summary>
/// 文件版本服务
/// </summary>
public class FileVersionService : IFileVersionService
{
    private readonly FileRepository _fileRepository;
    private readonly ILogger<FileVersionService> _logger;

    public FileVersionService(
        FileRepository fileRepository,
        ILogger<FileVersionService> logger)
    {
        _fileRepository = fileRepository;
        _logger = logger;
    }

    /// <summary>
    /// 创建新版本（上传同名文件时）
    /// </summary>
    public async Task<UploadedFile> CreateNewVersionAsync(
        Guid parentFileId,
        string filePath,
        long fileSize,
        string userId,
        string? mimeType = null,
        CancellationToken cancellationToken = default)
    {
        // 获取父文件
        var parentFile = await _fileRepository.GetByIdAsync(parentFileId, cancellationToken);
        if (parentFile == null)
        {
            throw new InvalidOperationException($"父文件不存在: {parentFileId}");
        }

        // 验证用户是文件所有者
        if (parentFile.OwnerId != userId)
        {
            throw new UnauthorizedAccessException($"只有文件所有者可以创建新版本: {parentFileId}");
        }

        // 标记旧版本为非最新
        await _fileRepository.MarkAsNotLatestAsync(parentFileId, cancellationToken);

        // 创建新版本
        var newVersion = UploadedFile.CreateNewVersion(
            parentFile,
            filePath,
            fileSize,
            mimeType);

        await _fileRepository.SaveAsync(newVersion, cancellationToken);

        _logger.LogInformation("创建文件新版本: FileId={FileId}, Version={Version}, ParentId={ParentId}",
            newVersion.Id, newVersion.Version, parentFileId);

        return newVersion;
    }

    /// <summary>
    /// 获取文件的所有版本
    /// </summary>
    public async Task<List<UploadedFile>> GetVersionHistoryAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        // 获取当前文件
        var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken);
        if (file == null)
        {
            throw new InvalidOperationException($"文件不存在: {fileId}");
        }

        // 找到根文件 ID（如果当前文件有父文件，追溯到根）
        var rootFileId = fileId;
        var currentFile = file;

        while (currentFile.ParentFileId.HasValue)
        {
            rootFileId = currentFile.ParentFileId.Value;
            currentFile = await _fileRepository.GetByIdAsync(rootFileId, cancellationToken);

            if (currentFile == null)
            {
                break;
            }
        }

        // 获取所有版本
        var versions = await _fileRepository.GetVersionsAsync(rootFileId, cancellationToken);

        _logger.LogInformation("获取文件版本历史: FileId={FileId}, VersionCount={Count}",
            fileId, versions.Count);

        return versions;
    }

    /// <summary>
    /// 恢复到特定版本（创建新版本指向旧版本内容）
    /// </summary>
    public async Task<UploadedFile> RestoreVersionAsync(
        Guid fileId,
        int version,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // 获取版本历史
        var versions = await GetVersionHistoryAsync(fileId, cancellationToken);

        // 查找目标版本
        var targetVersion = versions.FirstOrDefault(v => v.Version == version);
        if (targetVersion == null)
        {
            throw new InvalidOperationException($"版本不存在: Version={version}");
        }

        // 获取当前最新版本
        var latestVersion = versions.FirstOrDefault(v => v.IsLatest);
        if (latestVersion == null)
        {
            throw new InvalidOperationException("找不到最新版本");
        }

        // 验证用户是文件所有者
        if (latestVersion.OwnerId != userId)
        {
            throw new UnauthorizedAccessException($"只有文件所有者可以恢复版本: {fileId}");
        }

        // 标记当前最新版本为非最新
        await _fileRepository.MarkAsNotLatestAsync(latestVersion.Id, cancellationToken);

        // 创建新版本，指向旧版本的内容
        var restoredVersion = UploadedFile.CreateNewVersion(
            latestVersion,
            targetVersion.FilePath,  // 使用目标版本的文件路径
            targetVersion.FileSize,
            targetVersion.MimeType);

        await _fileRepository.SaveAsync(restoredVersion, cancellationToken);

        _logger.LogInformation("恢复文件版本: FileId={FileId}, TargetVersion={TargetVersion}, NewVersion={NewVersion}",
            fileId, version, restoredVersion.Version);

        return restoredVersion;
    }

    /// <summary>
    /// 获取最新版本
    /// </summary>
    public async Task<UploadedFile?> GetLatestVersionAsync(
        Guid rootFileId,
        CancellationToken cancellationToken = default)
    {
        return await _fileRepository.GetLatestVersionAsync(rootFileId, cancellationToken);
    }
}
