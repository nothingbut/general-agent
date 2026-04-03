namespace GeneralAgent.Infrastructure.FileStorage;

/// <summary>
/// 文件存储配置选项
/// </summary>
public class FileStorageOptions
{
    /// <summary>
    /// 文件存储根目录
    /// 默认: ~/.general-agent/
    /// </summary>
    public string RootDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".general-agent");

    /// <summary>
    /// SQLite 数据库文件路径
    /// 默认: ~/.general-agent/files.db
    /// </summary>
    public string DatabasePath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".general-agent",
        "files.db");

    /// <summary>
    /// 最大文件大小（字节）
    /// 默认: 5 MB
    /// </summary>
    public long MaxFileSizeBytes { get; init; } = 5 * 1024 * 1024;

    /// <summary>
    /// 允许的文件扩展名（白名单）
    /// 默认: 文本、代码、配置文件
    /// </summary>
    public HashSet<string> AllowedExtensions { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        // 文本文件
        ".txt", ".md", ".markdown",

        // 代码文件
        ".cs", ".py", ".js", ".ts", ".jsx", ".tsx",
        ".rs", ".go", ".java", ".cpp", ".c", ".h",
        ".rb", ".php", ".swift", ".kt", ".scala",

        // 配置文件
        ".json", ".yaml", ".yml", ".xml", ".toml",
        ".ini", ".conf", ".config",

        // Web 文件
        ".html", ".css", ".scss", ".less",

        // Shell 脚本
        ".sh", ".bash", ".zsh", ".ps1", ".bat"
    };

    /// <summary>
    /// 文件内容最大长度（字符数）
    /// 超过此长度将被截断，避免超出 LLM token 限制
    /// 默认: 10000 字符（约 2500 tokens）
    /// </summary>
    public int MaxContentLength { get; init; } = 10000;
}
