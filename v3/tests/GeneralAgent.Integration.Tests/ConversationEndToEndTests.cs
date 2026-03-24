using FluentAssertions;
using GeneralAgent.Application;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure;
using GeneralAgent.Infrastructure.Storage;
using GeneralAgent.Infrastructure.LLM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Integration.Tests;

/// <summary>
/// 端到端集成测试：完整对话流程
/// </summary>
public class ConversationEndToEndTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly string _dbPath;

    public ConversationEndToEndTests()
    {
        // 使用临时数据库
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");

        var services = new ServiceCollection();

        // 配置
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgentDb"] = $"Data Source={_dbPath}",
                ["LLM:DefaultProvider"] = "Mock",
                ["LLM:Providers:Mock:Name"] = "Mock",
                ["LLM:Providers:Mock:BaseUrl"] = "http://localhost:11434",
                ["LLM:Providers:Mock:DefaultModel"] = "mock-model",
                ["LLM:Providers:Mock:TimeoutSeconds"] = "120"
            })
            .Build();

        // 注册所有层
        services.AddInfrastructure($"Data Source={_dbPath}");
        services.AddLLMInfrastructure(configuration);
        services.AddApplicationLayer(configuration);

        // 注册日志
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        _serviceProvider = services.BuildServiceProvider();

        // 确保数据库已创建
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task EndToEnd_NonStreamingConversation_ShouldPersistMessages()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
        var conversationService = scope.ServiceProvider.GetRequiredService<ConversationService>();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        // 创建会话
        var session = await sessionService.CreateSessionAsync("测试会话");

        // Act - 发送第一条消息（非流式）
        var response1 = await conversationService.SendMessageAsync(
            session.Id,
            "你好",
            providerName: null);

        // 发送第二条消息
        var response2 = await conversationService.SendMessageAsync(
            session.Id,
            "介绍一下你自己",
            providerName: null);

        // Assert - 验证响应
        response1.Should().NotBeNullOrEmpty();
        response2.Should().NotBeNullOrEmpty();

        // 验证消息持久化
        var messages = await messageRepo.GetBySessionAsync(session.Id);
        messages.Should().HaveCount(4); // 2 user + 2 assistant

        // 验证消息顺序
        messages[0].Role.Should().Be(MessageRole.User);
        messages[0].Content.Should().Be("你好");

        messages[1].Role.Should().Be(MessageRole.Assistant);
        messages[1].Content.Should().NotBeNullOrEmpty();

        messages[2].Role.Should().Be(MessageRole.User);
        messages[2].Content.Should().Be("介绍一下你自己");

        messages[3].Role.Should().Be(MessageRole.Assistant);
        messages[3].Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task EndToEnd_StreamingConversation_ShouldPersistFullMessage()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
        var conversationService = scope.ServiceProvider.GetRequiredService<ConversationService>();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        var session = await sessionService.CreateSessionAsync("流式测试");

        // Act - 流式发送消息
        var chunks = new List<string>();
        await foreach (var chunk in conversationService.SendMessageStreamAsync(
            session.Id,
            "你好",
            providerName: null))
        {
            chunks.Add(chunk);
        }

        // Assert - 验证流式响应
        chunks.Should().NotBeEmpty();
        var fullResponse = string.Join("", chunks);
        fullResponse.Should().NotBeNullOrEmpty();

        // 验证消息持久化
        var messages = await messageRepo.GetBySessionAsync(session.Id);
        messages.Should().HaveCount(2); // 1 user + 1 assistant

        messages[0].Role.Should().Be(MessageRole.User);
        messages[0].Content.Should().Be("你好");

        messages[1].Role.Should().Be(MessageRole.Assistant);
        messages[1].Content.Should().Be(fullResponse);
    }

    [Fact]
    public async Task EndToEnd_MultipleSessionsIndependence_ShouldIsolateMessages()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
        var conversationService = scope.ServiceProvider.GetRequiredService<ConversationService>();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        // 创建两个独立会话
        var session1 = await sessionService.CreateSessionAsync("会话1");
        var session2 = await sessionService.CreateSessionAsync("会话2");

        // Act - 在两个会话中分别发送消息
        await conversationService.SendMessageAsync(session1.Id, "会话1的消息");
        await conversationService.SendMessageAsync(session2.Id, "会话2的消息");

        // Assert - 验证消息隔离
        var messages1 = await messageRepo.GetBySessionAsync(session1.Id);
        var messages2 = await messageRepo.GetBySessionAsync(session2.Id);

        messages1.Should().HaveCount(2); // 1 user + 1 assistant
        messages2.Should().HaveCount(2);

        messages1[0].Content.Should().Be("会话1的消息");
        messages2[0].Content.Should().Be("会话2的消息");

        // 验证 SessionId 正确
        messages1.Should().AllSatisfy(m => m.SessionId.Should().Be(session1.Id));
        messages2.Should().AllSatisfy(m => m.SessionId.Should().Be(session2.Id));
    }

    [Fact]
    public async Task EndToEnd_SessionPersistence_ShouldSurviveRestart()
    {
        // Arrange - 第一个 scope（模拟首次启动）
        Guid sessionId;
        {
            using var scope = _serviceProvider.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            var conversationService = scope.ServiceProvider.GetRequiredService<ConversationService>();

            var session = await sessionService.CreateSessionAsync("持久化测试");
            sessionId = session.Id;

            await conversationService.SendMessageAsync(session.Id, "测试消息");
        }

        // Act - 第二个 scope（模拟重启后）
        {
            using var scope = _serviceProvider.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

            // 验证会话仍然存在
            var session = await sessionService.GetSessionAsync(sessionId);
            session.Should().NotBeNull();
            session!.Title.Should().Be("持久化测试");

            // 验证消息仍然存在
            var messages = await messageRepo.GetBySessionAsync(sessionId);
            messages.Should().HaveCount(2);
            messages[0].Content.Should().Be("测试消息");
        }
    }

    [Fact]
    public async Task EndToEnd_SessionList_ShouldReturnAllSessions()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();

        // 创建多个会话
        await sessionService.CreateSessionAsync("会话1");
        await sessionService.CreateSessionAsync("会话2");
        await sessionService.CreateSessionAsync("会话3");

        // Act
        var pagedResult = await sessionService.ListSessionsAsync(limit: 10);

        // Assert
        pagedResult.Total.Should().BeGreaterOrEqualTo(3);
        pagedResult.Items.Should().HaveCountGreaterOrEqualTo(3);
        pagedResult.Items.Should().Contain(s => s.Title == "会话1");
        pagedResult.Items.Should().Contain(s => s.Title == "会话2");
        pagedResult.Items.Should().Contain(s => s.Title == "会话3");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();

        // 清理临时数据库
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // 忽略清理失败
            }
        }
    }
}
