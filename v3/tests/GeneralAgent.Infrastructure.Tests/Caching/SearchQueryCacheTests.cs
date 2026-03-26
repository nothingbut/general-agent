namespace GeneralAgent.Infrastructure.Tests.Caching;

using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Caching;

public class SearchQueryCacheTests
{
    [Fact]
    public void Get_CacheMiss_ReturnsNull()
    {
        // Arrange
        var cache = new SearchQueryCache();

        // Act
        var result = cache.Get("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Set_Get_ReturnsValue()
    {
        // Arrange
        var cache = new SearchQueryCache();
        var query = new SearchQuery
        {
            NaturalQuery = "test query",
            Criteria = new SearchCriteria { Keywords = new List<string> { "test" } }
        };

        // Act
        cache.Set("test query", query);
        var result = cache.Get("test query");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test query", result.NaturalQuery);
    }

    [Fact]
    public void Set_ExceedsCapacity_EvictsLRU()
    {
        // Arrange
        var cache = new SearchQueryCache(capacity: 3);

        // Act
        cache.Set("query1", new SearchQuery { NaturalQuery = "query1" });
        cache.Set("query2", new SearchQuery { NaturalQuery = "query2" });
        cache.Set("query3", new SearchQuery { NaturalQuery = "query3" });
        cache.Set("query4", new SearchQuery { NaturalQuery = "query4" }); // 淘汰 query1

        // Assert
        Assert.Null(cache.Get("query1")); // 最久未使用的被淘汰
        Assert.NotNull(cache.Get("query2"));
        Assert.NotNull(cache.Get("query3"));
        Assert.NotNull(cache.Get("query4"));
    }

    [Fact]
    public void Get_UpdatesAccessOrder()
    {
        // Arrange
        var cache = new SearchQueryCache(capacity: 3);
        cache.Set("query1", new SearchQuery { NaturalQuery = "query1" });
        cache.Set("query2", new SearchQuery { NaturalQuery = "query2" });
        cache.Set("query3", new SearchQuery { NaturalQuery = "query3" });

        // Act
        cache.Get("query1"); // 访问 query1，更新顺序
        cache.Set("query4", new SearchQuery { NaturalQuery = "query4" }); // 淘汰 query2

        // Assert
        Assert.NotNull(cache.Get("query1")); // query1 被访问过，不会被淘汰
        Assert.Null(cache.Get("query2")); // query2 是最久未使用的
        Assert.NotNull(cache.Get("query3"));
        Assert.NotNull(cache.Get("query4"));
    }

    [Fact]
    public void Get_ExpiredEntry_ReturnsNull()
    {
        // Arrange
        var cache = new SearchQueryCache(ttl: TimeSpan.FromMilliseconds(50));
        cache.Set("query1", new SearchQuery { NaturalQuery = "query1" });

        // Act
        Thread.Sleep(100); // 等待过期
        var result = cache.Get("query1");

        // Assert
        Assert.Null(result);
    }
}
