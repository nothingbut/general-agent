namespace GeneralAgent.Infrastructure.LLM;

/// <summary>
/// LLM 配置选项
/// </summary>
public sealed class LLMOptions
{
    /// <summary>
    /// 默认使用的提供商
    /// </summary>
    public string DefaultProvider { get; init; } = "Ollama";

    /// <summary>
    /// 配置的提供商列表
    /// </summary>
    public Dictionary<string, LLMProviderConfig> Providers { get; init; } = new();
}

/// <summary>
/// LLM 提供商配置
/// </summary>
public sealed class LLMProviderConfig
{
    /// <summary>
    /// 提供商名称
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// API 基础 URL
    /// </summary>
    public string BaseUrl { get; init; } = "";

    /// <summary>
    /// 默认模型名称
    /// </summary>
    public string DefaultModel { get; init; } = "";

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; init; } = 120;
}
