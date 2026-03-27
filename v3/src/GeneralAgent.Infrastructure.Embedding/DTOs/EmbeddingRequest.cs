namespace GeneralAgent.Infrastructure.Embedding.DTOs;

/// <summary>
/// Ollama Embedding API 请求 DTO
/// </summary>
public sealed class EmbeddingRequest
{
    /// <summary>
    /// 使用的模型名称
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// 要生成 Embedding 的文本
    /// </summary>
    public required string Prompt { get; init; }
}
