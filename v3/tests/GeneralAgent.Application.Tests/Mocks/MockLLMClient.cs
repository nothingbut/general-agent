using System.Runtime.CompilerServices;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Application.Tests.Mocks;

/// <summary>
/// 用于测试的 Mock LLM 客户端实现
/// </summary>
public sealed class MockLLMClient : ILLMClient
{
    private readonly string _responseContent;
    private readonly TimeSpan? _simulateDelay;
    private readonly bool _shouldThrow;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="providerName">提供商名称，默认 "Mock"</param>
    /// <param name="responseContent">默认响应内容，默认 "Mock response"</param>
    /// <param name="simulateDelay">模拟网络延迟，默认 null</param>
    /// <param name="shouldThrow">是否抛出异常，默认 false</param>
    public MockLLMClient(
        string providerName = "Mock",
        string responseContent = "Mock response",
        TimeSpan? simulateDelay = null,
        bool shouldThrow = false)
    {
        ProviderName = providerName;
        _responseContent = responseContent;
        _simulateDelay = simulateDelay;
        _shouldThrow = shouldThrow;
    }

    /// <summary>
    /// 提供商名称
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// 非流式补全实现
    /// </summary>
    public async Task<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken ct = default)
    {
        // 处理取消令牌
        ct.ThrowIfCancellationRequested();

        // 模拟延迟
        if (_simulateDelay.HasValue)
        {
            await Task.Delay(_simulateDelay.Value, ct);
        }

        // 检查是否应该抛出异常
        if (_shouldThrow)
        {
            throw new LLMException(
                "Mock LLM client configured to throw exception",
                ProviderName,
                LLMErrorType.Unknown);
        }

        // 返回模拟响应
        var usage = new TokenUsage
        {
            PromptTokens = EstimateTokens(_responseContent) / 2,
            CompletionTokens = EstimateTokens(_responseContent) / 2
        };

        return new CompletionResponse
        {
            Content = _responseContent,
            Model = request.Model,
            Usage = usage,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 流式补全实现
    /// </summary>
    public async IAsyncEnumerable<StreamChunk> StreamAsync(
        CompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 处理取消令牌
        ct.ThrowIfCancellationRequested();

        // 模拟延迟
        if (_simulateDelay.HasValue)
        {
            await Task.Delay(_simulateDelay.Value, ct);
        }

        // 检查是否应该抛出异常
        if (_shouldThrow)
        {
            throw new LLMException(
                "Mock LLM client configured to throw exception",
                ProviderName,
                LLMErrorType.Unknown);
        }

        // 将响应内容分块流式返回
        const int chunkSize = 2;
        var tokens = EstimateTokens(_responseContent);
        var completionTokens = 0;

        for (int i = 0; i < _responseContent.Length; i += chunkSize)
        {
            ct.ThrowIfCancellationRequested();

            var delta = _responseContent.Substring(i, Math.Min(chunkSize, _responseContent.Length - i));
            completionTokens = (int)Math.Ceiling((i + delta.Length) / 4.0); // 粗略估计

            yield return new StreamChunk
            {
                Delta = delta,
                IsComplete = false,
                Usage = null
            };
        }

        // 返回最终块，包含完整的 token 统计
        var finalUsage = new TokenUsage
        {
            PromptTokens = tokens / 2,
            CompletionTokens = completionTokens
        };

        yield return new StreamChunk
        {
            Delta = string.Empty,
            IsComplete = true,
            Usage = finalUsage
        };
    }

    /// <summary>
    /// 创建成功的 Mock 客户端
    /// </summary>
    public static MockLLMClient CreateSuccess() =>
        new MockLLMClient(responseContent: "Mock response");

    /// <summary>
    /// 创建失败的 Mock 客户端
    /// </summary>
    public static MockLLMClient CreateFailure() =>
        new MockLLMClient(shouldThrow: true);

    /// <summary>
    /// 创建带延迟的 Mock 客户端
    /// </summary>
    public static MockLLMClient CreateWithDelay(TimeSpan delay) =>
        new MockLLMClient(simulateDelay: delay);

    /// <summary>
    /// 估计字符串中的 token 数量（粗略估计）
    /// </summary>
    private static int EstimateTokens(string text)
    {
        // 粗略估计：平均每 4 个字符约 1 个 token
        return Math.Max(1, text.Length / 4);
    }
}
