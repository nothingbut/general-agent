using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Processors;

/// <summary>
/// 文本文件处理器（.txt, .md, .markdown）
/// </summary>
public class TextFileProcessor : IFileProcessor
{
    private readonly ILogger<TextFileProcessor> _logger;

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".log"
    };

    public IReadOnlySet<string> SupportedExtensions => _supportedExtensions;

    public int Priority => 10;

    public TextFileProcessor(ILogger<TextFileProcessor> logger)
    {
        _logger = logger;
    }

    public bool CanProcess(string fileExtension)
    {
        return _supportedExtensions.Contains(fileExtension);
    }

    public async Task<ProcessedFileContent> ProcessAsync(
        string filePath,
        int maxLength,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var originalLength = content.Length;

        // 统计行数
        var lineCount = content.Split('\n').Length;

        var metadata = new Dictionary<string, object>
        {
            ["line_count"] = lineCount,
            ["encoding"] = "utf-8"
        };

        // 检查是否需要截断
        if (content.Length > maxLength)
        {
            _logger.LogWarning(
                "文本文件内容过长（{Length} 字符），截断到 {MaxLength} 字符: {FilePath}",
                content.Length,
                maxLength,
                filePath);

            content = content[..maxLength];
            content += "\n\n... [内容已截断] ...";

            return ProcessedFileContent.CreateTruncated(content, originalLength, metadata);
        }

        return ProcessedFileContent.Create(content, metadata);
    }
}
