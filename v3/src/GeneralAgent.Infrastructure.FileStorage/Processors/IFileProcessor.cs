namespace GeneralAgent.Infrastructure.FileStorage.Processors;

/// <summary>
/// 文件处理器接口
/// </summary>
public interface IFileProcessor
{
    /// <summary>
    /// 支持的文件扩展名
    /// </summary>
    IReadOnlySet<string> SupportedExtensions { get; }

    /// <summary>
    /// 处理器优先级（数字越小优先级越高）
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 判断是否可以处理指定文件类型
    /// </summary>
    bool CanProcess(string fileExtension);

    /// <summary>
    /// 处理文件内容
    /// </summary>
    /// <param name="filePath">文件绝对路径</param>
    /// <param name="maxLength">最大内容长度</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理后的内容</returns>
    Task<ProcessedFileContent> ProcessAsync(
        string filePath,
        int maxLength,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理后的文件内容
/// </summary>
public record ProcessedFileContent
{
    /// <summary>
    /// 文本内容
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 是否被截断
    /// </summary>
    public bool IsTruncated { get; init; }

    /// <summary>
    /// 原始长度
    /// </summary>
    public int OriginalLength { get; init; }

    /// <summary>
    /// 处理后长度
    /// </summary>
    public int ProcessedLength { get; init; }

    /// <summary>
    /// 额外元数据（如代码语言、行数等）
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// 创建未截断的内容
    /// </summary>
    public static ProcessedFileContent Create(string content, Dictionary<string, object>? metadata = null)
    {
        return new ProcessedFileContent
        {
            Content = content,
            IsTruncated = false,
            OriginalLength = content.Length,
            ProcessedLength = content.Length,
            Metadata = metadata
        };
    }

    /// <summary>
    /// 创建截断的内容
    /// </summary>
    public static ProcessedFileContent CreateTruncated(
        string content,
        int originalLength,
        Dictionary<string, object>? metadata = null)
    {
        return new ProcessedFileContent
        {
            Content = content,
            IsTruncated = true,
            OriginalLength = originalLength,
            ProcessedLength = content.Length,
            Metadata = metadata
        };
    }
}
