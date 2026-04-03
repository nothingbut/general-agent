namespace GeneralAgent.Infrastructure.FileStorage.Models;

/// <summary>
/// 上传文件模型
/// </summary>
public record UploadedFile
{
    /// <summary>
    /// 文件 ID
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 所属会话 ID
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// 原始文件名
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// 存储路径（相对于根目录）
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// 文件类型（扩展名，如 .txt, .cs, .json）
    /// </summary>
    public string FileType { get; init; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// MIME 类型
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// 上传时间
    /// </summary>
    public DateTime UploadedAt { get; init; }

    /// <summary>
    /// 文件摘要（可选）
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 标签（逗号分隔）
    /// </summary>
    public string? Tags { get; init; }

    /// <summary>
    /// 额外元数据（JSON）
    /// </summary>
    public string? Metadata { get; init; }

    /// <summary>
    /// 创建新的文件记录
    /// </summary>
    public static UploadedFile Create(
        string sessionId,
        string fileName,
        string filePath,
        string fileType,
        long fileSize,
        string? mimeType = null,
        string? summary = null,
        string? tags = null)
    {
        return new UploadedFile
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            FileName = fileName,
            FilePath = filePath,
            FileType = fileType,
            FileSize = fileSize,
            MimeType = mimeType,
            UploadedAt = DateTime.UtcNow,
            Summary = summary,
            Tags = tags
        };
    }

    /// <summary>
    /// 创建副本并更新摘要
    /// </summary>
    public UploadedFile WithSummary(string summary)
    {
        return this with { Summary = summary };
    }

    /// <summary>
    /// 创建副本并更新标签
    /// </summary>
    public UploadedFile WithTags(string tags)
    {
        return this with { Tags = tags };
    }
}
