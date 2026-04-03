using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Processors;

/// <summary>
/// 文件处理器服务（自动选择合适的处理器）
/// </summary>
public class FileProcessorService
{
    private readonly IEnumerable<IFileProcessor> _processors;
    private readonly ILogger<FileProcessorService> _logger;

    public FileProcessorService(
        IEnumerable<IFileProcessor> processors,
        ILogger<FileProcessorService> logger)
    {
        _processors = processors.OrderBy(p => p.Priority).ToList();
        _logger = logger;
    }

    /// <summary>
    /// 处理文件内容
    /// </summary>
    public async Task<ProcessedFileContent> ProcessFileAsync(
        string filePath,
        int maxLength,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filePath);

        if (string.IsNullOrEmpty(extension))
        {
            throw new ArgumentException("文件没有扩展名", nameof(filePath));
        }

        // 查找支持该文件类型的处理器
        var processor = _processors.FirstOrDefault(p => p.CanProcess(extension));

        if (processor == null)
        {
            _logger.LogWarning("未找到支持文件类型 {Extension} 的处理器，使用默认文本处理", extension);
            return await ProcessAsTextAsync(filePath, maxLength, cancellationToken);
        }

        _logger.LogDebug(
            "使用处理器 {ProcessorType} 处理文件 {FilePath}",
            processor.GetType().Name,
            filePath);

        return await processor.ProcessAsync(filePath, maxLength, cancellationToken);
    }

    /// <summary>
    /// 获取支持的文件扩展名列表
    /// </summary>
    public IReadOnlySet<string> GetSupportedExtensions()
    {
        var allExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var processor in _processors)
        {
            foreach (var ext in processor.SupportedExtensions)
            {
                allExtensions.Add(ext);
            }
        }

        return allExtensions;
    }

    /// <summary>
    /// 检查文件类型是否受支持
    /// </summary>
    public bool IsSupported(string fileExtension)
    {
        return _processors.Any(p => p.CanProcess(fileExtension));
    }

    /// <summary>
    /// 默认文本处理（当没有匹配的处理器时）
    /// </summary>
    private async Task<ProcessedFileContent> ProcessAsTextAsync(
        string filePath,
        int maxLength,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var originalLength = content.Length;

        var metadata = new Dictionary<string, object>
        {
            ["processor"] = "default",
            ["encoding"] = "utf-8"
        };

        if (content.Length > maxLength)
        {
            content = content[..maxLength];
            content += "\n\n... [内容已截断] ...";

            return ProcessedFileContent.CreateTruncated(content, originalLength, metadata);
        }

        return ProcessedFileContent.Create(content, metadata);
    }
}
