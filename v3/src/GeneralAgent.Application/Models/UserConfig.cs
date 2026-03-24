namespace GeneralAgent.Application.Models;

/// <summary>
/// 用户配置模型
/// </summary>
public sealed record UserConfig
{
    /// <summary>
    /// 默认 LLM 提供商（Ollama, Anthropic）
    /// </summary>
    public string DefaultProvider { get; init; } = "Ollama";

    /// <summary>
    /// Ollama 默认模型
    /// </summary>
    public string OllamaModel { get; init; } = "qwen2.5:latest";

    /// <summary>
    /// Ollama 基础 URL
    /// </summary>
    public string OllamaBaseUrl { get; init; } = "http://localhost:11434";

    /// <summary>
    /// Anthropic API Key
    /// </summary>
    public string? AnthropicApiKey { get; init; }

    /// <summary>
    /// Anthropic 默认模型
    /// </summary>
    public string AnthropicModel { get; init; } = "claude-3-5-sonnet-20241022";

    /// <summary>
    /// 默认会话标题
    /// </summary>
    public string DefaultSessionTitle { get; init; } = "新对话";

    /// <summary>
    /// 是否启用流式输出
    /// </summary>
    public bool EnableStreaming { get; init; } = true;

    /// <summary>
    /// 列表默认显示数量
    /// </summary>
    public int DefaultListLimit { get; init; } = 20;

    /// <summary>
    /// 配置文件版本
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// 创建默认配置
    /// </summary>
    public static UserConfig Default() => new();

    /// <summary>
    /// 从环境变量覆盖配置
    /// </summary>
    public UserConfig ApplyEnvironmentVariables()
    {
        var provider = Environment.GetEnvironmentVariable("AGENT_PROVIDER");
        var ollamaModel = Environment.GetEnvironmentVariable("AGENT_OLLAMA_MODEL");
        var ollamaBaseUrl = Environment.GetEnvironmentVariable("AGENT_OLLAMA_BASE_URL");
        var anthropicApiKey = Environment.GetEnvironmentVariable("AGENT_ANTHROPIC_API_KEY");
        var anthropicModel = Environment.GetEnvironmentVariable("AGENT_ANTHROPIC_MODEL");
        var streaming = Environment.GetEnvironmentVariable("AGENT_STREAMING");

        return this with
        {
            DefaultProvider = provider ?? DefaultProvider,
            OllamaModel = ollamaModel ?? OllamaModel,
            OllamaBaseUrl = ollamaBaseUrl ?? OllamaBaseUrl,
            AnthropicApiKey = anthropicApiKey ?? AnthropicApiKey,
            AnthropicModel = anthropicModel ?? AnthropicModel,
            EnableStreaming = streaming != null ? bool.Parse(streaming) : EnableStreaming
        };
    }
}
