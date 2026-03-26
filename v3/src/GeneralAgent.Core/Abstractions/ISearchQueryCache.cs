namespace GeneralAgent.Core.Abstractions;

using GeneralAgent.Core.Models;

/// <summary>
/// 搜索查询缓存接口
/// </summary>
public interface ISearchQueryCache
{
    /// <summary>
    /// 获取缓存的查询
    /// </summary>
    SearchQuery? Get(string naturalQuery);

    /// <summary>
    /// 设置缓存
    /// </summary>
    void Set(string naturalQuery, SearchQuery searchQuery);

    /// <summary>
    /// 清空缓存
    /// </summary>
    void Clear();
}
