namespace GeneralAgent.Infrastructure.FileStorage.Models;

/// <summary>
/// 文件引用模型（用于解析对话中的 @file: 引用）
/// </summary>
public class FileReference
{
    /// <summary>
    /// 原始引用文本（如 @file:config.json 或 @file:abc123）
    /// </summary>
    public string OriginalText { get; init; } = string.Empty;

    /// <summary>
    /// 文件名（如果是按文件名引用）
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// 文件 ID（如果是按 ID 引用）
    /// </summary>
    public Guid? FileId { get; init; }

    /// <summary>
    /// 在消息中的起始位置
    /// </summary>
    public int StartIndex { get; init; }

    /// <summary>
    /// 引用文本长度
    /// </summary>
    public int Length { get; init; }

    /// <summary>
    /// 是否是 ID 引用
    /// </summary>
    public bool IsIdReference => FileId.HasValue;

    /// <summary>
    /// 创建文件名引用
    /// </summary>
    public static FileReference CreateFileNameReference(
        string originalText,
        string fileName,
        int startIndex,
        int length)
    {
        return new FileReference
        {
            OriginalText = originalText,
            FileName = fileName,
            StartIndex = startIndex,
            Length = length
        };
    }

    /// <summary>
    /// 创建 ID 引用
    /// </summary>
    public static FileReference CreateIdReference(
        string originalText,
        Guid fileId,
        int startIndex,
        int length)
    {
        return new FileReference
        {
            OriginalText = originalText,
            FileId = fileId,
            StartIndex = startIndex,
            Length = length
        };
    }
}
