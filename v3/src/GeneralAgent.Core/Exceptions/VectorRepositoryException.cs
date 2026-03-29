namespace GeneralAgent.Core.Exceptions;

/// <summary>
/// 向量数据库相关异常
/// </summary>
public class VectorRepositoryException : Exception
{
    public VectorRepositoryException() { }
    public VectorRepositoryException(string message) : base(message) { }
    public VectorRepositoryException(string message, Exception innerException) : base(message, innerException) { }
}
