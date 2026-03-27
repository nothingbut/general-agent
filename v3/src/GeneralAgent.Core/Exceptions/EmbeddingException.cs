namespace GeneralAgent.Core.Exceptions;

/// <summary>
/// Embedding 生成相关异常
/// </summary>
public class EmbeddingException : Exception
{
    public EmbeddingException() { }
    public EmbeddingException(string message) : base(message) { }
    public EmbeddingException(string message, Exception innerException) : base(message, innerException) { }
}
