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

    // ==================== 跨会话访问新增字段 ====================

    /// <summary>
    /// 文件所有者用户 ID
    /// </summary>
    public string OwnerId { get; init; } = "system";

    /// <summary>
    /// 访问级别
    /// </summary>
    public FileAccessLevel AccessLevel { get; init; } = FileAccessLevel.Private;

    /// <summary>
    /// 文件版本号
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// 父版本文件 ID（用于版本链）
    /// </summary>
    public Guid? ParentFileId { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// 是否为最新版本
    /// </summary>
    public bool IsLatest { get; init; } = true;

    /// <summary>
    /// 创建新的文件记录
    /// </summary>
    public static UploadedFile Create(
        string sessionId,
        string fileName,
        string filePath,
        string fileType,
        long fileSize,
        string ownerId,
        string? mimeType = null,
        string? summary = null,
        string? tags = null,
        FileAccessLevel accessLevel = FileAccessLevel.Private)
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
            Tags = tags,
            OwnerId = ownerId,
            AccessLevel = accessLevel,
            Version = 1,
            IsLatest = true
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

    /// <summary>
    /// 创建副本并更新访问级别
    /// </summary>
    public UploadedFile WithAccessLevel(FileAccessLevel accessLevel)
    {
        return this with { AccessLevel = accessLevel };
    }

    /// <summary>
    /// 创建副本并更新元数据
    /// </summary>
    public UploadedFile WithMetadata(string metadata)
    {
        return this with { Metadata = metadata, UpdatedAt = DateTime.UtcNow };
    }

    /// <summary>
    /// 创建新版本文件
    /// </summary>
    public static UploadedFile CreateNewVersion(
        UploadedFile parent,
        string newFilePath,
        long newFileSize,
        string? newMimeType = null)
    {
        return new UploadedFile
        {
            Id = Guid.NewGuid(),
            SessionId = parent.SessionId,
            FileName = parent.FileName,
            FilePath = newFilePath,
            FileType = parent.FileType,
            FileSize = newFileSize,
            MimeType = newMimeType ?? parent.MimeType,
            UploadedAt = parent.UploadedAt,
            Summary = parent.Summary,
            Tags = parent.Tags,
            Metadata = parent.Metadata,
            OwnerId = parent.OwnerId,
            AccessLevel = parent.AccessLevel,
            Version = parent.Version + 1,
            ParentFileId = parent.Id,
            UpdatedAt = DateTime.UtcNow,
            IsLatest = true
        };
    }
}
