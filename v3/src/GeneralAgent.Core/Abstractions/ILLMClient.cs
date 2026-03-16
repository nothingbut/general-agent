using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// LLM 客户端接口
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// 提供商名称（如 "Ollama", "LMStudio"）
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 非流式补全
    /// </summary>
    /// <param name="request">补全请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>补全响应</returns>
    /// <exception cref="Exceptions.LLMException">LLM 调用失败</exception>
    Task<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// 流式补全
    /// </summary>
    /// <param name="request">补全请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>流式响应块</returns>
    /// <exception cref="Exceptions.LLMException">LLM 调用失败</exception>
    IAsyncEnumerable<StreamChunk> StreamAsync(
        CompletionRequest request,
        CancellationToken ct = default);
}
