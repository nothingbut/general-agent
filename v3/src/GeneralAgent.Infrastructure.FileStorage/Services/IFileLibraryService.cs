using GeneralAgent.Infrastructure.FileStorage.Models;

namespace GeneralAgent.Infrastructure.FileStorage.Services;

/// <summary>
/// 文件库服务接口（跨会话文件访问）
/// </summary>
public interface IFileLibraryService
{
    /// <summary>
    /// 列出用户可访问的所有文件（跨会话）
    /// </summary>
    Task<List<UploadedFile>> ListAccessibleFilesAsync(
        string userId,
        FileAccessLevel? filterByLevel = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 搜索文件（按名称、标签、摘要）
    /// </summary>
    Task<List<UploadedFile>> SearchFilesAsync(
        string userId,
        string keyword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取文件（带权限检查）
    /// </summary>
    Task<UploadedFile?> GetFileAsync(
        Guid fileId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出用户拥有的文件
    /// </summary>
    Task<List<UploadedFile>> ListOwnedFilesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出与用户共享的文件
    /// </summary>
    Task<List<UploadedFile>> ListSharedFilesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出公开文件
    /// </summary>
    Task<List<UploadedFile>> ListPublicFilesAsync(
        CancellationToken cancellationToken = default);
}
