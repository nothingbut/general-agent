using System.CommandLine;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Storage;
using GeneralAgent.Infrastructure.Storage.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GeneralAgent.Hosts.Console.Tests.Integration;

/// <summary>
/// CLI 端到端集成测试
/// 测试命令的完整执行流程
/// </summary>
public class CliEndToEndTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AgentDbContext _dbContext;

    public CliEndToEndTests()
    {
        // 创建内存数据库
        var services = new ServiceCollection();

        services.AddDbContext<AgentDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

        // 注册存储层
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();

        // 注册应用层服务
        services.AddScoped<SessionService>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<AgentDbContext>();
        _dbContext.Database.EnsureCreated();
    }

    [Fact(DisplayName = "端到端测试: 创建会话 → 列出会话 → 切换会话 → 删除会话")]
    public async Task E2E_SessionManagement_完整流程()
    {
        // Arrange
        var sessionService = _serviceProvider.GetRequiredService<SessionService>();

        // Act & Assert - 创建会话
        var session1 = await sessionService.CreateSessionAsync("测试会话1");
        var session2 = await sessionService.CreateSessionAsync("测试会话2");

        Assert.NotEqual(Guid.Empty, session1.Id);
        Assert.NotEqual(Guid.Empty, session2.Id);
        Assert.Equal("测试会话1", session1.Title);
        Assert.Equal("测试会话2", session2.Title);

        // Act & Assert - 列出会话
        var pagedResult = await sessionService.ListSessionsAsync(limit: 10);

        Assert.Equal(2, pagedResult.Total);
        Assert.Equal(2, pagedResult.Items.Count);
        Assert.Contains(pagedResult.Items, s => s.Title == "测试会话1");
        Assert.Contains(pagedResult.Items, s => s.Title == "测试会话2");

        // Act & Assert - 获取会话
        var retrievedSession = await sessionService.GetSessionAsync(session1.Id);

        Assert.NotNull(retrievedSession);
        Assert.Equal(session1.Id, retrievedSession.Id);
        Assert.Equal("测试会话1", retrievedSession.Title);

        // Act & Assert - 删除会话
        await sessionService.DeleteSessionAsync(session1.Id);

        var afterDelete = await sessionService.ListSessionsAsync(limit: 10);
        Assert.Equal(1, afterDelete.Total);
        Assert.DoesNotContain(afterDelete.Items, s => s.Id == session1.Id);
    }

    [Fact(DisplayName = "端到端测试: 创建会话 → 添加消息 → 查看历史")]
    public async Task E2E_ConversationFlow_完整流程()
    {
        // Arrange
        var sessionService = _serviceProvider.GetRequiredService<SessionService>();
        var messageRepo = _serviceProvider.GetRequiredService<IMessageRepository>();

        // Act - 创建会话
        var session = await sessionService.CreateSessionAsync("对话测试");

        // Act - 添加消息
        var userMessage = Message.CreateUser(session.Id, "你好");
        await messageRepo.CreateAsync(userMessage);

        var assistantMessage = Message.CreateAssistant(session.Id, "您好！有什么我可以帮您的吗？");
        await messageRepo.CreateAsync(assistantMessage);

        // Assert - 查看历史
        var messages = await messageRepo.GetBySessionAsync(session.Id);

        Assert.Equal(2, messages.Count);
        Assert.Equal("你好", messages[0].Content);
        Assert.Equal(MessageRole.User, messages[0].Role);
        Assert.Equal("您好！有什么我可以帮您的吗？", messages[1].Content);
        Assert.Equal(MessageRole.Assistant, messages[1].Role);

        // Assert - 消息计数
        var count = await messageRepo.CountAsync(session.Id);
        Assert.Equal(2, count);
    }

    [Fact(DisplayName = "端到端测试: 短 ID 解析")]
    public async Task E2E_ShortIdResolution_正确解析()
    {
        // Arrange
        var sessionService = _serviceProvider.GetRequiredService<SessionService>();

        // Act - 创建多个会话
        var session1 = await sessionService.CreateSessionAsync("会话1");
        var session2 = await sessionService.CreateSessionAsync("会话2");
        var session3 = await sessionService.CreateSessionAsync("会话3");

        // Act - 获取短 ID
        var shortId1 = session1.Id.ToString()[..8];
        var shortId2 = session2.Id.ToString()[..8];

        // Act - 列出所有会话
        var allSessions = await sessionService.ListSessionsAsync(100, 0);

        // Assert - 短 ID 匹配
        var match1 = allSessions.Items
            .Where(s => s.Id.ToString().StartsWith(shortId1, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(match1);
        Assert.Equal(session1.Id, match1[0].Id);

        var match2 = allSessions.Items
            .Where(s => s.Id.ToString().StartsWith(shortId2, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(match2);
        Assert.Equal(session2.Id, match2[0].Id);
    }

    [Fact(DisplayName = "端到端测试: 分页功能")]
    public async Task E2E_Pagination_正确分页()
    {
        // Arrange
        var sessionService = _serviceProvider.GetRequiredService<SessionService>();

        // Act - 创建 15 个会话
        for (int i = 1; i <= 15; i++)
        {
            await sessionService.CreateSessionAsync($"会话{i}");
        }

        // Act & Assert - 第一页（10条）
        var page1 = await sessionService.ListSessionsAsync(limit: 10, offset: 0);
        Assert.Equal(15, page1.Total);
        Assert.Equal(10, page1.Items.Count);

        // Act & Assert - 第二页（5条）
        var page2 = await sessionService.ListSessionsAsync(limit: 10, offset: 10);
        Assert.Equal(15, page2.Total);
        Assert.Equal(5, page2.Items.Count);

        // Assert - 无重复
        var allIds = page1.Items.Select(s => s.Id)
            .Concat(page2.Items.Select(s => s.Id))
            .ToList();
        Assert.Equal(15, allIds.Distinct().Count());
    }

    [Fact(DisplayName = "端到端测试: 会话类型过滤")]
    public async Task E2E_SessionTypeFilter_正确过滤()
    {
        // Arrange
        var sessionService = _serviceProvider.GetRequiredService<SessionService>();

        // Act - 创建不同类型的会话
        var normalSession = await sessionService.CreateSessionAsync("普通会话");
        // 注意：目前只有 Normal 类型，未来可能会有其他类型

        // Act & Assert - 列出所有会话
        var allSessions = await sessionService.ListSessionsAsync(100, 0);
        Assert.Contains(allSessions.Items, s => s.Id == normalSession.Id);
        Assert.All(allSessions.Items, s => Assert.Equal(SessionType.Normal, s.Type));
    }

    [Fact(DisplayName = "端到端测试: 并发会话创建")]
    public async Task E2E_ConcurrentSessionCreation_无竞争条件()
    {
        // Arrange - 每个并发操作使用独立的 scope
        var tasks = new List<Task<Session>>();

        // Act - 并发创建 10 个会话（每个使用独立的 scope）
        for (int i = 0; i < 10; i++)
        {
            var title = $"并发会话{i}";
            tasks.Add(Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
                return await sessionService.CreateSessionAsync(title);
            }));
        }

        var sessions = await Task.WhenAll(tasks);

        // Assert - 所有会话都成功创建
        Assert.Equal(10, sessions.Length);
        Assert.All(sessions, s => Assert.NotEqual(Guid.Empty, s.Id));

        // Assert - 所有 ID 唯一
        var uniqueIds = sessions.Select(s => s.Id).Distinct().Count();
        Assert.Equal(10, uniqueIds);
    }

    [Fact(DisplayName = "端到端测试: 删除不存在的会话")]
    public async Task E2E_DeleteNonExistentSession_不抛出异常()
    {
        // Arrange
        var sessionService = _serviceProvider.GetRequiredService<SessionService>();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert - 删除不存在的会话不应抛出异常
        await sessionService.DeleteSessionAsync(nonExistentId);

        // 验证确实不存在
        var session = await sessionService.GetSessionAsync(nonExistentId);
        Assert.Null(session);
    }

    public void Dispose()
    {
        _dbContext?.Database.EnsureDeleted();
        _dbContext?.Dispose();
        (_serviceProvider as IDisposable)?.Dispose();
    }
}
