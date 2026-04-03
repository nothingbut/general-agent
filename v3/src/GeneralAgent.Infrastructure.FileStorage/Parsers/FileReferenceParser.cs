using System.Text.RegularExpressions;
using GeneralAgent.Infrastructure.FileStorage.Models;
using GeneralAgent.Infrastructure.FileStorage.Processors;
using GeneralAgent.Infrastructure.FileStorage.Services;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Parsers;

/// <summary>
/// 文件引用解析器（解析消息中的 @file: 引用）
/// </summary>
public partial class FileReferenceParser
{
    private readonly FileStorageService _fileStorageService;
    private readonly ILogger<FileReferenceParser> _logger;

    // 正则表达式：@file:文件名 或 @file:GUID
    // 支持格式：
    // - @file:config.json
    // @file:abc123
    // - @file:abc12345-1234-1234-1234-123456789abc
    [GeneratedRegex(@"@file:([a-zA-Z0-9._\-]+)", RegexOptions.Compiled)]
    private static partial Regex FileReferenceRegex();

    public FileReferenceParser(
        FileStorageService fileStorageService,
        ILogger<FileReferenceParser> logger)
    {
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    /// <summary>
    /// 从消息中提取所有文件引用
    /// </summary>
    public List<FileReference> ExtractReferences(string message)
    {
        var references = new List<FileReference>();
        var matches = FileReferenceRegex().Matches(message);

        foreach (Match match in matches)
        {
            var referenceText = match.Value; // @file:xxx
            var identifier = match.Groups[1].Value; // xxx

            // 判断是 ID 还是文件名
            if (Guid.TryParse(identifier, out var fileId))
            {
                references.Add(FileReference.CreateIdReference(
                    referenceText,
                    fileId,
                    match.Index,
                    match.Length));
            }
            else
            {
                references.Add(FileReference.CreateFileNameReference(
                    referenceText,
                    identifier,
                    match.Index,
                    match.Length));
            }
        }

        _logger.LogDebug("从消息中提取到 {Count} 个文件引用", references.Count);
        return references;
    }

    /// <summary>
    /// 解析并替换消息中的文件引用
    /// </summary>
    /// <param name="message">原始消息</param>
    /// <param name="sessionId">当前会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>替换后的消息</returns>
    public async Task<ProcessedMessage> ProcessMessageAsync(
        string message,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var references = ExtractReferences(message);

        if (references.Count == 0)
        {
            return new ProcessedMessage
            {
                OriginalMessage = message,
                ProcessedContent = message,
                ResolvedFiles = new List<ResolvedFileReference>()
            };
        }

        var resolvedFiles = new List<ResolvedFileReference>();
        var processedMessage = message;
        var offsetAdjustment = 0; // 跟踪替换导致的偏移量变化

        // 按位置顺序处理引用（从前往后）
        foreach (var reference in references.OrderBy(r => r.StartIndex))
        {
            var resolved = await ResolveReferenceAsync(reference, sessionId, cancellationToken);
            resolvedFiles.Add(resolved);

            if (resolved.IsResolved)
            {
                // 构建替换文本
                var replacement = BuildReplacementText(resolved);

                // 计算当前引用的实际位置（考虑之前的替换）
                var actualIndex = reference.StartIndex + offsetAdjustment;

                // 替换文本
                processedMessage = processedMessage.Remove(actualIndex, reference.Length);
                processedMessage = processedMessage.Insert(actualIndex, replacement);

                // 更新偏移量
                offsetAdjustment += replacement.Length - reference.Length;

                _logger.LogInformation(
                    "成功解析文件引用: {Reference} -> {FileName}",
                    reference.OriginalText,
                    resolved.File?.FileName);
            }
            else
            {
                _logger.LogWarning(
                    "无法解析文件引用: {Reference}, 原因: {Error}",
                    reference.OriginalText,
                    resolved.Error);
            }
        }

        return new ProcessedMessage
        {
            OriginalMessage = message,
            ProcessedContent = processedMessage,
            ResolvedFiles = resolvedFiles
        };
    }

    /// <summary>
    /// 解析单个文件引用
    /// </summary>
    private async Task<ResolvedFileReference> ResolveReferenceAsync(
        FileReference reference,
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            UploadedFile? file = null;

            if (reference.IsIdReference)
            {
                // 按 ID 查找
                file = await _fileStorageService.GetFileAsync(reference.FileId!.Value, cancellationToken);

                if (file == null)
                {
                    return ResolvedFileReference.CreateUnresolved(
                        reference,
                        $"未找到 ID 为 {reference.FileId} 的文件");
                }
            }
            else
            {
                // 按文件名查找
                var files = await _fileStorageService.GetFilesByNameAsync(
                    reference.FileName!,
                    sessionId,
                    cancellationToken);

                if (files.Count == 0)
                {
                    return ResolvedFileReference.CreateUnresolved(
                        reference,
                        $"未找到名为 '{reference.FileName}' 的文件");
                }

                if (files.Count > 1)
                {
                    _logger.LogWarning(
                        "找到多个名为 '{FileName}' 的文件，使用最新上传的",
                        reference.FileName);
                }

                // 使用最新上传的文件
                file = files.OrderByDescending(f => f.UploadedAt).First();
            }

            // 读取文件内容
            var content = await _fileStorageService.ReadFileContentAsync(file, cancellationToken);

            return ResolvedFileReference.CreateResolved(reference, file, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析文件引用失败: {Reference}", reference.OriginalText);
            return ResolvedFileReference.CreateUnresolved(reference, ex.Message);
        }
    }

    /// <summary>
    /// 构建替换文本
    /// </summary>
    private static string BuildReplacementText(ResolvedFileReference resolved)
    {
        if (!resolved.IsResolved || resolved.File == null || resolved.Content == null)
        {
            return resolved.Reference.OriginalText; // 保持原样
        }

        // 格式：
        // <file name="文件名" type="类型" size="大小">
        // 文件内容
        // </file>
        var header = $"<file name=\"{resolved.File.FileName}\" " +
                     $"type=\"{resolved.File.FileType}\" " +
                     $"size=\"{resolved.File.FileSize}\">";

        var footer = "</file>";

        var truncatedInfo = resolved.Content.IsTruncated
            ? $"\n[内容已截断: 原始 {resolved.Content.OriginalLength} 字符，显示 {resolved.Content.ProcessedLength} 字符]"
            : "";

        return $"{header}\n{resolved.Content.Content}{truncatedInfo}\n{footer}";
    }
}

/// <summary>
/// 处理后的消息
/// </summary>
public record ProcessedMessage
{
    /// <summary>
    /// 原始消息
    /// </summary>
    public string OriginalMessage { get; init; } = string.Empty;

    /// <summary>
    /// 处理后的消息（文件引用已替换为内容）
    /// </summary>
    public string ProcessedContent { get; init; } = string.Empty;

    /// <summary>
    /// 解析的文件列表
    /// </summary>
    public List<ResolvedFileReference> ResolvedFiles { get; init; } = new();

    /// <summary>
    /// 是否包含文件引用
    /// </summary>
    public bool HasFileReferences => ResolvedFiles.Count > 0;

    /// <summary>
    /// 是否所有引用都解析成功
    /// </summary>
    public bool AllReferencesResolved => ResolvedFiles.All(r => r.IsResolved);
}

/// <summary>
/// 解析后的文件引用
/// </summary>
public record ResolvedFileReference
{
    /// <summary>
    /// 原始引用
    /// </summary>
    public FileReference Reference { get; init; } = null!;

    /// <summary>
    /// 是否解析成功
    /// </summary>
    public bool IsResolved { get; init; }

    /// <summary>
    /// 解析后的文件
    /// </summary>
    public UploadedFile? File { get; init; }

    /// <summary>
    /// 文件内容
    /// </summary>
    public ProcessedFileContent? Content { get; init; }

    /// <summary>
    /// 错误信息（如果解析失败）
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 创建已解析的引用
    /// </summary>
    public static ResolvedFileReference CreateResolved(
        FileReference reference,
        UploadedFile file,
        ProcessedFileContent content)
    {
        return new ResolvedFileReference
        {
            Reference = reference,
            IsResolved = true,
            File = file,
            Content = content,
            Error = null
        };
    }

    /// <summary>
    /// 创建未解析的引用
    /// </summary>
    public static ResolvedFileReference CreateUnresolved(
        FileReference reference,
        string error)
    {
        return new ResolvedFileReference
        {
            Reference = reference,
            IsResolved = false,
            File = null,
            Content = null,
            Error = error
        };
    }
}
