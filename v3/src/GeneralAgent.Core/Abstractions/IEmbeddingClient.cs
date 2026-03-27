namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// Embedding 向量生成客户端接口
/// </summary>
public interface IEmbeddingClient
{
    /// <summary>
    /// 提供商名称（如 "Ollama", "OpenAI"）
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 向量维度（如 768, 1536）
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// 为单个文本生成 Embedding 向量
    /// </summary>
    /// <param name="text">输入文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>归一化的向量数组（长度 = Dimensions）</returns>
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量生成 Embedding 向量（优化性能）
    /// </summary>
    /// <param name="texts">输入文本列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>向量列表（与输入顺序对应）</returns>
    Task<IReadOnlyList<float[]>> GenerateBatchEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}
