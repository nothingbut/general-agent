namespace GeneralAgent.Core.Exceptions;

/// <summary>
/// 存储层异常
/// </summary>
public sealed class StorageException : AgentException
{
    public StorageException(string message) : base(message)
    {
    }

    public StorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
