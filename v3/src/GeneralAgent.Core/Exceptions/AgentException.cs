namespace GeneralAgent.Core.Exceptions;

/// <summary>
/// Agent 系统基础异常
/// </summary>
public class AgentException : Exception
{
    public AgentException(string message) : base(message)
    {
    }

    public AgentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
