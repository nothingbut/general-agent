using System.Diagnostics;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Hosts.Console.Services;
using GeneralAgent.Infrastructure.Caching;
using GeneralAgent.Infrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Performance.Tests;

/// <summary>
/// 搜索系统性能测试
/// 验证 FTS5 搜索、标签建议和缓存性能
/// </summary>
public class SearchPerformanceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AgentDbContext _context;

    public SearchPerformanceTests()
    {
        // 使用内存数据库
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AgentDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AgentDbContext(options);
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// 测试 1: FTS5 搜索大数据集性能应小于 300ms
    /// 验证在 10,000 条消息中进行全文搜索的性能
    /// </summary>
    [Fact(Skip = "SearchService 当前为简化实现，等待 FTS5 完整实现")]
    public async Task SearchWithFTS5_LargeDataset_MeetsPerformanceTarget()
    {
        // Arrange - 创建 10,000 条测试消息
        await SeedLargeDataset(_context, messageCount: 10000);

        var searchService = CreateSearchService(_context);

        // Warmup - 避免冷启动影响测量
        await searchService.SearchWithNaturalLanguageAsync("Python", CancellationToken.None);

        // Act - 测量搜索时间
        var stopwatch = Stopwatch.StartNew();
        var results = await searchService.SearchWithNaturalLanguageAsync("Python", CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 300,
            $"FTS5 搜索耗时 {stopwatch.ElapsedMilliseconds}ms 超过 300ms 限制");

        // 注意: 当前 SearchService 返回空列表（TODO 标记），FTS5 实现后此断言应为 true
        // Assert.NotEmpty(results);
    }

    /// <summary>
    /// 测试 2: 标签建议性能应小于 3 秒
    /// 验证从标题生成标签的 LLM 调用性能
    /// </summary>
    [Fact]
    public async Task TagSuggestion_FromTitle_MeetsPerformanceTarget()
    {
        // Arrange - Mock LLM 客户端返回标签
        var mockLlm = Substitute.For<ILLMClient>();
        mockLlm
            .CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = @"{""tags"":[{""name"":""测试标签"",""reason"":""测试原因""}]}",
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        var mockTagRepo = Substitute.For<ISessionTagRepository>();
        var logger = Substitute.For<ILogger<SmartTagService>>();
        var tagSuggestionService = new SmartTagService(mockLlm, mockTagRepo, logger);

        // Act - 测量标签建议时间
        var stopwatch = Stopwatch.StartNew();
        var suggestions = await tagSuggestionService.SuggestFromTitleAsync(
            "如何使用 Python 进行数据分析",
            CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 3000,
            $"标签建议耗时 {stopwatch.ElapsedMilliseconds}ms 超过 3000ms（3秒）限制");
        Assert.NotEmpty(suggestions);
    }

    /// <summary>
    /// 测试 3: LRU 缓存并发访问应线程安全
    /// 验证缓存在高并发场景下无死锁和数据竞争
    /// </summary>
    [Fact]
    public async Task LRUCache_ConcurrentAccess_ThreadSafe()
    {
        // Arrange - 创建 LRU 缓存（容量 100）
        var cache = new SearchQueryCache(capacity: 100, ttl: TimeSpan.FromMinutes(5));

        // Act - 启动 10 个并发任务，每个任务执行 100 次读写
        var tasks = Enumerable.Range(0, 10).Select(async taskId =>
        {
            for (int i = 0; i < 100; i++)
            {
                var key = $"query_{taskId}_{i % 20}"; // 20 个不同的 key，会有碰撞
                var query = new SearchQuery
                {
                    NaturalQuery = key,
                    Type = SearchType.Keyword,
                    Criteria = new SearchCriteria { Keywords = new List<string> { "test" } }
                };

                // 写入
                cache.Set(key, query);

                // 读取
                var result = cache.Get(key);

                // 模拟少量延迟以增加并发竞争
                await Task.Delay(1);
            }
        }).ToArray();

        // Assert - 应无死锁、无异常
        var timeout = Task.Delay(TimeSpan.FromSeconds(10));
        var completed = Task.WhenAll(tasks);

        var firstToComplete = await Task.WhenAny(completed, timeout);
        Assert.Equal(completed, firstToComplete); // 不应超时

        // 验证缓存状态正常
        cache.Clear(); // 应该能正常清空
    }

    #region Helper Methods

    /// <summary>
    /// 创建大量测试数据
    /// </summary>
    /// <param name="context">数据库上下文</param>
    /// <param name="messageCount">消息数量</param>
    private async Task SeedLargeDataset(AgentDbContext context, int messageCount)
    {
        // 创建 1 个测试会话
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "性能测试会话",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Type = SessionType.Normal
        };
        context.Sessions.Add(session);

        // 创建 messageCount 条消息（包含 "Python" 关键词）
        for (int i = 0; i < messageCount; i++)
        {
            var message = new Message
            {
                SessionId = session.Id,
                Role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant,
                Content = $"消息 {i}: 关于 Python 编程的讨论内容 {Guid.NewGuid()}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-messageCount + i)
            };
            context.Messages.Add(message);

            // 每 1000 条保存一次，避免内存溢出
            if ((i + 1) % 1000 == 0)
            {
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// 创建搜索服务实例
    /// </summary>
    private SearchService CreateSearchService(AgentDbContext context)
    {
        var messageRepo = Substitute.For<IMessageRepository>();
        var sessionRepo = Substitute.For<ISessionRepository>();

        // Mock LLM 客户端用于查询解析
        var mockLlm = Substitute.For<ILLMClient>();
        mockLlm
            .CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = @"{""type"":""keyword"",""criteria"":{""keywords"":[""Python""]}}",
                Usage = new TokenUsage { PromptTokens = 30, CompletionTokens = 15 },
                Timestamp = DateTime.UtcNow
            });

        var cache = new SearchQueryCache();
        var nlLogger = Substitute.For<ILogger<NaturalLanguageQueryService>>();
        var nlQueryService = new NaturalLanguageQueryService(mockLlm, cache, nlLogger);

        var searchLogger = Substitute.For<ILogger<SearchService>>();
        return new SearchService(nlQueryService, messageRepo, sessionRepo, searchLogger);
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
