namespace GeneralAgent.Core.Exceptions;

/// <summary>
/// LLM 调用异常
/// </summary>
public sealed class LLMException : AgentException
{
    /// <summary>
    /// 提供商名称
    /// </summary>
    public string? ProviderName { get; }

    /// <summary>
    /// 错误类型
    /// </summary>
    public LLMErrorType ErrorType { get; }

    public LLMException(string message)
        : base(message)
    {
        ProviderName = null;
        ErrorType = LLMErrorType.Unknown;
    }

    public LLMException(
        string message,
        string? providerName = null,
        LLMErrorType errorType = LLMErrorType.Unknown,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderName = providerName;
        ErrorType = errorType;
    }
}

/// <summary>
/// LLM 错误类型
/// </summary>
public enum LLMErrorType
{
    /// <summary>
    /// 网络连接失败
    /// </summary>
    NetworkError,

    /// <summary>
    /// 请求超时
    /// </summary>
    TimeoutError,

    /// <summary>
    /// 认证失败
    /// </summary>
    AuthenticationError,

    /// <summary>
    /// 模型不存在
    /// </summary>
    ModelNotFound,

    /// <summary>
    /// 速率限制
    /// </summary>
    RateLimitError,

    /// <summary>
    /// 服务器错误
    /// </summary>
    ServerError,

    /// <summary>
    /// 未知错误
    /// </summary>
    Unknown
}
