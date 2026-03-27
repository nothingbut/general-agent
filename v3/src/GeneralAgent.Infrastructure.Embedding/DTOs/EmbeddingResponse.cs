namespace GeneralAgent.Infrastructure.Embedding.DTOs;

/// <summary>
/// Ollama Embedding API 响应 DTO
/// </summary>
public sealed class EmbeddingResponse
{
    /// <summary>
    /// 生成的向量数组
    /// </summary>
    public required float[] Embedding { get; init; }
}
