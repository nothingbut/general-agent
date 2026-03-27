namespace GeneralAgent.Infrastructure.Memory;

/// <summary>
/// 记忆系统配置选项
/// </summary>
public class MemoryOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Memory";

    /// <summary>
    /// 记忆存储根目录（默认: ~/.agent/memory/）
    /// </summary>
    public string RootDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".agent",
        "memory");

    /// <summary>
    /// 索引文件名
    /// </summary>
    public string IndexFileName { get; set; } = "MEMORY.md";

    /// <summary>
    /// 是否在启动时验证索引
    /// </summary>
    public bool ValidateIndexOnStartup { get; set; } = true;

    /// <summary>
    /// 是否自动重建损坏的索引
    /// </summary>
    public bool AutoRebuildCorruptedIndex { get; set; } = true;

    /// <summary>
    /// 记忆文件编码
    /// </summary>
    public string FileEncoding { get; set; } = "utf-8";
}
