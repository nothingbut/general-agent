namespace GeneralAgent.Infrastructure.Caching;

using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;

/// <summary>
/// 搜索查询 LRU 缓存实现
/// </summary>
public sealed class SearchQueryCache : ISearchQueryCache
{
    private readonly LinkedList<CacheEntry> _lruList = new();
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cache = new();
    private readonly int _capacity;
    private readonly TimeSpan _ttl;
    private readonly object _lock = new();

    public SearchQueryCache(int capacity = 100, TimeSpan? ttl = null)
    {
        _capacity = capacity;
        _ttl = ttl ?? TimeSpan.FromHours(1);
    }

    public SearchQuery? Get(string naturalQuery)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(naturalQuery, out var node))
            {
                // 检查是否过期
                if (DateTime.UtcNow - node.Value.Timestamp < _ttl)
                {
                    // 移到链表头部（标记为最近使用）
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    return node.Value.Query;
                }

                // 过期，移除
                _cache.Remove(naturalQuery);
                _lruList.Remove(node);
            }

            return null;
        }
    }

    public void Set(string naturalQuery, SearchQuery searchQuery)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(naturalQuery, out var existingNode))
            {
                // 更新现有项
                existingNode.Value = new CacheEntry(searchQuery, DateTime.UtcNow);
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
            }
            else
            {
                // 添加新项
                if (_cache.Count >= _capacity)
                {
                    // 淘汰最久未使用的项
                    var lastNode = _lruList.Last!;
                    _cache.Remove(lastNode.Value.Query.NaturalQuery);
                    _lruList.RemoveLast();
                }

                var newNode = new LinkedListNode<CacheEntry>(
                    new CacheEntry(searchQuery, DateTime.UtcNow)
                );
                _lruList.AddFirst(newNode);
                _cache[naturalQuery] = newNode;
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _lruList.Clear();
        }
    }

    private record CacheEntry(SearchQuery Query, DateTime Timestamp);
}
