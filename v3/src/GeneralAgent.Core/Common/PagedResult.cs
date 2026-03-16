namespace GeneralAgent.Core.Common;

/// <summary>
/// 分页结果
/// </summary>
/// <typeparam name="T">项目类型</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>
    /// 当前页的项目
    /// </summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>
    /// 总项目数
    /// </summary>
    public int Total { get; }

    /// <summary>
    /// 每页限制
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// 偏移量
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// 是否有下一页
    /// </summary>
    public bool HasNextPage => Offset + Items.Count < Total;

    /// <summary>
    /// 是否有上一页
    /// </summary>
    public bool HasPreviousPage => Offset > 0;

    public PagedResult(IReadOnlyList<T> items, int total, int limit, int offset)
    {
        Items = items;
        Total = total;
        Limit = limit;
        Offset = offset;
    }
}
