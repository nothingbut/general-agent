namespace GeneralAgent.Core.Common;

/// <summary>
/// 函数式错误处理结果类型
/// </summary>
/// <typeparam name="T">成功时的值类型</typeparam>
public readonly record struct Result<T>
{
    /// <summary>
    /// 成功时的值
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// 失败时的错误消息
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess => Error is null;

    private Result(T value)
    {
        Value = value;
        Error = null;
    }

    private Result(string error)
    {
        Value = default;
        Error = error;
    }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static Result<T> Failure(string error) => new(error);

    /// <summary>
    /// 模式匹配
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}
