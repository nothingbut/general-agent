using GeneralAgent.Infrastructure.FileStorage.Models;

namespace GeneralAgent.Infrastructure.FileStorage.Services;

/// <summary>
/// 文件版本服务接口
/// </summary>
public interface IFileVersionService
{
    /// <summary>
    /// 创建新版本（上传同名文件时）
    /// </summary>
    Task<UploadedFile> CreateNewVersionAsync(
        Guid parentFileId,
        string filePath,
        long fileSize,
        string userId,
        string? mimeType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取文件的所有版本
    /// </summary>
    Task<List<UploadedFile>> GetVersionHistoryAsync(
        Guid fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复到特定版本（创建新版本指向旧版本内容）
    /// </summary>
    Task<UploadedFile> RestoreVersionAsync(
        Guid fileId,
        int version,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取最新版本
    /// </summary>
    Task<UploadedFile?> GetLatestVersionAsync(
        Guid rootFileId,
        CancellationToken cancellationToken = default);
}
