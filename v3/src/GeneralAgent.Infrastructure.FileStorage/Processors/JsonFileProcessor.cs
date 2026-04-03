using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Processors;

/// <summary>
/// JSON/YAML 文件处理器
/// </summary>
public class JsonFileProcessor : IFileProcessor
{
    private readonly ILogger<JsonFileProcessor> _logger;

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".yaml", ".yml", ".xml", ".toml",
        ".ini", ".conf", ".config"
    };

    public IReadOnlySet<string> SupportedExtensions => _supportedExtensions;

    public int Priority => 30;

    public JsonFileProcessor(ILogger<JsonFileProcessor> logger)
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
        var extension = Path.GetExtension(filePath);

        var metadata = new Dictionary<string, object>
        {
            ["format"] = GetFormatName(extension),
            ["encoding"] = "utf-8"
        };

        // 尝试验证 JSON 格式（仅用于 .json 文件）
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                metadata["valid_json"] = true;
                metadata["root_type"] = doc.RootElement.ValueKind.ToString();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("JSON 格式验证失败: {Message}", ex.Message);
                metadata["valid_json"] = false;
                metadata["parse_error"] = ex.Message;
            }
        }

        // 检查是否需要截断
        if (content.Length > maxLength)
        {
            _logger.LogWarning(
                "配置文件内容过长（{Length} 字符），截断到 {MaxLength} 字符: {FilePath}",
                content.Length,
                maxLength,
                filePath);

            content = content[..maxLength];
            content += "\n\n... [内容已截断] ...";

            return ProcessedFileContent.CreateTruncated(content, originalLength, metadata);
        }

        return ProcessedFileContent.Create(content, metadata);
    }

    private static string GetFormatName(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".json" => "json",
            ".yaml" or ".yml" => "yaml",
            ".xml" => "xml",
            ".toml" => "toml",
            ".ini" => "ini",
            ".conf" or ".config" => "config",
            _ => "text"
        };
    }
}
