using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Processors;

/// <summary>
/// 代码文件处理器
/// </summary>
public class CodeFileProcessor : IFileProcessor
{
    private readonly ILogger<CodeFileProcessor> _logger;

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".py", ".js", ".ts", ".jsx", ".tsx",
        ".rs", ".go", ".java", ".cpp", ".c", ".h", ".hpp",
        ".rb", ".php", ".swift", ".kt", ".scala",
        ".html", ".css", ".scss", ".less",
        ".sh", ".bash", ".zsh", ".ps1", ".bat"
    };

    private static readonly Dictionary<string, string> _languageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp",
        [".py"] = "python",
        [".js"] = "javascript",
        [".ts"] = "typescript",
        [".jsx"] = "javascript",
        [".tsx"] = "typescript",
        [".rs"] = "rust",
        [".go"] = "go",
        [".java"] = "java",
        [".cpp"] = "cpp",
        [".c"] = "c",
        [".h"] = "c",
        [".hpp"] = "cpp",
        [".rb"] = "ruby",
        [".php"] = "php",
        [".swift"] = "swift",
        [".kt"] = "kotlin",
        [".scala"] = "scala",
        [".html"] = "html",
        [".css"] = "css",
        [".scss"] = "scss",
        [".less"] = "less",
        [".sh"] = "bash",
        [".bash"] = "bash",
        [".zsh"] = "zsh",
        [".ps1"] = "powershell",
        [".bat"] = "batch"
    };

    public IReadOnlySet<string> SupportedExtensions => _supportedExtensions;

    public int Priority => 20;

    public CodeFileProcessor(ILogger<CodeFileProcessor> logger)
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

        // 统计代码行数
        var lines = content.Split('\n');
        var lineCount = lines.Length;
        var nonEmptyLineCount = lines.Count(line => !string.IsNullOrWhiteSpace(line));

        // 检测编程语言
        var language = _languageMap.TryGetValue(extension, out var lang) ? lang : "text";

        var metadata = new Dictionary<string, object>
        {
            ["language"] = language,
            ["line_count"] = lineCount,
            ["non_empty_lines"] = nonEmptyLineCount,
            ["encoding"] = "utf-8"
        };

        // 检查是否需要截断
        if (content.Length > maxLength)
        {
            _logger.LogWarning(
                "代码文件内容过长（{Length} 字符），截断到 {MaxLength} 字符: {FilePath}",
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
