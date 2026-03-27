namespace GeneralAgent.Infrastructure.Embedding;

/// <summary>
/// Embedding 配置选项
/// </summary>
public sealed class EmbeddingOptions
{
    /// <summary>
    /// 配置段名称
    /// </summary>
    public const string SectionName = "Embedding";

    /// <summary>
    /// 提供商名称（当前仅支持 "Ollama"）
    /// </summary>
    public string Provider { get; init; } = "Ollama";

    /// <summary>
    /// Embedding 服务基础 URL
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:11434";

    /// <summary>
    /// Embedding 模型名称
    /// </summary>
    public string Model { get; init; } = "nomic-embed-text";

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;
}
